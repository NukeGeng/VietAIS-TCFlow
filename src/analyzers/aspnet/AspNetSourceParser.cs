using System.Globalization;
using System.Text.RegularExpressions;
using VietAIS.TCFlow.Analyzers.Core;

namespace VietAIS.TCFlow.Analyzers.AspNet;

internal sealed record EndpointBinding(
    string? ApiRequestType,
    string? CommandType,
    IReadOnlyList<CSharpFieldDefinition> Parameters,
    string? MethodName);

internal sealed record OpenApiDetails(
    string OperationName,
    string Summary,
    string Description,
    string ResponseType,
    string SuccessStatus,
    IReadOnlyList<string> ErrorStatuses,
    string ApiVersion);

internal sealed record AuthorizationFact(string Permission, RepositoryFile File, int Index);

internal static partial class AspNetSourceParser
{
    public static void Parse(
        RepositoryFile file,
        CSharpSourceCatalog source,
        AspNetRoutingCatalog routing,
        AspNetSemanticCatalog semantics,
        AspNetAnalysisAccumulator accumulator)
    {
        foreach (Match match in EndpointRegex().Matches(file.Content))
        {
            ParseEndpoint(file, match, source, routing, semantics, accumulator);
        }
    }

    private static void ParseEndpoint(
        RepositoryFile file,
        Match match,
        CSharpSourceCatalog source,
        AspNetRoutingCatalog routing,
        AspNetSemanticCatalog semantics,
        AspNetAnalysisAccumulator accumulator)
    {
        var openIndex = file.Content.IndexOf('(', match.Index + match.Length - 1);
        var closeIndex = CSharpTextParsing.FindBalancedClose(file.Content, openIndex, '(', ')');
        if (closeIndex < 0)
        {
            return;
        }

        var arguments = CSharpTextParsing.SplitTopLevel(file.Content[(openIndex + 1)..closeIndex]);
        if (arguments.Count == 0 || !CSharpTextParsing.TryReadRoute(arguments[0], out var localRoute))
        {
            return;
        }

        var receiver = match.Groups["receiver"].Value;
        var method = match.Groups["method"].Value.ToUpperInvariant();
        var prefix = routing.ResolvePrefix(file, receiver, match.Index);
        var level = prefix is null ? EvidenceLevel.Inferred : EvidenceLevel.Confirmed;
        var route = CSharpTextParsing.CombineRoute(prefix?.Prefix, localRoute);
        var statementEnd = CSharpTextParsing.FindStatementEnd(file.Content, closeIndex + 1);
        var chain = file.Content[(closeIndex + 1)..Math.Min(file.Content.Length, statementEnd + 1)];
        var binding = ResolveBinding(file, arguments, source);
        var openApi = ParseOpenApi(chain, binding, source);
        var commandType = binding.CommandType ?? binding.ApiRequestType;
        var handler = commandType is not null && semantics.TryGetHandler(commandType, out var handlerDefinition)
            ? handlerDefinition
            : null;
        var validator = commandType is not null && semantics.TryGetValidator(commandType, out var validatorDefinition)
            ? validatorDefinition
            : binding.ApiRequestType is not null && semantics.TryGetValidator(binding.ApiRequestType, out validatorDefinition)
                ? validatorDefinition
                : null;

        var evidenceIds = new List<string>();
        var endpointEvidence = accumulator.AddEvidence(
            file,
            match.Groups["method"].Index,
            $"{method} {route}",
            level,
            "aspnet.endpoint",
            openApi.OperationName);
        evidenceIds.Add(endpointEvidence);

        var requestFields = BuildRequestFields(binding, source, validator);
        var responseFields = BuildResponseFields(openApi.ResponseType, source);
        var authorizationFacts = ParseAuthorizationFacts(
            chain,
            closeIndex + 1,
            file,
            prefix,
            handler,
            source);
        var authorizationEvidence = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var fact in authorizationFacts)
        {
            var evidence = accumulator.AddEvidence(
                fact.File,
                fact.Index,
                $"Authorization requires {fact.Permission}.",
                EvidenceLevel.Confirmed,
                "aspnet.authorization");
            evidenceIds.Add(evidence);
            authorizationEvidence[fact.Permission] = evidence;
        }
        var permissions = authorizationEvidence.Keys.Order(StringComparer.Ordinal).ToArray();

        var endpointArtifactId = accumulator.AddArtifact(
            ArtifactKind.AspNetEndpoint,
            string.IsNullOrEmpty(openApi.OperationName) ? $"{method} {route}" : openApi.OperationName,
            file,
            level,
            evidenceIds,
            new Dictionary<string, string>
            {
                ["method"] = method,
                ["route"] = route,
                ["requestType"] = binding.ApiRequestType ?? string.Empty,
                ["commandType"] = commandType ?? string.Empty,
                ["responseType"] = openApi.ResponseType,
                ["successStatus"] = openApi.SuccessStatus,
                ["errorStatuses"] = string.Join(',', openApi.ErrorStatuses),
                ["apiVersion"] = openApi.ApiVersion,
                ["authorization"] = string.Join(',', permissions)
            });

        AddRequestArtifact(binding.ApiRequestType, requestFields, endpointArtifactId, endpointEvidence, source, accumulator);
        AddResponseArtifact(openApi.ResponseType, responseFields, endpointArtifactId, endpointEvidence, source, accumulator);
        AddValidatorArtifact(validator, endpointArtifactId, accumulator, evidenceIds);
        AddHandlerArtifact(handler, endpointArtifactId, source, accumulator, evidenceIds);
        AddOpenApiArtifact(openApi, file, chain, closeIndex + 1, endpointArtifactId, accumulator, evidenceIds);
        foreach (var permission in permissions)
        {
            accumulator.AddDependency(
                endpointArtifactId,
                permission,
                DependencyKind.Authorizes,
                EvidenceLevel.Confirmed,
                authorizationEvidence[permission]);
        }

        accumulator.AddContract(new Contract(
            StableIdentity.Create("contract", "backend", method, route, file.RelativePath),
            ContractDirection.BackendActual,
            method,
            route,
            level,
            requestFields,
            responseFields,
            openApi.ErrorStatuses,
            IsPaginated(binding, openApi),
            permissions,
            evidenceIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()));

        if (prefix is null)
        {
            accumulator.AddDiagnostic(new AnalyzerDiagnostic(
                "ASPNET001",
                $"The route prefix for {method} {localRoute} could not be resolved.",
                EvidenceLevel.Inferred,
                new SourceLocation(
                    file.RelativePath,
                    CSharpTextParsing.LineNumber(file.Content, match.Groups["method"].Index),
                    CSharpTextParsing.LineNumber(file.Content, match.Groups["method"].Index))));
        }
    }

    private static EndpointBinding ResolveBinding(
        RepositoryFile file,
        IReadOnlyList<string> arguments,
        CSharpSourceCatalog source)
    {
        if (arguments.Count < 2)
        {
            return new EndpointBinding(null, null, [], null);
        }

        var handlerExpression = arguments[1].Trim();
        var lambda = LambdaParametersRegex().Match(handlerExpression);
        if (lambda.Success)
        {
            var parameters = ParseBoundParameters(file, lambda.Groups["parameters"].Value);
            var lambdaRequestType = SelectRequestType(parameters, source);
            return new EndpointBinding(lambdaRequestType, lambdaRequestType, parameters, null);
        }

        if (!IdentifierRegex().IsMatch(handlerExpression))
        {
            return new EndpointBinding(null, null, [], null);
        }

        var method = source.FindMethod(file, handlerExpression);
        if (method is null)
        {
            return new EndpointBinding(null, null, [], handlerExpression);
        }

        var requestType = SelectRequestType(method.Parameters, source);
        var command = CommandConstructionRegex().Match(method.Body);
        return new EndpointBinding(
            requestType,
            command.Success ? command.Groups["type"].Value : requestType,
            method.Parameters,
            method.Name);
    }

    private static IReadOnlyList<CSharpFieldDefinition> ParseBoundParameters(
        RepositoryFile file,
        string sourceText)
    {
        var fields = new List<CSharpFieldDefinition>();
        foreach (var parameter in CSharpTextParsing.SplitTopLevel(sourceText))
        {
            var value = CSharpTextParsing.RemoveAttributes(parameter).Trim();
            var separator = value.LastIndexOfAny([' ', '\t', '\r', '\n']);
            if (separator <= 0)
            {
                continue;
            }

            var type = value[..separator].Trim();
            var name = value[(separator + 1)..].Trim();
            fields.Add(new CSharpFieldDefinition(
                name,
                CSharpTextParsing.NormalizeType(type),
                !type.EndsWith('?'),
                new SourceLocation(file.RelativePath, 1, 1, name)));
        }

        return fields;
    }

    private static string? SelectRequestType(
        IReadOnlyList<CSharpFieldDefinition> parameters,
        CSharpSourceCatalog source) => parameters
        .Select(parameter => parameter.Type)
        .FirstOrDefault(type => source.TryGetType(type, out _) &&
            (type.EndsWith("Request", StringComparison.Ordinal) ||
             type.EndsWith("Command", StringComparison.Ordinal) ||
             type.EndsWith("Query", StringComparison.Ordinal)));

    private static IReadOnlyList<ContractField> BuildRequestFields(
        EndpointBinding binding,
        CSharpSourceCatalog source,
        AspNetValidatorDefinition? validator)
    {
        var fields = new List<CSharpFieldDefinition>();
        if (binding.ApiRequestType is not null && source.TryGetType(binding.ApiRequestType, out var request))
        {
            fields.AddRange(request.Fields);
        }

        fields.AddRange(binding.Parameters.Where(parameter =>
            IsBoundScalar(parameter.Type) &&
            !fields.Any(field => field.Name == parameter.Name)));
        return fields
            .Select(field =>
            {
                var validations = validator?.Rules.GetValueOrDefault(field.Name)?.Rules ?? [];
                return new ContractField(
                    field.Name,
                    field.Type,
                    field.Required || validations.Any(IsRequiredRule),
                    EvidenceLevel.Confirmed,
                    field.Location)
                {
                    Validations = validations
                };
            })
            .ToArray();
    }

    private static IReadOnlyList<ContractField> BuildResponseFields(
        string responseType,
        CSharpSourceCatalog source)
    {
        if (string.IsNullOrWhiteSpace(responseType) || !source.TryGetType(responseType, out var response))
        {
            return [];
        }

        return response.Fields.Select(field => new ContractField(
            field.Name,
            field.Type,
            field.Required,
            EvidenceLevel.Confirmed,
            field.Location)).ToArray();
    }

    private static IReadOnlyList<AuthorizationFact> ParseAuthorizationFacts(
        string chain,
        int chainIndex,
        RepositoryFile file,
        AspNetRoutePrefix? prefix,
        AspNetHandlerDefinition? handler,
        CSharpSourceCatalog source)
    {
        var facts = new Dictionary<string, AuthorizationFact>(StringComparer.Ordinal);
        foreach (Match permission in RequirePermissionRegex().Matches(chain))
        {
            var value = source.ResolveConstant(permission.Groups["permission"].Value);
            facts[value] = new AuthorizationFact(value, file, chainIndex + permission.Index);
        }

        if (handler is not null)
        {
            var authorizationIndex = handler.Type.File.Content.IndexOf(
                "EnsureAuthorizedAsync",
                handler.Type.Index,
                StringComparison.Ordinal);
            foreach (var permission in handler.Permissions)
            {
                facts.TryAdd(
                    permission,
                    new AuthorizationFact(
                        permission,
                        handler.Type.File,
                        authorizationIndex < 0 ? handler.Type.Index : authorizationIndex));
            }
        }

        if (facts.Count == 0 &&
            (prefix?.RequiresAuthorization == true || chain.Contains("RequireAuthorization", StringComparison.Ordinal)))
        {
            var authorizationFile = prefix?.File ?? file;
            var authorizationIndex = prefix?.Index ?? chainIndex;
            facts["authenticated"] = new AuthorizationFact(
                "authenticated",
                authorizationFile,
                authorizationIndex);
        }

        return facts.Values.OrderBy(fact => fact.Permission, StringComparer.Ordinal).ToArray();
    }

    private static OpenApiDetails ParseOpenApi(
        string chain,
        EndpointBinding binding,
        CSharpSourceCatalog source)
    {
        var operation = ReadStringOrNameof(WithNameRegex().Match(chain));
        var summary = ReadLiteral(WithSummaryRegex().Match(chain));
        var description = ReadLiteral(WithDescriptionRegex().Match(chain));
        var response = ParseProduces(chain).FirstOrDefault();
        var responseType = response.Type ?? string.Empty;
        if (string.IsNullOrWhiteSpace(responseType) && binding.CommandType is not null)
        {
            responseType = AspNetSemanticCatalog.ResolveResponseType(source, binding.CommandType) ?? string.Empty;
        }

        var errors = ProducesProblemRegex().Matches(chain)
            .Select(match => match.Groups["status"].Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var apiVersion = ApiVersionRegex().Match(chain);
        return new OpenApiDetails(
            operation,
            summary,
            description,
            responseType,
            string.IsNullOrEmpty(response.Status) ? "200" : response.Status,
            errors,
            apiVersion.Success ? NormalizeApiVersion(apiVersion.Groups["version"].Value) : string.Empty);
    }

    private static IReadOnlyList<(string Type, string Status)> ParseProduces(string chain)
    {
        var results = new List<(string Type, string Status)>();
        foreach (Match match in ProducesStartRegex().Matches(chain))
        {
            var openType = chain.IndexOf('<', match.Index);
            var closeType = CSharpTextParsing.FindBalancedClose(chain, openType, '<', '>');
            if (closeType < 0)
            {
                continue;
            }

            var openArguments = chain.IndexOf('(', closeType + 1);
            var arguments = openArguments < 0
                ? string.Empty
                : CSharpTextParsing.ExtractBalanced(chain, openArguments, '(', ')');
            var status = StatusCodeRegex().Match(arguments);
            results.Add((
                chain[(openType + 1)..closeType].Trim(),
                status.Success ? status.Groups["status"].Value : string.Empty));
        }

        return results;
    }

    private static void AddRequestArtifact(
        string? requestType,
        IReadOnlyList<ContractField> fields,
        string endpointId,
        string endpointEvidence,
        CSharpSourceCatalog source,
        AspNetAnalysisAccumulator accumulator)
    {
        if (requestType is null || !source.TryGetType(requestType, out var definition))
        {
            return;
        }

        var evidence = accumulator.AddEvidence(
            definition.File,
            definition.Index,
            $"Request DTO {definition.Name} declares {fields.Count} bound fields.",
            EvidenceLevel.Confirmed,
            "aspnet.request-dto",
            definition.Name);
        var artifact = accumulator.AddArtifact(
            ArtifactKind.RequestDto,
            definition.Name,
            definition.File,
            EvidenceLevel.Confirmed,
            [evidence],
            new Dictionary<string, string> { ["fields"] = string.Join(',', fields.Select(field => field.Name)) });
        accumulator.AddDependency(endpointId, artifact, DependencyKind.Accepts, EvidenceLevel.Confirmed, endpointEvidence);
    }

    private static void AddResponseArtifact(
        string responseType,
        IReadOnlyList<ContractField> fields,
        string endpointId,
        string endpointEvidence,
        CSharpSourceCatalog source,
        AspNetAnalysisAccumulator accumulator)
    {
        if (string.IsNullOrWhiteSpace(responseType) || !source.TryGetType(responseType, out var definition))
        {
            return;
        }

        var evidence = accumulator.AddEvidence(
            definition.File,
            definition.Index,
            $"Response DTO {definition.Name} declares {fields.Count} fields.",
            EvidenceLevel.Confirmed,
            "aspnet.response-dto",
            definition.Name);
        var artifact = accumulator.AddArtifact(
            ArtifactKind.ResponseDto,
            definition.Name,
            definition.File,
            EvidenceLevel.Confirmed,
            [evidence],
            new Dictionary<string, string> { ["fields"] = string.Join(',', fields.Select(field => field.Name)) });
        accumulator.AddDependency(endpointId, artifact, DependencyKind.Returns, EvidenceLevel.Confirmed, endpointEvidence);
    }

    private static void AddValidatorArtifact(
        AspNetValidatorDefinition? validator,
        string endpointId,
        AspNetAnalysisAccumulator accumulator,
        ICollection<string> endpointEvidenceIds)
    {
        if (validator is null)
        {
            return;
        }

        var evidence = accumulator.AddEvidence(
            validator.Type.File,
            validator.Type.Index,
            $"{validator.Type.Name} validates {validator.RequestType}.",
            EvidenceLevel.Confirmed,
            "aspnet.validator",
            validator.Type.Name);
        endpointEvidenceIds.Add(evidence);
        var artifact = accumulator.AddArtifact(
            ArtifactKind.Validator,
            validator.Type.Name,
            validator.Type.File,
            EvidenceLevel.Confirmed,
            [evidence],
            new Dictionary<string, string>
            {
                ["requestType"] = validator.RequestType,
                ["rules"] = string.Join(';', validator.Rules.Values.Select(rule =>
                    $"{rule.Field}={string.Join('|', rule.Rules)}"))
            });
        accumulator.AddDependency(endpointId, artifact, DependencyKind.Validates, EvidenceLevel.Confirmed, evidence);
    }

    private static void AddHandlerArtifact(
        AspNetHandlerDefinition? handler,
        string endpointId,
        CSharpSourceCatalog source,
        AspNetAnalysisAccumulator accumulator,
        ICollection<string> endpointEvidenceIds)
    {
        if (handler is null)
        {
            return;
        }

        var evidence = accumulator.AddEvidence(
            handler.Type.File,
            handler.Type.Index,
            $"{handler.Type.Name} handles {handler.RequestType}.",
            EvidenceLevel.Confirmed,
            "aspnet.handler",
            handler.Type.Name);
        endpointEvidenceIds.Add(evidence);
        var handlerId = accumulator.AddArtifact(
            ArtifactKind.Handler,
            handler.Type.Name,
            handler.Type.File,
            EvidenceLevel.Confirmed,
            [evidence],
            new Dictionary<string, string>
            {
                ["requestType"] = handler.RequestType,
                ["responseType"] = handler.ResponseType
            });
        accumulator.AddDependency(endpointId, handlerId, DependencyKind.DelegatesTo, EvidenceLevel.Confirmed, evidence);

        foreach (var dependency in handler.Type.ConstructorParameters)
        {
            var simpleName = CSharpTextParsing.SimpleTypeName(dependency.Type);
            var dependencyFile = source.TryGetType(simpleName, out var definition)
                ? definition.File
                : handler.Type.File;
            var dependencyEvidence = accumulator.AddEvidence(
                handler.Type.File,
                handler.Type.Index,
                $"{handler.Type.Name} depends on {dependency.Type}.",
                EvidenceLevel.Confirmed,
                "aspnet.handler-dependency",
                dependency.Name);
            var serviceId = accumulator.AddArtifact(
                simpleName.StartsWith('I') ? ArtifactKind.Interface : ArtifactKind.Service,
                dependency.Type,
                dependencyFile,
                EvidenceLevel.Confirmed,
                [dependencyEvidence]);
            accumulator.AddDependency(handlerId, serviceId, DependencyKind.Uses, EvidenceLevel.Confirmed, dependencyEvidence);
        }
    }

    private static void AddOpenApiArtifact(
        OpenApiDetails openApi,
        RepositoryFile file,
        string chain,
        int chainIndex,
        string endpointId,
        AspNetAnalysisAccumulator accumulator,
        ICollection<string> endpointEvidenceIds)
    {
        if (string.IsNullOrEmpty(openApi.OperationName) &&
            string.IsNullOrEmpty(openApi.Summary) &&
            string.IsNullOrEmpty(openApi.ResponseType))
        {
            return;
        }

        var evidence = accumulator.AddEvidence(
            file,
            chainIndex + Math.Max(0, chain.IndexOf(".With", StringComparison.Ordinal)),
            $"OpenAPI operation {openApi.OperationName} produces {openApi.ResponseType}.",
            EvidenceLevel.Confirmed,
            "aspnet.openapi",
            openApi.OperationName);
        endpointEvidenceIds.Add(evidence);
        var artifact = accumulator.AddArtifact(
            ArtifactKind.OpenApiOperation,
            string.IsNullOrEmpty(openApi.OperationName) ? openApi.ResponseType : openApi.OperationName,
            file,
            EvidenceLevel.Confirmed,
            [evidence],
            new Dictionary<string, string>
            {
                ["summary"] = openApi.Summary,
                ["description"] = openApi.Description,
                ["responseType"] = openApi.ResponseType,
                ["successStatus"] = openApi.SuccessStatus,
                ["errorStatuses"] = string.Join(',', openApi.ErrorStatuses),
                ["apiVersion"] = openApi.ApiVersion
            });
        accumulator.AddDependency(endpointId, artifact, DependencyKind.Produces, EvidenceLevel.Confirmed, evidence);
    }

    private static bool IsPaginated(EndpointBinding binding, OpenApiDetails openApi) =>
        openApi.ResponseType.Contains("PagedList", StringComparison.Ordinal) ||
        binding.Parameters.Any(parameter => parameter.Name is "page" or "pageNumber" or "pageSize");

    private static bool IsBoundScalar(string type) => CSharpTextParsing.NormalizeType(type) is
        "string" or "Guid" or "int" or "long" or "short" or "decimal" or "double" or "float" or
        "bool" or "DateTime" or "DateTimeOffset";

    private static bool IsRequiredRule(string rule) =>
        rule is "notEmpty" or "notNull" || rule.StartsWith("length:", StringComparison.Ordinal);

    private static string ReadStringOrNameof(Match match) => !match.Success
        ? string.Empty
        : match.Groups["name"].Success
            ? match.Groups["name"].Value
            : match.Groups["literal"].Value;

    private static string ReadLiteral(Match match) =>
        match.Success ? match.Groups["literal"].Value : string.Empty;

    private static string NormalizeApiVersion(string value) =>
        string.Join('.', value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));

    [GeneratedRegex(@"\b(?<receiver>[A-Za-z_$][\w$]*)\s*\.\s*Map(?<method>Get|Post|Put|Delete|Patch)\s*\(", RegexOptions.IgnoreCase)]
    private static partial Regex EndpointRegex();

    [GeneratedRegex(@"(?:async\s*)?\((?<parameters>.*?)\)\s*=>", RegexOptions.Singleline)]
    private static partial Regex LambdaParametersRegex();

    [GeneratedRegex(@"\bnew\s+(?<type>[A-Za-z_$][\w$]*(?:Command|Query))\s*\(")]
    private static partial Regex CommandConstructionRegex();

    [GeneratedRegex(@"^[A-Za-z_$][\w$]*$")]
    private static partial Regex IdentifierRegex();

    [GeneratedRegex("""\.RequirePermission\s*\(\s*(?<permission>"[^"]*"|[A-Za-z_$][\w$.]*)\s*\)""")]
    private static partial Regex RequirePermissionRegex();

    [GeneratedRegex("""\.WithName\s*\(\s*(?:nameof\s*\(\s*(?<name>[A-Za-z_$][\w$]*)\s*\)|"(?<literal>[^"]*)")\s*\)""")]
    private static partial Regex WithNameRegex();

    [GeneratedRegex("""\.WithSummary\s*\(\s*"(?<literal>[^"]*)"\s*\)""")]
    private static partial Regex WithSummaryRegex();

    [GeneratedRegex("""\.WithDescription\s*\(\s*"(?<literal>[^"]*)"\s*\)""")]
    private static partial Regex WithDescriptionRegex();

    [GeneratedRegex(@"\.Produces\s*<")]
    private static partial Regex ProducesStartRegex();

    [GeneratedRegex(@"\.ProducesProblem\s*\(\s*StatusCodes\.Status(?<status>\d{3})[A-Za-z_$][\w$]*")]
    private static partial Regex ProducesProblemRegex();

    [GeneratedRegex(@"StatusCodes\.Status(?<status>\d{3})[A-Za-z_$][\w$]*")]
    private static partial Regex StatusCodeRegex();

    [GeneratedRegex(@"\.MapToApiVersion\s*\(\s*(?:new\s+ApiVersion\s*\()?\s*(?<version>\d+(?:\s*,\s*\d+)?)")]
    private static partial Regex ApiVersionRegex();
}

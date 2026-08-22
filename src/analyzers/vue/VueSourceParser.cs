using System.Globalization;
using System.Text.RegularExpressions;
using VietAIS.TCFlow.Analyzers.Core;

namespace VietAIS.TCFlow.Analyzers.Vue;

internal static partial class VueSourceParser
{
    public static void Parse(
        RepositoryFile file,
        VueTypeCatalog types,
        VueAnalysisAccumulator accumulator)
    {
        AddTypeArtifacts(file, types, accumulator);
        var componentId = string.Equals(
            Path.GetExtension(file.RelativePath),
            ".vue",
            StringComparison.OrdinalIgnoreCase)
            ? ParseComponent(file, accumulator)
            : null;
        ParseImports(file, componentId, accumulator);
        ParseReactiveState(file, componentId, accumulator);
        ParseForms(file, componentId, accumulator);
        ParsePinia(file, componentId, accumulator);
        ParseRoutes(file, componentId, accumulator);
        ParsePermissions(file, componentId, accumulator);
        ParseApiCalls(file, componentId, types, accumulator);
    }

    private static void AddTypeArtifacts(
        RepositoryFile file,
        VueTypeCatalog types,
        VueAnalysisAccumulator accumulator)
    {
        foreach (var type in types.All.Where(type => type.Path == file.RelativePath))
        {
            var evidenceId = accumulator.AddEvidence(
                file,
                type.Index,
                $"TypeScript interface {type.Name} declares {type.Fields.Count} fields.",
                EvidenceLevel.Confirmed,
                "vue.typescript.interface",
                type.Name);
            accumulator.AddArtifact(
                ArtifactKind.TypeScriptInterface,
                type.Name,
                file,
                EvidenceLevel.Confirmed,
                [evidenceId],
                new Dictionary<string, string>
                {
                    ["fields"] = string.Join(',', type.Fields.Select(field => field.Name)),
                    ["fieldCount"] = type.Fields.Count.ToString(CultureInfo.InvariantCulture)
                });
        }
    }

    private static string ParseComponent(RepositoryFile file, VueAnalysisAccumulator accumulator)
    {
        var name = Path.GetFileNameWithoutExtension(file.RelativePath);
        var evidence = new List<string>
        {
            accumulator.AddEvidence(
                file,
                0,
                $"{file.RelativePath} is a Vue single-file component.",
                EvidenceLevel.Confirmed,
                "vue.component",
                name)
        };
        var metadata = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["scriptSetup"] = file.Content.Contains("<script setup", StringComparison.OrdinalIgnoreCase)
                .ToString(CultureInfo.InvariantCulture).ToLowerInvariant(),
            ["hasLoadingState"] = LoadingStateRegex().IsMatch(file.Content).ToString().ToLowerInvariant(),
            ["hasErrorState"] = ErrorStateRegex().IsMatch(file.Content).ToString().ToLowerInvariant(),
            ["hasValidation"] = ValidationRegex().IsMatch(file.Content).ToString().ToLowerInvariant(),
            ["hasFilters"] = FilterRegex().IsMatch(file.Content).ToString().ToLowerInvariant(),
            ["hasPagination"] = PaginationRegex().IsMatch(file.Content).ToString().ToLowerInvariant(),
            ["hasSearch"] = SearchRegex().IsMatch(file.Content).ToString().ToLowerInvariant()
        };

        var props = ParsePropertyBlock(file.Content, DefinePropsRegex());
        if (props.Count > 0)
        {
            metadata["props"] = string.Join(',', props);
            evidence.Add(accumulator.AddEvidence(
                file,
                DefinePropsRegex().Match(file.Content).Index,
                $"defineProps declares: {string.Join(", ", props)}.",
                EvidenceLevel.Confirmed,
                "vue.defineProps",
                name));
        }

        var emits = DefineEmitsRegex().Matches(file.Content)
            .Select(match => match.Groups["event"].Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (emits.Length > 0)
        {
            metadata["emits"] = string.Join(',', emits);
            evidence.Add(accumulator.AddEvidence(
                file,
                DefineEmitsRegex().Match(file.Content).Index,
                $"defineEmits declares: {string.Join(", ", emits)}.",
                EvidenceLevel.Confirmed,
                "vue.defineEmits",
                name));
        }

        return accumulator.AddArtifact(
            ArtifactKind.VueComponent,
            name,
            file,
            EvidenceLevel.Confirmed,
            evidence,
            metadata);
    }

    private static void ParseImports(
        RepositoryFile file,
        string? componentId,
        VueAnalysisAccumulator accumulator)
    {
        if (componentId is null)
        {
            return;
        }

        foreach (Match match in ImportRegex().Matches(file.Content))
        {
            var target = match.Groups["target"].Value;
            var evidenceId = accumulator.AddEvidence(
                file,
                match.Index,
                $"The component imports {target}.",
                EvidenceLevel.Confirmed,
                "vue.import");
            accumulator.AddDependency(
                componentId,
                target,
                DependencyKind.Imports,
                EvidenceLevel.Confirmed,
                evidenceId);
        }
    }

    private static void ParseReactiveState(
        RepositoryFile file,
        string? componentId,
        VueAnalysisAccumulator accumulator)
    {
        foreach (Match match in ReactiveStateRegex().Matches(file.Content))
        {
            var name = match.Groups["name"].Value;
            var stateKind = match.Groups["kind"].Value;
            var evidenceId = accumulator.AddEvidence(
                file,
                match.Index,
                $"Reactive state {name} uses {stateKind}.",
                EvidenceLevel.Confirmed,
                "vue.reactive-state",
                name);
            var artifactKind = PaginationRegex().IsMatch(name)
                ? ArtifactKind.Pagination
                : FilterRegex().IsMatch(name) || SearchRegex().IsMatch(name)
                    ? ArtifactKind.Filter
                    : ArtifactKind.ReactiveState;
            var artifactId = accumulator.AddArtifact(
                artifactKind,
                name,
                file,
                EvidenceLevel.Confirmed,
                [evidenceId],
                new Dictionary<string, string> { ["stateKind"] = stateKind });
            if (componentId is not null)
            {
                accumulator.AddDependency(
                    componentId,
                    artifactId,
                    DependencyKind.Contains,
                    EvidenceLevel.Confirmed,
                    evidenceId);
            }
        }
    }

    private static void ParseForms(
        RepositoryFile file,
        string? componentId,
        VueAnalysisAccumulator accumulator)
    {
        foreach (Match match in FormFieldRegex().Matches(file.Content))
        {
            var attributes = match.Groups["attributes"].Value;
            var modelMatch = ModelRegex().Match(attributes);
            if (!modelMatch.Success)
            {
                continue;
            }

            var model = modelMatch.Groups["model"].Value;
            var element = match.Groups["element"].Value.ToLowerInvariant();
            var typeMatch = TypeAttributeRegex().Match(attributes);
            var evidenceId = accumulator.AddEvidence(
                file,
                match.Index,
                $"{element} binds to {model} through v-model.",
                EvidenceLevel.Confirmed,
                "vue.form-field",
                model);
            var validations = ParseValidationAttributes(attributes);
            var artifactId = accumulator.AddArtifact(
                ArtifactKind.FormField,
                model,
                file,
                EvidenceLevel.Confirmed,
                [evidenceId],
                new Dictionary<string, string>
                {
                    ["element"] = element,
                    ["type"] = typeMatch.Success ? typeMatch.Groups["type"].Value : element,
                    ["validations"] = string.Join(',', validations)
                });
            if (componentId is not null)
            {
                accumulator.AddDependency(
                    componentId,
                    artifactId,
                    DependencyKind.Contains,
                    EvidenceLevel.Confirmed,
                    evidenceId);
            }
        }
    }

    private static void ParsePinia(
        RepositoryFile file,
        string? componentId,
        VueAnalysisAccumulator accumulator)
    {
        foreach (Match match in PiniaRegex().Matches(file.Content))
        {
            var name = match.Groups["name"].Value;
            var evidenceId = accumulator.AddEvidence(
                file,
                match.Index,
                $"defineStore registers Pinia store {name}.",
                EvidenceLevel.Confirmed,
                "vue.pinia",
                name);
            var artifactId = accumulator.AddArtifact(
                ArtifactKind.PiniaStore,
                name,
                file,
                EvidenceLevel.Confirmed,
                [evidenceId]);
            if (componentId is not null)
            {
                accumulator.AddDependency(
                    componentId,
                    artifactId,
                    DependencyKind.Uses,
                    EvidenceLevel.Confirmed,
                    evidenceId);
            }
        }
    }

    private static void ParseRoutes(
        RepositoryFile file,
        string? componentId,
        VueAnalysisAccumulator accumulator)
    {
        foreach (Match match in RouteRegex().Matches(file.Content))
        {
            var path = match.Groups["path"].Value;
            var name = match.Groups["name"].Value;
            var target = match.Groups["component"].Success ? match.Groups["component"].Value : string.Empty;
            var evidenceId = accumulator.AddEvidence(
                file,
                match.Index,
                $"Vue Router declares route {name} at {path}.",
                EvidenceLevel.Confirmed,
                "vue.router",
                name);
            var routeId = accumulator.AddArtifact(
                ArtifactKind.VueRoute,
                name,
                file,
                EvidenceLevel.Confirmed,
                [evidenceId],
                new Dictionary<string, string>
                {
                    ["path"] = path,
                    ["component"] = target
                });
            if (componentId is not null)
            {
                accumulator.AddDependency(
                    componentId,
                    routeId,
                    DependencyKind.NavigatesTo,
                    EvidenceLevel.Confirmed,
                    evidenceId);
            }
        }
    }

    private static void ParsePermissions(
        RepositoryFile file,
        string? componentId,
        VueAnalysisAccumulator accumulator)
    {
        foreach (Match match in PermissionRegex().Matches(file.Content))
        {
            var permission = match.Groups["permission"].Value;
            var evidenceId = accumulator.AddEvidence(
                file,
                match.Index,
                $"The UI checks permission {permission}.",
                EvidenceLevel.Confirmed,
                "vue.permission",
                permission);
            var artifactId = accumulator.AddArtifact(
                ArtifactKind.PermissionCheck,
                permission,
                file,
                EvidenceLevel.Confirmed,
                [evidenceId]);
            if (componentId is not null)
            {
                accumulator.AddDependency(
                    componentId,
                    artifactId,
                    DependencyKind.Uses,
                    EvidenceLevel.Confirmed,
                    evidenceId);
            }
        }
    }

    private static void ParseApiCalls(
        RepositoryFile file,
        string? componentId,
        VueTypeCatalog types,
        VueAnalysisAccumulator accumulator)
    {
        foreach (Match match in ApiCallStartRegex().Matches(file.Content))
        {
            var openIndex = match.Index + match.Value.LastIndexOf('(');
            var argumentsText = TextParsing.ExtractBalanced(file.Content, openIndex, '(', ')');
            var arguments = TextParsing.SplitTopLevel(argumentsText);
            if (arguments.Count == 0 || !TextParsing.TryReadLiteral(arguments[0], out var route, out var interpolated))
            {
                continue;
            }

            var function = match.Groups["function"].Value;
            var method = match.Groups["method"].Success
                ? match.Groups["method"].Value.ToUpperInvariant()
                : ResolveOptionsMethod(function, arguments);
            var level = interpolated ? EvidenceLevel.Inferred : EvidenceLevel.Confirmed;
            var evidenceIds = new List<string>();
            var evidenceId = accumulator.AddEvidence(
                file,
                match.Index,
                $"{method} {route}",
                level,
                "vue.api-call");
            evidenceIds.Add(evidenceId);
            var responseType = match.Groups["generic"].Value.Trim();
            var requestExpression = ResolveRequestExpression(function, method, arguments);
            var requestFields = ParseContractFields(requestExpression, file, match.Index, types, level);
            var declaredResponseFields = ParseResponseFields(responseType, types, level);
            var responseUsageFields = ParseResponseUsage(file, match.Index, openIndex + argumentsText.Length + 1);
            var responseFields = declaredResponseFields
                .Concat(responseUsageFields)
                .GroupBy(field => field.Name, StringComparer.Ordinal)
                .Select(group => group.OrderBy(field => field.Type == "unknown").First())
                .OrderBy(field => field.Name, StringComparer.Ordinal)
                .ToArray();
            foreach (var field in requestFields.Where(field => field.Validations.Count > 0))
            {
                var formField = FindFormField(file.Content, field.Name);
                if (formField is null)
                {
                    continue;
                }

                evidenceIds.Add(accumulator.AddEvidence(
                    file,
                    formField.Index,
                    $"Request field {field.Name} declares validation: {string.Join(", ", field.Validations)}.",
                    EvidenceLevel.Confirmed,
                    "vue.contract-validation",
                    field.Name));
            }

            if (responseUsageFields.Count > 0)
            {
                evidenceIds.Add(accumulator.AddEvidence(
                    file,
                    responseUsageFields[0].Location.StartLine == 1
                        ? match.Index
                        : FindLineStartIndex(file.Content, responseUsageFields[0].Location.StartLine),
                    $"The response is read through fields: {string.Join(", ", responseUsageFields.Select(field => field.Name))}.",
                    EvidenceLevel.Confirmed,
                    "vue.api-response-usage"));
            }
            var permissions = PermissionRegex().Matches(file.Content)
                .Select(permission => permission.Groups["permission"].Value)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            var apiArtifactId = accumulator.AddArtifact(
                ArtifactKind.ApiCall,
                $"{method} {route}",
                file,
                level,
                evidenceIds,
                new Dictionary<string, string>
                {
                    ["method"] = method,
                    ["route"] = route,
                    ["requestFields"] = string.Join(',', requestFields.Select(field => field.Name)),
                    ["responseFields"] = string.Join(',', responseFields.Select(field => field.Name)),
                    ["responseUsage"] = string.Join(',', responseUsageFields.Select(field => field.Name))
                });
            if (componentId is not null)
            {
                accumulator.AddDependency(componentId, route, DependencyKind.Calls, level, evidenceId);
                accumulator.AddDependency(
                    componentId,
                    apiArtifactId,
                    DependencyKind.Contains,
                    level,
                    evidenceId);
            }

            var contractId = StableIdentity.Create("contract", "frontend", method, route, file.RelativePath);
            accumulator.AddContract(new Contract(
                contractId,
                ContractDirection.FrontendExpected,
                method,
                route,
                level,
                requestFields,
                responseFields,
                ErrorStateRegex().IsMatch(file.Content) ? ["error"] : [],
                PaginationRegex().IsMatch(route) || requestFields.Any(field => PaginationRegex().IsMatch(field.Name)),
                permissions,
                evidenceIds));
            accumulator.AddCapability(
                CapabilityName(method, route),
                EvidenceLevel.Inferred,
                evidenceIds,
                componentId is null ? [apiArtifactId] : [componentId, apiArtifactId]);

            if (interpolated)
            {
                accumulator.AddDiagnostic(new AnalyzerDiagnostic(
                    "VUE001",
                    $"Route '{route}' contains interpolation and remains inferred.",
                    EvidenceLevel.Inferred,
                    new SourceLocation(
                        file.RelativePath,
                        TextParsing.LineNumber(file.Content, match.Index),
                        TextParsing.LineNumber(file.Content, match.Index))));
            }
        }
    }

    private static IReadOnlyList<ContractField> ParseContractFields(
        string? expression,
        RepositoryFile file,
        int callIndex,
        VueTypeCatalog types,
        EvidenceLevel level)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return [];
        }

        var value = expression.Trim();
        var variableType = ResolveVariableType(file.Content, value);
        if (variableType is not null && types.TryGet(variableType, out var definition))
        {
            return definition.Fields.Select(field => new ContractField(
                field.Name,
                field.Type,
                field.Required,
                EvidenceLevel.Confirmed,
                field.Location)
            {
                Validations = FormValidations(file.Content, field.Name)
            }).ToArray();
        }

        if (!value.StartsWith('{') || !value.EndsWith('}'))
        {
            return [];
        }

        var line = TextParsing.LineNumber(file.Content, callIndex);
        return TextParsing.SplitTopLevel(value[1..^1])
            .Where(item => !item.StartsWith("...", StringComparison.Ordinal))
            .Select(item =>
            {
                var separator = item.IndexOf(':');
                var name = (separator < 0 ? item : item[..separator]).Trim();
                var fieldValue = separator < 0 ? item : item[(separator + 1)..].Trim();
                return new ContractField(
                    name,
                    InferType(fieldValue),
                    true,
                    level,
                    new SourceLocation(file.RelativePath, line, line, name))
                {
                    Validations = FormValidations(file.Content, name)
                };
            })
            .Where(field => IdentifierRegex().IsMatch(field.Name))
            .OrderBy(field => field.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<ContractField> ParseResponseFields(
        string responseType,
        VueTypeCatalog types,
        EvidenceLevel level)
    {
        if (string.IsNullOrWhiteSpace(responseType) || !types.TryGet(responseType, out var definition))
        {
            return [];
        }

        return definition.Fields.Select(field => new ContractField(
            field.Name,
            field.Type,
            field.Required,
            level == EvidenceLevel.Confirmed ? EvidenceLevel.Confirmed : EvidenceLevel.Inferred,
            field.Location)).ToArray();
    }

    private static IReadOnlyList<ContractField> ParseResponseUsage(
        RepositoryFile file,
        int callIndex,
        int closeIndex)
    {
        var prefixStart = Math.Max(0, callIndex - 240);
        var prefix = file.Content[prefixStart..callIndex];
        var assignment = ApiAssignmentRegex().Match(prefix);
        if (!assignment.Success)
        {
            return [];
        }

        var variable = assignment.Groups["name"].Value;
        var searchStart = Math.Clamp(closeIndex + 1, 0, file.Content.Length);
        var searchLength = Math.Min(4_000, file.Content.Length - searchStart);
        var usageSource = file.Content.Substring(searchStart, searchLength);
        var usageRegex = new Regex(
            $@"\b{Regex.Escape(variable)}(?:\.data)?\.(?<field>[A-Za-z_$][\w$]*)",
            RegexOptions.CultureInvariant);
        return usageRegex.Matches(usageSource)
            .Select(usage => new ContractField(
                usage.Groups["field"].Value,
                "unknown",
                true,
                EvidenceLevel.Confirmed,
                new SourceLocation(
                    file.RelativePath,
                    TextParsing.LineNumber(file.Content, searchStart + usage.Index),
                    TextParsing.LineNumber(file.Content, searchStart + usage.Index),
                    usage.Groups["field"].Value)))
            .DistinctBy(field => field.Name, StringComparer.Ordinal)
            .OrderBy(field => field.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static int FindLineStartIndex(string source, int lineNumber)
    {
        if (lineNumber <= 1)
        {
            return 0;
        }

        var line = 1;
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index] == '\n' && ++line == lineNumber)
            {
                return index + 1;
            }
        }

        return source.Length;
    }

    private static string ResolveOptionsMethod(string function, IReadOnlyList<string> arguments)
    {
        if (function is not ("fetch" or "apiRequest") || arguments.Count < 2)
        {
            return "GET";
        }

        var method = MethodOptionRegex().Match(arguments[1]);
        return method.Success ? method.Groups["method"].Value.ToUpperInvariant() : "GET";
    }

    private static string? ResolveRequestExpression(
        string function,
        string method,
        IReadOnlyList<string> arguments)
    {
        if (function is "fetch" or "apiRequest")
        {
            if (arguments.Count < 2)
            {
                return null;
            }

            var body = JsonBodyRegex().Match(arguments[1]);
            return body.Success ? body.Groups["body"].Value.Trim() : null;
        }

        return method is "GET" or "DELETE" || arguments.Count < 2 ? null : arguments[1];
    }

    private static string? ResolveVariableType(string source, string expression)
    {
        if (!IdentifierRegex().IsMatch(expression))
        {
            return null;
        }

        var pattern = $@"\b(?:const|let)\s+{Regex.Escape(expression)}\s*:\s*(?<type>[A-Za-z_$][\w$]*(?:<[^>]+>)?(?:\[\])?)";
        var match = Regex.Match(source, pattern);
        return match.Success ? match.Groups["type"].Value : null;
    }

    private static string InferType(string expression)
    {
        var value = expression.Trim();
        if ((value.StartsWith('"') && value.EndsWith('"')) ||
            (value.StartsWith('\'') && value.EndsWith('\'')) ||
            (value.StartsWith('`') && value.EndsWith('`')))
        {
            return "string";
        }

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
        {
            return "number";
        }

        return value is "true" or "false" ? "boolean" : "unknown";
    }

    private static IReadOnlyList<string> FormValidations(string source, string fieldName)
    {
        var match = FindFormField(source, fieldName);
        return match is null ? [] : ParseValidationAttributes(match.Groups["attributes"].Value);
    }

    private static Match? FindFormField(string source, string fieldName) => FormFieldRegex()
        .Matches(source)
        .FirstOrDefault(match =>
        {
            var model = ModelRegex().Match(match.Groups["attributes"].Value);
            return model.Success && string.Equals(
                model.Groups["model"].Value.Split('.').Last(),
                fieldName,
                StringComparison.Ordinal);
        });

    private static IReadOnlyList<string> ParseValidationAttributes(string attributes) =>
        ValidationAttributeRegex().Matches(attributes)
            .Select(attribute =>
            {
                var name = attribute.Groups["name"].Value.ToLowerInvariant();
                var value = attribute.Groups["double"].Success
                    ? attribute.Groups["double"].Value
                    : attribute.Groups["single"].Success
                        ? attribute.Groups["single"].Value
                        : attribute.Groups["bare"].Value;
                return string.IsNullOrEmpty(value) ? name : $"{name}:{value}";
            })
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static string CapabilityName(string method, string route)
    {
        var resource = route.Split('?', 2)[0]
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault(segment => !segment.StartsWith("${", StringComparison.Ordinal) &&
                !segment.StartsWith(':')) ?? "resource";
        var normalized = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(resource.Replace('-', ' '));
        var action = method switch
        {
            "POST" => "Create",
            "PUT" or "PATCH" => "Update",
            "DELETE" => "Delete",
            _ => "View"
        };
        return $"{action} {normalized}";
    }

    private static IReadOnlyList<string> ParsePropertyBlock(string source, Regex regex)
    {
        var match = regex.Match(source);
        if (!match.Success)
        {
            return [];
        }

        return PropertyRegex().Matches(match.Groups["body"].Value)
            .Select(property => property.Groups["name"].Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    [GeneratedRegex(@"defineProps\s*<\s*\{(?<body>.*?)\}\s*>\s*\(", RegexOptions.Singleline)]
    private static partial Regex DefinePropsRegex();

    [GeneratedRegex("""defineEmits\s*<.*?\(\s*(?:event|e)\s*:\s*['"](?<event>[^'"]+)['"]""", RegexOptions.Singleline)]
    private static partial Regex DefineEmitsRegex();

    [GeneratedRegex(@"(?<name>[A-Za-z_$][\w$]*)\??\s*:")]
    private static partial Regex PropertyRegex();

    [GeneratedRegex("""import\s+.+?\s+from\s+['"](?<target>[^'"]+)['"]""")]
    private static partial Regex ImportRegex();

    [GeneratedRegex(@"\b(?:const|let)\s+(?<name>[A-Za-z_$][\w$]*)\s*=\s*(?<kind>ref|reactive|computed)\s*(?:<[^>]+>)?\s*\(")]
    private static partial Regex ReactiveStateRegex();

    [GeneratedRegex(@"<(?<element>input|select|textarea)\b(?<attributes>[^>]*)>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex FormFieldRegex();

    [GeneratedRegex("""v-model(?:\.[\w-]+)?\s*=\s*['"](?<model>[^'"]+)['"]""", RegexOptions.IgnoreCase)]
    private static partial Regex ModelRegex();

    [GeneratedRegex("""\btype\s*=\s*['"](?<type>[^'"]+)['"]""", RegexOptions.IgnoreCase)]
    private static partial Regex TypeAttributeRegex();

    [GeneratedRegex("""\b(?<name>required|min|max|minlength|maxlength|pattern)\b(?:\s*=\s*(?:"(?<double>[^"]*)"|'(?<single>[^']*)'|(?<bare>[^\s>]+)))?""", RegexOptions.IgnoreCase)]
    private static partial Regex ValidationAttributeRegex();

    [GeneratedRegex("""defineStore\s*\(\s*['"](?<name>[^'"]+)['"]""")]
    private static partial Regex PiniaRegex();

    [GeneratedRegex("""path\s*:\s*['"](?<path>[^'"]+)['"][\s\S]{0,240}?name\s*:\s*['"](?<name>[^'"]+)['"](?:[\s\S]{0,240}?component\s*:\s*(?<component>[A-Za-z_$][\w$]*))?""", RegexOptions.IgnoreCase)]
    private static partial Regex RouteRegex();

    [GeneratedRegex("""hasPermission\s*\(\s*['"](?<permission>[^'"]+)['"]""")]
    private static partial Regex PermissionRegex();

    [GeneratedRegex(@"(?<callee>(?<object>[A-Za-z_$][\w$]*)\s*\.\s*(?<method>get|post|put|patch|delete)|(?<function>fetch|apiRequest))\s*(?:<(?<generic>[^>]+)>)?\s*\(", RegexOptions.IgnoreCase)]
    private static partial Regex ApiCallStartRegex();

    [GeneratedRegex(@"\b(?:const|let)\s+(?<name>[A-Za-z_$][\w$]*)\s*=\s*(?:await\s*)?$")]
    private static partial Regex ApiAssignmentRegex();

    [GeneratedRegex("""\bmethod\s*:\s*['"](?<method>GET|POST|PUT|PATCH|DELETE)['"]""", RegexOptions.IgnoreCase)]
    private static partial Regex MethodOptionRegex();

    [GeneratedRegex(@"\bbody\s*:\s*JSON\.stringify\s*\((?<body>.*?)\)", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex JsonBodyRegex();

    [GeneratedRegex(@"^[A-Za-z_$][\w$]*$")]
    private static partial Regex IdentifierRegex();

    [GeneratedRegex(@"\b(?:isLoading|loading|pending)\b", RegexOptions.IgnoreCase)]
    private static partial Regex LoadingStateRegex();

    [GeneratedRegex(@"\b(?:error|errors|hasError)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ErrorStateRegex();

    [GeneratedRegex(@"\b(?:required|minlength|maxlength|pattern|validate|validator|rules)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ValidationRegex();

    [GeneratedRegex(@"\b(?:filter|filters)\w*\b", RegexOptions.IgnoreCase)]
    private static partial Regex FilterRegex();

    [GeneratedRegex(@"\b(?:page|pageNumber|pageSize|pagination|hasNext|hasPrevious)\w*\b", RegexOptions.IgnoreCase)]
    private static partial Regex PaginationRegex();

    [GeneratedRegex(@"\b(?:search|keyword|query)\w*\b", RegexOptions.IgnoreCase)]
    private static partial Regex SearchRegex();
}

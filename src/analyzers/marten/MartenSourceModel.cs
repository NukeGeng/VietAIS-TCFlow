using System.Text.RegularExpressions;
using VietAIS.TCFlow.Analyzers.Core;

namespace VietAIS.TCFlow.Analyzers.Marten;

internal enum MartenOperationKind
{
    Query,
    Load,
    Store,
    Delete,
    SaveChanges
}

internal sealed record MartenTypeScope(
    string Name,
    RepositoryFile File,
    int DeclarationIndex,
    int BodyIndex,
    string Header,
    string Body,
    string SessionType,
    string SessionVariable,
    bool IsHandler);

internal sealed record MartenOperationFact(
    MartenTypeScope Scope,
    MartenOperationKind Kind,
    string? DocumentType,
    int Index,
    bool HasPagination);

internal sealed record MartenDocumentDeclaration(string Name, RepositoryFile File, int Index);

internal sealed record MartenSchemaFact(string DocumentType, RepositoryFile File, int Index);

internal sealed partial class MartenSourceModel
{
    public IReadOnlyList<MartenTypeScope> Scopes { get; private init; } = [];

    public IReadOnlyList<MartenOperationFact> Operations { get; private init; } = [];

    public IReadOnlyDictionary<string, MartenDocumentDeclaration> Declarations { get; private init; } =
        new Dictionary<string, MartenDocumentDeclaration>();

    public IReadOnlyList<MartenSchemaFact> SchemaFacts { get; private init; } = [];

    public static MartenSourceModel Create(IEnumerable<RepositoryFile> files)
    {
        var ordered = files.OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToArray();
        var declarations = ParseDeclarations(ordered);
        var schemas = ParseSchemas(ordered);
        var scopes = ParseScopes(ordered);
        var operations = scopes.SelectMany(ParseOperations)
            .OrderBy(operation => operation.Scope.File.RelativePath, StringComparer.Ordinal)
            .ThenBy(operation => operation.Index)
            .ToArray();
        return new MartenSourceModel
        {
            Scopes = scopes,
            Operations = operations,
            Declarations = declarations,
            SchemaFacts = schemas
        };
    }

    private static IReadOnlyDictionary<string, MartenDocumentDeclaration> ParseDeclarations(
        IEnumerable<RepositoryFile> files)
    {
        var declarations = new Dictionary<string, MartenDocumentDeclaration>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            foreach (Match match in TypeDeclarationRegex().Matches(file.Content))
            {
                var name = match.Groups["name"].Value;
                declarations[name] = new MartenDocumentDeclaration(name, file, match.Groups["name"].Index);
            }
        }

        return declarations;
    }

    private static IReadOnlyList<MartenSchemaFact> ParseSchemas(IEnumerable<RepositoryFile> files) => files
        .SelectMany(file => SchemaRegex().Matches(file.Content)
            .Select(match => new MartenSchemaFact(
                CleanType(match.Groups["type"].Value),
                file,
                match.Index)))
        .OrderBy(fact => fact.File.RelativePath, StringComparer.Ordinal)
        .ThenBy(fact => fact.Index)
        .ToArray();

    private static IReadOnlyList<MartenTypeScope> ParseScopes(IEnumerable<RepositoryFile> files)
    {
        var scopes = new List<MartenTypeScope>();
        foreach (var file in files)
        {
            foreach (Match match in TypeDeclarationRegex().Matches(file.Content))
            {
                var bodyOpen = FindBodyOpen(file.Content, match.Index + match.Length);
                if (bodyOpen < 0)
                {
                    continue;
                }

                var bodyClose = MartenTextParsing.FindBalancedClose(file.Content, bodyOpen, '{', '}');
                if (bodyClose < 0)
                {
                    continue;
                }

                var header = file.Content[match.Index..bodyOpen];
                var session = SessionRegex().Match(header);
                if (!session.Success)
                {
                    continue;
                }

                scopes.Add(new MartenTypeScope(
                    match.Groups["name"].Value,
                    file,
                    match.Groups["name"].Index,
                    bodyOpen + 1,
                    header,
                    file.Content[(bodyOpen + 1)..bodyClose],
                    session.Groups["type"].Value,
                    session.Groups["name"].Value,
                    header.Contains("IRequestHandler", StringComparison.Ordinal)));
            }
        }

        return scopes.OrderBy(scope => scope.File.RelativePath, StringComparer.Ordinal)
            .ThenBy(scope => scope.DeclarationIndex)
            .ToArray();
    }

    private static IReadOnlyList<MartenOperationFact> ParseOperations(MartenTypeScope scope)
    {
        var operations = new List<MartenOperationFact>();
        var session = Regex.Escape(scope.SessionVariable);
        AddGenericOperations(scope, operations, QueryRegex(session), MartenOperationKind.Query);
        AddGenericOperations(scope, operations, LoadRegex(session), MartenOperationKind.Load);
        AddGenericOperations(scope, operations, GenericDeleteRegex(session), MartenOperationKind.Delete);
        AddArgumentOperations(scope, operations, StoreRegex(session), MartenOperationKind.Store);
        AddArgumentOperations(scope, operations, DeleteRegex(session), MartenOperationKind.Delete, skipGeneric: true);

        foreach (Match save in Regex.Matches(
                     scope.Body,
                     $@"\b{session}\s*\.\s*SaveChangesAsync\s*\(",
                     RegexOptions.CultureInvariant))
        {
            operations.Add(new MartenOperationFact(
                scope,
                MartenOperationKind.SaveChanges,
                null,
                scope.BodyIndex + save.Index,
                false));
        }

        return operations
            .DistinctBy(operation => new { operation.Kind, operation.DocumentType, operation.Index })
            .ToArray();
    }

    private static void AddGenericOperations(
        MartenTypeScope scope,
        ICollection<MartenOperationFact> operations,
        Regex regex,
        MartenOperationKind kind)
    {
        foreach (Match match in regex.Matches(scope.Body))
        {
            var statementEnd = MartenTextParsing.FindStatementEnd(scope.Body, match.Index + match.Length);
            var statement = scope.Body[match.Index..Math.Min(scope.Body.Length, statementEnd + 1)];
            operations.Add(new MartenOperationFact(
                scope,
                kind,
                CleanType(match.Groups["type"].Value),
                scope.BodyIndex + match.Index,
                kind == MartenOperationKind.Query &&
                    statement.Contains(".Skip(", StringComparison.Ordinal) &&
                    statement.Contains(".Take(", StringComparison.Ordinal)));
        }
    }

    private static void AddArgumentOperations(
        MartenTypeScope scope,
        ICollection<MartenOperationFact> operations,
        Regex regex,
        MartenOperationKind kind,
        bool skipGeneric = false)
    {
        foreach (Match match in regex.Matches(scope.Body))
        {
            if (skipGeneric && match.Value.Contains('<'))
            {
                continue;
            }

            var openIndex = scope.Body.IndexOf('(', match.Index);
            var closeIndex = MartenTextParsing.FindBalancedClose(scope.Body, openIndex, '(', ')');
            if (closeIndex < 0)
            {
                continue;
            }

            foreach (var argument in MartenTextParsing.SplitTopLevel(scope.Body[(openIndex + 1)..closeIndex]))
            {
                var documentType = ResolveArgumentType(scope.Body, argument, match.Index);
                if (documentType is null)
                {
                    continue;
                }

                operations.Add(new MartenOperationFact(
                    scope,
                    kind,
                    documentType,
                    scope.BodyIndex + match.Index,
                    false));
            }
        }
    }

    private static string? ResolveArgumentType(string body, string expression, int beforeIndex)
    {
        var value = expression.Trim();
        var direct = NewTypeRegex().Match(value);
        if (direct.Success)
        {
            return CleanType(direct.Groups["type"].Value);
        }

        if (!IdentifierRegex().IsMatch(value))
        {
            return null;
        }

        var prefix = body[..Math.Min(beforeIndex, body.Length)];
        var pattern = $@"\b(?:var|[A-Za-z_$][\w$<>,?.\[\]]*)\s+{Regex.Escape(value)}\s*=\s*(?:await\s+)?(?:(?:new\s+(?<newType>[A-Za-z_$][\w$.]*)\s*\()|(?:[A-Za-z_$][\w$]*\s*\.\s*LoadAsync\s*<(?<loadType>[^>]+)>))";
        var assignments = Regex.Matches(prefix, pattern, RegexOptions.CultureInvariant);
        var assignment = assignments.LastOrDefault();
        if (assignment is null)
        {
            return null;
        }

        var type = assignment.Groups["newType"].Success
            ? assignment.Groups["newType"].Value
            : assignment.Groups["loadType"].Value;
        return CleanType(type);
    }

    private static int FindBodyOpen(string source, int startIndex)
    {
        var round = 0;
        var angle = 0;
        for (var index = startIndex; index < source.Length; index++)
        {
            switch (source[index])
            {
                case '(':
                    round++;
                    break;
                case ')':
                    round--;
                    break;
                case '<':
                    angle++;
                    break;
                case '>':
                    angle--;
                    break;
                case '{' when round == 0 && angle == 0:
                    return index;
                case ';' when round == 0 && angle == 0:
                    return -1;
            }
        }

        return -1;
    }

    private static string CleanType(string value)
    {
        var type = value.Trim().TrimEnd('?');
        var separator = type.LastIndexOf('.');
        return separator >= 0 ? type[(separator + 1)..] : type;
    }

    private static Regex QueryRegex(string session) => new(
        $@"\b{session}\s*\.\s*Query\s*<(?<type>[^>]+)>\s*\(",
        RegexOptions.CultureInvariant);

    private static Regex LoadRegex(string session) => new(
        $@"\b{session}\s*\.\s*LoadAsync\s*<(?<type>[^>]+)>\s*\(",
        RegexOptions.CultureInvariant);

    private static Regex GenericDeleteRegex(string session) => new(
        $@"\b{session}\s*\.\s*Delete\s*<(?<type>[^>]+)>\s*\(",
        RegexOptions.CultureInvariant);

    private static Regex StoreRegex(string session) => new(
        $@"\b{session}\s*\.\s*Store\s*\(",
        RegexOptions.CultureInvariant);

    private static Regex DeleteRegex(string session) => new(
        $@"\b{session}\s*\.\s*Delete(?:\s*<[^>]+>)?\s*\(",
        RegexOptions.CultureInvariant);

    [GeneratedRegex(@"(?m)^\s*(?:(?:public|internal|private|protected|static|sealed|abstract|partial|readonly)\s+)*(?:record(?:\s+(?:class|struct))?|class|interface)\s+(?<name>[A-Za-z_$][\w$]*)")]
    private static partial Regex TypeDeclarationRegex();

    [GeneratedRegex(@"\b(?<type>IQuerySession|IDocumentSession)\s+(?<name>[A-Za-z_$][\w$]*)")]
    private static partial Regex SessionRegex();

    [GeneratedRegex(@"\.Schema\s*\.\s*For\s*<(?<type>[^>]+)>\s*\(")]
    private static partial Regex SchemaRegex();

    [GeneratedRegex(@"\bnew\s+(?<type>[A-Za-z_$][\w$.]*)\s*\(")]
    private static partial Regex NewTypeRegex();

    [GeneratedRegex(@"^[A-Za-z_$][\w$]*$")]
    private static partial Regex IdentifierRegex();
}

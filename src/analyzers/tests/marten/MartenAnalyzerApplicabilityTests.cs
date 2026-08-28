using VietAIS.TCFlow.Analyzers.Core;
using VietAIS.TCFlow.Analyzers.Marten;
using Xunit;

namespace VietAIS.TCFlow.Analyzers.Marten.Tests;

public sealed class MartenAnalyzerApplicabilityTests
{
    [Fact]
    public void MartenSignalsAreRequiredBeforeCSharpFilesAreAnalyzedAsPersistenceSource()
    {
        var analyzer = new MartenAnalyzer();
        RepositoryFile[] plainRepository =
        [new("Program.cs", "/repo/Program.cs", "Console.WriteLine(\"hello\");")];
        RepositoryFile[] martenRepository =
        [new("Handler.cs", "/repo/Handler.cs", "public sealed class Handler(IDocumentSession session);")];

        Assert.False(analyzer.SupportsRepository(plainRepository));
        Assert.True(analyzer.SupportsRepository(martenRepository));
    }
}

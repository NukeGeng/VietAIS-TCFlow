using VietAIS.TCFlow.Analyzers.AspNet;
using VietAIS.TCFlow.Analyzers.Core;
using Xunit;

namespace VietAIS.TCFlow.Analyzers.AspNet.Tests;

public sealed class AspNetAnalyzerApplicabilityTests
{
    [Fact]
    public void AspNetSignalsAreRequiredBeforeCSharpFilesAreAnalyzedAsWebSource()
    {
        var analyzer = new AspNetAnalyzer();
        RepositoryFile[] consoleRepository =
        [new("Program.cs", "/repo/Program.cs", "Console.WriteLine(\"hello\");")];
        RepositoryFile[] webRepository =
        [new("Program.cs", "/repo/Program.cs", "var builder = WebApplication.CreateBuilder(args);")];

        Assert.False(analyzer.SupportsRepository(consoleRepository));
        Assert.True(analyzer.SupportsRepository(webRepository));
    }
}

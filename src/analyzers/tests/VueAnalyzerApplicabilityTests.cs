using VietAIS.TCFlow.Analyzers.Core;
using VietAIS.TCFlow.Analyzers.Vue;
using Xunit;

namespace VietAIS.TCFlow.Analyzers.Tests;

public sealed class VueAnalyzerApplicabilityTests
{
    [Fact]
    public void VueSignalsAreRequiredBeforeTypeScriptFilesAreAnalyzedAsVue()
    {
        var analyzer = new VueAnalyzer();
        RepositoryFile[] nextRepository =
        [
            new("package.json", "/repo/package.json", "{\"dependencies\":{\"next\":\"latest\"}}"),
            new("src/app/page.tsx", "/repo/src/app/page.tsx", "export default () => <main />;")
        ];
        RepositoryFile[] vueRepository =
        [
            new("package.json", "/repo/package.json", "{\"dependencies\":{\"vue\":\"latest\"}}"),
            new("src/main.ts", "/repo/src/main.ts", "import { createApp } from 'vue';")
        ];

        Assert.False(analyzer.SupportsRepository(nextRepository));
        Assert.True(analyzer.SupportsRepository(vueRepository));
    }
}

namespace DependencyAnalyzer.Tests;

/// <summary>
/// Validates that the test report document exists and contains the expected structure.
/// </summary>
public class TestReportDocTests
{
    private static string GetReportPath()
    {
        var testDir = AppContext.BaseDirectory;
        var repoRoot = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", ".."));
        return Path.Combine(repoRoot, "docs", "test-report.md");
    }

    [Fact]
    public void TestReport_FileExists()
    {
        var path = GetReportPath();
        Assert.True(File.Exists(path), $"Test report not found at {path}");
    }

    [Fact]
    public void TestReport_ContainsTraceabilityMatrix()
    {
        var content = File.ReadAllText(GetReportPath());
        Assert.Contains("Requirement Traceability Matrix", content);
    }

    [Fact]
    public void TestReport_ContainsAllTestFiles()
    {
        var content = File.ReadAllText(GetReportPath());
        Assert.Contains("RoslynWorkspaceBuilderTests", content);
        Assert.Contains("DependencyGraphBuilderTests", content);
        Assert.Contains("TransitiveFanInAnalyzerTests", content);
        Assert.Contains("MarkdownReportGeneratorTests", content);
        Assert.Contains("EndToEndTests", content);
        Assert.Contains("ComprehensiveDependencyTests", content);
        Assert.Contains("GapProbeTests", content);
        Assert.Contains("Round3AuditProbeTests", content);
        Assert.Contains("Round4FinalSweepTests", content);
        Assert.Contains("PortableExeTests", content);
        Assert.Contains("CiWorkflowTests", content);
    }

    [Fact]
    public void TestReport_ContainsCoverageByConstruct()
    {
        var content = File.ReadAllText(GetReportPath());
        Assert.Contains("Coverage by C# Construct Category", content);
    }

    [Fact]
    public void TestReport_ContainsCrossCheckHistory()
    {
        var content = File.ReadAllText(GetReportPath());
        Assert.Contains("Cross-Check History", content);
    }
}

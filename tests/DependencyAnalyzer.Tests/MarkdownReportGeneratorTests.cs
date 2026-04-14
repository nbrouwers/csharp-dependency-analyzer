using DependencyAnalyzer.Models;
using DependencyAnalyzer.Reporting;

namespace DependencyAnalyzer.Tests;

public class MarkdownReportGeneratorTests
{
    [Fact]
    public void Generate_ContainsTargetHeader()
    {
        var result = new AnalysisResult
        {
            TargetFqn = "N.Target",
            FanInElements = new List<FanInElement>()
        };

        var generator = new MarkdownReportGenerator();
        var report = generator.Generate(result);

        Assert.Contains("## Target: `N.Target`", report);
    }

    [Fact]
    public void Generate_ContainsDateHeader()
    {
        var result = new AnalysisResult
        {
            TargetFqn = "N.Target",
            FanInElements = new List<FanInElement>()
        };

        var generator = new MarkdownReportGenerator();
        var report = generator.Generate(result);

        Assert.Contains("## Date:", report);
    }

    [Fact]
    public void Generate_EmptyFanIn_ShowsNoElementsMessage()
    {
        var result = new AnalysisResult
        {
            TargetFqn = "N.Target",
            FanInElements = new List<FanInElement>()
        };

        var generator = new MarkdownReportGenerator();
        var report = generator.Generate(result);

        Assert.Contains("No fan-in elements found", report);
    }

    [Fact]
    public void Generate_WithElements_ContainsTableRows()
    {
        var result = new AnalysisResult
        {
            TargetFqn = "N.Target",
            FanInElements = new List<FanInElement>
            {
                new("N.ClassA", ElementKind.Class, "Field type `Target`"),
                new("N.IFoo", ElementKind.Interface, "Method return type `Target`"),
            }
        };

        var generator = new MarkdownReportGenerator();
        var report = generator.Generate(result);

        Assert.Contains("N.ClassA", report);
        Assert.Contains("N.IFoo", report);
        Assert.Contains("Class", report);
        Assert.Contains("Interface", report);
        Assert.Contains("Field type", report);
    }

    [Fact]
    public void Generate_ContainsMetricsTable()
    {
        var result = new AnalysisResult
        {
            TargetFqn = "N.Target",
            FanInElements = new List<FanInElement>
            {
                new("N.A", ElementKind.Class, "reason"),
                new("N.B", ElementKind.Class, "reason"),
                new("N.I", ElementKind.Interface, "reason"),
            }
        };

        var generator = new MarkdownReportGenerator();
        var report = generator.Generate(result);

        Assert.Contains("## Metrics", report);
        Assert.Contains("| **Total** | **3** |", report);
    }

    [Fact]
    public void GenerateToFile_WritesFile()
    {
        var result = new AnalysisResult
        {
            TargetFqn = "N.Target",
            FanInElements = new List<FanInElement>()
        };

        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".md");
        try
        {
            var generator = new MarkdownReportGenerator();
            generator.GenerateToFile(result, tempFile);

            Assert.True(File.Exists(tempFile));
            var content = File.ReadAllText(tempFile);
            Assert.Contains("N.Target", content);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}

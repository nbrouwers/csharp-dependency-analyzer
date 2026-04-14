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
            FanInElements = new List<FanInElement>(),
            MaxTransitiveDepth = 0,
            FanInEdges = new List<(string, string)>()
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
            FanInElements = new List<FanInElement>(),
            MaxTransitiveDepth = 0,
            FanInEdges = new List<(string, string)>()
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
            FanInElements = new List<FanInElement>(),
            MaxTransitiveDepth = 0,
            FanInEdges = new List<(string, string)>()
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
            },
            MaxTransitiveDepth = 1,
            FanInEdges = new List<(string, string)>
            {
                ("N.ClassA", "N.Target"),
                ("N.IFoo", "N.Target")
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
            },
            MaxTransitiveDepth = 1,
            FanInEdges = new List<(string, string)>
            {
                ("N.A", "N.Target"),
                ("N.B", "N.Target"),
                ("N.I", "N.Target")
            }
        };

        var generator = new MarkdownReportGenerator();
        var report = generator.Generate(result);

        Assert.Contains("## Metrics", report);
        Assert.Contains("| **Total** | **3** |", report);
    }

    [Fact]
    public void Generate_ContainsMaxTransitiveDepthRow()
    {
        var result = new AnalysisResult
        {
            TargetFqn = "N.Target",
            FanInElements = new List<FanInElement>
            {
                new("N.A", ElementKind.Class, "reason"),
            },
            MaxTransitiveDepth = 4,
            FanInEdges = new List<(string, string)>
            {
                ("N.A", "N.Target")
            }
        };

        var generator = new MarkdownReportGenerator();
        var report = generator.Generate(result);

        Assert.Contains("| **Max Transitive Depth** | **4** |", report);
    }

    [Fact]
    public void GenerateToFile_WritesFile()
    {
        var result = new AnalysisResult
        {
            TargetFqn = "N.Target",
            FanInElements = new List<FanInElement>(),
            MaxTransitiveDepth = 0,
            FanInEdges = new List<(string, string)>()
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

    [Fact]
    public void Generate_WithElements_ContainsMermaidGraph()
    {
        var result = new AnalysisResult
        {
            TargetFqn = "N.Target",
            FanInElements = new List<FanInElement>
            {
                new("N.A", ElementKind.Class, "reason"),
                new("N.B", ElementKind.Interface, "reason"),
            },
            MaxTransitiveDepth = 1,
            FanInEdges = new List<(string, string)>
            {
                ("N.A", "N.Target"),
                ("N.B", "N.Target")
            }
        };

        var generator = new MarkdownReportGenerator();
        var report = generator.Generate(result);

        Assert.Contains("## Dependency Graph", report);
        Assert.Contains("```mermaid", report);
        Assert.Contains("graph LR", report);
        Assert.Contains("classDef target", report);
    }

    [Fact]
    public void Generate_MermaidGraph_ContainsTargetNode()
    {
        var result = new AnalysisResult
        {
            TargetFqn = "N.Target",
            FanInElements = new List<FanInElement>
            {
                new("N.A", ElementKind.Class, "reason"),
            },
            MaxTransitiveDepth = 1,
            FanInEdges = new List<(string, string)>
            {
                ("N.A", "N.Target")
            }
        };

        var generator = new MarkdownReportGenerator();
        var report = generator.Generate(result);

        Assert.Contains("[\"Target\"]:::target", report);
    }

    [Fact]
    public void Generate_MermaidGraph_ContainsEdges()
    {
        var result = new AnalysisResult
        {
            TargetFqn = "N.Target",
            FanInElements = new List<FanInElement>
            {
                new("N.A", ElementKind.Class, "reason"),
                new("N.B", ElementKind.Class, "reason"),
            },
            MaxTransitiveDepth = 2,
            FanInEdges = new List<(string, string)>
            {
                ("N.A", "N.Target"),
                ("N.B", "N.A")
            }
        };

        var generator = new MarkdownReportGenerator();
        var report = generator.Generate(result);

        // Two edges should appear
        Assert.Equal(2, CountOccurrences(report, " --> "));
    }

    [Fact]
    public void Generate_MermaidGraph_AppliesKindStyles()
    {
        var result = new AnalysisResult
        {
            TargetFqn = "N.Target",
            FanInElements = new List<FanInElement>
            {
                new("N.A", ElementKind.Class, "reason"),
                new("N.I", ElementKind.Interface, "reason"),
            },
            MaxTransitiveDepth = 1,
            FanInEdges = new List<(string, string)>
            {
                ("N.A", "N.Target"),
                ("N.I", "N.Target")
            }
        };

        var generator = new MarkdownReportGenerator();
        var report = generator.Generate(result);

        Assert.Contains(":::kind_class", report);
        Assert.Contains(":::kind_interface", report);
    }

    [Fact]
    public void Generate_EmptyFanIn_NoMermaidGraph()
    {
        var result = new AnalysisResult
        {
            TargetFqn = "N.Target",
            FanInElements = new List<FanInElement>(),
            MaxTransitiveDepth = 0,
            FanInEdges = new List<(string, string)>()
        };

        var generator = new MarkdownReportGenerator();
        var report = generator.Generate(result);

        Assert.DoesNotContain("```mermaid", report);
    }

    [Fact]
    public void Generate_ContainsVersionLine()
    {
        var result = new AnalysisResult
        {
            TargetFqn = "N.Target",
            FanInElements = new List<FanInElement>(),
            MaxTransitiveDepth = 0,
            FanInEdges = new List<(string, string)>()
        };

        var generator = new MarkdownReportGenerator();
        var report = generator.Generate(result);

        Assert.Contains("*Generated by C# Dependency Analyzer v", report);
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }
}

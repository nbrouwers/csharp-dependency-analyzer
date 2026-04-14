using System.Text;
using DependencyAnalyzer.Models;

namespace DependencyAnalyzer.Reporting;

/// <summary>
/// Generates a Markdown dependency analysis report from an AnalysisResult.
/// </summary>
public sealed class MarkdownReportGenerator
{
    public string Generate(AnalysisResult result)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Dependency Analysis Report");
        sb.AppendLine();
        sb.AppendLine($"## Target: `{result.TargetFqn}`");
        sb.AppendLine();
        sb.AppendLine($"## Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();

        // Fan-In Elements table
        sb.AppendLine("## Fan-In Elements");
        sb.AppendLine();

        if (result.FanInElements.Count == 0)
        {
            sb.AppendLine("No fan-in elements found.");
        }
        else
        {
            sb.AppendLine("| # | Fully Qualified Name | Kind | Justification |");
            sb.AppendLine("|---|----------------------|------|---------------|");

            int index = 1;
            foreach (var element in result.FanInElements)
            {
                sb.AppendLine($"| {index} | `{element.FullyQualifiedName}` | {element.Kind} | {element.Justification} |");
                index++;
            }
        }

        sb.AppendLine();

        // Metrics table
        sb.AppendLine("## Metrics");
        sb.AppendLine();
        sb.AppendLine("| Kind | Count |");
        sb.AppendLine("|------|-------|");

        var metrics = result.MetricsByKind;

        // Show all kinds in enum order, even if count is 0
        foreach (var kind in System.Enum.GetValues<ElementKind>())
        {
            var count = metrics.TryGetValue(kind, out var c) ? c : 0;
            if (count > 0)
                sb.AppendLine($"| {kind} | {count} |");
        }

        sb.AppendLine($"| **Total** | **{result.TotalFanInCount}** |");
        sb.AppendLine($"| **Max Transitive Depth** | **{result.MaxTransitiveDepth}** |");
        sb.AppendLine();

        return sb.ToString();
    }

    public void GenerateToFile(AnalysisResult result, string outputPath)
    {
        var content = Generate(result);
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(outputPath, content);
    }
}

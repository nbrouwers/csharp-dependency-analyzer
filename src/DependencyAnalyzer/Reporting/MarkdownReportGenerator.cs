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

        // Dependency graph (Mermaid)
        if (result.FanInElements.Count > 0)
        {
            sb.AppendLine("## Dependency Graph");
            sb.AppendLine();
            sb.AppendLine("```mermaid");
            sb.AppendLine("graph LR");

            // Build a safe node ID from an FQN
            var nodeIds = new Dictionary<string, string>();
            int nodeCounter = 0;

            string GetNodeId(string fqn)
            {
                if (!nodeIds.TryGetValue(fqn, out var id))
                {
                    id = $"n{nodeCounter++}";
                    nodeIds[fqn] = id;
                }
                return id;
            }

            string GetShortName(string fqn)
            {
                var lastDot = fqn.LastIndexOf('.');
                return lastDot >= 0 ? fqn[(lastDot + 1)..] : fqn;
            }

            // Target node
            var targetId = GetNodeId(result.TargetFqn);
            sb.AppendLine($"    {targetId}[\"{GetShortName(result.TargetFqn)}\"]:::target");

            // Fan-in element nodes
            foreach (var element in result.FanInElements)
            {
                var id = GetNodeId(element.FullyQualifiedName);
                var cssClass = "kind_" + element.Kind.ToString().ToLowerInvariant();
                sb.AppendLine($"    {id}[\"{GetShortName(element.FullyQualifiedName)}\"]:::{cssClass}");
            }

            // Edges (dependor → what it depends on)
            foreach (var (source, target) in result.FanInEdges)
            {
                sb.AppendLine($"    {GetNodeId(source)} --> {GetNodeId(target)}");
            }

            // Style definitions
            sb.AppendLine("    classDef target fill:#f96,stroke:#333,stroke-width:2px");
            sb.AppendLine("    classDef kind_class fill:#bbf,stroke:#333");
            sb.AppendLine("    classDef kind_interface fill:#bfb,stroke:#333");
            sb.AppendLine("    classDef kind_struct fill:#fbf,stroke:#333");
            sb.AppendLine("    classDef kind_enum fill:#ffb,stroke:#333");
            sb.AppendLine("    classDef kind_record fill:#bff,stroke:#333");
            sb.AppendLine("    classDef kind_delegate fill:#fbb,stroke:#333");
            sb.AppendLine("```");
            sb.AppendLine();
        }

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

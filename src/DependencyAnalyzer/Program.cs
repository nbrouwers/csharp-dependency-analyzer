using System.CommandLine;
using System.Reflection;
using DependencyAnalyzer.Analysis;
using DependencyAnalyzer.Reporting;

// ---------------------------------------------------------------------------
// Shared options
// ---------------------------------------------------------------------------

var filesOption = new Option<string>(
    "--files",
    "Path to a text file containing one source file path per line")
{ IsRequired = true };

// ---------------------------------------------------------------------------
// 'analyze' subcommand
// ---------------------------------------------------------------------------

var analyzeCommand = new Command("analyze",
    "Compute dependency analysis for a target class and produce a structured report");

var targetOption = new Option<string>(
    "--target",
    "Fully qualified name of the target class")
{ IsRequired = true };

var outputOption = new Option<string>(
    "--output",
    () => "dependency-report.md",
    "Path for the output Markdown report file");

var modeOption = new Option<string>(
    "--mode",
    () => "fan-in",
    "Analysis mode: fan-in (default). Reserved for future use: fan-out, combined");

analyzeCommand.AddOption(targetOption);
analyzeCommand.AddOption(filesOption);
analyzeCommand.AddOption(outputOption);
analyzeCommand.AddOption(modeOption);

analyzeCommand.SetHandler(RunAnalyze, targetOption, filesOption, outputOption, modeOption);

// ---------------------------------------------------------------------------
// 'export' subcommand
// ---------------------------------------------------------------------------

var exportCommand = new Command("export",
    "Export the full dependency graph to an external format (e.g. Doxygen XML for Neo4j ingestion)");

var formatOption = new Option<string>(
    "--format",
    "Export format. Supported values: doxygen")
{ IsRequired = true };

var outputDirOption = new Option<string>(
    "--output-dir",
    "Directory to write the exported files into (created if absent)")
{ IsRequired = true };

var exportFilesOption = new Option<string>(
    "--files",
    "Path to a text file containing one source file path per line")
{ IsRequired = true };

exportCommand.AddOption(exportFilesOption);
exportCommand.AddOption(formatOption);
exportCommand.AddOption(outputDirOption);

exportCommand.SetHandler(RunExport, exportFilesOption, formatOption, outputDirOption);

// ---------------------------------------------------------------------------
// Root command
// ---------------------------------------------------------------------------

var rootCommand = new RootCommand(
    "C# Dependency Analyzer — static analysis of C# source dependencies")
{
    analyzeCommand,
    exportCommand
};

return await rootCommand.InvokeAsync(args);

// ---------------------------------------------------------------------------
// Handlers
// ---------------------------------------------------------------------------

static void RunAnalyze(string targetFqn, string filesPath, string outputPath, string mode)
{
    try
    {
        if (mode != "fan-in")
        {
            Console.Error.WriteLine($"ERROR: Unknown analysis mode '{mode}'. Supported: fan-in");
            Environment.ExitCode = 1;
            return;
        }

        var version = GetVersion();
        Console.WriteLine($"C# Dependency Analyzer v{version}");
        Console.WriteLine($"Mode:   analyze / {mode}");
        Console.WriteLine($"Target: {targetFqn}");
        Console.WriteLine();

        var sourceFiles = ReadFileList(filesPath);
        if (sourceFiles is null) return;

        Console.WriteLine($"Source files listed: {sourceFiles.Count}");
        Console.WriteLine();

        Console.WriteLine("[1/4] Building Roslyn compilation...");
        var workspaceBuilder = new RoslynWorkspaceBuilder();
        var compilation = workspaceBuilder.BuildCompilation(sourceFiles);

        Console.WriteLine();
        Console.WriteLine("[2/4] Resolving target class...");
        var targetSymbol = workspaceBuilder.ResolveTargetClass(compilation, targetFqn);
        Console.WriteLine($"Target resolved: {targetFqn} ({targetSymbol.TypeKind})");

        Console.WriteLine();
        Console.WriteLine("[3/4] Analyzing dependencies...");
        var graphBuilder = new DependencyGraphBuilder();
        var graph = graphBuilder.Build(compilation);

        Console.WriteLine();
        Console.WriteLine("[4/4] Computing transitive fan-in...");
        var analyzer = new TransitiveFanInAnalyzer();
        var result = analyzer.Analyze(targetFqn, graph);

        Console.WriteLine($"Found {result.TotalFanInCount} fan-in element(s).");
        Console.WriteLine($"Max transitive depth: {result.MaxTransitiveDepth} layer(s).");

        Console.WriteLine();
        var reportGenerator = new MarkdownReportGenerator();
        reportGenerator.GenerateToFile(result, outputPath);
        Console.WriteLine($"Report written to: {Path.GetFullPath(outputPath)}");
    }
    catch (InvalidOperationException ex)
    {
        Console.Error.WriteLine($"ERROR: {ex.Message}");
        Environment.ExitCode = 1;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"UNEXPECTED ERROR: {ex.Message}");
        Console.Error.WriteLine(ex.StackTrace);
        Environment.ExitCode = 2;
    }
}

static void RunExport(string filesPath, string format, string outputDir)
{
    try
    {
        if (format != "doxygen")
        {
            Console.Error.WriteLine($"ERROR: Unknown export format '{format}'. Supported: doxygen");
            Environment.ExitCode = 1;
            return;
        }

        var version = GetVersion();
        Console.WriteLine($"C# Dependency Analyzer v{version}");
        Console.WriteLine($"Mode:   export / {format}");
        Console.WriteLine();

        var sourceFiles = ReadFileList(filesPath);
        if (sourceFiles is null) return;

        Console.WriteLine($"Source files listed: {sourceFiles.Count}");
        Console.WriteLine();

        Console.WriteLine("[1/2] Building Roslyn compilation...");
        var workspaceBuilder = new RoslynWorkspaceBuilder();
        var compilation = workspaceBuilder.BuildCompilation(sourceFiles);

        Console.WriteLine();
        Console.WriteLine("[2/2] Building dependency graph...");
        var graphBuilder = new DependencyGraphBuilder();
        var graph = graphBuilder.Build(compilation);

        Console.WriteLine();
        Console.WriteLine($"[Doxygen] Writing XML to: {Path.GetFullPath(outputDir)}");
        var exporter = new DoxygenXmlExporter();
        var fileCount = exporter.Export(graph, outputDir);
        Console.WriteLine($"[Doxygen] Wrote {fileCount} XML file(s).");
    }
    catch (InvalidOperationException ex)
    {
        Console.Error.WriteLine($"ERROR: {ex.Message}");
        Environment.ExitCode = 1;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"UNEXPECTED ERROR: {ex.Message}");
        Console.Error.WriteLine(ex.StackTrace);
        Environment.ExitCode = 2;
    }
}

// ---------------------------------------------------------------------------
// Shared helpers
// ---------------------------------------------------------------------------

static string GetVersion() =>
    typeof(MarkdownReportGenerator).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
    ?? typeof(MarkdownReportGenerator).Assembly.GetName().Version?.ToString()
    ?? "unknown";

/// <summary>
/// Reads and resolves a file-list text file. Returns null and sets ExitCode = 1 on failure.
/// </summary>
static List<string>? ReadFileList(string filesPath)
{
    if (!File.Exists(filesPath))
    {
        Console.Error.WriteLine($"ERROR: File list not found: {filesPath}");
        Environment.ExitCode = 1;
        return null;
    }

    var basePath = Path.GetDirectoryName(Path.GetFullPath(filesPath)) ?? ".";
    return File.ReadAllLines(filesPath)
        .Select(line => line.Trim())
        .Where(line => !string.IsNullOrEmpty(line) && !line.StartsWith('#'))
        .Select(line => Path.IsPathRooted(line) ? line : Path.Combine(basePath, line))
        .ToList();
}


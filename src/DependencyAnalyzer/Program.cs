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
    "Export the full dependency graph to an external format (e.g. Doxygen XML or direct Neo4j import)");

var formatOption = new Option<string>(
    "--format",
    "Export format. Supported values: doxygen, neo4j, csv")
{ IsRequired = true };

// Required for --format doxygen; validated at runtime for other formats
var outputDirOption = new Option<string?>(
    "--output-dir",
    "Directory to write the exported files into (created if absent). Required for --format doxygen.");

var exportFilesOption = new Option<string>(
    "--files",
    "Path to a text file containing one source file path per line")
{ IsRequired = true };

// Neo4j connection options — used when --format neo4j
var neo4jUriOption = new Option<string>(
    "--neo4j-uri",
    () => "bolt://localhost:7687",
    "Bolt URI of the Neo4j server");

var neo4jUserOption = new Option<string>(
    "--neo4j-user",
    () => "neo4j",
    "Neo4j username");

var neo4jPasswordOption = new Option<string?>(
    "--neo4j-password",
    "Neo4j password. If omitted, the NEO4J_PASSWORD environment variable is read.");

var neo4jDatabaseOption = new Option<string>(
    "--neo4j-database",
    () => "neo4j",
    "Target Neo4j database name");

exportCommand.AddOption(exportFilesOption);
exportCommand.AddOption(formatOption);
exportCommand.AddOption(outputDirOption);
exportCommand.AddOption(neo4jUriOption);
exportCommand.AddOption(neo4jUserOption);
exportCommand.AddOption(neo4jPasswordOption);
exportCommand.AddOption(neo4jDatabaseOption);

exportCommand.SetHandler(RunExport,
    exportFilesOption, formatOption, outputDirOption,
    neo4jUriOption, neo4jUserOption, neo4jPasswordOption, neo4jDatabaseOption);

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

static void RunExport(
    string filesPath,
    string format,
    string? outputDir,
    string neo4jUri,
    string neo4jUser,
    string? neo4jPassword,
    string neo4jDatabase)
{
    try
    {
        if (format != "doxygen" && format != "neo4j" && format != "csv")
        {
            Console.Error.WriteLine($"ERROR: Unknown export format '{format}'. Supported: doxygen, neo4j, csv");
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

        int typeCount = graph.ElementKinds.Count;
        int edgeCount = graph.Edges.Values.Sum(e => e.Count);
        Console.WriteLine($"\nDiscovered {typeCount} type(s), {edgeCount} dependency edge(s).");

        if (format == "doxygen")
        {
            if (outputDir is null)
            {
                Console.Error.WriteLine("ERROR: --output-dir is required when --format is doxygen.");
                Environment.ExitCode = 1;
                return;
            }

            Console.WriteLine($"[Doxygen] Writing XML to: {Path.GetFullPath(outputDir)}");
            var exporter = new DoxygenXmlExporter();
            var fileCount = exporter.Export(graph, outputDir);
            Console.WriteLine($"[Doxygen] Wrote {fileCount} XML file(s).");
        }
        else if (format == "csv")
        {
            if (outputDir is null)
            {
                Console.Error.WriteLine("ERROR: --output-dir is required when --format is csv.");
                Environment.ExitCode = 1;
                return;
            }

            var projectRoot = Path.GetDirectoryName(Path.GetFullPath(filesPath)) ?? ".";
            Console.WriteLine($"[CSV] Writing to: {Path.GetFullPath(outputDir)}");
            var exporter = new CsvExporter();
            var (nodesWritten, relsWritten) = exporter.Export(graph, outputDir, projectRoot);
            Console.WriteLine($"[CSV] Wrote {nodesWritten} node(s) and {relsWritten} relationship(s).");
        }
        else // neo4j
        {
            var password = neo4jPassword ?? Environment.GetEnvironmentVariable("NEO4J_PASSWORD");
            if (string.IsNullOrEmpty(password))
            {
                Console.Error.WriteLine(
                    "ERROR: --neo4j-password is required when --format is neo4j. " +
                    "Alternatively, set the NEO4J_PASSWORD environment variable.");
                Environment.ExitCode = 1;
                return;
            }

            Console.WriteLine($"[Neo4j] Connecting to {neo4jUri} (database: {neo4jDatabase})...");
            var exporter = new Neo4jExporter();
            var (nodesWritten, relsWritten) = exporter.ExportAsync(graph, neo4jUri, neo4jUser, password, neo4jDatabase)
                .GetAwaiter().GetResult();
            Console.WriteLine($"[Neo4j] Wrote {nodesWritten} node(s) and {relsWritten} relationship(s).");
        }
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


using System.CommandLine;
using DependencyAnalyzer.Analysis;
using DependencyAnalyzer.Reporting;

var targetOption = new Option<string>(
    "--target",
    "Fully qualified name of the target class")
{ IsRequired = true };

var filesOption = new Option<string>(
    "--files",
    "Path to a text file containing one source file path per line")
{ IsRequired = true };

var outputOption = new Option<string>(
    "--output",
    () => "dependency-report.md",
    "Path for the output report file");

var rootCommand = new RootCommand("C# Dependency Analyzer — computes transitive fan-in for a target class")
{
    targetOption,
    filesOption,
    outputOption
};

rootCommand.SetHandler(Run, targetOption, filesOption, outputOption);

return await rootCommand.InvokeAsync(args);

static void Run(string targetFqn, string filesPath, string outputPath)
{
    try
    {
        Console.WriteLine($"C# Dependency Analyzer");
        Console.WriteLine($"Target: {targetFqn}");
        Console.WriteLine();

        // Read file list
        if (!File.Exists(filesPath))
        {
            Console.Error.WriteLine($"ERROR: File list not found: {filesPath}");
            Environment.ExitCode = 1;
            return;
        }

        var basePath = Path.GetDirectoryName(Path.GetFullPath(filesPath)) ?? ".";
        var sourceFiles = File.ReadAllLines(filesPath)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrEmpty(line) && !line.StartsWith('#'))
            .Select(line => Path.IsPathRooted(line) ? line : Path.Combine(basePath, line))
            .ToList();

        Console.WriteLine($"Source files listed: {sourceFiles.Count}");

        // Phase 1: Build compilation
        Console.WriteLine();
        Console.WriteLine("[1/4] Building Roslyn compilation...");
        var workspaceBuilder = new RoslynWorkspaceBuilder();
        var compilation = workspaceBuilder.BuildCompilation(sourceFiles);

        // Validate target
        Console.WriteLine();
        Console.WriteLine("[2/4] Resolving target class...");
        var targetSymbol = workspaceBuilder.ResolveTargetClass(compilation, targetFqn);
        Console.WriteLine($"Target resolved: {targetFqn} ({targetSymbol.TypeKind})");

        // Phase 2: Build dependency graph
        Console.WriteLine();
        Console.WriteLine("[3/4] Analyzing dependencies...");
        var graphBuilder = new DependencyGraphBuilder();
        var graph = graphBuilder.Build(compilation);

        // Phase 3: Compute transitive fan-in
        Console.WriteLine();
        Console.WriteLine("[4/4] Computing transitive fan-in...");
        var analyzer = new TransitiveFanInAnalyzer();
        var result = analyzer.Analyze(targetFqn, graph);

        Console.WriteLine($"Found {result.TotalFanInCount} fan-in element(s).");

        // Phase 4: Generate report
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

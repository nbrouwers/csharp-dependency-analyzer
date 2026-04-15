using System.Diagnostics;

namespace DependencyAnalyzer.Tests;

/// <summary>
/// Verifies that the build produces a self-contained single-file portable executable.
/// </summary>
public class PortableExeTests
{
    private static string GetProjectDir()
    {
        // Navigate from test bin dir up to the src project
        var testDir = AppContext.BaseDirectory; // tests/.../bin/Debug/net8.0/
        var repoRoot = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", ".."));
        return Path.Combine(repoRoot, "src", "DependencyAnalyzer");
    }

    /// <summary>
    /// Returns the full path to the dotnet executable that is currently running
    /// this test process. Using the full path avoids relying on PATH being set
    /// inside the spawned child process.
    /// </summary>
    private static string FindDotnet()
    {
        // The test host IS dotnet — resolve from the running process.
        var mainModule = Process.GetCurrentProcess().MainModule?.FileName;
        if (!string.IsNullOrEmpty(mainModule) &&
            Path.GetFileNameWithoutExtension(mainModule).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            return mainModule;

        // Fallback: user-install location on Windows.
        var localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        if (!string.IsNullOrEmpty(localAppData))
        {
            var candidate = Path.Combine(localAppData, "Microsoft", "dotnet", "dotnet.exe");
            if (File.Exists(candidate)) return candidate;
        }

        return "dotnet";
    }

    [Fact]
    public void Publish_ProducesSingleExeFile()
    {
        var projectDir = GetProjectDir();
        var publishDir = Path.Combine(projectDir, "bin", "TestPublish", "net8.0", "win-x64", "publish");

        // Clean previous test publish
        if (Directory.Exists(publishDir))
            Directory.Delete(publishDir, true);

        var psi = new ProcessStartInfo
        {
            FileName = FindDotnet(),
            Arguments = $"publish \"{projectDir}\" -c Release -o \"{publishDir}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi)!;
        process.WaitForExit(120_000);

        Assert.Equal(0, process.ExitCode);

        // Should produce exactly one .exe file
        var exeFiles = Directory.GetFiles(publishDir, "*.exe");
        Assert.Single(exeFiles);
        Assert.Equal("DependencyAnalyzer.exe", Path.GetFileName(exeFiles[0]));

        // The exe should be reasonably large (self-contained bundles the runtime)
        var fileInfo = new FileInfo(exeFiles[0]);
        Assert.True(fileInfo.Length > 10_000_000, $"Expected self-contained exe > 10MB, got {fileInfo.Length / 1_000_000}MB");

        // Should NOT have any other .dll files (single-file bundles them)
        var dllFiles = Directory.GetFiles(publishDir, "*.dll");
        Assert.Empty(dllFiles);
    }

    [Fact]
    public void PublishedExe_ShowsHelp()
    {
        var projectDir = GetProjectDir();
        var publishDir = Path.Combine(projectDir, "bin", "TestPublish", "net8.0", "win-x64", "publish");
        var exePath = Path.Combine(publishDir, "DependencyAnalyzer.exe");

        // This test depends on Publish_ProducesSingleExeFile having run, but also works standalone
        if (!File.Exists(exePath))
        {
            var psi2 = new ProcessStartInfo
            {
                FileName = FindDotnet(),
                Arguments = $"publish \"{projectDir}\" -c Release -o \"{publishDir}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi2)!;
            proc.WaitForExit(120_000);
        }

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = "--help",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(30_000);

        Assert.Equal(0, process.ExitCode);
        Assert.Contains("analyze", output);
        Assert.Contains("export", output);
        Assert.Contains("C# Dependency Analyzer", output);
    }

    [Fact]
    public void PublishedExe_AnalyzesSampleCodebase()
    {
        var projectDir = GetProjectDir();
        var publishDir = Path.Combine(projectDir, "bin", "TestPublish", "net8.0", "win-x64", "publish");
        var exePath = Path.Combine(publishDir, "DependencyAnalyzer.exe");

        if (!File.Exists(exePath))
        {
            var psi2 = new ProcessStartInfo
            {
                FileName = FindDotnet(),
                Arguments = $"publish \"{projectDir}\" -c Release -o \"{publishDir}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi2)!;
            proc.WaitForExit(120_000);
        }

        var repoRoot = Path.GetFullPath(Path.Combine(projectDir, "..", ".."));
        var fileList = Path.Combine(repoRoot, "samples", "SampleCodebase", "filelist.txt");
        var outputPath = Path.Combine(Path.GetTempPath(), $"portable-test-{Guid.NewGuid()}.md");

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = $"analyze --target \"SampleApp.Core.OrderService\" --files \"{fileList}\" --output \"{outputPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi)!;
            var stdout = process.StandardOutput.ReadToEnd();
            process.WaitForExit(30_000);

            Assert.Equal(0, process.ExitCode);
            Assert.Contains("Found 12 fan-in element(s)", stdout);

            Assert.True(File.Exists(outputPath), "Report file should be generated");
            var report = File.ReadAllText(outputPath);
            Assert.Contains("SampleApp.Core.OrderService", report);
            Assert.Contains("Fan-In Elements", report);
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }
}

using System.Reflection;
using System.Text.RegularExpressions;
using DependencyAnalyzer.Reporting;

namespace DependencyAnalyzer.Tests;

/// <summary>
/// Validates semantic versioning configuration and assembly embedding.
/// </summary>
public class VersionTests
{
    private static string GetCsprojPath()
    {
        var testDir = AppContext.BaseDirectory;
        var repoRoot = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", ".."));
        return Path.Combine(repoRoot, "src", "DependencyAnalyzer", "DependencyAnalyzer.csproj");
    }

    [Fact]
    public void Csproj_ContainsVersionElement()
    {
        var content = File.ReadAllText(GetCsprojPath());
        Assert.Matches(@"<Version>\d+\.\d+\.\d+</Version>", content);
    }

    [Fact]
    public void Csproj_VersionFollowsSemVer()
    {
        var content = File.ReadAllText(GetCsprojPath());
        var match = Regex.Match(content, @"<Version>(\d+\.\d+\.\d+)</Version>");
        Assert.True(match.Success, "Version element not found in csproj");

        var parts = match.Groups[1].Value.Split('.');
        Assert.Equal(3, parts.Length);
        Assert.All(parts, p => Assert.True(int.TryParse(p, out _), $"'{p}' is not a valid integer"));
    }

    [Fact]
    public void Assembly_HasInformationalVersion()
    {
        var assembly = typeof(MarkdownReportGenerator).Assembly;
        var attr = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        Assert.NotNull(attr);
        Assert.False(string.IsNullOrWhiteSpace(attr.InformationalVersion));
    }

    [Fact]
    public void Assembly_VersionMatchesCsproj()
    {
        var content = File.ReadAllText(GetCsprojPath());
        var match = Regex.Match(content, @"<Version>(\d+\.\d+\.\d+)</Version>");
        Assert.True(match.Success);
        var csprojVersion = match.Groups[1].Value;

        var assembly = typeof(MarkdownReportGenerator).Assembly;
        var attr = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        Assert.NotNull(attr);

        // InformationalVersion may have a +commit suffix; strip it
        var assemblyVersion = attr.InformationalVersion.Split('+')[0];
        Assert.Equal(csprojVersion, assemblyVersion);
    }
}

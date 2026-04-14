namespace DependencyAnalyzer.Tests;

/// <summary>
/// Validates that the CI workflow file exists and has the expected structure.
/// </summary>
public class CiWorkflowTests
{
    private static string GetWorkflowPath()
    {
        var testDir = AppContext.BaseDirectory;
        var repoRoot = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", ".."));
        return Path.Combine(repoRoot, ".github", "workflows", "ci.yml");
    }

    [Fact]
    public void Workflow_FileExists()
    {
        var path = GetWorkflowPath();
        Assert.True(File.Exists(path), $"CI workflow not found at {path}");
    }

    [Fact]
    public void Workflow_ContainsTestJob()
    {
        var content = File.ReadAllText(GetWorkflowPath());
        Assert.Contains("dotnet test", content);
    }

    [Fact]
    public void Workflow_ContainsPublishJob()
    {
        var content = File.ReadAllText(GetWorkflowPath());
        Assert.Contains("dotnet publish", content);
    }

    [Fact]
    public void Workflow_PublishDependsOnTest()
    {
        var content = File.ReadAllText(GetWorkflowPath());
        Assert.Contains("needs: test", content);
    }

    [Fact]
    public void Workflow_UploadsArtifact()
    {
        var content = File.ReadAllText(GetWorkflowPath());
        Assert.Contains("upload-artifact", content);
        Assert.Contains("DependencyAnalyzer", content);
    }

    [Fact]
    public void Workflow_TriggersOnPush()
    {
        var content = File.ReadAllText(GetWorkflowPath());
        Assert.Contains("push:", content);
    }

    [Fact]
    public void Workflow_TriggersOnPullRequest()
    {
        var content = File.ReadAllText(GetWorkflowPath());
        Assert.Contains("pull_request:", content);
    }
}

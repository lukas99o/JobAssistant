using System.Text.RegularExpressions;

namespace JobAssistant.Tests.Core;

public sealed class LauncherSmokeTests
{
	[Fact]
	public void RunScript_ReferencesExistingConsoleProject()
	{
		var repositoryRoot = FindRepositoryRoot();
		var runScriptPath = Path.Combine(repositoryRoot.FullName, "run.ps1");
		Assert.True(File.Exists(runScriptPath), "run.ps1 should exist at the repository root.");

		var script = File.ReadAllText(runScriptPath);
		var match = Regex.Match(script, @"--project\s+(?<path>[^\s]+\.csproj)");
		Assert.True(match.Success, "run.ps1 should invoke dotnet run with a .csproj path.");

		var projectPath = match.Groups["path"].Value.Trim('"', '\'');
		var fullProjectPath = Path.GetFullPath(Path.Combine(repositoryRoot.FullName, projectPath));

		Assert.True(File.Exists(fullProjectPath), $"run.ps1 points to a missing project file: {fullProjectPath}");
	}

	private static DirectoryInfo FindRepositoryRoot()
	{
		DirectoryInfo? current = new(AppContext.BaseDirectory);

		while (current is not null)
		{
			if (File.Exists(Path.Combine(current.FullName, "run.ps1"))
				&& File.Exists(Path.Combine(current.FullName, "JobAssistant.slnx")))
			{
				return current;
			}

			current = current.Parent;
		}

		throw new InvalidOperationException("Could not locate the repository root from the test output directory.");
	}
}
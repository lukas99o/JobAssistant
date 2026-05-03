using JobAssistant.Core.Configuration;
using JobAssistant.Core.Models;
using JobAssistant.Core.Services;
using CliConsole = System.Console;

namespace JobAssistant.ConsoleApp;

internal static class Program
{
	private static readonly DirectoryInfo RepositoryRoot = FindRepositoryRoot();
	private static readonly DirectoryInfo ConfigDirectory = new(Path.Combine(RepositoryRoot.FullName, "config"));
	private static readonly DirectoryInfo DataDirectory = new(Path.Combine(RepositoryRoot.FullName, "data"));
	private static readonly DirectoryInfo DocumentsDirectory = new(Path.Combine(RepositoryRoot.FullName, "documents"));

	private static async Task<int> Main()
	{
		CliConsole.WriteLine(new string('=', 50));
		CliConsole.WriteLine("  Job Application Assistant");
		CliConsole.WriteLine("  Arbetsformedlingen / JobTech API");
		CliConsole.WriteLine(new string('=', 50));

		var settings = LoadSettings();
		var profile = LoadProfile();
		var history = new JobHistoryStore(new FileInfo(Path.Combine(DataDirectory.FullName, "job_history.json")));

		CliConsole.WriteLine($"\nAuto-submit: {(settings.AutoSubmit ? "ON" : "OFF")}");

		var selectedFiles = DocumentSelector.SelectFiles(DocumentsDirectory);
		var query = PromptSearchQuery();

		using var apiClient = new JobSearchClient(settings);
		var results = await apiClient.SearchAsync(query);
		var (newJobs, skippedCount) = history.FilterNew(results.Jobs);

		CliConsole.WriteLine($"\nFound {results.Jobs.Count} jobs ({newJobs.Count} new, {skippedCount} already processed)");

		if (newJobs.Count == 0)
		{
			CliConsole.WriteLine("All jobs on the first page were already processed.");
			return 0;
		}

		CliConsole.WriteLine("\nNew jobs on the first page:");

		for (var index = 0; index < newJobs.Count; index++)
		{
			var job = newJobs[index];
			var city = string.IsNullOrWhiteSpace(job.WorkplaceCity) ? "Unknown location" : job.WorkplaceCity;

			CliConsole.WriteLine($"\n[{index + 1}/{newJobs.Count}] {job.Headline}");
			CliConsole.WriteLine($"  Employer: {job.EmployerName} - {city}");
			CliConsole.WriteLine($"  Method: {job.ApplicationMethod}");

			if (!string.IsNullOrWhiteSpace(job.ApplicationUrl))
			{
				CliConsole.WriteLine($"  URL: {job.ApplicationUrl}");
			}

			if (!string.IsNullOrWhiteSpace(job.ApplicationEmail))
			{
				CliConsole.WriteLine($"  Email: {job.ApplicationEmail}");
			}
		}

		CliConsole.WriteLine("\nSelected files:");
		CliConsole.WriteLine(selectedFiles.Display());
		CliConsole.WriteLine("\nCurrent status: browser automation and form filling are still being ported in the .NET app.");

		return 0;
	}

	private static Settings LoadSettings()
	{
		var loader = new SettingsLoader();
		return loader.Load(new FileInfo(Path.Combine(ConfigDirectory.FullName, "settings.yaml")));
	}

	private static UserProfile LoadProfile()
	{
		var profilePath = new FileInfo(Path.Combine(ConfigDirectory.FullName, "profile.yaml"));
		if (!profilePath.Exists)
		{
			CliConsole.WriteLine("Warning: profile.yaml not found. Form filling will be limited.");
			return new UserProfile();
		}

		var loader = new ProfileLoader();
		var profile = loader.Load(profilePath);
		var warnings = profile.Validate();

		if (warnings.Count > 0)
		{
			CliConsole.WriteLine("Profile warnings:");
			foreach (var warning in warnings)
			{
				CliConsole.WriteLine($"  - {warning}");
			}
		}

		return profile;
	}

	private static string PromptSearchQuery()
	{
		while (true)
		{
			CliConsole.Write("\nEnter search terms (e.g. 'python stockholm'): ");
			var query = CliConsole.ReadLine()?.Trim() ?? string.Empty;
			if (!string.IsNullOrWhiteSpace(query))
			{
				return query;
			}

			CliConsole.WriteLine("Search terms cannot be empty.");
		}
	}

	private static DirectoryInfo FindRepositoryRoot()
	{
		DirectoryInfo? current = new(AppContext.BaseDirectory);

		while (current is not null)
		{
			var hasConfig = Directory.Exists(Path.Combine(current.FullName, "config"));
			var hasData = Directory.Exists(Path.Combine(current.FullName, "data"));
			var hasDocuments = Directory.Exists(Path.Combine(current.FullName, "documents"));

			if (hasConfig && hasData && hasDocuments)
			{
				return current;
			}

			current = current.Parent;
		}

		throw new InvalidOperationException("Could not locate the repository root from the current application base directory.");
	}
}

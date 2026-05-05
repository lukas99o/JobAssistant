using JobAssistant.Browser;
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

	private sealed class SessionStats
	{
		public int Processed { get; set; }
		public int Applied { get; set; }
		public int Skipped { get; set; }
		public int Manual { get; set; }

		public void Display()
		{
			CliConsole.WriteLine("\n=== Session Summary ===");
			CliConsole.WriteLine($"  Processed: {Processed}");
			CliConsole.WriteLine($"  Applied (form filled): {Applied}");
			CliConsole.WriteLine($"  Manual (email/complex form): {Manual}");
			CliConsole.WriteLine($"  Skipped: {Skipped}");
		}
	}

	private static async Task<int> Main()
	{
		var exitCode = 0;

		CliConsole.WriteLine(new string('=', 50));
		CliConsole.WriteLine("  Job Application Assistant");
		CliConsole.WriteLine("  Arbetsformedlingen / JobTech API");
		CliConsole.WriteLine(new string('=', 50));

		var settings = LoadSettings();
		var profile = LoadProfile();
		var history = new JobHistoryStore(new FileInfo(Path.Combine(DataDirectory.FullName, "job_history.json")));
		var stats = new SessionStats();
		SelectedFiles? selectedFiles = null;
		var query = string.Empty;

		using var apiClient = new JobSearchClient(settings);
		using var descriptionEnricher = new JobDescriptionEnricher(settings);
		await using var browser = new BrowserManager(settings);
		var formAutomation = new FormAutomationService();

		try
		{
			if (settings.OllamaEnabled)
			{
				CliConsole.WriteLine("\nChecking Ollama availability...");
				await descriptionEnricher.EnsureReadyAsync();
				CliConsole.WriteLine($"  Ollama ready: {settings.OllamaModel} at {settings.OllamaBaseUrl}");
			}
			else
			{
				CliConsole.WriteLine("\nOllama enrichment disabled. Using local extraction.");
			}

			CliConsole.WriteLine($"\nAuto-submit: {(settings.AutoSubmit ? "ON" : "OFF")}");

			selectedFiles = DocumentSelector.SelectFiles(DocumentsDirectory);
			query = PromptSearchQuery();

			await browser.StartAsync();

			var offset = 0;

			while (true)
			{
				CliConsole.WriteLine($"\nSearching: '{query}' (offset {offset})...");
				var results = await apiClient.SearchAsync(query, offset);

				if (results.Jobs.Count == 0)
				{
					CliConsole.WriteLine("No jobs found.");
					var noResultChoice = EndOfPageMenu(hasMore: false);
					ApplyMenuChoice(noResultChoice, false, settings, ref selectedFiles, ref query, ref offset);
					continue;
				}

				var (newJobs, skippedCount) = history.FilterNew(results.Jobs);
				CliConsole.WriteLine($"Found {results.Jobs.Count} jobs ({newJobs.Count} new, {skippedCount} already processed)");

				if (newJobs.Count == 0)
				{
					CliConsole.WriteLine("All jobs on this page were already processed.");
					var hasMoreWithoutProcessing = offset + settings.ApiBatchSize < results.Total;
					var emptyChoice = EndOfPageMenu(hasMoreWithoutProcessing);
					ApplyMenuChoice(emptyChoice, hasMoreWithoutProcessing, settings, ref selectedFiles, ref query, ref offset);
					continue;
				}

				for (var index = 0; index < newJobs.Count; index++)
				{
					await ProcessJobAsync(
						newJobs[index],
						index + 1,
						newJobs.Count,
						apiClient,
						descriptionEnricher,
						browser,
						formAutomation,
						profile,
						selectedFiles,
						settings,
						history,
						query,
						stats);
				}

				var hasMore = offset + settings.ApiBatchSize < results.Total;
				var choice = EndOfPageMenu(hasMore);
				ApplyMenuChoice(choice, hasMore, settings, ref selectedFiles, ref query, ref offset);
			}
		}
		catch (JobDescriptionEnrichmentException exception)
		{
			CliConsole.WriteLine($"\nLLM enrichment error: {exception.Message}");
			exitCode = 1;
		}
		finally
		{
			stats.Display();
		}

		return exitCode;
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

	private static async Task ProcessJobAsync(
		JobListing job,
		int index,
		int total,
		JobSearchClient apiClient,
		JobDescriptionEnricher descriptionEnricher,
		BrowserManager browser,
		FormAutomationService formAutomation,
		UserProfile profile,
		SelectedFiles selectedFiles,
		Settings settings,
		JobHistoryStore history,
		string query,
		SessionStats stats)
	{
		job = await EnrichJobAsync(job, apiClient, descriptionEnricher);

		var city = string.IsNullOrWhiteSpace(job.WorkplaceCity) ? "Unknown location" : job.WorkplaceCity;
		CliConsole.WriteLine($"\n[{index}/{total}] {job.Headline}");
		CliConsole.WriteLine($"  Employer: {job.EmployerName} - {city}");

		if (job.ApplicationMethod == "external" && !string.IsNullOrWhiteSpace(job.ApplicationUrl))
		{
			CliConsole.WriteLine($"  Application URL: {job.ApplicationUrl}");

			try
			{
				var page = await browser.NavigateAsync(job.ApplicationUrl);
				var analysis = await formAutomation.AnalyzePageAsync(page, settings);

				if (analysis.Fields.Count == 0 && await browser.TryClickApplyButtonAsync())
				{
					analysis = await formAutomation.AnalyzePageAsync(browser.Page, settings);
				}

				if (analysis.Fields.Count == 0)
				{
					CliConsole.WriteLine($"  {analysis.Reason}");
					PromptForManualCompletion("  Complete the application manually, then press Enter to continue...");
					history.Record(job, "manual", query);
					stats.Manual++;
				}
				else if (analysis.IsSimple)
				{
					await formAutomation.FillFormAsync(browser.Page, analysis, profile, selectedFiles, settings, job: job);

					if (!settings.AutoSubmit)
					{
						PromptForManualCompletion("  Press Enter when done to continue...");
					}

					history.Record(job, "applied", query);
					stats.Applied++;
				}
				else
				{
					CliConsole.WriteLine($"  {analysis.Reason}");
					await formAutomation.FillFormAsync(browser.Page, analysis, profile, selectedFiles, settings, job: job, forceManual: true);
					PromptForManualCompletion("  Complete any remaining fields and submit manually, then press Enter to continue...");
					history.Record(job, "manual", query);
					stats.Manual++;
				}
			}
			catch (Exception exception)
			{
				CliConsole.WriteLine($"  Error navigating to application: {exception.Message}");
				stats.Skipped++;
			}
		}
		else if (job.ApplicationMethod == "email" && !string.IsNullOrWhiteSpace(job.ApplicationEmail))
		{
			var postingUrl = $"https://arbetsformedlingen.se/platsbanken/annonser/{job.Id}";

			CliConsole.WriteLine($"  Email application: {job.ApplicationEmail}");
			CliConsole.WriteLine($"  Job posting: {postingUrl}");

			try
			{
				await browser.NavigateAsync(postingUrl);
				CliConsole.WriteLine("  Opened job posting for manual review.");
				await formAutomation.PreparePersonalLetterForManualApplicationAsync(browser.Page, selectedFiles, job);
			}
			catch (Exception exception)
			{
				CliConsole.WriteLine($"  Could not open job posting: {exception.Message}");
			}

			if (!string.IsNullOrWhiteSpace(job.ApplicationInfo))
			{
				CliConsole.WriteLine($"  Instructions: {job.ApplicationInfo}");
			}

			PromptForManualCompletion("  Complete the application manually, then press Enter to continue...");
			history.Record(job, "manual", query);
			stats.Manual++;
		}
		else
		{
			CliConsole.WriteLine("  No application method found. Skipping.");

			if (!string.IsNullOrWhiteSpace(job.ApplicationInfo))
			{
				CliConsole.WriteLine($"  Info: {job.ApplicationInfo}");
			}

			stats.Skipped++;
		}

		stats.Processed++;

		if (settings.ActionDelay > 0)
		{
			await Task.Delay(TimeSpan.FromSeconds(settings.ActionDelay));
		}
	}

	private static async Task<JobListing> EnrichJobAsync(
		JobListing job,
		JobSearchClient apiClient,
		JobDescriptionEnricher descriptionEnricher)
	{
		var detailedJob = await apiClient.GetAdAsync(job.Id) ?? job;
		if (string.IsNullOrWhiteSpace(detailedJob.Description) && !string.IsNullOrWhiteSpace(job.Description))
		{
			detailedJob = detailedJob with { Description = job.Description };
		}

		var analysis = await descriptionEnricher.AnalyzeAsync(detailedJob.Description);
		if (!string.IsNullOrWhiteSpace(analysis.WarningMessage))
		{
			CliConsole.WriteLine($"  {analysis.WarningMessage}");
		}

		return detailedJob with
		{
			CompanyDesc = analysis.CompanyDesc,
			CompanyKeywords = analysis.CompanyKeywords.ToArray(),
		};
	}

	private static string EndOfPageMenu(bool hasMore)
	{
		CliConsole.WriteLine("\n=== Page Complete ===");
		CliConsole.WriteLine("What would you like to do?");

		HashSet<string> validChoices;
		if (hasMore)
		{
			CliConsole.WriteLine("  1. Continue to next page");
			CliConsole.WriteLine("  2. Select new files and continue to next page");
			CliConsole.WriteLine("  3. Select new files and start a new job search");
			CliConsole.WriteLine("  4. Start a new job search (keep current files)");
			validChoices = new HashSet<string>(StringComparer.Ordinal) { "1", "2", "3", "4" };
		}
		else
		{
			CliConsole.WriteLine("  No more results for this search.");
			CliConsole.WriteLine("  1. Select new files and start a new job search");
			CliConsole.WriteLine("  2. Start a new job search (keep current files)");
			validChoices = new HashSet<string>(StringComparer.Ordinal) { "1", "2" };
		}

		while (true)
		{
			CliConsole.Write($"Choice [{string.Join('/', validChoices.OrderBy(value => value, StringComparer.Ordinal))}]: ");
			var choice = CliConsole.ReadLine()?.Trim() ?? string.Empty;
			if (validChoices.Contains(choice))
			{
				return choice;
			}

			CliConsole.WriteLine($"Invalid choice. Enter {string.Join(" or ", validChoices.OrderBy(value => value, StringComparer.Ordinal))}.");
		}
	}

	private static void ApplyMenuChoice(
		string choice,
		bool hasMore,
		Settings settings,
		ref SelectedFiles selectedFiles,
		ref string query,
		ref int offset)
	{
		if (hasMore)
		{
			switch (choice)
			{
				case "1":
					offset += settings.ApiBatchSize;
					break;
				case "2":
					selectedFiles = DocumentSelector.SelectFiles(DocumentsDirectory);
					offset += settings.ApiBatchSize;
					break;
				case "3":
					selectedFiles = DocumentSelector.SelectFiles(DocumentsDirectory);
					query = PromptSearchQuery();
					offset = 0;
					break;
				case "4":
					query = PromptSearchQuery();
					offset = 0;
					break;
			}

			return;
		}

		switch (choice)
		{
			case "1":
				selectedFiles = DocumentSelector.SelectFiles(DocumentsDirectory);
				query = PromptSearchQuery();
				offset = 0;
				break;
			case "2":
				query = PromptSearchQuery();
				offset = 0;
				break;
		}
	}

	private static void PromptForManualCompletion(string prompt)
	{
		CliConsole.Write(prompt);
		CliConsole.ReadLine();
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

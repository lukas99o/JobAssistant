using JobAssistant.Core.Models;
using JobAssistant.Core.Services;
using CliConsole = System.Console;

namespace JobAssistant.ConsoleApp;

internal static class DocumentSelector
{
    private static readonly DocumentCopyService CopyService = new();

    private static readonly HashSet<string> IgnoredFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ".gitkeep",
        ".ds_store",
        "thumbs.db",
        "desktop.ini",
    };

    public static SelectedFiles SelectFiles(DirectoryInfo documentsDirectory)
    {
        CliConsole.WriteLine("\n=== Document Selection ===");

        var cv = PrepareSelectedCopy(
            PickOne(ListFiles(new DirectoryInfo(Path.Combine(documentsDirectory.FullName, "CVs"))), "CV"),
            "CV.Copies");
        var letter = PrepareSelectedCopy(
            PickOne(ListFiles(new DirectoryInfo(Path.Combine(documentsDirectory.FullName, "PersonalLetters"))), "personal letter (PDF)"),
            "PersonalLetters.Copies");
        var letterText = PrepareSelectedCopy(
            PickOne(ListFiles(new DirectoryInfo(Path.Combine(documentsDirectory.FullName, "PersonalLettersText"))), "personal letter text (.txt)"),
            "PersonalLettersText.Copies");
        var formLetterText = PrepareSelectedCopy(
            PickOne(ListFiles(new DirectoryInfo(Path.Combine(documentsDirectory.FullName, "PersonalLettersText"))), "form personal letter text (.txt)"),
            "PersonalLettersText.Copies");
        var other = PrepareSelectedCopy(
            PickOne(ListFiles(new DirectoryInfo(Path.Combine(documentsDirectory.FullName, "Other"))), "other file"),
            "Other.Copies");

        var selected = new SelectedFiles(cv, letter, letterText, formLetterText, other);

        CliConsole.WriteLine($"\nSelected files:\n{selected.Display()}");
        return selected;
    }

    private static FileInfo? PrepareSelectedCopy(FileInfo? selectedFile, string copiesFolderName)
    {
        return CopyService.GetOrCreateCopy(selectedFile, copiesFolderName);
    }

    private static List<FileInfo> ListFiles(DirectoryInfo folder)
    {
        if (!folder.Exists)
        {
            return new List<FileInfo>();
        }

        return folder.EnumerateFiles()
            .Where(file => !IgnoredFiles.Contains(file.Name))
            .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static FileInfo? PickOne(IReadOnlyList<FileInfo> files, string category)
    {
        if (files.Count == 0)
        {
            CliConsole.WriteLine($"  No {category} files found.");
            return null;
        }

        CliConsole.WriteLine($"\n  Available {category}:");
        for (var index = 0; index < files.Count; index++)
        {
            CliConsole.WriteLine($"    {index + 1}. {files[index].Name}");
        }

        CliConsole.WriteLine("    0. None");

        while (true)
        {
            CliConsole.Write($"  Select {category} [0-{files.Count}]: ");
            var input = CliConsole.ReadLine()?.Trim() ?? string.Empty;

            if (input == "0")
            {
                return null;
            }

            if (int.TryParse(input, out var choice) && choice >= 1 && choice <= files.Count)
            {
                return files[choice - 1];
            }

            CliConsole.WriteLine($"  Invalid choice. Enter 0-{files.Count}.");
        }
    }
}
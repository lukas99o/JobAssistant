namespace JobAssistant.Core.Models;

public sealed record SelectedFiles(
    FileInfo? CvPath = null,
    FileInfo? PersonalLetterPath = null,
    FileInfo? PersonalLetterTextPath = null,
    FileInfo? PersonalLetterFormTextPath = null,
    FileInfo? OtherPath = null)
{
    public FileInfo? GetPreferredPersonalLetterTextFile()
    {
        if (PersonalLetterFormTextPath is not null)
        {
            return PersonalLetterFormTextPath;
        }

        if (PersonalLetterTextPath is not null)
        {
            return PersonalLetterTextPath;
        }

        if (PersonalLetterPath is not null && PersonalLetterPath.Extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
        {
            return PersonalLetterPath;
        }

        return null;
    }

    public string Display()
    {
        var lines = new[]
        {
            $"  CV: {CvPath?.Name ?? "None"}",
            $"  Personal letter: {PersonalLetterPath?.Name ?? "None"}",
            $"  Personal letter text: {PersonalLetterTextPath?.Name ?? "None"}",
            $"  Form personal letter text: {PersonalLetterFormTextPath?.Name ?? "None"}",
            $"  Other file: {OtherPath?.Name ?? "None"}",
        };

        return string.Join(Environment.NewLine, lines);
    }
}
namespace JobAssistant.Core.Models;

public sealed record SelectedFiles(FileInfo? CvPath = null, FileInfo? PersonalLetterPath = null, FileInfo? OtherPath = null)
{
    public string Display()
    {
        var lines = new[]
        {
            $"  CV: {CvPath?.Name ?? "None"}",
            $"  Personal letter: {PersonalLetterPath?.Name ?? "None"}",
            $"  Other file: {OtherPath?.Name ?? "None"}",
        };

        return string.Join(Environment.NewLine, lines);
    }
}
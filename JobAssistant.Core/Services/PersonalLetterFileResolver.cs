using JobAssistant.Core.Models;

namespace JobAssistant.Core.Services;

public static class PersonalLetterFileResolver
{
    public static FileInfo? GetEditableTextSource(SelectedFiles selectedFiles)
    {
        var preferredTextFile = selectedFiles.GetPreferredPersonalLetterTextFile();
        return preferredTextFile?.Exists == true ? preferredTextFile : null;
    }

    public static FileInfo? GetPreferredUploadFile(
        SelectedFiles selectedFiles,
        FileInfo? editableTextFile,
        FileInfo? editablePdfFile,
        string? acceptAttribute)
    {
        if (editablePdfFile?.Exists == true && AcceptsPdf(acceptAttribute))
        {
            return editablePdfFile;
        }

        if (editableTextFile?.Exists == true && AcceptsPlainText(acceptAttribute) && !AcceptsPdf(acceptAttribute))
        {
            return editableTextFile;
        }

        if (selectedFiles.PersonalLetterPath?.Exists == true)
        {
            return selectedFiles.PersonalLetterPath;
        }

        var preferredTextFile = GetEditableTextSource(selectedFiles);
        if (preferredTextFile is not null && AcceptsPlainText(acceptAttribute))
        {
            return preferredTextFile;
        }

        return null;
    }

    public static bool AcceptsPdf(string? acceptAttribute)
    {
        if (string.IsNullOrWhiteSpace(acceptAttribute))
        {
            return true;
        }

        return acceptAttribute
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Any(token => token.Equals(".pdf", StringComparison.OrdinalIgnoreCase)
                || token.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
                || token.Equals("application/x-pdf", StringComparison.OrdinalIgnoreCase)
                || token.Equals("*/*", StringComparison.OrdinalIgnoreCase));
    }

    public static bool AcceptsPlainText(string? acceptAttribute)
    {
        if (string.IsNullOrWhiteSpace(acceptAttribute))
        {
            return true;
        }

        return acceptAttribute
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Any(token => token.Equals(".txt", StringComparison.OrdinalIgnoreCase)
                || token.Equals("text/plain", StringComparison.OrdinalIgnoreCase)
                || token.Equals("text/*", StringComparison.OrdinalIgnoreCase)
                || token.Equals("*/*", StringComparison.OrdinalIgnoreCase));
    }
}
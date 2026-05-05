using System.Text.RegularExpressions;
using JobAssistant.Core.Models;

namespace JobAssistant.Core.Services;

public static class ApplicationReferenceFormatter
{
    private static readonly Regex ExplicitReferenceRegex = new(
        @"(?:referens(?:nummer)?|reference|ref(?:erence)?)(?:\s*(?:nummer|number|nr|no))?\s*(?:[:#-]|\s+ar\s+|\s+is\s+)?\s*(?<value>[A-Za-z0-9][A-Za-z0-9/_-]{3,})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex StructuredReferenceRegex = new(
        @"\b(?=[A-Za-z0-9-]{10,}\b)(?=[A-Za-z0-9-]*\d)[A-Za-z]+[A-Za-z0-9]*(?:-[A-Za-z0-9]+){2,}\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string? ExtractReference(JobListing? job)
    {
        return job is null ? null : ExtractReference(job.ApplicationInfo, job.Description);
    }

    public static string? ExtractReference(params string?[] texts)
    {
        foreach (var text in texts)
        {
            var reference = ExtractReferenceFromText(text);
            if (!string.IsNullOrWhiteSpace(reference))
            {
                return reference;
            }
        }

        return null;
    }

    public static string BuildApplicationNotes(JobListing? job)
    {
        if (job is null)
        {
            return string.Empty;
        }

        var notes = new List<string>();
        var reference = ExtractReference(job);
        if (!string.IsNullOrWhiteSpace(reference))
        {
            notes.Add($"Include this reference in your personal letter: {reference}");
        }

        AddNote(notes, NormalizeWhitespace(job.ApplicationInfo));
        AddNote(notes, ExtractRelevantDescriptionLine(job.Description, reference));

        return string.Join(Environment.NewLine + Environment.NewLine, notes);
    }

    public static string? ExtractRelevantInstruction(params string?[] texts)
    {
        var reference = ExtractReference(texts);
        foreach (var text in texts)
        {
            var relevantLine = ExtractRelevantDescriptionLine(text, reference);
            if (!string.IsNullOrWhiteSpace(relevantLine))
            {
                return relevantLine;
            }
        }

        return null;
    }

    public static string PrependReferenceToPersonalLetter(string? personalLetterText, JobListing? job)
    {
        var reference = ExtractReference(job);
        if (string.IsNullOrWhiteSpace(reference))
        {
            return personalLetterText ?? string.Empty;
        }

        var currentText = personalLetterText ?? string.Empty;
        if (currentText.Contains(reference, StringComparison.OrdinalIgnoreCase))
        {
            return currentText;
        }

        var prefix = $"Referens: {reference}";
        var body = currentText.TrimStart();
        return string.IsNullOrWhiteSpace(body)
            ? prefix
            : $"{prefix}{Environment.NewLine}{Environment.NewLine}{body}";
    }

    private static string? ExtractReferenceFromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        foreach (var line in EnumerateLines(text))
        {
            var explicitMatch = ExplicitReferenceRegex.Match(line);
            if (explicitMatch.Success)
            {
                return CleanReference(explicitMatch.Groups["value"].Value);
            }
        }

        var structuredMatch = StructuredReferenceRegex.Match(text);
        return structuredMatch.Success ? CleanReference(structuredMatch.Value) : null;
    }

    private static string? ExtractRelevantDescriptionLine(string? description, string? reference)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        foreach (var line in EnumerateLines(description))
        {
            if (IsRelevantLine(line, reference))
            {
                return NormalizeWhitespace(line);
            }
        }

        return null;
    }

    private static bool IsRelevantLine(string line, string? reference)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(reference) && line.Contains(reference, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return line.Contains("referens", StringComparison.OrdinalIgnoreCase)
            || line.Contains("reference", StringComparison.OrdinalIgnoreCase)
            || StructuredReferenceRegex.IsMatch(line);
    }

    private static IEnumerable<string> EnumerateLines(string text)
    {
        return text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static void AddNote(List<string> notes, string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return;
        }

        if (notes.Any(existing => string.Equals(existing, note, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        notes.Add(note);
    }

    private static string? NormalizeWhitespace(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return WhitespaceRegex.Replace(text.Trim(), " ");
    }

    private static string? CleanReference(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim().Trim('.', ',', ';', ':', ')', ']', '}').TrimStart('(', '[', '{');
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
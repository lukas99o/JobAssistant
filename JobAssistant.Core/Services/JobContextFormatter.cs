using System.Text;
using JobAssistant.Core.Models;

namespace JobAssistant.Core.Services;

public static class JobContextFormatter
{
    private const int SummaryWrapWidth = 88;

    public static string FormatForContextWindow(JobListing? job)
    {
        return Format(job);
    }

    public static string GetWindowTitle(JobListing? job)
    {
        if (job is null)
        {
            return "Job Context";
        }

        var titleParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(job.Headline))
        {
            titleParts.Add(job.Headline.Trim());
        }

        if (!string.IsNullOrWhiteSpace(job.EmployerName))
        {
            titleParts.Add(job.EmployerName.Trim());
        }

        return titleParts.Count == 0 ? "Job Context" : string.Join(" | ", titleParts);
    }

    private static string Format(JobListing? job)
    {
        if (job is null)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine("Job Context");
        builder.AppendLine(new string('=', 11));
        builder.AppendLine();
        builder.AppendLine("This window is reference-only while you tailor your personal letter.");
        builder.AppendLine("Its content is not submitted with the application.");

        AppendField(builder, "Role", job.Headline);
        AppendField(builder, "Company", job.EmployerName);
        AppendField(builder, "Location", job.WorkplaceCity);

        if (!string.IsNullOrWhiteSpace(job.ApplicationUrl))
        {
            AppendField(builder, "Application URL", job.ApplicationUrl);
        }
        else if (!string.IsNullOrWhiteSpace(job.ApplicationEmail))
        {
            AppendField(builder, "Application Email", job.ApplicationEmail);
        }

        var applicationNotes = ApplicationReferenceFormatter.BuildApplicationNotes(job);
        AppendSection(builder, "Application Notes", WrapText(applicationNotes, SummaryWrapWidth));

        AppendSection(builder, "Summary", WrapText(job.CompanyDesc, SummaryWrapWidth));
        AppendKeywordSection(builder, job.CompanyKeywords);

        return builder.ToString().TrimEnd();
    }

    private static void AppendField(StringBuilder builder, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine($"{label}: {value.Trim()}");
    }

    private static void AppendSection(StringBuilder builder, string label, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine($"{label}:");
        foreach (var line in value.Split(Environment.NewLine))
        {
            builder.AppendLine($"  {line}");
        }
    }

    private static void AppendKeywordSection(StringBuilder builder, IReadOnlyList<string> keywords)
    {
        if (keywords.Count == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine("Keywords:");
        foreach (var keyword in keywords)
        {
            builder.AppendLine($"  - {keyword}");
        }
    }

    private static string WrapText(string? text, int lineWidth)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalizedParagraphs = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split("\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(paragraph => paragraph.Trim())
            .Where(paragraph => !string.IsNullOrWhiteSpace(paragraph))
            .ToArray();

        if (normalizedParagraphs.Length == 0)
        {
            return string.Empty;
        }

        var wrappedParagraphs = normalizedParagraphs.Select(paragraph => WrapParagraph(paragraph, lineWidth));
        return string.Join(Environment.NewLine + Environment.NewLine, wrappedParagraphs);
    }

    private static string WrapParagraph(string paragraph, int lineWidth)
    {
        var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        var currentLineLength = 0;

        foreach (var word in words)
        {
            var separatorLength = currentLineLength == 0 ? 0 : 1;
            if (currentLineLength + separatorLength + word.Length > lineWidth)
            {
                builder.AppendLine();
                builder.Append(word);
                currentLineLength = word.Length;
                continue;
            }

            if (separatorLength == 1)
            {
                builder.Append(' ');
            }

            builder.Append(word);
            currentLineLength += separatorLength + word.Length;
        }

        return builder.ToString();
    }
}
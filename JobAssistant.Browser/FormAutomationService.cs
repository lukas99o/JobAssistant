using System.Text.RegularExpressions;
using JobAssistant.Core.Models;
using Microsoft.Playwright;
using CliConsole = System.Console;

namespace JobAssistant.Browser;

public sealed record FormField(
    ILocator Locator,
    string Tag,
    string InputType,
    string ElementId,
    string Name,
    string Label,
    bool IsFileUpload = false,
    string QuestionLabel = "",
    string OptionValue = "");

public sealed record FormAnalysis(
    bool IsSimple,
    IReadOnlyList<FormField> Fields,
    bool HasFileUpload,
    ILocator? SubmitButton,
    string Reason = "")
{
    public static FormAnalysis NoForm(string reason)
    {
        return new FormAnalysis(false, Array.Empty<FormField>(), false, null, reason);
    }
}

public sealed class FormAutomationService
{
    private static readonly HashSet<string> IgnoredInputTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "hidden",
        "submit",
        "button",
        "reset",
        "image",
    };

    private static readonly Dictionary<string, string> FieldMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["email"] = "email",
        ["e-post"] = "email",
        ["mail"] = "email",
        ["first_name"] = "first_name",
        ["firstname"] = "first_name",
        ["förnamn"] = "first_name",
        ["fornamn"] = "first_name",
        ["last_name"] = "last_name",
        ["lastname"] = "last_name",
        ["efternamn"] = "last_name",
        ["surname"] = "last_name",
        ["name"] = "full_name",
        ["namn"] = "full_name",
        ["phone"] = "phone",
        ["telefon"] = "phone",
        ["tel"] = "phone",
        ["mobile"] = "phone",
        ["mobil"] = "phone",
        ["street"] = "street",
        ["gata"] = "street",
        ["gatuadress"] = "street",
        ["adress"] = "street",
        ["address"] = "street",
        ["city"] = "city",
        ["stad"] = "city",
        ["ort"] = "city",
        ["bostadsort"] = "city",
        ["postal"] = "postal_code",
        ["postnummer"] = "postal_code",
        ["zip"] = "postal_code",
        ["country"] = "country",
        ["land"] = "country",
        ["linkedin"] = "linkedin",
        ["github"] = "github",
        ["website"] = "website",
        ["hemsida"] = "website",
        ["url"] = "website",
        ["title"] = "title",
        ["titel"] = "title",
        ["role"] = "title",
        ["befattning"] = "title",
        ["position"] = "title",
        ["organisation"] = "organization",
        ["organization"] = "organization",
    };

    private static readonly Dictionary<string, string> YesNoDefaults = new(StringComparer.OrdinalIgnoreCase)
    {
        ["skallkrav"] = "Yes",
        ["meriterande"] = "Yes",
    };

    private static readonly string[] CvKeywords = ["cv", "resume", "curriculum", "meritförteckning", "cv/resume"];
    private static readonly string[] LetterKeywords = ["personligt brev", "personal letter", "cover letter", "brev", "motivational", "motivation"];
    private static readonly string[] OtherKeywords = ["other", "övrigt", "övriga", "additional", "attachment", "bilaga", "dokument"];

    private static readonly string[] SubmitSelectors =
    {
        "button[type='submit']",
        "input[type='submit']",
        "button:has-text('Submit')",
        "button:has-text('Skicka')",
        "button:has-text('Apply')",
        "button:has-text('Ansök')",
    };

    public async Task<FormAnalysis> AnalyzePageAsync(IPage page, Settings settings)
    {
        var forms = page.Locator("form");
        var formCount = await forms.CountAsync();

        if (formCount == 0)
        {
            return FormAnalysis.NoForm("No forms found on page");
        }

        ILocator? targetForm = null;
        for (var index = 0; index < formCount; index++)
        {
            var form = forms.Nth(index);
            if (await SafeIsVisibleAsync(form))
            {
                targetForm = form;
                break;
            }
        }

        if (targetForm is null)
        {
            return FormAnalysis.NoForm("No visible forms found");
        }

        var fields = new List<FormField>();
        var hasFileUpload = false;

        foreach (var selector in new[] { "input", "textarea", "select" })
        {
            var elements = targetForm.Locator(selector);
            var elementCount = await elements.CountAsync();

            for (var index = 0; index < elementCount; index++)
            {
                var element = elements.Nth(index);
                var inputType = (await element.GetAttributeAsync("type") ?? "text").ToLowerInvariant();
                var isVisible = await SafeIsVisibleAsync(element);

                if (!isVisible && !(selector == "input" && inputType == "file"))
                {
                    continue;
                }

                if (IgnoredInputTypes.Contains(inputType))
                {
                    continue;
                }

                var name = await element.GetAttributeAsync("name") ?? string.Empty;
                var elementId = await element.GetAttributeAsync("id") ?? string.Empty;
                var placeholder = await element.GetAttributeAsync("placeholder") ?? string.Empty;
                var optionValue = await element.GetAttributeAsync("value") ?? string.Empty;

                var labelText = string.Empty;
                if (!string.IsNullOrWhiteSpace(elementId))
                {
                    var label = page.Locator($"label[for=\"{elementId}\"]");
                    if (await label.CountAsync() > 0)
                    {
                        labelText = (await label.First.InnerTextAsync()).Trim();
                    }
                }

                if (string.IsNullOrWhiteSpace(labelText))
                {
                    labelText = await element.GetAttributeAsync("aria-label") ?? placeholder;
                }

                var questionLabel = string.Empty;
                if (inputType is "radio" or "checkbox")
                {
                    questionLabel = await element.EvaluateAsync<string>(
                        """
                        e => {
                            const fieldset = e.closest('fieldset');
                            if (fieldset) {
                                const legend = fieldset.querySelector('legend');
                                if (legend) {
                                    return legend.textContent.trim();
                                }
                            }

                            let node = e.parentElement;
                            while (node) {
                                const prompt = node.querySelector('label, legend, p, span');
                                if (prompt && prompt.textContent.trim()) {
                                    return prompt.textContent.trim();
                                }

                                node = node.parentElement;
                                if (node && node.tagName === 'FORM') {
                                    break;
                                }
                            }

                            return '';
                        }
                        """) ?? string.Empty;
                }

                var isFileUpload = inputType == "file";
                hasFileUpload |= isFileUpload;

                fields.Add(new FormField(
                    element,
                    selector,
                    inputType,
                    elementId,
                    name,
                    labelText,
                    isFileUpload,
                    questionLabel,
                    optionValue));
            }
        }

        var submitButton = await FindSubmitButtonAsync(targetForm);
        var nonFileFieldCount = fields.Count(field => !field.IsFileUpload);
        var isSimple = nonFileFieldCount <= settings.MaxSimpleFormFields;
        var reason = isSimple ? string.Empty : $"Form has {nonFileFieldCount} fields (max {settings.MaxSimpleFormFields})";

        return new FormAnalysis(isSimple, fields, hasFileUpload, submitButton, reason);
    }

    public async Task<bool> FillFormAsync(
        IPage page,
        FormAnalysis analysis,
        UserProfile profile,
        SelectedFiles selectedFiles,
        Settings settings,
        bool forceManual = false,
        CancellationToken cancellationToken = default)
    {
        if (!analysis.IsSimple)
        {
            CliConsole.WriteLine($"  Complex form detected: {analysis.Reason}. Attempting partial autofill.");
        }

        var filledCount = 0;

        foreach (var field in analysis.Fields)
        {
            if (field.IsFileUpload)
            {
                if (await TryAttachFileAsync(page, field, selectedFiles))
                {
                    filledCount++;
                }

                continue;
            }

            if (field.Tag == "textarea" && IsPersonalLetterTextarea(field))
            {
                var letterText = ReadPersonalLetterText(selectedFiles);
                if (!string.IsNullOrWhiteSpace(letterText) && await TryFillAsync(field, letterText))
                {
                    CliConsole.WriteLine($"    Filled personal letter text for '{DisplayName(field)}'");
                    filledCount++;
                    await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken);
                }

                continue;
            }

            var profileField = MatchField(field);
            if (!string.IsNullOrWhiteSpace(profileField))
            {
                var value = GetProfileValue(profile, profileField);
                if (!string.IsNullOrWhiteSpace(value) && await TryPopulateFieldAsync(field, value))
                {
                    CliConsole.WriteLine($"    Filled '{DisplayName(field)}' -> {profileField}");
                    filledCount++;
                    await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken);
                }

                continue;
            }

            var predefinedAnswer = MatchFormAnswer(field, profile);
            if (!string.IsNullOrWhiteSpace(predefinedAnswer) && await TryPopulateFieldAsync(field, predefinedAnswer))
            {
                CliConsole.WriteLine($"    Filled '{DisplayName(field)}' -> form_answer: {predefinedAnswer}");
                filledCount++;
                await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken);
                continue;
            }

            var defaultAnswer = MatchYesNoDefault(field);
            if (!string.IsNullOrWhiteSpace(defaultAnswer) && await TryPopulateFieldAsync(field, defaultAnswer))
            {
                CliConsole.WriteLine($"    Filled '{DisplayName(field)}' -> default: {defaultAnswer}");
                filledCount++;
                await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken);
            }
        }

        CliConsole.WriteLine($"  Filled {filledCount} field(s).");

        var shouldAutoSubmit = settings.AutoSubmit && analysis.IsSimple && !forceManual;
        if (shouldAutoSubmit && analysis.SubmitButton is not null)
        {
            CliConsole.WriteLine("  Auto-submitting form...");
            await analysis.SubmitButton.ClickAsync();
            if (settings.ActionDelay > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(settings.ActionDelay), cancellationToken);
            }

            CliConsole.WriteLine("  Form submitted.");
        }
        else
        {
            CliConsole.WriteLine("  Review the form and submit manually.");
        }

        return true;
    }

    private static async Task<ILocator?> FindSubmitButtonAsync(ILocator form)
    {
        foreach (var selector in SubmitSelectors)
        {
            var button = form.Locator(selector);
            if (await button.CountAsync() > 0 && await SafeIsVisibleAsync(button.First))
            {
                return button.First;
            }
        }

        return null;
    }

    private static string? MatchField(FormField field)
    {
        var searchText = SearchText(field);
        foreach (var (keyword, value) in FieldMap)
        {
            if (searchText.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }

    private static string GetProfileValue(UserProfile profile, string attribute)
    {
        return attribute switch
        {
            "first_name" => profile.FirstName,
            "last_name" => profile.LastName,
            "full_name" => profile.FullName,
            "email" => profile.Email,
            "phone" => profile.Phone,
            "street" => profile.Street,
            "city" => profile.City,
            "postal_code" => profile.PostalCode,
            "country" => profile.Country,
            "organization" => profile.Organization,
            "title" => profile.Title,
            "linkedin" => profile.Linkedin,
            "github" => profile.Github,
            "website" => profile.Website,
            _ => string.Empty,
        };
    }

    private static string MatchFileUpload(FormField field)
    {
        var label = field.Label.ToLowerInvariant();
        var name = field.Name.ToLowerInvariant();

        foreach (var keyword in LetterKeywords)
        {
            if (label.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return "letter";
            }
        }

        foreach (var keyword in OtherKeywords)
        {
            if (label.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return "other";
            }
        }

        if (Regex.IsMatch(label, @"\bcv\b", RegexOptions.IgnoreCase) || CvKeywords.Skip(1).Any(keyword => label.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
        {
            return "cv";
        }

        foreach (var keyword in LetterKeywords)
        {
            if (name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return "letter";
            }
        }

        foreach (var keyword in OtherKeywords)
        {
            if (name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return "other";
            }
        }

        if (Regex.IsMatch(name, @"\bcv\b", RegexOptions.IgnoreCase) || CvKeywords.Skip(1).Any(keyword => name.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
        {
            return "cv";
        }

        return "other";
    }

    private static bool IsPersonalLetterTextarea(FormField field)
    {
        var searchText = $"{field.Name} {field.Label}";
        return LetterKeywords.Any(keyword => searchText.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static string ReadPersonalLetterText(SelectedFiles selectedFiles)
    {
        var preferredTextFile = selectedFiles.PersonalLetterTextPath;
        if (preferredTextFile is not null)
        {
            return TryReadTextFile(preferredTextFile);
        }

        if (selectedFiles.PersonalLetterPath is not null && selectedFiles.PersonalLetterPath.Extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
        {
            return TryReadTextFile(selectedFiles.PersonalLetterPath);
        }

        return string.Empty;
    }

    private static string TryReadTextFile(FileInfo path)
    {
        try
        {
            return File.ReadAllText(path.FullName).Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string? MatchFormAnswer(FormField field, UserProfile profile)
    {
        var searchText = SearchText(field);

        foreach (var (keyword, value) in profile.FormAnswers.Languages)
        {
            if (searchText.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        foreach (var (keyword, value) in profile.FormAnswers.YesNo)
        {
            if (searchText.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || searchText.Contains(keyword.Replace("_", " ", StringComparison.OrdinalIgnoreCase), StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        foreach (var (keyword, value) in profile.FormAnswers.Text)
        {
            if (searchText.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || searchText.Contains(keyword.Replace("_", " ", StringComparison.OrdinalIgnoreCase), StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }

    private static string? MatchYesNoDefault(FormField field)
    {
        var searchText = SearchText(field);
        foreach (var (keyword, answer) in YesNoDefaults)
        {
            if (searchText.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return answer;
            }
        }

        return null;
    }

    private static async Task<bool> TryAttachFileAsync(IPage page, FormField field, SelectedFiles selectedFiles)
    {
        var uploadType = MatchFileUpload(field);
        FileInfo? fileToUpload = uploadType switch
        {
            "cv" when selectedFiles.CvPath?.Exists == true => selectedFiles.CvPath,
            "letter" when selectedFiles.PersonalLetterPath?.Exists == true => selectedFiles.PersonalLetterPath,
            "other" when selectedFiles.OtherPath?.Exists == true => selectedFiles.OtherPath,
            _ when selectedFiles.CvPath?.Exists == true => selectedFiles.CvPath,
            _ => null,
        };

        if (fileToUpload is null)
        {
            return false;
        }

        var uploadLocator = await ResolveFileInputLocatorAsync(page, field);
        if (uploadLocator is null)
        {
            CliConsole.WriteLine($"    Could not locate a valid file input for '{DisplayName(field)}'.");
            return false;
        }

        try
        {
            await uploadLocator.SetInputFilesAsync(fileToUpload.FullName);
            CliConsole.WriteLine($"    Attached ({uploadType}): {fileToUpload.Name}");
            return true;
        }
        catch (Exception exception)
        {
            CliConsole.WriteLine($"    Failed to attach files: {exception.Message}");
            return false;
        }
    }

    private static async Task<ILocator?> ResolveFileInputLocatorAsync(IPage page, FormField field)
    {
        var candidates = new List<ILocator>();
        if (!string.IsNullOrWhiteSpace(field.ElementId))
        {
            candidates.Add(page.Locator($"input[type='file'][id=\"{field.ElementId}\"]"));
        }

        if (!string.IsNullOrWhiteSpace(field.Name))
        {
            candidates.Add(page.Locator($"input[type='file'][name=\"{field.Name}\"]"));
        }

        candidates.Add(field.Locator);

        foreach (var candidate in candidates)
        {
            if (await candidate.CountAsync() == 0)
            {
                continue;
            }

            var resolved = candidate.First;
            var type = await resolved.GetAttributeAsync("type") ?? string.Empty;
            if (type.Equals("file", StringComparison.OrdinalIgnoreCase))
            {
                return resolved;
            }
        }

        return null;
    }

    private static async Task<bool> TryPopulateFieldAsync(FormField field, string value)
    {
        try
        {
            if (field.Tag == "select")
            {
                await field.Locator.SelectOptionAsync(new[] { new SelectOptionValue { Label = value } });
                return true;
            }

            if (field.InputType == "checkbox")
            {
                if (IsAffirmative(value))
                {
                    if (!await field.Locator.IsCheckedAsync())
                    {
                        await field.Locator.CheckAsync();
                    }

                    return true;
                }

                if (IsNegative(value))
                {
                    if (await field.Locator.IsCheckedAsync())
                    {
                        await field.Locator.UncheckAsync();
                    }

                    return true;
                }

                return false;
            }

            if (field.InputType == "radio")
            {
                if (!IsOptionMatch(field, value))
                {
                    return false;
                }

                await field.Locator.CheckAsync();
                return true;
            }

            return await TryFillAsync(field, value);
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> TryFillAsync(FormField field, string value)
    {
        try
        {
            await field.Locator.FillAsync(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsOptionMatch(FormField field, string answer)
    {
        var searchText = $"{field.Label} {field.Name} {field.OptionValue}".ToLowerInvariant();
        var normalizedAnswer = answer.Trim().ToLowerInvariant();

        return normalizedAnswer switch
        {
            "yes" or "ja" or "true" => ContainsAny(searchText, "yes", "ja", "true"),
            "no" or "nej" or "false" => ContainsAny(searchText, "no", "nej", "false"),
            _ => searchText.Contains(normalizedAnswer, StringComparison.OrdinalIgnoreCase),
        };
    }

    private static bool IsAffirmative(string answer)
    {
        return answer.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || answer.Equals("true", StringComparison.OrdinalIgnoreCase)
            || answer.Equals("ja", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNegative(string answer)
    {
        return answer.Equals("no", StringComparison.OrdinalIgnoreCase)
            || answer.Equals("false", StringComparison.OrdinalIgnoreCase)
            || answer.Equals("nej", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsAny(string searchText, params string[] values)
    {
        return values.Any(value => searchText.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    private static string SearchText(FormField field)
    {
        return $"{field.Name} {field.Label} {field.QuestionLabel}".ToLowerInvariant();
    }

    private static string DisplayName(FormField field)
    {
        if (!string.IsNullOrWhiteSpace(field.Label))
        {
            return field.Label;
        }

        if (!string.IsNullOrWhiteSpace(field.Name))
        {
            return field.Name;
        }

        return field.Tag;
    }

    private static async Task<bool> SafeIsVisibleAsync(ILocator locator)
    {
        try
        {
            return await locator.IsVisibleAsync();
        }
        catch
        {
            return false;
        }
    }
}
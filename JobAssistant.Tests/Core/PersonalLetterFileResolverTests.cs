using JobAssistant.Core.Models;
using JobAssistant.Core.Services;

namespace JobAssistant.Tests.Core;

public sealed class PersonalLetterFileResolverTests
{
    [Fact]
    public void AcceptsPlainText_ReturnsTrueWhenAcceptIsBlank()
    {
        Assert.True(PersonalLetterFileResolver.AcceptsPlainText(null));
        Assert.True(PersonalLetterFileResolver.AcceptsPlainText(string.Empty));
    }

    [Theory]
    [InlineData(".txt")]
    [InlineData("text/plain")]
    [InlineData("application/pdf, text/plain")]
    public void AcceptsPlainText_ReturnsTrueForSupportedTokens(string accept)
    {
        Assert.True(PersonalLetterFileResolver.AcceptsPlainText(accept));
    }

    [Theory]
    [InlineData(".pdf")]
    [InlineData("application/pdf")]
    [InlineData("application/pdf, text/plain")]
    public void AcceptsPdf_ReturnsTrueForSupportedTokens(string accept)
    {
        Assert.True(PersonalLetterFileResolver.AcceptsPdf(accept));
    }

    [Fact]
    public void GetPreferredUploadFile_PrefersEditablePdfWhenFieldAcceptsPdf()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), $"jobassistant-letter-resolver-{Guid.NewGuid():N}");

        try
        {
            var directory = Directory.CreateDirectory(rootPath);
            var originalLetter = CreateFile(directory.FullName, "letter.pdf");
            var editableText = CreateFile(directory.FullName, "letter-tailored.txt");
            var editablePdf = CreateFile(directory.FullName, "letter-tailored.pdf");
            var selectedFiles = new SelectedFiles(PersonalLetterPath: originalLetter);

            var uploadFile = PersonalLetterFileResolver.GetPreferredUploadFile(selectedFiles, editableText, editablePdf, "application/pdf");

            Assert.Equal(editablePdf.FullName, uploadFile?.FullName);
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    [Fact]
    public void GetPreferredUploadFile_FallsBackToOriginalLetterWhenTextIsNotAccepted()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), $"jobassistant-letter-resolver-{Guid.NewGuid():N}");

        try
        {
            var directory = Directory.CreateDirectory(rootPath);
            var originalLetter = CreateFile(directory.FullName, "letter.pdf");
            var editableText = CreateFile(directory.FullName, "letter-tailored.txt");
            var editablePdf = CreateFile(directory.FullName, "letter-tailored.pdf");
            var selectedFiles = new SelectedFiles(PersonalLetterPath: originalLetter);

            var uploadFile = PersonalLetterFileResolver.GetPreferredUploadFile(selectedFiles, editableText, editablePdf, "text/plain");

            Assert.Equal(editableText.FullName, uploadFile?.FullName);
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    [Fact]
    public void GetPreferredUploadFile_FallsBackToOriginalLetterWhenTailoredPdfIsMissing()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), $"jobassistant-letter-resolver-{Guid.NewGuid():N}");

        try
        {
            var directory = Directory.CreateDirectory(rootPath);
            var originalLetter = CreateFile(directory.FullName, "letter.pdf");
            var selectedFiles = new SelectedFiles(PersonalLetterPath: originalLetter);

            var uploadFile = PersonalLetterFileResolver.GetPreferredUploadFile(selectedFiles, null, null, "application/pdf");

            Assert.Equal(originalLetter.FullName, uploadFile?.FullName);
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    [Fact]
    public void GetPreferredUploadFile_UsesEditableTextWhenNoOriginalLetterExists()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), $"jobassistant-letter-resolver-{Guid.NewGuid():N}");

        try
        {
            var directory = Directory.CreateDirectory(rootPath);
            var editableText = CreateFile(directory.FullName, "letter-tailored.txt");
            var selectedFiles = new SelectedFiles();

            var uploadFile = PersonalLetterFileResolver.GetPreferredUploadFile(selectedFiles, editableText, null, "text/plain");

            Assert.Equal(editableText.FullName, uploadFile?.FullName);
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    private static FileInfo CreateFile(string directory, string name)
    {
        var path = Path.Combine(directory, name);
        File.WriteAllText(path, name);
        return new FileInfo(path);
    }
}
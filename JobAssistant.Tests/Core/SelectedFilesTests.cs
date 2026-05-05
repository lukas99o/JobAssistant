using JobAssistant.Core.Models;
using JobAssistant.Core.Services;

namespace JobAssistant.Tests.Core;

public sealed class SelectedFilesTests
{
	[Fact]
	public void Display_IncludesPersonalLetterText()
	{
		var selectedFiles = new SelectedFiles(
			new FileInfo("cv.pdf"),
			new FileInfo("letter.pdf"),
			new FileInfo("letter.txt"),
			new FileInfo("portfolio.pdf"));

		var display = selectedFiles.Display();

		Assert.Contains("CV: cv.pdf", display);
		Assert.Contains("Personal letter: letter.pdf", display);
		Assert.Contains("Personal letter text: letter.txt", display);
		Assert.Contains("Other file: portfolio.pdf", display);
	}

	[Fact]
	public void GetPreferredPersonalLetterTextFile_ReturnsSelectedTextFile()
	{
		var selectedFiles = new SelectedFiles(
			PersonalLetterTextPath: new FileInfo("default-letter.txt"));

		var preferredFile = selectedFiles.GetPreferredPersonalLetterTextFile();

		Assert.Equal("default-letter.txt", preferredFile?.Name);
	}

	[Fact]
	public void GetPreferredPersonalLetterTextFile_FallsBackToDefaultText()
	{
		var selectedFiles = new SelectedFiles(
			PersonalLetterTextPath: new FileInfo("default-letter.txt"));

		var preferredFile = selectedFiles.GetPreferredPersonalLetterTextFile();

		Assert.Equal("default-letter.txt", preferredFile?.Name);
	}

	[Fact]
	public void GetPreferredPersonalLetterTextFile_FallsBackToTextPersonalLetter()
	{
		var selectedFiles = new SelectedFiles(
			PersonalLetterPath: new FileInfo("letter.txt"));

		var preferredFile = selectedFiles.GetPreferredPersonalLetterTextFile();

		Assert.Equal("letter.txt", preferredFile?.Name);
	}

	[Fact]
	public void CreateOrReplaceCopy_CreatesCopyInCopiesSubfolder()
	{
		var service = new DocumentCopyService();
		var rootPath = Path.Combine(Path.GetTempPath(), $"jobassistant-copy-test-{Guid.NewGuid():N}");

		try
		{
			var sourceDirectory = Directory.CreateDirectory(Path.Combine(rootPath, "CVs"));
			var sourcePath = Path.Combine(sourceDirectory.FullName, "CV.pdf");
			File.WriteAllText(sourcePath, "cv-content");

			var copy = service.CreateOrReplaceCopy(new FileInfo(sourcePath), "CV.Copies");

			Assert.NotNull(copy);
			Assert.Equal(Path.Combine(sourceDirectory.FullName, "CV.Copies", "CV_C.pdf"), copy!.FullName);
			Assert.True(copy.Exists);
			Assert.Equal("cv-content", File.ReadAllText(copy.FullName));
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
	public void CreateOrReplaceCopy_OverwritesExistingCopy()
	{
		var service = new DocumentCopyService();
		var rootPath = Path.Combine(Path.GetTempPath(), $"jobassistant-copy-test-{Guid.NewGuid():N}");

		try
		{
			var sourceDirectory = Directory.CreateDirectory(Path.Combine(rootPath, "CVs"));
			var sourcePath = Path.Combine(sourceDirectory.FullName, "CV.pdf");
			File.WriteAllText(sourcePath, "original-content");

			var copiesDirectory = Directory.CreateDirectory(Path.Combine(sourceDirectory.FullName, "CV.Copies"));
			var existingCopyPath = Path.Combine(copiesDirectory.FullName, "CV_C.pdf");
			File.WriteAllText(existingCopyPath, "existing-copy");

			var copy = service.CreateOrReplaceCopy(new FileInfo(sourcePath), "CV.Copies");

			Assert.NotNull(copy);
			Assert.Equal(existingCopyPath, copy!.FullName);
			Assert.Equal("original-content", File.ReadAllText(copy.FullName));
		}
		finally
		{
			if (Directory.Exists(rootPath))
			{
				Directory.Delete(rootPath, recursive: true);
			}
		}
	}
}
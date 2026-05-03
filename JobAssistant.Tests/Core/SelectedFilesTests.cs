using JobAssistant.Core.Models;

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
}
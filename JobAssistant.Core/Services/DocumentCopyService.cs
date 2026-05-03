namespace JobAssistant.Core.Services;

public sealed class DocumentCopyService
{
    public FileInfo? GetOrCreateCopy(FileInfo? selectedFile, string copiesFolderName)
    {
        if (selectedFile is null)
        {
            return null;
        }

        if (!selectedFile.Exists)
        {
            throw new FileNotFoundException($"Selected file does not exist: {selectedFile.FullName}", selectedFile.FullName);
        }

        var sourceDirectory = selectedFile.Directory
            ?? throw new InvalidOperationException("Selected file must have a parent directory.");

        var copiesDirectory = new DirectoryInfo(Path.Combine(sourceDirectory.FullName, copiesFolderName));
        copiesDirectory.Create();

        var copyName = $"{Path.GetFileNameWithoutExtension(selectedFile.Name)}_C{selectedFile.Extension}";
        var copyPath = Path.Combine(copiesDirectory.FullName, copyName);

        if (!File.Exists(copyPath))
        {
            File.Copy(selectedFile.FullName, copyPath);
        }

        return new FileInfo(copyPath);
    }
}
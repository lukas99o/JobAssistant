namespace JobAssistant.Core.Services;

public sealed class DocumentCopyService
{
    public FileInfo? CreateOrReplaceCopy(FileInfo? selectedFile, string copiesFolderName)
    {
        if (selectedFile is null)
        {
            return null;
        }

        if (!selectedFile.Exists)
        {
            throw new FileNotFoundException($"Selected file does not exist: {selectedFile.FullName}", selectedFile.FullName);
        }

        var copy = GetCopyFile(selectedFile, copiesFolderName);
        if (!string.Equals(selectedFile.FullName, copy.FullName, StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(selectedFile.FullName, copy.FullName, overwrite: true);
        }

        return copy;
    }

    private static FileInfo GetCopyFile(FileInfo selectedFile, string copiesFolderName)
    {
        var sourceDirectory = selectedFile.Directory
            ?? throw new InvalidOperationException("Selected file must have a parent directory.");

        var copiesDirectory = new DirectoryInfo(Path.Combine(sourceDirectory.FullName, copiesFolderName));
        copiesDirectory.Create();

        var copyName = $"{Path.GetFileNameWithoutExtension(selectedFile.Name)}_C{selectedFile.Extension}";
        var copyPath = Path.Combine(copiesDirectory.FullName, copyName);
        return new FileInfo(copyPath);
    }
}
namespace KassenLager.App.Services;

public enum PickFileKind
{
    Excel,
    Backup,
}

/// <summary>A picked file, copied into the app's cache so it can be read more than once.</summary>
public sealed record PickedFile(string FileName, string LocalPath);

public interface IFileService
{
    /// <summary>Folder for files that are shared right after creation (emptied on each export).</summary>
    string ExportDirectory { get; }

    /// <summary>Folder for the automatic backups taken before a restore.</summary>
    string AutomaticBackupDirectory { get; }

    /// <summary>Returns <c>null</c> when the user cancels.</summary>
    Task<PickedFile?> PickAsync(PickFileKind kind);

    /// <summary>Creates a file in <see cref="ExportDirectory"/> and returns its path.</summary>
    Task<string> CreateExportAsync(string fileName, Func<Stream, Task> write);

    /// <summary>Opens the Android share sheet (Drive, e-mail …).</summary>
    Task ShareAsync(string path, string title);

    /// <summary>
    /// Lets the user choose between saving the file to a folder on the device (e.g. Download)
    /// and sharing it; returns <c>true</c> if the file was saved or shared.
    /// </summary>
    Task<bool> SaveOrShareAsync(string path, string title);

    /// <summary>Empties <see cref="ExportDirectory"/>.</summary>
    void ClearExports();
}

public sealed class FileService(IDialogService dialogs) : IFileService
{
    private const string SaveOnDevice = "Auf dem Gerät speichern";
    private const string ShareWithApp = "Teilen (Drive, E-Mail …)";

    private const string ExcelMimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static readonly FilePickerFileType ExcelFiles = new(new Dictionary<DevicePlatform, IEnumerable<string>>
    {
        [DevicePlatform.Android] = [ExcelMimeType],
    });

    // Cloud apps report SQLite files with various MIME types; the content is validated after picking.
    private static readonly FilePickerFileType AnyFiles = new(new Dictionary<DevicePlatform, IEnumerable<string>>
    {
        [DevicePlatform.Android] = ["*/*"],
    });

    public string ExportDirectory { get; } = Path.Combine(FileSystem.CacheDirectory, "exports");

    public string AutomaticBackupDirectory { get; } = Path.Combine(FileSystem.AppDataDirectory, "backups");

    private string PickedDirectory { get; } = Path.Combine(FileSystem.CacheDirectory, "picked");

    public async Task<PickedFile?> PickAsync(PickFileKind kind)
    {
        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = kind == PickFileKind.Excel ? "Excel-Datei wählen" : "Datensicherung wählen",
            FileTypes = kind == PickFileKind.Excel ? ExcelFiles : AnyFiles,
        });
        if (result is null)
        {
            return null;
        }

        if (Directory.Exists(PickedDirectory))
        {
            Directory.Delete(PickedDirectory, recursive: true);
        }

        Directory.CreateDirectory(PickedDirectory);
        var localPath = Path.Combine(PickedDirectory, Path.GetFileName(result.FileName));
        await using (var source = await result.OpenReadAsync())
        await using (var target = File.Create(localPath))
        {
            await source.CopyToAsync(target);
        }

        return new PickedFile(result.FileName, localPath);
    }

    public async Task<string> CreateExportAsync(string fileName, Func<Stream, Task> write)
    {
        ClearExports();
        Directory.CreateDirectory(ExportDirectory);
        var path = Path.Combine(ExportDirectory, fileName);
        await using (var stream = File.Create(path))
        {
            await write(stream);
        }

        return path;
    }

    public Task ShareAsync(string path, string title) =>
        Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = title,
            File = new ShareFile(path, path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ? ExcelMimeType : "application/octet-stream"),
        });

    public async Task<bool> SaveOrShareAsync(string path, string title)
    {
        var choice = await dialogs.ChooseAsync(title, null, SaveOnDevice, ShareWithApp);
        if (choice == ShareWithApp)
        {
            await ShareAsync(path, title);
            return true;
        }

        if (choice != SaveOnDevice)
        {
            return false;
        }

        // Opens the system folder picker; the user chooses where the file goes.
        await using var stream = File.OpenRead(path);
        var result = await CommunityToolkit.Maui.Storage.FileSaver.Default.SaveAsync(Path.GetFileName(path), stream);
        if (result.IsSuccessful)
        {
            await dialogs.ToastAsync($"Gespeichert: {Path.GetFileName(result.FilePath)}");
            return true;
        }

        // Cancelling the folder picker is not an error.
        if (result.Exception is not null and not OperationCanceledException && result.Exception.GetType().Name != "FileSaveException")
        {
            throw result.Exception;
        }

        return false;
    }

    public void ClearExports()
    {
        if (Directory.Exists(ExportDirectory))
        {
            Directory.Delete(ExportDirectory, recursive: true);
        }
    }
}

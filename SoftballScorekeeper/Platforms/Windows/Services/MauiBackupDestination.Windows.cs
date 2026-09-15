using Microsoft.Maui.Platform;
using SoftballScorekeeper.Infrastructure.Backup;
using Windows.Storage.Pickers;

namespace SoftballScorekeeper.Services;

public sealed partial class MauiBackupDestination
{
    private partial async Task<BackupDestinationResult> SavePlatformAsync(
        string sourceFilePath,
        string suggestedFileName,
        CancellationToken cancellationToken
    )
    {
        var window = Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView
            as MauiWinUIWindow
            ?? throw new InvalidOperationException("The Windows application window is unavailable.");
        var picker = new FileSavePicker
        {
            SuggestedFileName = Path.GetFileNameWithoutExtension(suggestedFileName),
            DefaultFileExtension = ".zip",
        };
        picker.FileTypeChoices.Add("ZIP archive", [".zip"]);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, window.WindowHandle);

        var file = await picker.PickSaveFileAsync();
        if (file is null)
            return BackupDestinationResult.Cancelled;

        await using var source = new FileStream(
            sourceFilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read
        );
        await using var destination = await file.OpenStreamForWriteAsync();
        destination.SetLength(0);
        await source.CopyToAsync(destination, cancellationToken);
        await destination.FlushAsync(cancellationToken);
        return BackupDestinationResult.Saved;
    }
}

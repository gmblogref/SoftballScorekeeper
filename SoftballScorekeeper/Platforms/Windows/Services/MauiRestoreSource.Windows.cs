using Microsoft.Maui.Platform;
using SoftballScorekeeper.Infrastructure.Backup;
using Windows.Storage.Pickers;

namespace SoftballScorekeeper.Services;

public sealed partial class MauiRestoreSource
{
    private partial async Task<RestoreSourceArtifact?> SelectPlatformAsync(
        CancellationToken cancellationToken
    )
    {
        var window = Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView
            as MauiWinUIWindow
            ?? throw new InvalidOperationException("The Windows application window is unavailable.");
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".zip");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, window.WindowHandle);

        var file = await picker.PickSingleFileAsync();
        if (file is null)
            return null;

        await using var source = await file.OpenStreamForReadAsync();
        return await CopyToPrivateCacheAsync(source, cancellationToken);
    }
}

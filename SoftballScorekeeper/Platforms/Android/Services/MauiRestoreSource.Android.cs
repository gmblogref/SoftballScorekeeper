using Android.App;
using Android.Content;
using SoftballScorekeeper.Infrastructure.Backup;

namespace SoftballScorekeeper.Services;

public sealed partial class MauiRestoreSource
{
    private const int OpenBackupDocumentRequestCode = 8102;

    private partial async Task<RestoreSourceArtifact?> SelectPlatformAsync(
        CancellationToken cancellationToken
    )
    {
        var activity = Platform.CurrentActivity
            ?? throw new InvalidOperationException("The Android activity is unavailable.");
        var completion = new TaskCompletionSource<Intent?>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        void HandleResult(object? sender, ActivityResultEventArgs args)
        {
            if (args.RequestCode == OpenBackupDocumentRequestCode)
                completion.TrySetResult(args.ResultCode == Result.Ok ? args.Data : null);
        }

        MainActivity.ActivityResultReceived += HandleResult;
        try
        {
            using var intent = new Intent(Intent.ActionOpenDocument);
            intent.AddCategory(Intent.CategoryOpenable);
            intent.SetType("application/zip");
            activity.StartActivityForResult(intent, OpenBackupDocumentRequestCode);

            using var registration = cancellationToken.Register(() =>
                completion.TrySetCanceled(cancellationToken));
            var resultIntent = await completion.Task;
            var sourceUri = resultIntent?.Data;
            if (sourceUri is null)
                return null;

            await using var sourceStream = activity.ContentResolver?.OpenInputStream(sourceUri)
                ?? throw new IOException("The selected backup document could not be opened.");
            return await CopyToPrivateCacheAsync(sourceStream, cancellationToken);
        }
        finally
        {
            MainActivity.ActivityResultReceived -= HandleResult;
        }
    }
}

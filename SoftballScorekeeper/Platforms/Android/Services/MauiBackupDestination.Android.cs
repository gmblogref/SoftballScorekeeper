using Android.App;
using Android.Content;
using SoftballScorekeeper.Infrastructure.Backup;

namespace SoftballScorekeeper.Services;

public sealed partial class MauiBackupDestination
{
    private const int CreateBackupDocumentRequestCode = 8101;

    private partial async Task<BackupDestinationResult> SavePlatformAsync(
        string sourceFilePath,
        string suggestedFileName,
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
            if (args.RequestCode == CreateBackupDocumentRequestCode)
                completion.TrySetResult(args.ResultCode == Result.Ok ? args.Data : null);
        }

        MainActivity.ActivityResultReceived += HandleResult;
        try
        {
            using var intent = new Intent(Intent.ActionCreateDocument);
            intent.AddCategory(Intent.CategoryOpenable);
            intent.SetType("application/zip");
            intent.PutExtra(Intent.ExtraTitle, suggestedFileName);
            activity.StartActivityForResult(intent, CreateBackupDocumentRequestCode);

            using var registration = cancellationToken.Register(() =>
                completion.TrySetCanceled(cancellationToken));
            var resultIntent = await completion.Task;
            var destinationUri = resultIntent?.Data;
            if (destinationUri is null)
                return BackupDestinationResult.Cancelled;

            await using var source = new FileStream(
                sourceFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read
            );
            await using var destination = activity.ContentResolver?.OpenOutputStream(
                destinationUri,
                "w"
            ) ?? throw new IOException("The selected backup destination could not be opened.");
            await source.CopyToAsync(destination, cancellationToken);
            await destination.FlushAsync(cancellationToken);
            return BackupDestinationResult.Saved;
        }
        finally
        {
            MainActivity.ActivityResultReceived -= HandleResult;
        }
    }
}

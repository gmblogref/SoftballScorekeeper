using System.Text;

namespace SoftballScorekeeper.Services;

public sealed class LicenseDocumentLoader(
    Func<string, CancellationToken, Task<Stream>> openStreamAsync
)
{
    public async Task<LicenseDocumentLoadResult> LoadAsync(
        string assetPath,
        string unavailableText,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetPath);
        ArgumentNullException.ThrowIfNull(unavailableText);

        try
        {
            await using var stream = await openStreamAsync(assetPath, cancellationToken);
            using var reader = new StreamReader(
                stream,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true
            );
            var content = await reader.ReadToEndAsync(cancellationToken);
            return string.IsNullOrWhiteSpace(content)
                ? new LicenseDocumentLoadResult(unavailableText, false)
                : new LicenseDocumentLoadResult(content, true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return new LicenseDocumentLoadResult(unavailableText, false);
        }
    }
}

public sealed record LicenseDocumentLoadResult(string Content, bool IsSuccessful);

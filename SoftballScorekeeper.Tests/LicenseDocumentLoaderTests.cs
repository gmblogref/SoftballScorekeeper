using System.Text;
using SoftballScorekeeper.Services;

namespace SoftballScorekeeper.Tests;

public sealed class LicenseDocumentLoaderTests
{
    [Fact]
    public async Task LoadAsync_returns_document_text_without_changing_it()
    {
        const string content = "Required Notice:\r\nLicense text with trailing spaces.  \r\n";
        var loader = new LicenseDocumentLoader(
            (_, _) => Task.FromResult<Stream>(
                new MemoryStream(Encoding.UTF8.GetBytes(content))
            )
        );

        var result = await loader.LoadAsync("licenses/test.txt", "Unavailable");

        Assert.True(result.IsSuccessful);
        Assert.Equal(content, result.Content);
    }

    [Fact]
    public async Task LoadAsync_returns_fallback_when_asset_is_missing()
    {
        var loader = new LicenseDocumentLoader(
            (_, _) => Task.FromException<Stream>(new FileNotFoundException())
        );

        var result = await loader.LoadAsync("licenses/missing.txt", "Unavailable");

        Assert.False(result.IsSuccessful);
        Assert.Equal("Unavailable", result.Content);
    }

    [Fact]
    public async Task LoadAsync_returns_fallback_when_asset_is_unreadable_or_empty()
    {
        var unreadableLoader = new LicenseDocumentLoader(
            (_, _) => Task.FromException<Stream>(new UnauthorizedAccessException())
        );
        var emptyLoader = new LicenseDocumentLoader(
            (_, _) => Task.FromResult<Stream>(new MemoryStream())
        );

        var unreadable = await unreadableLoader.LoadAsync(
            "licenses/unreadable.txt",
            "Unavailable"
        );
        var empty = await emptyLoader.LoadAsync("licenses/empty.txt", "Unavailable");

        Assert.False(unreadable.IsSuccessful);
        Assert.Equal("Unavailable", unreadable.Content);
        Assert.False(empty.IsSuccessful);
        Assert.Equal("Unavailable", empty.Content);
    }
}

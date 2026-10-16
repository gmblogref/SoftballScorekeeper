using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;

namespace SoftballScorekeeper.Tests;

public sealed class WindowsLicensePackagingTests : IDisposable
{
    private static readonly string RepositoryRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..")
    );

    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(),
        $"SoftballScorekeeper-WindowsPackaging-{Guid.NewGuid():N}"
    );

    [Fact]
    public void Package_IncludesVerifiedWindowsLicensesAndProducesRepeatableZip()
    {
        var publishDirectory = CreateFakePublishDirectory(_testRoot);
        var firstZip = Path.Combine(_testRoot, "first.zip");
        var secondZip = Path.Combine(_testRoot, "second.zip");
        var executableHashBefore = ComputeSha256(Path.Combine(publishDirectory, "SoftballScorekeeper.exe"));

        var firstResult = RunPackager(PackagingScript, publishDirectory, firstZip);
        var secondResult = RunPackager(PackagingScript, publishDirectory, secondZip);

        Assert.True(firstResult.ExitCode == 0, firstResult.CombinedOutput);
        Assert.True(secondResult.ExitCode == 0, secondResult.CombinedOutput);
        Assert.True(File.Exists(firstZip));
        Assert.Equal(ComputeSha256(firstZip), ComputeSha256(secondZip));
        Assert.Equal(
            executableHashBefore,
            ComputeSha256(Path.Combine(publishDirectory, "SoftballScorekeeper.exe"))
        );

        var expectedLicenseSources = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["LICENSE.txt"] = "LICENSE",
            ["THIRD-PARTY-NOTICES.txt"] = "THIRD-PARTY-NOTICES.md",
            ["licenses/common/Apache-2.0-LICENSE.txt"] = "licenses/distribution/common/Apache-2.0-LICENSE.txt",
            ["licenses/common/CommunityToolkit.Mvvm-8.4.2-LICENSE.md"] = "licenses/distribution/common/CommunityToolkit.Mvvm-8.4.2-LICENSE.md",
            ["licenses/common/CommunityToolkit.Mvvm-8.4.2-THIRD-PARTY-NOTICES.txt"] = "licenses/distribution/common/CommunityToolkit.Mvvm-8.4.2-THIRD-PARTY-NOTICES.txt",
            ["licenses/common/DotNet-MIT-LICENSE.txt"] = "licenses/distribution/common/DotNet-MIT-LICENSE.txt",
            ["licenses/common/Microsoft.Maui-9.0.111-THIRD-PARTY-NOTICES.txt"] = "licenses/distribution/common/Microsoft.Maui-9.0.111-THIRD-PARTY-NOTICES.txt",
            ["licenses/common/sqlite-net-pcl-1.11.285-LICENSE.txt"] = "licenses/distribution/common/sqlite-net-pcl-1.11.285-LICENSE.txt",
            ["licenses/common/SQLite-PUBLIC-DOMAIN.txt"] = "licenses/distribution/common/SQLite-PUBLIC-DOMAIN.txt",
            ["licenses/windows/Microsoft.Graphics.Win2D-1.3.2-EULA.html"] = "licenses/distribution/windows/Microsoft.Graphics.Win2D-1.3.2-EULA.html",
            ["licenses/windows/Microsoft.IO.RecyclableMemoryStream-3.0.1-LICENSE.txt"] = "licenses/distribution/windows/Microsoft.IO.RecyclableMemoryStream-3.0.1-LICENSE.txt",
            ["licenses/windows/Microsoft.NETCore.App.Runtime.win-x64-9.0.21-THIRD-PARTY-NOTICES.txt"] = "licenses/distribution/windows/Microsoft.NETCore.App.Runtime.win-x64-9.0.21-THIRD-PARTY-NOTICES.txt",
            ["licenses/windows/Microsoft.Web.WebView2-1.0.3179.45-LICENSE.txt"] = "licenses/distribution/windows/Microsoft.Web.WebView2-1.0.3179.45-LICENSE.txt",
            ["licenses/windows/Microsoft.Web.WebView2-1.0.3179.45-NOTICE.txt"] = "licenses/distribution/windows/Microsoft.Web.WebView2-1.0.3179.45-NOTICE.txt",
            ["licenses/windows/Microsoft.Windows.SDK.NET.Ref-10.0.19041.57-LICENSE.rtf"] = "licenses/distribution/windows/Microsoft.Windows.SDK.NET.Ref-10.0.19041.57-LICENSE.rtf",
            ["licenses/windows/Microsoft.WindowsAppSDK-1.7.250606001-LICENSE.txt"] = "licenses/distribution/windows/Microsoft.WindowsAppSDK-1.7.250606001-LICENSE.txt",
            ["licenses/windows/Microsoft.WindowsAppSDK-1.7.250606001-NOTICE.txt"] = "licenses/distribution/windows/Microsoft.WindowsAppSDK-1.7.250606001-NOTICE.txt",
        };

        using var archive = ZipFile.OpenRead(firstZip);
        var entries = archive.Entries.ToDictionary(entry => entry.FullName, StringComparer.Ordinal);
        Assert.Contains("SoftballScorekeeper.exe", entries.Keys);
        Assert.Contains("runtime/runtime.dll", entries.Keys);
        Assert.DoesNotContain("SoftballScorekeeper.pdb", entries.Keys);
        Assert.DoesNotContain(entries.Keys, name => name.StartsWith("licenses/android/", StringComparison.OrdinalIgnoreCase));

        var actualLicenseEntries = entries.Keys
            .Where(name =>
                name is "LICENSE.txt" or "THIRD-PARTY-NOTICES.txt"
                || name.StartsWith("licenses/", StringComparison.Ordinal)
            )
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expectedLicenseSources.Keys.Order(StringComparer.Ordinal), actualLicenseEntries);

        foreach (var (destination, source) in expectedLicenseSources)
        {
            using var entryStream = entries[destination].Open();
            Assert.Equal(
                ComputeSha256(Path.Combine(RepositoryRoot, source.Replace('/', Path.DirectorySeparatorChar))),
                Convert.ToHexString(SHA256.HashData(entryStream))
            );
        }
    }

    [Fact]
    public void Package_WhenRequiredLicenseIsMissing_FailsWithoutCreatingOutput()
    {
        var fixtureRoot = Path.Combine(_testRoot, "license-fixture");
        CopyLicenseFixture(fixtureRoot);
        var publishDirectory = CreateFakePublishDirectory(Path.Combine(_testRoot, "missing"));
        var outputZip = Path.Combine(_testRoot, "missing-license.zip");
        var missingLicense = Path.Combine(
            fixtureRoot,
            "licenses",
            "distribution",
            "windows",
            "Microsoft.Graphics.Win2D-1.3.2-EULA.html"
        );
        File.Delete(missingLicense);

        var result = RunPackager(
            Path.Combine(fixtureRoot, "scripts", "package-windows-portable.ps1"),
            publishDirectory,
            outputZip
        );

        Assert.NotEqual(0, result.ExitCode);
        Assert.True(
            result.CombinedOutput.Contains("license source is missing", StringComparison.OrdinalIgnoreCase),
            result.CombinedOutput
        );
        Assert.False(File.Exists(outputZip));
    }

    [Fact]
    public void Package_WhenCollectedLicenseHashChanges_FailsWithoutCreatingOutput()
    {
        var fixtureRoot = Path.Combine(_testRoot, "checksum-fixture");
        CopyLicenseFixture(fixtureRoot);
        var publishDirectory = CreateFakePublishDirectory(Path.Combine(_testRoot, "checksum"));
        var outputZip = Path.Combine(_testRoot, "checksum-mismatch.zip");
        File.AppendAllText(
            Path.Combine(
                fixtureRoot,
                "licenses",
                "distribution",
                "windows",
                "Microsoft.WindowsAppSDK-1.7.250606001-NOTICE.txt"
            ),
            "changed"
        );

        var result = RunPackager(
            Path.Combine(fixtureRoot, "scripts", "package-windows-portable.ps1"),
            publishDirectory,
            outputZip
        );

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Distribution checksum mismatch", result.CombinedOutput);
        Assert.False(File.Exists(outputZip));
    }

    [Fact]
    public void Package_WhenOutputExists_FailsWithoutOverwritingIt()
    {
        var publishDirectory = CreateFakePublishDirectory(Path.Combine(_testRoot, "existing"));
        var outputZip = Path.Combine(_testRoot, "existing.zip");
        var sentinel = new byte[] { 0x51, 0x37, 0x19, 0xF1 };
        File.WriteAllBytes(outputZip, sentinel);

        var result = RunPackager(PackagingScript, publishDirectory, outputZip);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("will not be overwritten", result.CombinedOutput);
        Assert.Equal(sentinel, File.ReadAllBytes(outputZip));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    private static string PackagingScript => Path.Combine(
        RepositoryRoot,
        "scripts",
        "package-windows-portable.ps1"
    );

    private static string CreateFakePublishDirectory(string root)
    {
        var publishDirectory = Path.Combine(root, "publish");
        Directory.CreateDirectory(Path.Combine(publishDirectory, "runtime"));
        File.WriteAllBytes(
            Path.Combine(publishDirectory, "SoftballScorekeeper.exe"),
            new byte[] { 0x53, 0x53, 0x4B, 0x01 }
        );
        File.WriteAllText(Path.Combine(publishDirectory, "runtime", "runtime.dll"), "runtime-content");
        File.WriteAllText(Path.Combine(publishDirectory, "SoftballScorekeeper.pdb"), "symbols");
        return publishDirectory;
    }

    private static void CopyLicenseFixture(string fixtureRoot)
    {
        Directory.CreateDirectory(Path.Combine(fixtureRoot, "scripts"));
        File.Copy(PackagingScript, Path.Combine(fixtureRoot, "scripts", "package-windows-portable.ps1"));
        File.Copy(Path.Combine(RepositoryRoot, "LICENSE"), Path.Combine(fixtureRoot, "LICENSE"));
        File.Copy(
            Path.Combine(RepositoryRoot, "THIRD-PARTY-NOTICES.md"),
            Path.Combine(fixtureRoot, "THIRD-PARTY-NOTICES.md")
        );
        CopyDirectory(Path.Combine(RepositoryRoot, "licenses"), Path.Combine(fixtureRoot, "licenses"));
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }

        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }

    private static PackagingResult RunPackager(
        string scriptPath,
        string publishDirectory,
        string outputZip
    )
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "powershell.exe" : "pwsh",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(scriptPath);
        startInfo.ArgumentList.Add("-PublishDirectory");
        startInfo.ArgumentList.Add(publishDirectory);
        startInfo.ArgumentList.Add("-OutputZip");
        startInfo.ArgumentList.Add(outputZip);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException(
            "Could not start PowerShell packaging process."
        );
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        Assert.True(process.WaitForExit(120_000), "Windows license packaging process timed out.");

        return new PackagingResult(
            process.ExitCode,
            standardOutput.GetAwaiter().GetResult() + standardError.GetAwaiter().GetResult()
        );
    }

    private static string ComputeSha256(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private sealed record PackagingResult(int ExitCode, string CombinedOutput);
}

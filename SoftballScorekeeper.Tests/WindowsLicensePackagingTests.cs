using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

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
        var fixtureRoot = Path.Combine(_testRoot, "valid-fixture");
        CopyLicenseFixture(fixtureRoot);
        var publishDirectory = CreateFakePublishDirectory(_testRoot);
        var firstOutputDirectory = Path.Combine(_testRoot, "first-output");
        var secondOutputDirectory = Path.Combine(_testRoot, "second-output");
        Directory.CreateDirectory(firstOutputDirectory);
        Directory.CreateDirectory(secondOutputDirectory);
        var firstZip = OutputZip(firstOutputDirectory);
        var secondZip = OutputZip(secondOutputDirectory);
        var stagingDirectory = Path.Combine(_testRoot, "explicit-staging");
        var executableHashBefore = ComputeSha256(
            Path.Combine(publishDirectory, "SoftballScorekeeper.exe")
        );

        var fixtureScript = Path.Combine(fixtureRoot, "scripts", "package-windows-portable.ps1");
        var firstResult = RunPackager(
            fixtureScript,
            publishDirectory,
            firstZip,
            stagingDirectory: stagingDirectory
        );
        var secondResult = RunPackager(fixtureScript, publishDirectory, secondZip);

        Assert.True(firstResult.ExitCode == 0, firstResult.CombinedOutput);
        Assert.True(secondResult.ExitCode == 0, secondResult.CombinedOutput);
        Assert.True(File.Exists(firstZip));
        Assert.False(Directory.Exists(stagingDirectory));
        Assert.Equal(ComputeSha256(firstZip), ComputeSha256(secondZip));
        Assert.Equal(
            executableHashBefore,
            ComputeSha256(Path.Combine(publishDirectory, "SoftballScorekeeper.exe"))
        );

        var expectedLicenseSources = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["LICENSE.txt"] = "LICENSE",
            ["LICENSE-INFO.txt"] = "licenses/WINDOWS-DISTRIBUTION-NOTICE.txt",
            ["THIRD-PARTY-NOTICES.txt"] = "THIRD-PARTY-NOTICES.md",
            ["licenses/common/Apache-2.0-LICENSE.txt"] =
                "licenses/distribution/common/Apache-2.0-LICENSE.txt",
            ["licenses/common/CommunityToolkit.Mvvm-8.4.2-LICENSE.md"] =
                "licenses/distribution/common/CommunityToolkit.Mvvm-8.4.2-LICENSE.md",
            ["licenses/common/CommunityToolkit.Mvvm-8.4.2-THIRD-PARTY-NOTICES.txt"] =
                "licenses/distribution/common/CommunityToolkit.Mvvm-8.4.2-THIRD-PARTY-NOTICES.txt",
            ["licenses/common/DotNet-MIT-LICENSE.txt"] =
                "licenses/distribution/common/DotNet-MIT-LICENSE.txt",
            ["licenses/common/Microsoft.Maui-10.0.110-THIRD-PARTY-NOTICES.txt"] =
                "licenses/distribution/common/Microsoft.Maui-10.0.110-THIRD-PARTY-NOTICES.txt",
            ["licenses/common/sqlite-net-pcl-1.11.285-LICENSE.txt"] =
                "licenses/distribution/common/sqlite-net-pcl-1.11.285-LICENSE.txt",
            ["licenses/common/SQLite-PUBLIC-DOMAIN.txt"] =
                "licenses/distribution/common/SQLite-PUBLIC-DOMAIN.txt",
            ["licenses/windows/Microsoft.Graphics.Win2D-1.4.0-EULA.html"] =
                "licenses/distribution/windows/Microsoft.Graphics.Win2D-1.4.0-EULA.html",
            ["licenses/windows/Microsoft.IO.RecyclableMemoryStream-3.0.1-LICENSE.txt"] =
                "licenses/distribution/windows/Microsoft.IO.RecyclableMemoryStream-3.0.1-LICENSE.txt",
            [
                "licenses/windows/Microsoft.NETCore.App.Runtime.win-x64-10.0.13-THIRD-PARTY-NOTICES.txt"
            ] =
                "licenses/distribution/windows/Microsoft.NETCore.App.Runtime.win-x64-10.0.13-THIRD-PARTY-NOTICES.txt",
            ["licenses/windows/Microsoft.Web.WebView2-1.0.3179.45-LICENSE.txt"] =
                "licenses/distribution/windows/Microsoft.Web.WebView2-1.0.3179.45-LICENSE.txt",
            ["licenses/windows/Microsoft.Web.WebView2-1.0.3179.45-NOTICE.txt"] =
                "licenses/distribution/windows/Microsoft.Web.WebView2-1.0.3179.45-NOTICE.txt",
            ["licenses/windows/Microsoft.Windows.SDK.NET.Ref-10.0.19041.57-LICENSE.rtf"] =
                "licenses/distribution/windows/Microsoft.Windows.SDK.NET.Ref-10.0.19041.57-LICENSE.rtf",
            ["licenses/windows/Microsoft.WindowsAppSDK-1.8.260529003-LICENSE.txt"] =
                "licenses/distribution/windows/Microsoft.WindowsAppSDK-1.8.260529003-LICENSE.txt",
            ["licenses/windows/Microsoft.WindowsAppSDK-1.8.260529003-NOTICE.txt"] =
                "licenses/distribution/windows/Microsoft.WindowsAppSDK-1.8.260529003-NOTICE.txt",
            ["licenses/windows/Microsoft.WindowsAppSDK.DWrite-1.8.25122902-LICENSE.txt"] =
                "licenses/distribution/windows/Microsoft.WindowsAppSDK.DWrite-1.8.25122902-LICENSE.txt",
            ["licenses/windows/Microsoft.WindowsAppSDK.ML-1.8.2197-LICENSE.txt"] =
                "licenses/distribution/windows/Microsoft.WindowsAppSDK.ML-1.8.2197-LICENSE.txt",
            ["licenses/windows/Microsoft.WindowsAppSDK.ML-1.8.2197-THIRD-PARTY-NOTICES.txt"] =
                "licenses/distribution/windows/Microsoft.WindowsAppSDK.ML-1.8.2197-THIRD-PARTY-NOTICES.txt",
        };

        using var archive = ZipFile.OpenRead(firstZip);
        var entries = archive.Entries.ToDictionary(entry => entry.FullName, StringComparer.Ordinal);
        Assert.Contains("SoftballScorekeeper.exe", entries.Keys);
        Assert.Contains("runtime/runtime.dll", entries.Keys);
        Assert.DoesNotContain("SoftballScorekeeper.pdb", entries.Keys);
        Assert.DoesNotContain(
            entries.Keys,
            name => name.StartsWith("licenses/android/", StringComparison.OrdinalIgnoreCase)
        );

        var actualLicenseEntries = entries
            .Keys.Where(name =>
                name is "LICENSE.txt" or "LICENSE-INFO.txt" or "THIRD-PARTY-NOTICES.txt"
                || name.StartsWith("licenses/", StringComparison.Ordinal)
            )
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            expectedLicenseSources.Keys.Order(StringComparer.Ordinal),
            actualLicenseEntries
        );

        foreach (var (destination, source) in expectedLicenseSources)
        {
            using var entryStream = entries[destination].Open();
            Assert.Equal(
                ComputeSha256(
                    Path.Combine(RepositoryRoot, source.Replace('/', Path.DirectorySeparatorChar))
                ),
                Convert.ToHexString(SHA256.HashData(entryStream))
            );
        }

        using var noticeReader = new StreamReader(entries["LICENSE-INFO.txt"].Open());
        var notice = noticeReader.ReadToEnd();
        Assert.Contains("PolyForm Noncommercial", notice, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "REVIEW BEFORE DOWNLOADING OR USING",
            notice,
            StringComparison.OrdinalIgnoreCase
        );
        Assert.Contains(
            "application license does not replace",
            notice,
            StringComparison.OrdinalIgnoreCase
        );
        Assert.Contains("licenses/windows/", notice, StringComparison.Ordinal);
        Assert.Contains(
            "Microsoft has reviewed or approved",
            notice,
            StringComparison.OrdinalIgnoreCase
        );
        Assert.Contains("noncommercial use", notice, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("LICENSE", "2F9EEF0091CCB898B1FFD285B0EF09B7A02AF5650D137A252E59A5D7AB958296")]
    [InlineData(
        "THIRD-PARTY-NOTICES.md",
        "7F127B15DF0C6E2F642F6388405EF29E0F65F958CD577A8172F4C7A6E1EC0A05"
    )]
    public void SourceMappedLicense_HasByteStableGitAttributeAndContent(
        string relativePath,
        string expectedSha256
    )
    {
        Assert.Equal("set", GetGitAttribute(relativePath, "text"));
        Assert.Equal("lf", GetGitAttribute(relativePath, "eol"));
        Assert.Equal(expectedSha256, ComputeSha256(Path.Combine(RepositoryRoot, relativePath)));
    }

    [Fact]
    public void Package_WhenRequiredLicenseIsMissing_FailsWithoutCreatingOutput()
    {
        var fixtureRoot = Path.Combine(_testRoot, "license-fixture");
        CopyLicenseFixture(fixtureRoot);
        var publishDirectory = CreateFakePublishDirectory(Path.Combine(_testRoot, "missing"));
        var outputZip = OutputZip(Path.Combine(_testRoot, "missing-license-output"));
        Directory.CreateDirectory(Path.GetDirectoryName(outputZip)!);
        var missingLicense = Path.Combine(
            fixtureRoot,
            "licenses",
            "distribution",
            "windows",
            "Microsoft.Graphics.Win2D-1.4.0-EULA.html"
        );
        File.Delete(missingLicense);

        var result = RunPackager(
            Path.Combine(fixtureRoot, "scripts", "package-windows-portable.ps1"),
            publishDirectory,
            outputZip
        );

        Assert.NotEqual(0, result.ExitCode);
        Assert.True(
            result.CombinedOutput.Contains(
                "license source is missing",
                StringComparison.OrdinalIgnoreCase
            ),
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
        var outputZip = OutputZip(Path.Combine(_testRoot, "checksum-output"));
        Directory.CreateDirectory(Path.GetDirectoryName(outputZip)!);
        File.AppendAllText(
            Path.Combine(
                fixtureRoot,
                "licenses",
                "distribution",
                "windows",
                "Microsoft.WindowsAppSDK-1.8.260529003-NOTICE.txt"
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
    public void Package_WhenDistributionNoticeIsMissing_FailsWithoutCreatingOutput()
    {
        var fixtureRoot = Path.Combine(_testRoot, "missing-notice-fixture");
        CopyLicenseFixture(fixtureRoot);
        var publishDirectory = CreateFakePublishDirectory(
            Path.Combine(_testRoot, "missing-notice")
        );
        var outputZip = OutputZip(Path.Combine(_testRoot, "missing-notice-output"));
        Directory.CreateDirectory(Path.GetDirectoryName(outputZip)!);
        File.Delete(Path.Combine(fixtureRoot, "licenses", "WINDOWS-DISTRIBUTION-NOTICE.txt"));

        var result = RunPackager(
            Path.Combine(fixtureRoot, "scripts", "package-windows-portable.ps1"),
            publishDirectory,
            outputZip
        );

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(
            "license source is missing",
            result.CombinedOutput,
            StringComparison.OrdinalIgnoreCase
        );
        Assert.False(File.Exists(outputZip));
    }

    [Fact]
    public void Package_WhenDistributionNoticeHashChanges_FailsWithoutCreatingOutput()
    {
        var fixtureRoot = Path.Combine(_testRoot, "notice-checksum-fixture");
        CopyLicenseFixture(fixtureRoot);
        var publishDirectory = CreateFakePublishDirectory(
            Path.Combine(_testRoot, "notice-checksum")
        );
        var outputZip = OutputZip(Path.Combine(_testRoot, "notice-checksum-output"));
        Directory.CreateDirectory(Path.GetDirectoryName(outputZip)!);
        File.AppendAllText(
            Path.Combine(fixtureRoot, "licenses", "WINDOWS-DISTRIBUTION-NOTICE.txt"),
            "changed"
        );

        var result = RunPackager(
            Path.Combine(fixtureRoot, "scripts", "package-windows-portable.ps1"),
            publishDirectory,
            outputZip
        );

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(
            "Windows distribution notice SHA-256 does not match",
            result.CombinedOutput
        );
        Assert.False(File.Exists(outputZip));
    }

    [Fact]
    public void Package_WhenOutputExists_FailsWithoutOverwritingIt()
    {
        var fixtureRoot = Path.Combine(_testRoot, "existing-fixture");
        CopyLicenseFixture(fixtureRoot);
        var publishDirectory = CreateFakePublishDirectory(Path.Combine(_testRoot, "existing"));
        var outputDirectory = Path.Combine(_testRoot, "existing-output");
        Directory.CreateDirectory(outputDirectory);
        var outputZip = OutputZip(outputDirectory);
        var sentinel = new byte[] { 0x51, 0x37, 0x19, 0xF1 };
        File.WriteAllBytes(outputZip, sentinel);

        var result = RunPackager(
            Path.Combine(fixtureRoot, "scripts", "package-windows-portable.ps1"),
            publishDirectory,
            outputZip
        );

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("will not be overwritten", result.CombinedOutput);
        Assert.Equal(sentinel, File.ReadAllBytes(outputZip));
    }

    [Fact]
    public void Package_WhenManifestDoesNotTargetRequestedVersion_FailsClosed()
    {
        var fixtureRoot = Path.Combine(_testRoot, "target-fixture");
        CopyLicenseFixture(fixtureRoot);
        SetManifestReleaseTarget(fixtureRoot, "v2.0.1");
        var publishDirectory = CreateFakePublishDirectory(Path.Combine(_testRoot, "target"));
        var outputDirectory = Path.Combine(_testRoot, "target-output");
        Directory.CreateDirectory(outputDirectory);
        var outputZip = OutputZip(outputDirectory);

        var result = RunPackager(
            Path.Combine(fixtureRoot, "scripts", "package-windows-portable.ps1"),
            publishDirectory,
            outputZip
        );

        Assert.NotEqual(0, result.ExitCode);
        const string expectedMessage = "not requested release 'v1.0.0'";
        Assert.True(
            result.CombinedOutput.Contains(expectedMessage, StringComparison.Ordinal),
            $"PowerShell packaging failure did not contain '{expectedMessage}'."
                + Environment.NewLine
                + result.DiagnosticSummary
        );
        Assert.False(File.Exists(outputZip));
    }

    [Fact]
    public void PowerShellDiagnosticNormalization_RemovesGuttersWithoutChangingExceptionContent()
    {
        var capturedUbuntuPowerShell7Stderr = string.Join(
            Environment.NewLine,
            "\u001B[31;1mException: \u001B[0m/tmp/SoftballScorekeeper-WindowsPackaging-a839fd741a47440398c70a5021cd7802/target-fixture/scripts/package-windows-portable.ps1:213",
            "\u001B[31;1mLine |\u001B[0m",
            "\u001B[31;1m 213 | \u001B[0m    throw \"Production license manifest targets '$($Manifest.relea …",
            "\u001B[31;1m     | \u001B[0m    ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~",
            "\u001B[31;1m     | \u001B[0mProduction license manifest targets 'v2.0.1', not requested release",
            "\u001B[31;1m     | \u001B[0m'v1.0.0'. Reconcile and explicitly retarget the production license",
            "\u001B[31;1m     | \u001B[0minventory before packaging this version. Preserve meaningful alpha | beta."
        );

        var normalized = NormalizePowerShellOutput(capturedUbuntuPowerShell7Stderr);

        Assert.Contains("not requested release 'v1.0.0'", normalized);
        Assert.Contains("meaningful alpha | beta", normalized);
        Assert.DoesNotContain(
            "not requested release 'v1.0.0'",
            NormalizePowerShellOutput("Exception: unrelated package failure")
        );
    }

    [Fact]
    public void Package_WhenExecutableIsMissing_FailsWithoutCreatingOutput()
    {
        var fixtureRoot = Path.Combine(_testRoot, "missing-exe-fixture");
        CopyLicenseFixture(fixtureRoot);
        var publishDirectory = CreateFakePublishDirectory(Path.Combine(_testRoot, "missing-exe"));
        File.Delete(Path.Combine(publishDirectory, "SoftballScorekeeper.exe"));
        var outputDirectory = Path.Combine(_testRoot, "missing-exe-output");
        Directory.CreateDirectory(outputDirectory);
        var outputZip = OutputZip(outputDirectory);

        var result = RunPackager(
            Path.Combine(fixtureRoot, "scripts", "package-windows-portable.ps1"),
            publishDirectory,
            outputZip
        );

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("does not contain SoftballScorekeeper.exe", result.CombinedOutput);
        Assert.False(File.Exists(outputZip));
    }

    [Fact]
    public void Package_WhenExecutableVersionResourceIsMalformed_FailsWithoutCreatingOutput()
    {
        var fixtureRoot = Path.Combine(_testRoot, "malformed-version-fixture");
        CopyLicenseFixture(fixtureRoot);
        var publishDirectory = CreateFakePublishDirectory(
            Path.Combine(_testRoot, "malformed-version")
        );
        File.WriteAllBytes(
            Path.Combine(publishDirectory, "SoftballScorekeeper.exe"),
            [0x4D, 0x5A, 0x00, 0x00]
        );
        var outputDirectory = Path.Combine(_testRoot, "malformed-version-output");
        Directory.CreateDirectory(outputDirectory);
        var outputZip = OutputZip(outputDirectory);

        var result = RunPackager(
            Path.Combine(fixtureRoot, "scripts", "package-windows-portable.ps1"),
            publishDirectory,
            outputZip
        );

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("version metadata could not be inspected", result.CombinedOutput);
        Assert.False(File.Exists(outputZip));
    }

    [Fact]
    public void Package_WhenSensitiveFileExists_FailsWithoutCreatingOutput()
    {
        var fixtureRoot = Path.Combine(_testRoot, "sensitive-fixture");
        CopyLicenseFixture(fixtureRoot);
        var publishDirectory = CreateFakePublishDirectory(Path.Combine(_testRoot, "sensitive"));
        File.WriteAllText(Path.Combine(publishDirectory, "unexpected.pfx"), "not-a-real-key");
        var outputDirectory = Path.Combine(_testRoot, "sensitive-output");
        Directory.CreateDirectory(outputDirectory);
        var outputZip = OutputZip(outputDirectory);

        var result = RunPackager(
            Path.Combine(fixtureRoot, "scripts", "package-windows-portable.ps1"),
            publishDirectory,
            outputZip
        );

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("forbidden package or sensitive file", result.CombinedOutput);
        Assert.False(File.Exists(outputZip));
    }

    [Fact]
    public void Package_WhenExecutableVersionDoesNotMatch_FailsWithoutCreatingOutput()
    {
        var fixtureRoot = Path.Combine(_testRoot, "version-fixture");
        CopyLicenseFixture(fixtureRoot);
        SetManifestReleaseTarget(fixtureRoot, "v2.0.1");
        var publishDirectory = CreateFakePublishDirectory(Path.Combine(_testRoot, "version"));
        var outputDirectory = Path.Combine(_testRoot, "version-output");
        Directory.CreateDirectory(outputDirectory);
        var outputZip = OutputZip(outputDirectory, "2.0.1");

        var result = RunPackager(
            Path.Combine(fixtureRoot, "scripts", "package-windows-portable.ps1"),
            publishDirectory,
            outputZip,
            releaseVersion: "2.0.1"
        );

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("file version mismatch", result.CombinedOutput);
        Assert.False(File.Exists(outputZip));
    }

    [Fact]
    public void Package_WhenExecutableProductVersionDoesNotMatch_FailsWithoutCreatingOutput()
    {
        var fixtureRoot = Path.Combine(_testRoot, "product-version-fixture");
        CopyLicenseFixture(fixtureRoot);
        var publishDirectory = CreateFakePublishDirectory(
            Path.Combine(_testRoot, "product-version")
        );
        SetExecutableProductVersion(
            Path.Combine(publishDirectory, "SoftballScorekeeper.exe"),
            9,
            8,
            7,
            6
        );
        var outputDirectory = Path.Combine(_testRoot, "product-version-output");
        Directory.CreateDirectory(outputDirectory);
        var outputZip = OutputZip(outputDirectory);

        var result = RunPackager(
            Path.Combine(fixtureRoot, "scripts", "package-windows-portable.ps1"),
            publishDirectory,
            outputZip
        );

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("product version mismatch", result.CombinedOutput);
        Assert.False(File.Exists(outputZip));
    }

    [Fact]
    public void Package_WhenArtifactNameDoesNotMatchVersion_FailsWithoutCreatingOutput()
    {
        var fixtureRoot = Path.Combine(_testRoot, "name-fixture");
        CopyLicenseFixture(fixtureRoot);
        var publishDirectory = CreateFakePublishDirectory(Path.Combine(_testRoot, "name"));
        var outputZip = Path.Combine(_testRoot, "wrong-name.zip");

        var result = RunPackager(
            Path.Combine(fixtureRoot, "scripts", "package-windows-portable.ps1"),
            publishDirectory,
            outputZip
        );

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("must be named", result.CombinedOutput);
        Assert.False(File.Exists(outputZip));
    }

    [Fact]
    public void VerifyExistingPackage_RevalidatesDefinitiveZipAndRejectsUnexpectedSensitiveEntry()
    {
        var fixtureRoot = Path.Combine(_testRoot, "verify-fixture");
        CopyLicenseFixture(fixtureRoot);
        var script = Path.Combine(fixtureRoot, "scripts", "package-windows-portable.ps1");
        var publishDirectory = CreateFakePublishDirectory(Path.Combine(_testRoot, "verify"));
        var outputDirectory = Path.Combine(_testRoot, "verify-output");
        Directory.CreateDirectory(outputDirectory);
        var outputZip = OutputZip(outputDirectory);
        var packageResult = RunPackager(script, publishDirectory, outputZip);

        Assert.True(packageResult.ExitCode == 0, packageResult.CombinedOutput);
        var verifyResult = RunVerifier(script, outputZip);
        Assert.True(verifyResult.ExitCode == 0, verifyResult.CombinedOutput);

        using (var archive = ZipFile.Open(outputZip, ZipArchiveMode.Update))
        {
            var sensitiveEntry = archive.CreateEntry("unexpected.pfx");
            using var writer = new StreamWriter(sensitiveEntry.Open());
            writer.Write("not-a-real-key");
        }

        var rejectedResult = RunVerifier(script, outputZip);
        Assert.NotEqual(0, rejectedResult.ExitCode);
        Assert.Contains("sensitive file", rejectedResult.CombinedOutput);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    private static string PackagingScript =>
        Path.Combine(RepositoryRoot, "scripts", "package-windows-portable.ps1");

    private static string CreateFakePublishDirectory(string root)
    {
        var publishDirectory = Path.Combine(root, "publish");
        Directory.CreateDirectory(Path.Combine(publishDirectory, "runtime"));
        var testAssembly = typeof(WindowsLicensePackagingTests).Assembly.Location;
        File.Copy(testAssembly, Path.Combine(publishDirectory, "SoftballScorekeeper.exe"));
        File.Copy(testAssembly, Path.Combine(publishDirectory, "SoftballScorekeeper.dll"));
        File.WriteAllText(Path.Combine(publishDirectory, "coreclr.dll"), "runtime-content");
        File.WriteAllText(Path.Combine(publishDirectory, "hostfxr.dll"), "runtime-content");
        File.WriteAllText(Path.Combine(publishDirectory, "hostpolicy.dll"), "runtime-content");
        File.WriteAllText(
            Path.Combine(publishDirectory, "runtime", "runtime.dll"),
            "runtime-content"
        );
        File.WriteAllText(Path.Combine(publishDirectory, "SoftballScorekeeper.pdb"), "symbols");
        return publishDirectory;
    }

    private static void CopyLicenseFixture(string fixtureRoot)
    {
        Directory.CreateDirectory(Path.Combine(fixtureRoot, "scripts"));
        File.Copy(
            PackagingScript,
            Path.Combine(fixtureRoot, "scripts", "package-windows-portable.ps1")
        );
        File.Copy(
            Path.Combine(RepositoryRoot, "scripts", "windows-pe-version-info.ps1"),
            Path.Combine(fixtureRoot, "scripts", "windows-pe-version-info.ps1")
        );
        File.Copy(Path.Combine(RepositoryRoot, "LICENSE"), Path.Combine(fixtureRoot, "LICENSE"));
        File.Copy(
            Path.Combine(RepositoryRoot, "THIRD-PARTY-NOTICES.md"),
            Path.Combine(fixtureRoot, "THIRD-PARTY-NOTICES.md")
        );
        CopyDirectory(
            Path.Combine(RepositoryRoot, "licenses"),
            Path.Combine(fixtureRoot, "licenses")
        );
        SetManifestReleaseTarget(fixtureRoot, "v1.0.0");
    }

    private static void SetManifestReleaseTarget(string fixtureRoot, string releaseTarget)
    {
        var manifestPath = Path.Combine(
            fixtureRoot,
            "licenses",
            "distribution",
            "production-license-manifest.json"
        );
        var manifest =
            JsonNode.Parse(File.ReadAllText(manifestPath))
            ?? throw new InvalidOperationException(
                "Could not parse fixture production license manifest."
            );
        manifest["release_target"] = releaseTarget;
        File.WriteAllText(
            manifestPath,
            manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true })
        );
    }

    private static string OutputZip(string directory, string releaseVersion = "1.0.0") =>
        Path.Combine(directory, $"SoftballScorekeeper-{releaseVersion}-windows-x64-portable.zip");

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
        string outputZip,
        string releaseVersion = "1.0.0",
        string? stagingDirectory = null
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
        startInfo.ArgumentList.Add("-ReleaseVersion");
        startInfo.ArgumentList.Add(releaseVersion);
        if (stagingDirectory is not null)
        {
            startInfo.ArgumentList.Add("-StagingDirectory");
            startInfo.ArgumentList.Add(stagingDirectory);
        }

        using var process =
            Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start PowerShell packaging process.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        Assert.True(process.WaitForExit(120_000), "Windows license packaging process timed out.");

        return new PackagingResult(
            process.ExitCode,
            NormalizePowerShellOutput(standardOutput.GetAwaiter().GetResult()),
            NormalizePowerShellOutput(standardError.GetAwaiter().GetResult()),
            FormatCommandContext(startInfo)
        );
    }

    private static PackagingResult RunVerifier(
        string scriptPath,
        string inputZip,
        string releaseVersion = "1.0.0"
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
        startInfo.ArgumentList.Add("-InputZip");
        startInfo.ArgumentList.Add(inputZip);
        startInfo.ArgumentList.Add("-ReleaseVersion");
        startInfo.ArgumentList.Add(releaseVersion);

        using var process =
            Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                "Could not start PowerShell verification process."
            );
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        Assert.True(process.WaitForExit(120_000), "Windows package verification timed out.");
        return new PackagingResult(
            process.ExitCode,
            NormalizePowerShellOutput(standardOutput.GetAwaiter().GetResult()),
            NormalizePowerShellOutput(standardError.GetAwaiter().GetResult()),
            FormatCommandContext(startInfo)
        );
    }

    private static string NormalizePowerShellOutput(string output)
    {
        var withoutAnsi = Regex.Replace(output, "\u001B\\[[0-?]*[ -/]*[@-~]", string.Empty);
        var withoutDiagnosticGutters = Regex.Replace(
            withoutAnsi,
            @"(?m)^(?:Line[ \t]*\|[ \t]*|[ \t]*\d+[ \t]*\|[ \t]?|[ \t]+\|[ \t]?)",
            string.Empty
        );
        return Regex.Replace(withoutDiagnosticGutters, @"\s+", " ").Trim();
    }

    private static string FormatCommandContext(ProcessStartInfo startInfo) =>
        $"{startInfo.FileName} "
        + string.Join(" ", startInfo.ArgumentList.Select(argument => $"[{argument}]"));

    private static string ComputeSha256(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static void SetExecutableProductVersion(
        string path,
        ushort major,
        ushort minor,
        ushort build,
        ushort revision
    )
    {
        var bytes = File.ReadAllBytes(path);
        var signature = BitConverter.GetBytes(0xFEEF04BDu);
        var versionKey = System.Text.Encoding.Unicode.GetBytes("VS_VERSION_INFO\0");
        var fixedInfoOffset = -1;
        for (var index = 0; index <= bytes.Length - signature.Length; index++)
        {
            if (!bytes.AsSpan(index, signature.Length).SequenceEqual(signature))
            {
                continue;
            }

            var followsVersionKey = false;
            for (var padding = 0; padding <= 3; padding++)
            {
                var keyOffset = index - padding - versionKey.Length;
                if (
                    keyOffset >= 0
                    && bytes.AsSpan(keyOffset, versionKey.Length).SequenceEqual(versionKey)
                )
                {
                    followsVersionKey = true;
                    break;
                }
            }
            if (followsVersionKey)
            {
                Assert.Equal(-1, fixedInfoOffset);
                fixedInfoOffset = index;
            }
        }
        Assert.True(fixedInfoOffset >= 0, "Test executable lacks VS_FIXEDFILEINFO.");

        var mostSignificant = ((uint)major << 16) | minor;
        var leastSignificant = ((uint)build << 16) | revision;
        BitConverter.GetBytes(mostSignificant).CopyTo(bytes, fixedInfoOffset + 16);
        BitConverter.GetBytes(leastSignificant).CopyTo(bytes, fixedInfoOffset + 20);
        File.WriteAllBytes(path, bytes);
    }

    private static string GetGitAttribute(string relativePath, string attribute)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = RepositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("check-attr");
        startInfo.ArgumentList.Add(attribute);
        startInfo.ArgumentList.Add("--");
        startInfo.ArgumentList.Add(relativePath);

        using var process =
            Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start Git attribute inspection.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        Assert.True(process.WaitForExit(30_000), "Git attribute inspection timed out.");
        var output = standardOutput.GetAwaiter().GetResult().Trim();
        var error = standardError.GetAwaiter().GetResult().Trim();
        Assert.True(process.ExitCode == 0, $"Git attribute inspection failed: {error}");

        var prefix = $"{relativePath}: {attribute}: ";
        Assert.StartsWith(prefix, output, StringComparison.Ordinal);
        return output[prefix.Length..];
    }

    private sealed record PackagingResult(
        int ExitCode,
        string StandardOutput,
        string StandardError,
        string CommandContext
    )
    {
        public string CombinedOutput => $"{StandardOutput} {StandardError}".Trim();

        public string DiagnosticSummary =>
            $"Exit code: {ExitCode}"
            + Environment.NewLine
            + $"Command: {CommandContext}"
            + Environment.NewLine
            + $"Normalized stdout: {(StandardOutput.Length == 0 ? "<empty>" : StandardOutput)}"
            + Environment.NewLine
            + $"Normalized stderr: {(StandardError.Length == 0 ? "<empty>" : StandardError)}";
    }
}

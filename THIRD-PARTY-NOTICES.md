# Third-Party Notices

This document identifies verified third-party material used by Softball
Scorekeeper. It is separate from, and does not change, the license selected for
Softball Scorekeeper's own source code.

The inventory below is based on the repository assets and the restored .NET 10
NuGet graphs inspected on October 23, 2026. Build and runtime dependency graphs
can change when the SDK, workloads, or packages are updated. Release packaging
must therefore regenerate and review the dependency inventory and carry forward
the applicable upstream license and notice files.

## Open Sans fonts

The application bundles these font files; this compliance step did not modify
their bytes:

| File | Embedded version | SHA-256 |
| --- | --- | --- |
| `SoftballScorekeeper/Resources/Fonts/OpenSans-Regular.ttf` | 1.10 | `614FEDE5458EA182EF47686045A92418983EB826D28CCE8329534926DF6C19F4` |
| `SoftballScorekeeper/Resources/Fonts/OpenSans-Semibold.ttf` | 1.10 | `AA92A2C258500ACA3F1D148366E4E0713C0911A70307BBB8C439EBEEC5EC1D1A` |

Their embedded metadata states:

- Regular: `Digitized data copyright © 2010-2011, Google Corporation.`
- Semibold: `Digitized data copyright © 2011, Google Corporation.`
- Open Sans is a trademark of Google and may be registered in certain
  jurisdictions.
- Both files are licensed under the Apache License, Version 2.0.

The complete applicable license is in
[`licenses/OpenSans-Apache-2.0.txt`](licenses/OpenSans-Apache-2.0.txt); its
authoritative text is published by the
[Apache Software Foundation](https://www.apache.org/licenses/LICENSE-2.0.txt).
These version 1.10 files are not the later Open Sans release covered by the
[current OFL file](https://github.com/googlefonts/opensans/blob/main/OFL.txt).
Google Fonts records that the family was updated and its licensing unified
under OFL in
[March 2021](https://github.com/google/fonts/blob/main/ofl/opensans/DESCRIPTION.en_us.html).

Every Android or Windows package that contains these fonts must give recipients
a copy of the Apache 2.0 license and preserve the notices above. For Android,
include a readable third-party-notices resource in the APK and make it
reasonably accessible to the user. For the Windows portable distribution,
place this file and the referenced license at the ZIP root.

## Production NuGet dependencies

The application projects directly reference the following production packages:

| Package | Resolved version | Verified package license |
| --- | --- | --- |
| CommunityToolkit.Mvvm | 8.4.2 | MIT; package also contains `ThirdPartyNotices.txt` |
| Microsoft.Maui.Controls | 10.0.110 | MIT; package also contains `THIRD-PARTY-NOTICES.TXT` |
| Microsoft.Extensions.Logging.Debug | 10.0.13 | MIT |
| sqlite-net-pcl | 1.11.285 | MIT, copyright Krueger Systems, Inc. |

The restored production graphs include build-only packages and target-specific
dependencies. The following distributed
dependency groups require release-time notice handling:

- .NET MAUI, .NET Community Toolkit, and Microsoft.Extensions assemblies use
  MIT licenses and may carry their own third-party notices.
- `SQLitePCLRaw.core` and `SQLitePCLRaw.provider.e_sqlite3` 3.0.3 declare
  Apache-2.0. `SourceGear.sqlite3` 3.53.3 identifies SQLite as public domain.
- AndroidX, Google Android, Kotlin, and related Xamarin binding packages declare
  composite `MIT AND Apache-2.0` licenses. The Glide binding family additionally
  declares BSD-2-Clause material. Their package-provided third-party notices
  must be preserved when the corresponding material is distributed.
- The Android Release build contains the arm64 and x86_64 variants of the .NET
  Mono Android runtime 10.0.13 and .NET for Android runtime 36.1.118. The exact
  `Microsoft.NETCore.App.Runtime.Mono.android-arm64` and
  `Microsoft.NETCore.App.Runtime.Mono.android-x64` 10.0.13 packs supply
  byte-identical MIT license and third-party-notice files; the inventory retains
  one verified copy of each shared legal document and records both packages.
- The Windows target resolves Microsoft Windows App SDK 1.8.260529003,
  Microsoft WebView2 1.0.3179.45, and Microsoft Graphics Win2D 1.4.0. These
  packages provide product-specific license or notice terms that must accompany
  any redistributed components; they must not be represented as covered solely
  by the application's license or by a generic MIT notice.

This is a verified inventory and remediation starting point, not a representation
that every transitive binary and native component has received a completed legal
audit. A future release must inventory the actual final APK and Windows ZIP,
copy all applicable package-provided license and notice texts into the
distribution, and verify that build-only packages are excluded.

## Version 1.0.1 notice collection

The version-specific production inventory and preserved upstream files collected
for the planned v1.0.1 maintenance release are under
[`licenses/distribution`](licenses/distribution). The structured
[`production-license-manifest.json`](licenses/distribution/production-license-manifest.json)
records exact package versions, platform scope, sources, notice files, intended
package destinations, exclusions, and verification status. `SHA256SUMS.txt`
records the preserved files' repository hashes.

This collection is wired into the Android APK and Windows portable-ZIP
packaging. Every runtime component identified by the v1.0.1 production
inventory has a mapped license or notice source. Release validation must still
compare the final artifacts to this inventory and investigate any added or
changed components.

## Microsoft Windows component terms

The Microsoft terms below apply to their identified components independently of
Softball Scorekeeper's PolyForm Noncommercial license. The application license
does not replace, amend, or grant rights under these Microsoft agreements.

- `Microsoft.Graphics.Win2D` 1.4.0 requires license acceptance in its exact
  NuGet metadata and links the Microsoft Win2D EULA. The original Microsoft URL
  no longer serves that agreement, so the byte-preserved
  [`Microsoft.Graphics.Win2D-1.4.0-EULA.html`](licenses/distribution/windows/Microsoft.Graphics.Win2D-1.4.0-EULA.html)
  is from the Internet Archive capture dated January 13, 2025 at 18:09:23 UTC,
  before version 1.4.0 was published. It is not represented as MIT-only.
- `Microsoft.WindowsAppSDK` 1.8.260529003 has an exact package-supplied
  [`LICENSE.txt`](licenses/distribution/windows/Microsoft.WindowsAppSDK-1.8.260529003-LICENSE.txt)
  and [`NOTICE.txt`](licenses/distribution/windows/Microsoft.WindowsAppSDK-1.8.260529003-NOTICE.txt).
  The NOTICE includes the package's incorporated third-party attribution. The
  split Windows App SDK graph also supplies a distinct
  [`DWrite/Widgets license`](licenses/distribution/windows/Microsoft.WindowsAppSDK.DWrite-1.8.25122902-LICENSE.txt),
  plus the ML package's exact
  [`license`](licenses/distribution/windows/Microsoft.WindowsAppSDK.ML-1.8.2197-LICENSE.txt)
  and [`third-party notices`](licenses/distribution/windows/Microsoft.WindowsAppSDK.ML-1.8.2197-THIRD-PARTY-NOTICES.txt).
- `Microsoft.Windows.SDK.NET.Ref` 10.0.19041.57 links the preserved
  [`Microsoft Windows SDK RTF`](licenses/distribution/windows/Microsoft.Windows.SDK.NET.Ref-10.0.19041.57-LICENSE.rtf).
  The RTF has no package-version marker, but the exact package points to it and
  Microsoft's Windows SDK REDIST list identifies the package files as
  distributable unmodified as part of an application.
- `Microsoft.Web.WebView2` 1.0.3179.45 has an exact package-supplied
  [`LICENSE.txt`](licenses/distribution/windows/Microsoft.Web.WebView2-1.0.3179.45-LICENSE.txt)
  and [`NOTICE.txt`](licenses/distribution/windows/Microsoft.Web.WebView2-1.0.3179.45-NOTICE.txt).
  The NOTICE preserves its BSD and LGPL-related upstream material.

The Win2D, Windows App SDK, and Windows SDK terms require downstream users to
agree to sufficiently protective terms. Whether making the exact agreements
available in a portable ZIP, with use-based acceptance language, satisfies that
requirement remains unresolved. This inventory records the terms but does not
claim that question has been answered; obtain Microsoft clarification or
qualified legal review before publishing v1.0.1.

## Test-only dependencies

The following direct packages are used only by automated test projects and are
not application distribution dependencies:

- Microsoft.NET.Test.Sdk 18.11.0
- xunit 2.9.3
- xunit.runner.visualstudio 4.0.1
- Appium.WebDriver 9.0.0
- Selenium.Support 4.51.0
- Selenium.WebDriver 4.51.0

Their licenses remain applicable to anyone redistributing those packages or
their contents, but they are not notices for the Android APK or Windows portable
application unless a future packaging change includes them.

## Release packaging requirement

Before publishing a release:

1. Resolve and record the production dependency graph for each target.
2. Inventory the files actually included in the final APK and Windows ZIP.
3. Collect applicable upstream license and notice files without altering them.
4. Include a readable notice bundle inside the APK and at the Windows ZIP root.
5. Verify the notice bundle independently from the license governing Softball
   Scorekeeper's own source code.

using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.Services;

public static class LicenseDocumentCatalog
{
    public static IReadOnlyList<LicenseDocument> Create() =>
        new LicenseDocument[]
        {
            new(
                AppResources.LicenseDocumentApplication,
                "licenses/application/LICENSE.txt"
            ),
            new(
                AppResources.LicenseDocumentThirdPartyNotices,
                "licenses/THIRD-PARTY-NOTICES.txt"
            ),
            new(
                AppResources.LicenseDocumentOpenSans,
                "licenses/common/OpenSans-Apache-2.0.txt"
            ),
            new(
                AppResources.LicenseDocumentApache,
                "licenses/common/Apache-2.0-LICENSE.txt"
            ),
            new(
                AppResources.LicenseDocumentCommunityToolkitLicense,
                "licenses/common/CommunityToolkit.Mvvm-8.4.2-LICENSE.md"
            ),
            new(
                AppResources.LicenseDocumentCommunityToolkitNotices,
                "licenses/common/CommunityToolkit.Mvvm-8.4.2-THIRD-PARTY-NOTICES.txt"
            ),
            new(
                AppResources.LicenseDocumentDotNet,
                "licenses/common/DotNet-MIT-LICENSE.txt"
            ),
            new(
                AppResources.LicenseDocumentMauiNotices,
                "licenses/common/Microsoft.Maui-10.0.110-THIRD-PARTY-NOTICES.txt"
            ),
            new(
                AppResources.LicenseDocumentSqliteNet,
                "licenses/common/sqlite-net-pcl-1.11.285-LICENSE.txt"
            ),
            new(
                AppResources.LicenseDocumentSqlite,
                "licenses/common/SQLite-PUBLIC-DOMAIN.txt"
            ),
            new(
                AppResources.LicenseDocumentDotNetAndroid,
                "licenses/android/DotNet-Android-MIT-LICENSE.txt"
            ),
            new(
                AppResources.LicenseDocumentGsonNotices,
                "licenses/android/GoogleGson-2.13.1.1-THIRD-PARTY-NOTICES.txt"
            ),
            new(
                AppResources.LicenseDocumentAndroidRuntimeNotices,
                "licenses/android/Microsoft.Android.Runtime.Mono.36.android-arm64-36.1.118-THIRD-PARTY-NOTICES.txt"
            ),
            new(
                AppResources.LicenseDocumentMonoAndroidNotices,
                "licenses/android/Microsoft.NETCore.App.Runtime.Mono.android-10.0.13-THIRD-PARTY-NOTICES.txt"
            ),
            new(
                AppResources.LicenseDocumentGlideNotices,
                "licenses/android/Xamarin.Android.Glide-4.16.0.14-THIRD-PARTY-NOTICES.txt"
            ),
            new(
                AppResources.LicenseDocumentAndroidBindingsLicense,
                "licenses/android/Xamarin.AndroidBindings-LICENSE.md"
            ),
            new(
                AppResources.LicenseDocumentAndroidBindingsNotices,
                "licenses/android/Xamarin.AndroidBindings-standard-THIRD-PARTY-NOTICES.txt"
            ),
            new(
                AppResources.LicenseDocumentTinkNotices,
                "licenses/android/Xamarin.Google.Crypto.Tink.Android-1.18.0.1-THIRD-PARTY-NOTICES.txt"
            ),
            new(
                AppResources.LicenseDocumentErrorProneNotices,
                "licenses/android/Xamarin.Google.ErrorProne-2.41.0.1-THIRD-PARTY-NOTICES.txt"
            ),
            new(
                AppResources.LicenseDocumentKotlinJetBrainsNotices,
                "licenses/android/Xamarin.Kotlin-JetBrains-THIRD-PARTY-NOTICES.txt"
            ),
            new(
                AppResources.LicenseDocumentKotlinJSpecifyNotices,
                "licenses/android/Xamarin.Kotlin-JSpecify-THIRD-PARTY-NOTICES.txt"
            ),
        };
}

public sealed record LicenseDocument(string Title, string AssetPath);

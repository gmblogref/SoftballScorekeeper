using SoftballScorekeeper.Resources.Strings;
using SoftballScorekeeper.Services;

namespace SoftballScorekeeper.Views;

public partial class LicensesPage : ContentPage
{
    private readonly LicenseDocumentLoader documentLoader;
    private CancellationTokenSource? loadCancellation;

    public LicensesPage(LicenseDocumentLoader documentLoader)
    {
        InitializeComponent();
        this.documentLoader = documentLoader;
        DocumentPicker.ItemsSource = LicenseDocumentCatalog.Create().ToList();
        DocumentPicker.ItemDisplayBinding = new Binding(nameof(LicenseDocument.Title));
        DocumentPicker.SelectedIndex = 0;
    }

    protected override void OnDisappearing()
    {
        loadCancellation?.Cancel();
        base.OnDisappearing();
    }

    private async void OnDocumentSelectionChanged(object sender, EventArgs e)
    {
        if (DocumentPicker.SelectedItem is not LicenseDocument document)
            return;

        loadCancellation?.Cancel();
        loadCancellation?.Dispose();
        loadCancellation = new CancellationTokenSource();
        var cancellationToken = loadCancellation.Token;

        DocumentLoadingIndicator.IsRunning = true;
        DocumentLoadingIndicator.IsVisible = true;
        DocumentContentLabel.Text = string.Empty;

        try
        {
            var result = await documentLoader.LoadAsync(
                document.AssetPath,
                AppResources.LicenseDocumentUnavailable,
                cancellationToken
            );
            if (
                !cancellationToken.IsCancellationRequested
                && ReferenceEquals(DocumentPicker.SelectedItem, document)
            )
                DocumentContentLabel.Text = result.Content;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                DocumentLoadingIndicator.IsRunning = false;
                DocumentLoadingIndicator.IsVisible = false;
            }
        }
    }
}

using SoftballScorekeeper.Infrastructure.Data;
using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.Services;

public static class TestModeIndicator
{
    public static readonly BindableProperty IsEnabledProperty = BindableProperty.CreateAttached(
        "IsEnabled",
        typeof(bool),
        typeof(TestModeIndicator),
        false,
        propertyChanged: OnIsEnabledChanged
    );

    private static readonly BindableProperty IsWrappedProperty = BindableProperty.CreateAttached(
        "IsWrapped",
        typeof(bool),
        typeof(TestModeIndicator),
        false
    );

    public static bool GetIsEnabled(BindableObject bindable) =>
        (bool)bindable.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(BindableObject bindable, bool value) =>
        bindable.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(
        BindableObject bindable,
        object oldValue,
        object newValue
    )
    {
        if (bindable is not ContentPage page || newValue is not true)
            return;

        page.Loaded -= OnPageLoaded;
        page.Loaded += OnPageLoaded;
    }

    private static void OnPageLoaded(object? sender, EventArgs e)
    {
        if (sender is not ContentPage page || (bool)page.GetValue(IsWrappedProperty))
            return;

        var originalContent = page.Content;
        if (originalContent is null)
            return;

        page.Loaded -= OnPageLoaded;
        page.SetValue(IsWrappedProperty, true);

        var indicator = new Label
        {
            AutomationId = "TestModeIndicator",
            Text = AppResources.TestModeIndicator,
            BackgroundColor = Color.FromArgb("#FFF1F0"),
            FontAttributes = FontAttributes.Bold,
            FontSize = 18,
            HorizontalTextAlignment = TextAlignment.Center,
            Padding = new Thickness(8, 4),
            BindingContext = App.CurrentDatabaseModeState,
        };
        indicator.SetDynamicResource(Label.TextColorProperty, "Error");
        indicator.SetBinding(
            Label.IsVisibleProperty,
            nameof(DatabaseModeState.IsTestMode)
        );

        var layout = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
            },
        };
#if WINDOWS
        page.Content = null;
#endif
        layout.Add(indicator);
        layout.Add(originalContent, 0, 1);
        page.Content = layout;
    }
}

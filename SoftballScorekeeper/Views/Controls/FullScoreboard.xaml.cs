namespace SoftballScorekeeper.Views.Controls;

public partial class FullScoreboard : ContentView
{
    public static readonly BindableProperty ProfileProperty = BindableProperty.Create(
        nameof(Profile),
        typeof(FullScoreboardProfile),
        typeof(FullScoreboard),
        FullScoreboardProfile.LiveRegularPortrait,
        propertyChanged: OnProfileChanged
    );

    public static readonly BindableProperty VisitorNameProperty = CreateStringProperty(
        nameof(VisitorName)
    );
    public static readonly BindableProperty VisitorScoreProperty = BindableProperty.Create(
        nameof(VisitorScore),
        typeof(int),
        typeof(FullScoreboard),
        0
    );
    public static readonly BindableProperty HomeNameProperty = CreateStringProperty(
        nameof(HomeName)
    );
    public static readonly BindableProperty HomeScoreProperty = BindableProperty.Create(
        nameof(HomeScore),
        typeof(int),
        typeof(FullScoreboard),
        0
    );
    public static readonly BindableProperty VisitorDetailTextProperty = CreateStringProperty(
        nameof(VisitorDetailText)
    );
    public static readonly BindableProperty HomeDetailTextProperty = CreateStringProperty(
        nameof(HomeDetailText)
    );
    public static readonly BindableProperty HeaderTextProperty = CreateStringProperty(
        nameof(HeaderText)
    );
    public static readonly BindableProperty CombinedStatusTextProperty = CreateStringProperty(
        nameof(CombinedStatusText)
    );
    public static readonly BindableProperty InningStatusTextProperty = CreateStringProperty(
        nameof(InningStatusText)
    );
    public static readonly BindableProperty OutsStatusTextProperty = CreateStringProperty(
        nameof(OutsStatusText)
    );
    public static readonly BindableProperty IsOneUpEnabledProperty = BindableProperty.Create(
        nameof(IsOneUpEnabled),
        typeof(bool),
        typeof(FullScoreboard),
        false
    );
    public static readonly BindableProperty HeaderAutomationIdProperty = CreateStringProperty(
        nameof(HeaderAutomationId)
    );
    public static readonly BindableProperty VisitorNameAutomationIdProperty = CreateStringProperty(
        nameof(VisitorNameAutomationId)
    );
    public static readonly BindableProperty VisitorScoreAutomationIdProperty = CreateStringProperty(
        nameof(VisitorScoreAutomationId)
    );
    public static readonly BindableProperty HomeNameAutomationIdProperty = CreateStringProperty(
        nameof(HomeNameAutomationId)
    );
    public static readonly BindableProperty HomeScoreAutomationIdProperty = CreateStringProperty(
        nameof(HomeScoreAutomationId)
    );
    public static readonly BindableProperty CombinedStatusAutomationIdProperty =
        CreateStringProperty(nameof(CombinedStatusAutomationId));
    public static readonly BindableProperty InningStatusAutomationIdProperty = CreateStringProperty(
        nameof(InningStatusAutomationId)
    );
    public static readonly BindableProperty OutsStatusAutomationIdProperty = CreateStringProperty(
        nameof(OutsStatusAutomationId)
    );

    public FullScoreboard()
    {
        InitializeComponent();
        ApplyProfile();
    }

    public FullScoreboardProfile Profile
    {
        get => (FullScoreboardProfile)GetValue(ProfileProperty);
        set => SetValue(ProfileProperty, value);
    }

    public string VisitorName
    {
        get => (string)GetValue(VisitorNameProperty);
        set => SetValue(VisitorNameProperty, value);
    }

    public int VisitorScore
    {
        get => (int)GetValue(VisitorScoreProperty);
        set => SetValue(VisitorScoreProperty, value);
    }

    public string HomeName
    {
        get => (string)GetValue(HomeNameProperty);
        set => SetValue(HomeNameProperty, value);
    }

    public int HomeScore
    {
        get => (int)GetValue(HomeScoreProperty);
        set => SetValue(HomeScoreProperty, value);
    }

    public string VisitorDetailText
    {
        get => (string)GetValue(VisitorDetailTextProperty);
        set => SetValue(VisitorDetailTextProperty, value);
    }

    public string HomeDetailText
    {
        get => (string)GetValue(HomeDetailTextProperty);
        set => SetValue(HomeDetailTextProperty, value);
    }

    public string HeaderText
    {
        get => (string)GetValue(HeaderTextProperty);
        set => SetValue(HeaderTextProperty, value);
    }

    public string CombinedStatusText
    {
        get => (string)GetValue(CombinedStatusTextProperty);
        set => SetValue(CombinedStatusTextProperty, value);
    }

    public string InningStatusText
    {
        get => (string)GetValue(InningStatusTextProperty);
        set => SetValue(InningStatusTextProperty, value);
    }

    public string OutsStatusText
    {
        get => (string)GetValue(OutsStatusTextProperty);
        set => SetValue(OutsStatusTextProperty, value);
    }

    public bool IsOneUpEnabled
    {
        get => (bool)GetValue(IsOneUpEnabledProperty);
        set => SetValue(IsOneUpEnabledProperty, value);
    }

    public string HeaderAutomationId
    {
        get => (string)GetValue(HeaderAutomationIdProperty);
        set => SetValue(HeaderAutomationIdProperty, value);
    }

    public string VisitorNameAutomationId
    {
        get => (string)GetValue(VisitorNameAutomationIdProperty);
        set => SetValue(VisitorNameAutomationIdProperty, value);
    }

    public string VisitorScoreAutomationId
    {
        get => (string)GetValue(VisitorScoreAutomationIdProperty);
        set => SetValue(VisitorScoreAutomationIdProperty, value);
    }

    public string HomeNameAutomationId
    {
        get => (string)GetValue(HomeNameAutomationIdProperty);
        set => SetValue(HomeNameAutomationIdProperty, value);
    }

    public string HomeScoreAutomationId
    {
        get => (string)GetValue(HomeScoreAutomationIdProperty);
        set => SetValue(HomeScoreAutomationIdProperty, value);
    }

    public string CombinedStatusAutomationId
    {
        get => (string)GetValue(CombinedStatusAutomationIdProperty);
        set => SetValue(CombinedStatusAutomationIdProperty, value);
    }

    public string InningStatusAutomationId
    {
        get => (string)GetValue(InningStatusAutomationIdProperty);
        set => SetValue(InningStatusAutomationIdProperty, value);
    }

    public string OutsStatusAutomationId
    {
        get => (string)GetValue(OutsStatusAutomationIdProperty);
        set => SetValue(OutsStatusAutomationIdProperty, value);
    }

    private static BindableProperty CreateStringProperty(string propertyName) =>
        BindableProperty.Create(propertyName, typeof(string), typeof(FullScoreboard), string.Empty);

    private static void OnProfileChanged(
        BindableObject bindable,
        object oldValue,
        object newValue
    )
    {
        ((FullScoreboard)bindable).ApplyProfile();
    }

    private void ApplyProfile()
    {
        VisualStateManager.GoToState(ContentGrid, Profile.ToString());
    }
}

public enum FullScoreboardProfile
{
    LiveRegularPortrait,
    LiveCompactPortrait,
    LiveRegularLandscape,
    LiveCompactLandscape,
    CompletedPortrait,
    CompletedLandscape,
}

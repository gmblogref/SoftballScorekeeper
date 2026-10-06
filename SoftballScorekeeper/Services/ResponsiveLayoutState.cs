using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SoftballScorekeeper.Services;

public sealed class ResponsiveLayoutState : INotifyPropertyChanged
{
    public const double NarrowPortraitMaximumWidth = 700;
    public const double CompactPortraitMaximumHeight = 1300;
    public const double CompactLandscapeMaximumWidth = 1100;
    public const double CompactLandscapeMaximumHeight = 700;

    private ResponsiveLayoutProfile profile = ResponsiveLayoutProfile.RegularPortrait;
    private double width;
    private double height;

    public static ResponsiveLayoutState Current { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public ResponsiveLayoutProfile Profile
    {
        get => profile;
        private set
        {
            if (profile == value)
                return;

            profile = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsPortrait));
            OnPropertyChanged(nameof(IsLandscape));
            OnPropertyChanged(nameof(IsCompactLandscape));
        }
    }

    public double Width
    {
        get => width;
        private set
        {
            if (Math.Abs(width - value) < double.Epsilon)
                return;

            width = value;
            OnPropertyChanged();
        }
    }

    public double Height
    {
        get => height;
        private set
        {
            if (Math.Abs(height - value) < double.Epsilon)
                return;

            height = value;
            OnPropertyChanged();
        }
    }

    public bool IsPortrait =>
        Profile
            is ResponsiveLayoutProfile.NarrowPortrait
                or ResponsiveLayoutProfile.CompactPortrait
                or ResponsiveLayoutProfile.RegularPortrait;

    public bool IsLandscape => !IsPortrait;

    public bool IsCompactLandscape => Profile == ResponsiveLayoutProfile.CompactLandscape;

    public void Update(double availableWidth, double availableHeight)
    {
        if (availableWidth <= 0 || availableHeight <= 0)
            return;

        Width = availableWidth;
        Height = availableHeight;
        Profile = Classify(availableWidth, availableHeight);
    }

    public static ResponsiveLayoutProfile Classify(double availableWidth, double availableHeight)
    {
        if (availableWidth <= availableHeight)
        {
            if (availableWidth <= NarrowPortraitMaximumWidth)
                return ResponsiveLayoutProfile.NarrowPortrait;

            return availableHeight <= CompactPortraitMaximumHeight
                ? ResponsiveLayoutProfile.CompactPortrait
                : ResponsiveLayoutProfile.RegularPortrait;
        }

        return
            availableWidth <= CompactLandscapeMaximumWidth
            || availableHeight <= CompactLandscapeMaximumHeight
            ? ResponsiveLayoutProfile.CompactLandscape
            : ResponsiveLayoutProfile.RegularLandscape;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public enum ResponsiveLayoutProfile
{
    NarrowPortrait,
    CompactPortrait,
    RegularPortrait,
    CompactLandscape,
    RegularLandscape,
}

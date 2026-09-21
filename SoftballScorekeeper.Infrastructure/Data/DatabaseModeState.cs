using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SoftballScorekeeper.Infrastructure.Data;

public sealed class DatabaseModeState : INotifyPropertyChanged
{
    private DatabaseMode currentMode;

    public DatabaseModeState(DatabaseMode initialMode)
    {
        currentMode = initialMode;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public DatabaseMode CurrentMode => currentMode;

    public bool IsProductionMode => currentMode == DatabaseMode.Production;

    public bool IsTestMode => currentMode == DatabaseMode.Test;

    public bool CanUsePortableBackupAndRestore => IsProductionMode;

    internal void SetCurrentMode(DatabaseMode mode)
    {
        if (currentMode == mode)
            return;

        currentMode = mode;
        OnPropertyChanged(nameof(CurrentMode));
        OnPropertyChanged(nameof(IsProductionMode));
        OnPropertyChanged(nameof(IsTestMode));
        OnPropertyChanged(nameof(CanUsePortableBackupAndRestore));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

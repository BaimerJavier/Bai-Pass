using CommunityToolkit.Mvvm.ComponentModel;
using PasswordManager.Services;

namespace PasswordManager.ViewModels;

/// <summary>
/// Application shell. Hosts either the unlock screen or the vault, and swaps
/// between them as the user unlocks or locks the vault.
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly VaultService _vault = new();

    [ObservableProperty]
    public partial ViewModelBase CurrentView { get; set; }

    public MainViewModel()
    {
        CurrentView = CreateUnlockView();
    }

    private UnlockViewModel CreateUnlockView()
    {
        var unlock = new UnlockViewModel(_vault);
        unlock.Unlocked += OnUnlocked;
        return unlock;
    }

    private void OnUnlocked(string masterPassword)
    {
        var vaultVm = new VaultViewModel(_vault, masterPassword);
        vaultVm.Locked += OnLocked;
        CurrentView = vaultVm;
    }

    private void OnLocked()
    {
        CurrentView = CreateUnlockView();
    }
}

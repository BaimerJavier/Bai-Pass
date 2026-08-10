using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using PasswordManager.Services;

namespace PasswordManager.ViewModels;

/// <summary>
/// Application shell. Hosts either the unlock screen or the vault, and swaps
/// between them as the session is unlocked or locked. Owns the <see cref="SessionManager"/>.
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly SessionManager _session = new(new VaultService());

    [ObservableProperty]
    public partial ViewModelBase CurrentView { get; set; }

    public MainViewModel()
    {
        _session.Locked += OnLocked;
        // Inactivity timeout fires off the UI thread; marshal the actual lock onto it.
        _session.AutoLockRequested += () => Dispatcher.UIThread.Post(_session.Lock);

        CurrentView = CreateUnlockView();
    }

    /// <summary>Exposed so the window can report user input as activity.</summary>
    public SessionManager Session => _session;

    private UnlockViewModel CreateUnlockView()
    {
        var unlock = new UnlockViewModel(_session);
        unlock.Unlocked += OnUnlocked;
        return unlock;
    }

    private void OnUnlocked()
    {
        CurrentView = new VaultViewModel(_session);
    }

    private void OnLocked()
    {
        // Any password sitting in the clipboard should not outlive the session.
        ClipboardService.ClearNow();
        CurrentView = CreateUnlockView();
    }
}

using System;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using PasswordManager.Models;
using PasswordManager.Services;

namespace PasswordManager.ViewModels;

/// <summary>
/// Application shell. Hosts either the unlock screen or the vault, and swaps
/// between them as the session is unlocked or locked. Owns the <see cref="SessionManager"/>.
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly SessionManager _session = new(new VaultService());
    private readonly SettingsService _settingsService = new();
    private readonly AppSettings _settings;
    private readonly ISystemLockMonitor _osLock = new SystemLockMonitor();

    [ObservableProperty]
    public partial ViewModelBase CurrentView { get; set; }

    public MainViewModel()
    {
        _settings = _settingsService.Load();
        _session.AutoLockTimeout = TimeSpan.FromMinutes(_settings.AutoLockMinutes);

        _session.Locked += OnLocked;
        // Inactivity timeout fires off the UI thread; marshal the actual lock onto it.
        _session.AutoLockRequested += () => Dispatcher.UIThread.Post(_session.Lock);

        // OS events (screen lock, session switch, suspend) also force a lock.
        _osLock.LockRequested += () => Dispatcher.UIThread.Post(() =>
        {
            if (_session.IsUnlocked) _session.Lock();
        });
        _osLock.Start();

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
        CurrentView = new VaultViewModel(_session, _settingsService, _settings);
    }

    private void OnLocked()
    {
        // Any password sitting in the clipboard should not outlive the session.
        ClipboardService.ClearNow();
        CurrentView = CreateUnlockView();
    }
}

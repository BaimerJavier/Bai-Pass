using System;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace PasswordManager.Services;

/// <summary>
/// Locks the vault on OS events. On Windows it hooks workstation lock, session
/// disconnect/switch, and suspend via <see cref="SystemEvents"/>. On other platforms
/// it is a no-op (the inactivity auto-lock still applies).
/// </summary>
public sealed class SystemLockMonitor : ISystemLockMonitor
{
    public event Action? LockRequested;

    private bool _started;

    public void Start()
    {
        if (_started || !OperatingSystem.IsWindows()) return;
        _started = true;
        HookWindows();
    }

    public void Stop()
    {
        if (!_started || !OperatingSystem.IsWindows()) return;
        _started = false;
        UnhookWindows();
    }

    [SupportedOSPlatform("windows")]
    private void HookWindows()
    {
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    [SupportedOSPlatform("windows")]
    private void UnhookWindows()
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
    }

    [SupportedOSPlatform("windows")]
    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        switch (e.Reason)
        {
            case SessionSwitchReason.SessionLock:
            case SessionSwitchReason.ConsoleDisconnect:
            case SessionSwitchReason.RemoteDisconnect:
            case SessionSwitchReason.SessionLogoff:
                LockRequested?.Invoke();
                break;
        }
    }

    [SupportedOSPlatform("windows")]
    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        // Lock before the machine suspends so secrets are not sitting unlocked on resume.
        if (e.Mode == PowerModes.Suspend)
            LockRequested?.Invoke();
    }

    public void Dispose() => Stop();
}

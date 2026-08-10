using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using Avalonia.Threading;

namespace PasswordManager.Services;

/// <summary>
/// Thin wrapper over the platform clipboard, set once at startup from the main window.
/// Copied secrets are auto-cleared after a timeout so they don't linger.
/// </summary>
public static class ClipboardService
{
    private static IClipboard? _clipboard;
    private static CancellationTokenSource? _clearCts;

    public static void Init(IClipboard clipboard) => _clipboard = clipboard;

    /// <summary>Copies text and schedules the clipboard to be cleared after some seconds.</summary>
    public static async Task CopyAsync(string text, int clearAfterSeconds = 25)
    {
        if (_clipboard is null || string.IsNullOrEmpty(text)) return;

        await _clipboard.SetTextAsync(text);

        // Cancel any pending clear from a previous copy and schedule a fresh one.
        _clearCts?.Cancel();
        _clearCts = new CancellationTokenSource();
        var token = _clearCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(clearAfterSeconds), token);
                await Dispatcher.UIThread.InvokeAsync(() => _clipboard.ClearAsync());
            }
            catch (TaskCanceledException) { /* superseded by a newer copy */ }
        }, token);
    }
}

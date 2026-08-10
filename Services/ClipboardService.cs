using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using Avalonia.Threading;

namespace PasswordManager.Services;

/// <summary>
/// Thin wrapper over the platform clipboard. Copied secrets are auto-cleared after a timeout,
/// but only if the clipboard still holds what we copied — so we never wipe something the user
/// copied afterwards. Ownership is checked by comparing SHA-256 hashes, so the plaintext secret
/// is not retained for the duration of the timeout.
/// </summary>
public static class ClipboardService
{
    private static IClipboard? _clipboard;
    private static CancellationTokenSource? _clearCts;

    public static void Init(IClipboard clipboard) => _clipboard = clipboard;

    /// <summary>Immediately clears the clipboard and cancels any pending auto-clear (used on lock).</summary>
    public static void ClearNow()
    {
        _clearCts?.Cancel();
        if (_clipboard is null) return;
        Dispatcher.UIThread.Post(() => _clipboard.ClearAsync());
    }

    /// <summary>
    /// Copies <paramref name="text"/> and schedules a clear after <paramref name="clearAfterSeconds"/>.
    /// The clear only happens if the clipboard still contains the copied value.
    /// </summary>
    public static async Task CopyAsync(string text, int clearAfterSeconds = 15)
    {
        if (_clipboard is null || string.IsNullOrEmpty(text)) return;

        await _clipboard.SetTextAsync(text);

        // Remember only a hash of what we put on the clipboard (not the secret itself).
        byte[] expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(text));

        _clearCts?.Cancel();
        _clearCts = new CancellationTokenSource();
        var token = _clearCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(clearAfterSeconds), token);
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    if (_clipboard is null) return;
                    string? current = await _clipboard.TryGetTextAsync();
                    if (current is null) return;

                    byte[] currentHash = SHA256.HashData(Encoding.UTF8.GetBytes(current));
                    if (CryptographicOperations.FixedTimeEquals(currentHash, expectedHash))
                        await _clipboard.ClearAsync(); // still ours -> safe to clear
                });
            }
            catch (TaskCanceledException) { /* superseded by a newer copy or a lock */ }
        }, token);
    }
}

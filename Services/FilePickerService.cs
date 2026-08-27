using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace PasswordManager.Services;

/// <summary>
/// Thin wrapper over Avalonia's storage provider, initialized once from the main window
/// (mirrors <see cref="ClipboardService"/>'s pattern).
/// </summary>
public static class FilePickerService
{
    private static TopLevel? _topLevel;

    public static void Init(TopLevel topLevel) => _topLevel = topLevel;

    /// <summary>Shows a folder picker; returns the chosen folder's local path, or null if cancelled.</summary>
    public static async Task<string?> PickFolderAsync(string title)
    {
        if (_topLevel?.StorageProvider is not { } storageProvider) return null;

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
        });
        return folders.Count > 0 ? folders[0].Path.LocalPath : null;
    }
}

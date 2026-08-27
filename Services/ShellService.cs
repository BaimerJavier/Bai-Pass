using System;
using System.Diagnostics;
using System.IO;

namespace PasswordManager.Services;

/// <summary>OS shell convenience actions (open a folder in the file manager).</summary>
public static class ShellService
{
    /// <summary>
    /// Opens the containing folder of <paramref name="filePath"/> in the OS file manager,
    /// selecting the file when the platform supports it. Best-effort; failures are swallowed
    /// since this is a convenience action, not a security-relevant one.
    /// </summary>
    public static void OpenContainingFolder(string filePath)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{filePath}\"") { UseShellExecute = true });
            else if (OperatingSystem.IsMacOS())
                Process.Start(new ProcessStartInfo("open", $"-R \"{filePath}\"") { UseShellExecute = true });
            else
                Process.Start(new ProcessStartInfo(Path.GetDirectoryName(filePath) ?? ".") { UseShellExecute = true });
        }
        catch { /* best-effort */ }
    }
}

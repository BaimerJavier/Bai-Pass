using System;
using System.IO;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace PasswordManager.Services;

/// <summary>
/// Low-level, crash-safe file storage for the vault. Owns atomic writes, an encrypted
/// backup of the last valid vault, and per-user file permissions. Knows nothing about
/// cryptography — it moves opaque byte blobs and verifies them via a caller-supplied check.
/// </summary>
public sealed class VaultStorage
{
    public string VaultPath { get; }
    public string BackupPath => VaultPath + ".backup";
    private string TmpPath => VaultPath + ".tmp";

    public VaultStorage(string vaultPath)
    {
        VaultPath = vaultPath;
        Directory.CreateDirectory(Path.GetDirectoryName(vaultPath)!);
    }

    public bool Exists => File.Exists(VaultPath);
    public bool BackupExists => File.Exists(BackupPath);

    public byte[] Read() => File.ReadAllBytes(VaultPath);
    public byte[] ReadBackup() => File.ReadAllBytes(BackupPath);

    /// <summary>
    /// Writes <paramref name="data"/> without ever destroying the current valid vault on a crash:
    ///   1. write to a temp file and flush to disk,
    ///   2. read it back and run <paramref name="verify"/> (e.g. "does it decrypt?"),
    ///   3. atomically replace the vault, moving the previous one to the backup,
    ///   4. tighten file permissions.
    /// If verification fails the temp file is discarded and the existing vault is left untouched.
    /// </summary>
    public void WriteVerified(byte[] data, Func<byte[], bool> verify)
    {
        // 1. Durable write to temp.
        using (var fs = new FileStream(TmpPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            fs.Write(data, 0, data.Length);
            fs.Flush(flushToDisk: true);
        }

        // 2. Verify the temp file is readable and decryptable before we touch the real vault.
        byte[] readBack = File.ReadAllBytes(TmpPath);
        if (!verify(readBack))
        {
            TryDelete(TmpPath);
            throw new IOException("Verificacion del vault fallida; la boveda anterior no se modifico.");
        }

        // 3. Atomic replace, keeping the previous valid vault as the backup.
        if (File.Exists(VaultPath))
            File.Replace(TmpPath, VaultPath, BackupPath);
        else
            File.Move(TmpPath, VaultPath);

        // 4. Defense-in-depth: restrict to the current user (AES stays the real defense).
        HardenPermissions(VaultPath);
        if (File.Exists(BackupPath)) HardenPermissions(BackupPath);
    }

    private static void HardenPermissions(string path)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                HardenWindows(path);
            else
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite); // 0600
        }
        catch { /* best-effort; %APPDATA%/~ are already user-scoped */ }
    }

    [SupportedOSPlatform("windows")]
    private static void HardenWindows(string path)
    {
        var user = WindowsIdentity.GetCurrent().User;
        if (user is null) return;

        var security = new FileSecurity();
        security.SetOwner(user);
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new FileSystemAccessRule(
            user, FileSystemRights.FullControl, AccessControlType.Allow));
        new FileInfo(path).SetAccessControl(security);
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
    }
}

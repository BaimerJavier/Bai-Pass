# Security Model — Bai-Pass

Bai-Pass is a **local, offline** password manager. This document describes how it protects
your data, what it does **not** protect against, and how to report a vulnerability. It makes
no claim of absolute security.

## Cryptographic architecture

```
                MASTER PASSWORD (never stored)
                       │
                       ▼
                  Argon2id  (PMV2)   ── or PBKDF2-HMAC-SHA256 for legacy PMV1
                       │
                       ▼
               256-bit Vault Key  (held in a zeroizable buffer for the session)
                       │
       ┌───────────────┴────────────────┐
       ▼                                 ▼
 AES-256-GCM                        Session lifecycle
 vault encryption                   (auto-lock, OS-event lock)
       │                                 │
       ▼                                 ▼
  encrypted vault.dat               key zeroized on lock
```

- **Encryption:** AES-256-GCM (authenticated). Any modification of the file is detected on
  decrypt; on authentication failure **no plaintext is produced**.
- **Key derivation:**
  - **PMV2 (current):** Argon2id, memory 64 MiB, 3 passes, parallelism 4, 32-byte output.
  - **PMV1 (legacy, read + auto-migrated):** PBKDF2-HMAC-SHA256, 600,000 iterations.
- **Randomness:** all salts, nonces, and generated passwords use the OS CSPRNG
  (`RandomNumberGenerator`). Nonces are 96-bit and never reused with a given key.
- The **master password is never stored** anywhere (not on disk, not in the registry). There is
  **no recovery** if you forget it.

## Vault file format

`vault.dat` is a single binary file. The header is self-describing so parameters can evolve.

| Field | PMV1 (legacy) | PMV2 (current) |
|-------|---------------|----------------|
| magic | `"PMV1"` (4) | `"PMV2"` (4) |
| KDF params | — (PBKDF2 600k assumed) | memoryKiB (4, LE), iterations (4, LE), parallelism (1) |
| salt | 16 | 16 |
| nonce | 12 | 12 |
| GCM tag | 16 | 16 |
| ciphertext | rest | rest |

The ciphertext is the AES-256-GCM encryption of the entries serialized as JSON. KDF parameters
read from the header are **range-checked** before use (memory 8 KiB–1 GiB, iterations 1–24,
parallelism 1–16) so a crafted file cannot force a huge allocation.

### Migration

Opening a legacy **PMV1** vault transparently upgrades it to **PMV2 (Argon2id)**. The new vault
is written and verified to decrypt **before** replacing the old one, and the previous file is
kept as `vault.dat.backup`. A failure leaves the original PMV1 vault intact.

## Storage safety

- **Atomic writes:** save goes to `vault.dat.tmp`, is flushed to disk, is verified to decrypt,
  and only then atomically replaces `vault.dat`, moving the previous copy to `vault.dat.backup`.
  A crash cannot leave a half-written or unreadable vault.
- **Backup:** `vault.dat.backup` is the last valid vault — **also encrypted**. Bai-Pass never
  writes plaintext, decrypted JSON, keys, or clipboard contents to disk, logs, or temp files.
- **Permissions:** the vault is stored under your per-user profile
  (`%APPDATA%\PasswordManager` on Windows, `~/.config/PasswordManager` on Unix) and further
  restricted to the current user (owner-only ACL on Windows, mode 0600 on Unix). File
  permissions are defense-in-depth; **AES is the real protection**.

## Session & memory lifecycle

```
Unlock → derive key → drop the typed password
   ↓
Session: key in a zeroizable buffer + decrypted entries in memory
   ↓
Lock (manual / inactivity / OS event) → zeroize key → clear entries → clear clipboard → Lock screen
```

- The master password is handed to the session, used to derive the key, and then dropped. It is
  **not** retained for the session.
- Auto-lock on **inactivity** (default 10 min, configurable 1/5/10/15/30) and on **OS events**
  (Windows screen lock, session switch/disconnect, suspend).
- On lock, the 256-bit key is wiped with `CryptographicOperations.ZeroMemory`, decrypted entries
  are cleared, and any password on the clipboard is cleared.
- **Documented limitation:** while unlocked, the key and decrypted entries live in
  .NET-managed memory. .NET strings (the entry passwords, and the transient master-password
  string that arrives via UI text binding) are immutable and **cannot be zeroed**; they remain
  until garbage-collected. A privileged memory dump of the running, unlocked process can
  therefore expose secrets. We minimize lifetime and wipe what we control; we do not claim RAM
  is fully protected.

## Clipboard

Copying a password clears it from the clipboard after a configurable delay (default 15s), **but
only if the clipboard still holds that value** (checked by SHA-256 hash, so the plaintext is not
retained). Note the OS may still capture clipboard contents (Windows Clipboard History / cloud
clipboard) — outside the app's control.

## Threat model

### Bai-Pass is designed to protect against

- **Theft of `vault.dat` or `vault.dat.backup`** (including from backups or cloud sync): the
  attacker faces an **offline guessing attack against your master password**, slowed by Argon2id.
- **Tampering** with the vault: any modified byte fails GCM authentication.
- **Crash/power loss during save:** the previous valid vault is preserved.
- A **locked** application: the session key and full decrypted model are not intentionally kept.

### Bai-Pass does NOT protect against

A host that is already compromised **while the vault is unlocked**:

- keyloggers capturing your master password as you type it,
- malware or a debugger with access to the process memory,
- screen capture,
- clipboard monitoring,
- privileged process/memory dumping.

If the attacker has Administrator/root and active malware on the machine, no user-space password
manager can guarantee protection. Bai-Pass does not claim to.

### Offline brute force

If an attacker obtains `vault.dat`, the primary attack is **offline password guessing**. Your
protection is: a **strong master password**, Argon2id, a unique random salt, and strong KDF
parameters. A "5 failed attempts" lockout is **not** a defense here — an attacker with a copy of
the file ignores the UI entirely — so Bai-Pass does not rely on one.

## Master-password recommendations

- Prefer **length** over complexity rules. A long passphrase beats a short "P@ss1".
- Guidance: `< 12` weak · `12–15` acceptable · `16+` recommended.
- It is never stored or transmitted. If you lose it, your data is unrecoverable.

## Backups

Copy `vault.dat` (and optionally `vault.dat.backup`) to external/cloud storage. They stay
encrypted, so this is safe — their security still rests entirely on your master password.

## Reporting a vulnerability

Please report security issues privately via the repository's **GitHub Security Advisories**
("Report a vulnerability") rather than a public issue. Include reproduction steps and the
version/commit. We aim to acknowledge reports promptly. Please do not include real vault files or
real passwords in a report.

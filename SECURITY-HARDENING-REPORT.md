# Bai-Pass — Security Hardening Report

Scope: a defensive security audit and incremental hardening of Bai-Pass, a local .NET/C#
password manager. Priorities: **Confidentiality > Integrity > Availability > Convenience**.
Every change reduces a concrete risk. No cryptographic primitive was replaced without first
preserving the existing vault format and compatibility.

---

## Before

- **KDF:** PBKDF2-HMAC-SHA256, 600,000 iterations (hardcoded), fresh 16-byte salt per save.
- **Cipher:** AES-256-GCM, random 96-bit nonce, 128-bit tag. Correct and authenticated.
- **Format:** `"PMV1" | salt | nonce | tag | ciphertext`; parameters not stored (compiled constants).
- **Master password:** kept as a `string` for the whole session (`VaultViewModel._masterPassword`),
  re-deriving the key on every save.
- **Decrypted data:** JSON `byte[]`/`string` and `List<PasswordEntry>` held for the session; not wiped.
- **Lock:** UI navigation only — no zeroization, no teardown; clipboard could retain the password.
- **Auto-lock:** fixed **8 hours since unlock** (not inactivity).
- **Clipboard:** 25s, cleared **blindly** (would wipe a value the user copied afterwards).
- **Storage:** temp file + `File.Replace` with no fsync, no verification, no backup.
- **Errors:** wrong-password vs corrupted distinguishable; internal `ex.Message` shown in UI.
- **Generator:** CSPRNG pool draw; did not guarantee category presence; no config.
- **Supply chain:** no CI, no dependency audit, no CodeQL, no SECURITY.md.

The core cryptography was sound; the weaknesses were **defense-in-depth**: secret lifetime in
memory, session teardown, storage durability, and format evolvability.

---

## Vulnerabilities / weaknesses found

| Severity | Issue | Risk | Resolution |
|----------|-------|------|------------|
| HIGH | Master password kept as `string` all session | Exposed to memory dump; non-zeroable | Derive key once, hold zeroizable `SecureSessionKey`, drop password |
| HIGH | Lock didn't tear down secrets | Key/entries/clipboard survive "lock" | `SessionManager.Lock()` zeroizes key, clears entries, clears clipboard |
| HIGH | Auto-lock fixed 8h since unlock | Unattended machine stays open 8h | Inactivity-based auto-lock, default 10 min, configurable |
| HIGH | No lock on OS events | Screen-lock/suspend leave vault open | `SystemLockMonitor` locks on Windows lock/session-switch/suspend |
| MED | Decrypted JSON buffers not zeroed | Plaintext lingers in RAM | Wipe decrypted `byte[]` after use (`string` limitation documented) |
| MED | KDF not memory-hard | GPU/ASIC-friendlier than Argon2id | Argon2id (PMV2) for new/migrated vaults |
| MED | Format not self-describing | Params change breaks old vaults | Versioned header stores KDF + params (PMV2) |
| MED | No verify/backup on save | A fault could destroy the vault | Verify temp decrypts before atomic replace; keep encrypted `.backup` |
| MED | Clipboard blind clear, 25s | Wipes newer clipboard; long exposure | Clear only if still ours (hash-checked); default 15s, configurable |
| LOW | Wrong-password vs corrupted oracle + `ex.Message` in UI | Minor info disclosure | Unified unlock message; stop surfacing internal detail |
| LOW | `BAIPASS_VAULT` override shipped in prod | Malware could redirect the vault | Gated to DEBUG builds only |
| LOW | Weak master-password policy | Weak passwords | Length-based guidance documented (min floor kept) |
| LOW | Generator: category not guaranteed; no config | Rare missing category | Guarantee categories + Fisher-Yates shuffle + options |
| LOW | No vault file ACL hardening | Relies on profile defaults | Owner-only ACL (Windows) / 0600 (Unix) |
| INFO | No CI/audit/CodeQL/SECURITY.md | Supply chain | CI, Dependabot, CodeQL, SECURITY.md added |
| **NEW/MED** | *(introduced by PMV2, then fixed)* KDF params from untrusted header | Allocation bomb via huge Argon2 memory | Range-check params on read (mem 8KiB–1GiB, iter 1–24, par 1–16) |

> **No CRITICAL findings.** A stolen `vault.dat` was never trivially recoverable; the crypto was
> and remains authenticated AES-256-GCM over a strongly-derived key.

---

## After

- Session security centralized in `SessionManager` / `SecureSessionKey`; ViewModels no longer own
  key material.
- Master password derived once and dropped; key held in a zeroizable buffer; wiped on lock.
- Auto-lock on inactivity (configurable) **and** OS events.
- Argon2id (PMV2) with a self-describing, range-checked header; seamless, verified, reversible
  PMV1→PMV2 migration.
- Crash-safe atomic writes with an encrypted backup; owner-only file permissions.
- Hardened clipboard (ownership-checked, shorter default) and password generator.
- 49 automated tests (crypto round-trip, wrong password, tamper of every region, key/password
  compatibility, unicode/large vault, migration, storage/backup, crash-during-save, fuzz/
  corruption, KDF-param validation, session clear-on-lock, auto-lock timing, generator).
- CI (build + test + dependency audit), CodeQL, Dependabot, `SECURITY.md`.

### Cryptographic architecture

```
               MASTER PASSWORD  (never stored)
                       │
                       ▼
                  Argon2id (PMV2)     [PBKDF2-600k for legacy PMV1]
                       │
                       ▼
               256-bit Vault Key
                       │
       ┌───────────────┴────────────────┐
       │                                 │
       ▼                                 ▼
 AES-256-GCM                        Session lifecycle
 vault encryption                   (inactivity + OS-event lock)
       │                                 │
       ▼                                 ▼
  encrypted vault                   Zero key on Lock
```

### Vault format

- **PMV1 (legacy, read + auto-migrate):** `"PMV1" | salt(16) | nonce(12) | tag(16) | ct`. PBKDF2 600k.
- **PMV2 (current):** `"PMV2" | memKiB(4 LE) | iters(4 LE) | par(1) | salt(16) | nonce(12) | tag(16) | ct`.
  Argon2id (64 MiB / t=3 / p=4). Params range-checked on read.

### Memory lifecycle

```
Unlock
  ↓  derive key (Argon2id/PBKDF2 over stored salt)
Clear password input (reference dropped; string GC-managed)
  ↓
Use SessionKey (zeroizable byte[]) + decrypted entries
  ↓
Lock (manual / inactivity / OS event)
  ↓  Zero SessionKey
  ↓  Clear decrypted entries
  ↓  Clear clipboard
Lock screen
```

---

## Remaining risks

- **Unlocked-process memory exposure.** While unlocked, the key and entries are in managed
  memory; entry passwords and the transient master-password string are immutable .NET strings
  that cannot be zeroed and persist until GC. A privileged memory dump can expose them. Mitigated
  (short lifetime, zeroize what we control), not eliminated. This is inherent to a .NET desktop app.
- **Compromised host.** Keyloggers, in-process malware, screen capture, and clipboard monitoring
  are out of scope (see threat model in `SECURITY.md`).
- **OS clipboard history / cloud clipboard** may retain a copied password beyond our clearing.
- **Weak master password.** KDF only slows guessing; a weak password remains the dominant risk
  for a stolen vault.
- **Unsigned binaries.** Distribution is not yet code-signed (SmartScreen warning on first run);
  supply-chain signing is a future step.

---

## Verification

After every phase: `dotnet restore`, `dotnet build`, `dotnet test` — all green (49 tests).
Changes were made incrementally with small, semantic commits.

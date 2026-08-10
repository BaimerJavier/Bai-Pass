using System;
using System.Security.Cryptography;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordManager.Services;

namespace PasswordManager.ViewModels;

/// <summary>
/// First screen: create a new vault (with a master password) or unlock the existing one.
/// The typed password is handed to the <see cref="SessionManager"/> and then cleared from
/// this ViewModel so it is not held for the session.
/// </summary>
public partial class UnlockViewModel : ViewModelBase
{
    private readonly SessionManager _session;

    /// <summary>Raised once the vault is open.</summary>
    public event Action? Unlocked;

    /// <summary>True when no vault exists yet, so we show the "create master password" flow.</summary>
    public bool IsCreateMode { get; }

    public string Title => IsCreateMode ? "Crea tu contrasena maestra" : "Desbloquea tu boveda";
    public string ActionText => IsCreateMode ? "Crear boveda" : "Desbloquear";
    public bool IsUnlockMode => !IsCreateMode;

    [ObservableProperty] public partial string MasterPassword { get; set; } = "";
    [ObservableProperty] public partial string ConfirmPassword { get; set; } = "";
    [ObservableProperty] public partial string ErrorMessage { get; set; } = "";
    [ObservableProperty] public partial bool IsBusy { get; set; }

    public UnlockViewModel(SessionManager session)
    {
        _session = session;
        IsCreateMode = !_session.VaultExists;
    }

    [RelayCommand]
    private void Submit()
    {
        ErrorMessage = "";

        if (string.IsNullOrEmpty(MasterPassword))
        {
            ErrorMessage = "Escribe una contrasena.";
            return;
        }

        if (IsCreateMode)
        {
            if (MasterPassword.Length < 8)
            {
                ErrorMessage = "La contrasena maestra debe tener al menos 8 caracteres.";
                return;
            }
            if (MasterPassword != ConfirmPassword)
            {
                ErrorMessage = "Las contrasenas no coinciden.";
                return;
            }

            IsBusy = true;
            try
            {
                _session.CreateNew(MasterPassword);
                ClearInputs();
                Unlocked?.Invoke();
            }
            catch (Exception ex)
            {
                ErrorMessage = "No se pudo crear la boveda: " + ex.Message;
            }
            finally { IsBusy = false; }
        }
        else
        {
            IsBusy = true;
            try
            {
                _session.Unlock(MasterPassword);
                ClearInputs();
                Unlocked?.Invoke();
            }
            catch (CryptographicException)
            {
                // Unified message: do not reveal whether it was a wrong password or a
                // tampered/corrupted file (avoids an oracle).
                ErrorMessage = "No se pudo desbloquear la boveda. La contrasena es incorrecta o el archivo esta danado.";
            }
            catch (System.IO.InvalidDataException)
            {
                ErrorMessage = "No se pudo desbloquear la boveda. La contrasena es incorrecta o el archivo esta danado.";
            }
            catch (Exception)
            {
                ErrorMessage = "No se pudo abrir la boveda.";
            }
            finally { IsBusy = false; }
        }
    }

    private void ClearInputs()
    {
        // Drop our references to the typed password promptly. The underlying string stays
        // GC-managed (cannot be zeroed), but it is no longer retained for the session.
        MasterPassword = "";
        ConfirmPassword = "";
    }
}

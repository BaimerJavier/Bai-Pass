using System;
using System.Security.Cryptography;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordManager.Services;

namespace PasswordManager.ViewModels;

/// <summary>
/// First screen: create a new vault (with a master password) or unlock the existing one.
/// </summary>
public partial class UnlockViewModel : ViewModelBase
{
    private readonly VaultService _vault;

    /// <summary>Raised with the verified master password once the vault is open.</summary>
    public event Action<string>? Unlocked;

    /// <summary>True when no vault exists yet, so we show the "create master password" flow.</summary>
    public bool IsCreateMode { get; }

    public string Title => IsCreateMode ? "Crea tu contrasena maestra" : "Desbloquea tu boveda";
    public string ActionText => IsCreateMode ? "Crear boveda" : "Desbloquear";
    public bool IsUnlockMode => !IsCreateMode;

    [ObservableProperty] public partial string MasterPassword { get; set; } = "";
    [ObservableProperty] public partial string ConfirmPassword { get; set; } = "";
    [ObservableProperty] public partial string ErrorMessage { get; set; } = "";
    [ObservableProperty] public partial bool IsBusy { get; set; }

    public UnlockViewModel(VaultService vault)
    {
        _vault = vault;
        IsCreateMode = !_vault.VaultExists();
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
                // Create an empty encrypted vault right away so the file exists.
                _vault.Save(new System.Collections.Generic.List<Models.PasswordEntry>(), MasterPassword);
                Unlocked?.Invoke(MasterPassword);
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
                _ = _vault.Load(MasterPassword); // throws if password is wrong
                Unlocked?.Invoke(MasterPassword);
            }
            catch (CryptographicException)
            {
                ErrorMessage = "Contrasena incorrecta.";
            }
            catch (Exception ex)
            {
                ErrorMessage = "No se pudo abrir la boveda: " + ex.Message;
            }
            finally { IsBusy = false; }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordManager.Models;
using PasswordManager.Services;

namespace PasswordManager.ViewModels;

/// <summary>
/// Main screen once unlocked: entries grouped into collapsible folders plus an editable
/// detail panel. Every change is encrypted and written to disk immediately. Session
/// lifecycle (key, auto-lock, teardown) is owned by the <see cref="SessionManager"/>.
/// </summary>
public partial class VaultViewModel : ViewModelBase
{
    /// <summary>Label used for entries that have no folder assigned.</summary>
    public const string UngroupedName = "Sin carpeta";

    private readonly SessionManager _session;
    private readonly SettingsService _settingsService;
    private readonly Models.AppSettings _settings;
    private readonly IList<PasswordEntry> _all;

    // Remembers each folder's expanded/collapsed state across list rebuilds.
    private readonly Dictionary<string, bool> _expandState = new();

    /// <summary>Folders shown in the sidebar (collapsible groups of entries).</summary>
    public ObservableCollection<FolderGroup> Groups { get; } = new();

    /// <summary>Auto-lock timeout options offered in the UI (minutes).</summary>
    public int[] AutoLockOptions { get; } = { 1, 5, 10, 15, 30 };

    [ObservableProperty] public partial int SelectedAutoLockMinutes { get; set; }

    /// <summary>Distinct folder names, for the "Carpeta" autocomplete in the detail panel.</summary>
    public ObservableCollection<string> FolderNames { get; } = new();

    [ObservableProperty] public partial PasswordEntry? SelectedEntry { get; set; }
    [ObservableProperty] public partial string SearchText { get; set; } = "";
    [ObservableProperty] public partial bool IsPasswordVisible { get; set; }
    [ObservableProperty] public partial string StatusMessage { get; set; } = "";

    // Two-step delete confirmation state.
    [ObservableProperty] public partial bool DeleteStep1Visible { get; set; }
    [ObservableProperty] public partial bool DeleteStep2Visible { get; set; }
    [ObservableProperty] public partial string DeleteTargetName { get; set; } = "";

    public bool IsDeleteConfirmActive => DeleteStep1Visible || DeleteStep2Visible;

    partial void OnDeleteStep1VisibleChanged(bool value) => OnPropertyChanged(nameof(IsDeleteConfirmActive));
    partial void OnDeleteStep2VisibleChanged(bool value) => OnPropertyChanged(nameof(IsDeleteConfirmActive));

    public bool HasSelection => SelectedEntry is not null;
    public int EntryCount => _all.Count;

    public VaultViewModel(SessionManager session, SettingsService settingsService, Models.AppSettings settings)
    {
        _session = session;
        _settingsService = settingsService;
        _settings = settings;
        _all = _session.Entries;
        SelectedAutoLockMinutes = settings.AutoLockMinutes;
        RefreshGroups();
    }

    partial void OnSelectedAutoLockMinutesChanged(int value)
    {
        if (value <= 0) return;
        _session.AutoLockTimeout = TimeSpan.FromMinutes(value);
        _session.RegisterActivity();
        _settings.AutoLockMinutes = value;
        _settingsService.Save(_settings);
    }

    partial void OnSearchTextChanged(string value)
    {
        _session.RegisterActivity();
        RefreshGroups();
    }

    partial void OnSelectedEntryChanged(PasswordEntry? value)
    {
        IsPasswordVisible = false;
        OnPropertyChanged(nameof(HasSelection));
    }

    /// <summary>Selects an entry from the grouped list and updates the highlight.</summary>
    [RelayCommand]
    private void SelectEntry(PasswordEntry? entry)
    {
        _session.RegisterActivity();
        foreach (var e in _all)
            e.IsSelected = ReferenceEquals(e, entry);
        SelectedEntry = entry;
    }

    /// <summary>Rebuilds the folder groups from the (optionally search-filtered) entries.</summary>
    private void RefreshGroups()
    {
        IEnumerable<PasswordEntry> query = _all;
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            string s = SearchText.Trim();
            query = _all.Where(e =>
                e.Title.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                e.Username.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                e.Url.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                e.Folder.Contains(s, StringComparison.OrdinalIgnoreCase));
        }

        // Group by folder name; entries without a folder fall under UngroupedName.
        var grouped = query
            .GroupBy(e => string.IsNullOrWhiteSpace(e.Folder) ? UngroupedName : e.Folder.Trim())
            .OrderBy(g => g.Key == UngroupedName ? 1 : 0)          // named folders first
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        Groups.Clear();
        foreach (var g in grouped)
        {
            bool expanded = _expandState.TryGetValue(g.Key, out var e) ? e : true;
            var group = new FolderGroup(g.Key, expanded);
            // Keep the expand state in sync when the user collapses/expands.
            group.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(FolderGroup.IsExpanded) && s is FolderGroup fg)
                    _expandState[fg.Name] = fg.IsExpanded;
            };
            foreach (var entry in g.OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase))
                group.Entries.Add(entry);
            Groups.Add(group);
        }

        RefreshFolderNames();
    }

    private void RefreshFolderNames()
    {
        var names = _all
            .Select(e => e.Folder?.Trim() ?? "")
            .Where(f => !string.IsNullOrEmpty(f))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        FolderNames.Clear();
        foreach (var n in names) FolderNames.Add(n);
    }

    [RelayCommand]
    private void AddEntry()
    {
        // New entry inherits the folder of the current selection, so adding several to
        // the same folder in a row is quick.
        var entry = new PasswordEntry
        {
            Title = "Nueva entrada",
            Folder = SelectedEntry?.Folder ?? "",
        };
        _all.Add(entry);
        RefreshGroups();
        SelectEntry(entry);
        OnPropertyChanged(nameof(EntryCount));
        Persist("Entrada agregada.");
    }

    // Step 0: user clicked the trash button -> ask for the first confirmation.
    [RelayCommand]
    private void RequestDelete()
    {
        if (SelectedEntry is null) return;
        DeleteTargetName = SelectedEntry.DisplayName;
        DeleteStep2Visible = false;
        DeleteStep1Visible = true;
    }

    // Step 1 confirmed -> ask for the final (re)confirmation.
    [RelayCommand]
    private void ConfirmDeleteStep1()
    {
        DeleteStep1Visible = false;
        DeleteStep2Visible = true;
    }

    // Step 2 confirmed -> actually remove the entry (permanent).
    [RelayCommand]
    private void ConfirmDeleteStep2()
    {
        DeleteStep2Visible = false;
        if (SelectedEntry is null) return;
        _all.Remove(SelectedEntry);
        SelectedEntry = null;
        RefreshGroups();
        OnPropertyChanged(nameof(EntryCount));
        Persist("Entrada eliminada.");
    }

    // Cancel at any step -> nothing is deleted.
    [RelayCommand]
    private void CancelDelete()
    {
        DeleteStep1Visible = false;
        DeleteStep2Visible = false;
    }

    [RelayCommand]
    private void SaveEntry()
    {
        if (SelectedEntry is null) return;
        SelectedEntry.Modified = DateTime.UtcNow;
        RefreshGroups();
        Persist("Cambios guardados.");
    }

    [RelayCommand]
    private void GeneratePassword()
    {
        if (SelectedEntry is null) return;
        _session.RegisterActivity();
        SelectedEntry.Password = PasswordGenerator.Generate();
        IsPasswordVisible = true;
    }

    [RelayCommand]
    private async Task CopyPassword()
    {
        if (SelectedEntry is null) return;
        _session.RegisterActivity();
        await ClipboardService.CopyAsync(SelectedEntry.Password);
        Flash("Contrasena copiada (se borra en 25 s).");
    }

    [RelayCommand]
    private async Task CopyUsername()
    {
        if (SelectedEntry is null) return;
        _session.RegisterActivity();
        await ClipboardService.CopyAsync(SelectedEntry.Username, clearAfterSeconds: 60);
        Flash("Usuario copiado.");
    }

    [RelayCommand]
    private void TogglePasswordVisibility() => IsPasswordVisible = !IsPasswordVisible;

    [RelayCommand]
    private void Lock() => _session.Lock();

    private void Persist(string message)
    {
        _session.RegisterActivity();
        try
        {
            _session.Save();
            Flash(message);
        }
        catch (Exception)
        {
            // Do not surface internal details (paths, IO) to the UI.
            Flash("No se pudieron guardar los cambios.");
        }
    }

    private void Flash(string message)
    {
        StatusMessage = message;
        _ = ClearStatusLater();
    }

    private async Task ClearStatusLater()
    {
        await Task.Delay(3000);
        StatusMessage = "";
    }
}

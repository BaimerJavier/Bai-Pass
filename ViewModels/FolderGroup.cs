using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PasswordManager.Models;

namespace PasswordManager.ViewModels;

/// <summary>
/// A named, collapsible group of entries shown in the sidebar.
/// </summary>
public partial class FolderGroup : ObservableObject
{
    public string Name { get; }

    /// <summary>Whether the group is expanded in the UI (persisted per-session).</summary>
    [ObservableProperty] public partial bool IsExpanded { get; set; } = true;

    /// <summary>True while the header shows the rename textbox instead of the folder name.</summary>
    [ObservableProperty] public partial bool IsRenaming { get; set; }

    /// <summary>Working value of the rename textbox (committed via VaultViewModel.RenameFolder).</summary>
    [ObservableProperty] public partial string EditingName { get; set; } = "";

    public ObservableCollection<PasswordEntry> Entries { get; } = new();

    public int Count => Entries.Count;

    public FolderGroup(string name, bool isExpanded = true)
    {
        Name = name;
        IsExpanded = isExpanded;
    }
}

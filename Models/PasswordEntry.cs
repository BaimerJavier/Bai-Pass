using System;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PasswordManager.Models;

/// <summary>
/// A single stored credential. Serialized to JSON before encryption.
/// </summary>
public partial class PasswordEntry : ObservableObject
{
    [ObservableProperty] public partial string Title { get; set; } = "";
    [ObservableProperty] public partial string Username { get; set; } = "";
    [ObservableProperty] public partial string Password { get; set; } = "";
    [ObservableProperty] public partial string Url { get; set; } = "";
    [ObservableProperty] public partial string Notes { get; set; } = "";
    /// <summary>Folder/group this entry belongs to. Empty = ungrouped.</summary>
    [ObservableProperty] public partial string Folder { get; set; } = "";
    [ObservableProperty] public partial DateTime Created { get; set; } = DateTime.UtcNow;
    [ObservableProperty] public partial DateTime Modified { get; set; } = DateTime.UtcNow;

    /// <summary>UI-only selection highlight; not persisted.</summary>
    [ObservableProperty]
    [property: JsonIgnore]
    public partial bool IsSelected { get; set; }

    /// <summary>Fallback label shown in the list when Title is empty.</summary>
    [JsonIgnore]
    public string DisplayName =>
        !string.IsNullOrWhiteSpace(Title) ? Title
        : !string.IsNullOrWhiteSpace(Username) ? Username
        : "(sin titulo)";
}

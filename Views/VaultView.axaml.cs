using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using PasswordManager.ViewModels;

namespace PasswordManager.Views;

public partial class VaultView : UserControl
{
    // Detail panel width the user last chose (resizable with the splitter).
    private double _drawerWidth = 560;
    private VaultViewModel? _vm;

    public VaultView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_vm is not null) _vm.PropertyChanged -= OnVmChanged;
            _vm = DataContext as VaultViewModel;
            if (_vm is not null) _vm.PropertyChanged += OnVmChanged;
            ApplyDrawer();
        };
    }

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(VaultViewModel.HasSelection)) ApplyDrawer();
    }

    /// <summary>Shows the detail panel (at the remembered width) only while an entry is selected.</summary>
    private void ApplyDrawer()
    {
        var split = RootGrid.ColumnDefinitions[1];
        var drawer = RootGrid.ColumnDefinitions[2];
        bool show = _vm?.HasSelection == true;
        if (show)
        {
            drawer.MinWidth = 360;
            drawer.Width = new GridLength(_drawerWidth);
            split.Width = new GridLength(12);
        }
        else
        {
            if (drawer.ActualWidth >= 360) _drawerWidth = drawer.ActualWidth;
            drawer.MinWidth = 0;
            drawer.Width = new GridLength(0);
            split.Width = new GridLength(16);
        }
    }
}

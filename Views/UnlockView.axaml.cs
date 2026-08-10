using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace PasswordManager.Views;

public partial class UnlockView : UserControl
{
    public UnlockView()
    {
        InitializeComponent();
        // Focus the password box as soon as the view is shown.
        AttachedToVisualTree += (_, _) => MasterBox.Focus();
    }
}

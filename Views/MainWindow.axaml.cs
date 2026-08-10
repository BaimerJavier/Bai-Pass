using Avalonia.Controls;
using PasswordManager.Services;

namespace PasswordManager.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Opened += (_, _) =>
        {
            if (Clipboard is not null)
                ClipboardService.Init(Clipboard);
        };
    }
}

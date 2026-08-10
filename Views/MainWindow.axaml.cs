using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PasswordManager.Services;
using PasswordManager.ViewModels;

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

        // Report user input as activity so the inactivity auto-lock resets. Tunnel + handled
        // so it fires regardless of which control ultimately handles the event. RegisterActivity
        // just stores a timestamp, so doing it on pointer-move is cheap.
        AddHandler(PointerMovedEvent, OnUserActivity, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerPressedEvent, OnUserActivity, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(KeyDownEvent, OnUserActivity, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void OnUserActivity(object? sender, RoutedEventArgs e) =>
        (DataContext as MainViewModel)?.Session.RegisterActivity();
}

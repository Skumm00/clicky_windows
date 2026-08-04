using System.Diagnostics;
using Microsoft.Win32;
using System.Windows;
using ClickyWindows.Models;

namespace ClickyWindows;

public partial class WindowsUiRescueWindow : Window
{
    private readonly WindowsUiRescueKind _rescue;
    private RescueAction? _pendingAction;

    public WindowsUiRescueWindow(WindowsUiRescueKind rescue)
    {
        InitializeComponent();
        _rescue = rescue;
        ConfigureCard();
    }

    private void ConfigureCard()
    {
        if (_rescue == WindowsUiRescueKind.TaskbarNotResponding)
        {
            TitleText.Text = "Taskbar not responding";
            SummaryText.Text = "The taskbar and desktop are provided by Windows Explorer. Restarting it refreshes those surfaces without closing your open apps.";
            StepsList.ItemsSource = new[]
            {
                new RescueStep("1", "Press Ctrl + Shift + Esc to open Task Manager."),
                new RescueStep("2", "Find Windows Explorer in the Processes list."),
                new RescueStep("3", "Select it, then choose Restart task."),
            };
            PrimaryActionButton.Content = "Open Task Manager";
            SecondaryActionButton.Content = "Restart Explorer";
            return;
        }

        TitleText.Text = "Desktop icons disappeared";
        SummaryText.Text = "Windows can hide desktop icons without deleting any files. Restore the display setting first.";
        StepsList.ItemsSource = new[]
        {
            new RescueStep("1", "Right-click an empty area of the desktop."),
            new RescueStep("2", "Choose View."),
            new RescueStep("3", "Select Show desktop icons."),
        };
        PrimaryActionButton.Content = "Restore desktop icons";
        SecondaryActionButton.Content = "Open desktop";
    }

    private void PrimaryActionClick(object sender, RoutedEventArgs e)
    {
        if (_rescue == WindowsUiRescueKind.TaskbarNotResponding)
        {
            Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true });
            return;
        }

        RequestConfirmation(
            RescueAction.RestoreDesktopIcons,
            "This will turn the Windows desktop-icons setting back on and restart Windows Explorer. Your open apps will stay open, but the taskbar and desktop may briefly refresh.");
    }

    private void SecondaryActionClick(object sender, RoutedEventArgs e)
    {
        if (_rescue == WindowsUiRescueKind.TaskbarNotResponding)
        {
            RequestConfirmation(
                RescueAction.RestartExplorer,
                "This will restart Windows Explorer. Your open apps will stay open, but the taskbar and desktop may briefly disappear and return.");
            return;
        }

        Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true });
    }

    private void RequestConfirmation(RescueAction action, string message)
    {
        _pendingAction = action;
        ConfirmationText.Text = message;
        ConfirmationPanel.Visibility = Visibility.Visible;
    }

    private void ConfirmClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_pendingAction == RescueAction.RestoreDesktopIcons)
            {
                using var key = Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", writable: true);
                key?.SetValue("HideIcons", 0, RegistryValueKind.DWord);
            }

            RestartExplorer();
            ConfirmationPanel.Visibility = Visibility.Collapsed;
            SummaryText.Text = _pendingAction == RescueAction.RestoreDesktopIcons
                ? "Desktop icons have been restored. Your desktop refreshed to apply the change."
                : "Windows Explorer restarted. Give the taskbar a moment to return.";
            _pendingAction = null;
        }
        catch (Exception ex)
        {
            ConfirmationText.Text = $"Windows could not complete that action: {ex.Message}";
        }
    }

    private static void RestartExplorer()
    {
        foreach (var process in Process.GetProcessesByName("explorer"))
            process.Kill();
        Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true });
    }

    private void CancelConfirmationClick(object sender, RoutedEventArgs e)
    {
        _pendingAction = null;
        ConfirmationPanel.Visibility = Visibility.Collapsed;
    }

    private void CloseClick(object sender, RoutedEventArgs e) => Close();

    private sealed record RescueStep(string Number, string Text);

    private enum RescueAction
    {
        RestartExplorer,
        RestoreDesktopIcons,
    }
}

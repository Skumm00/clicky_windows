using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using ClickyWindows.Helpers;
using ClickyWindows.Services;
using ClickyWindows.Settings;

namespace ClickyWindows;

/// <summary>
/// Application entry point. Owns the tray icon and the companion lifecycle.
/// </summary>
public partial class App
{
    private NotifyIcon? _trayIcon;
    private CompanionManager? _companion;
    private OverlayWindow? _overlay;
    private MainWindow? _mainWindow;
    private SettingsWindow? _settingsWindow;
    private System.Threading.Mutex? _singleInstanceMutex;
    private AppSettings _settings = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Logger.OnError += msg => Dispatcher.Invoke(() => ShowBalloon(msg, ToolTipIcon.Error));
        Logger.OnInfo += msg => Dispatcher.Invoke(() => ShowBalloon(msg, ToolTipIcon.Info));
        Logger.Log("=== Clicky starting ===");

        _singleInstanceMutex = new System.Threading.Mutex(
            true, "ClickyWindows_SingleInstance", out bool isFirst);
        if (!isFirst)
        {
            System.Windows.MessageBox.Show(
                "Clicky is already running. Check the system tray.",
                "Clicky", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        _settings = AppSettings.Load();
        Logger.Log($"Settings loaded from: {AppSettings.SettingsPath}");
        SetupTrayIcon();

        if (_settings.IsConfigured)
        {
            StartCompanion();
            ShowBalloon($"Clicky ready! Hold {GetHotkeyDescription()} to talk.", ToolTipIcon.Info);
        }
        else
        {
            _settings.Save();
            Dispatcher.BeginInvoke(OpenSettings);
        }

        Logger.Log($"Log file: {Logger.LogFilePath}");
    }

    private void StartCompanion()
    {
        _companion = new CompanionManager(_settings);
        _overlay = new OverlayWindow();
        if (_settings.ShowCursorOverlay)
            _overlay.Show();

        _mainWindow = new MainWindow(_settings, _companion, _overlay);
        MainWindow = _mainWindow;
        _mainWindow.Show();
        Logger.Log($"Clicky ready. Hotkey: {GetHotkeyDescription()}");
    }

    private async void ApplySettings()
    {
        try
        {
            if (_companion != null)
                await _companion.DisposeAsync();

            _mainWindow?.Close();
            _overlay?.Close();
            _mainWindow = null;
            _overlay = null;
            _companion = null;

            StartCompanion();
            SetupTrayIcon();
            ShowBalloon($"Settings saved. Hold {GetHotkeyDescription()} to talk.", ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            Logger.Error($"Could not apply settings: {ex.Message}");
            System.Windows.MessageBox.Show(
                ex.Message, "Clicky settings", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenSettings()
    {
        try
        {
            if (_settingsWindow is { IsVisible: true })
            {
                _settingsWindow.Activate();
                return;
            }

            _settingsWindow = new SettingsWindow(_settings);
            _settingsWindow.SettingsSaved += ApplySettings;
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
            _settingsWindow.Activate();
        }
        catch (Exception ex)
        {
            Logger.Error($"Could not open settings: {ex}");
            System.Windows.MessageBox.Show(
                ex.Message, "Clicky startup error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenPrompt()
    {
        if (_mainWindow is null)
        {
            OpenSettings();
            return;
        }

        _mainWindow.OpenPrompt();
    }

    private void SetupTrayIcon()
    {
        _trayIcon?.Dispose();

        var hotkeyDescription = GetHotkeyDescription();
        _trayIcon = new NotifyIcon
        {
            Icon = AppIcon.CreateTrayIcon(),
            Text = _settings.IsConfigured
                ? $"Clicky - Hold {hotkeyDescription} to talk"
                : "Clicky - Setup required",
            Visible = true,
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_settings.IsConfigured
            ? $"Clicky  |  Hold {hotkeyDescription} to talk"
            : "Clicky  |  Setup required").Enabled = false;
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Settings", null, (_, _) => Dispatcher.Invoke(OpenSettings));
        menu.Items.Add("Ask Clicky", null, (_, _) => Dispatcher.Invoke(OpenPrompt));
        menu.Items.Add("Select Screen Area", null, (_, _) => Dispatcher.Invoke(() => _mainWindow?.OpenAreaSelection()));
        menu.Items.Add("View Log", null, (_, _) => OpenLog());
        menu.Items.Add("Open Data Folder", null, (_, _) => OpenSettingsFolder());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit Clicky", null, (_, _) => QuitApp());

        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(OpenSettings);
    }

    private string GetHotkeyDescription()
    {
        var parts = new List<string>();
        if ((_settings.HotkeyModifiers & 0x0004) != 0) parts.Add("Shift");
        if ((_settings.HotkeyModifiers & 0x0002) != 0) parts.Add("Ctrl");
        if ((_settings.HotkeyModifiers & 0x0001) != 0) parts.Add("Alt");
        if ((_settings.HotkeyModifiers & 0x0008) != 0) parts.Add("Win");
        parts.Add(_settings.HotkeyVirtualKey == 0x20
            ? "Space"
            : $"Key(0x{_settings.HotkeyVirtualKey:X})");
        return string.Join("+", parts);
    }

    private void ShowBalloon(string message, ToolTipIcon icon) =>
        _trayIcon?.ShowBalloonTip(5000, "Clicky", message, icon);

    private void OpenLog()
    {
        try { System.Diagnostics.Process.Start("notepad.exe", Logger.LogFilePath); }
        catch { }
    }

    private void OpenSettingsFolder()
    {
        var directory = Path.GetDirectoryName(AppSettings.SettingsPath)!;
        Directory.CreateDirectory(directory);
        System.Diagnostics.Process.Start("explorer.exe", directory);
    }

    private async void QuitApp()
    {
        _trayIcon?.Dispose();
        if (_companion != null)
            await _companion.DisposeAsync();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}

using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClickyWindows.Settings;

namespace ClickyWindows;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private bool _loading = true;

    public event Action? SettingsSaved;

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        LoadValues();
        _loading = false;
        RefreshValidation();
    }

    private void LoadValues()
    {
        AnthropicKeyBox.Password = _settings.AnthropicApiKey;
        GeminiKeyBox.Password = _settings.GeminiApiKey;
        AssemblyKeyBox.Password = _settings.AssemblyAiApiKey;
        ElevenKeyBox.Password = _settings.ElevenLabsApiKey;
        VoiceIdBox.Text = _settings.ElevenLabsVoiceId;
        ProviderBox.Text = _settings.AiProvider;
        ModelBox.Text = _settings.UsesGemini ? _settings.GeminiModel : _settings.ClaudeModel;
        ClaudeProxyBox.Text = _settings.ClaudeProxyUrl;
        ElevenProxyBox.Text = _settings.ElevenLabsProxyUrl;
        AssemblyTokenBox.Text = _settings.AssemblyAiTokenUrl;
        ShowOverlayBox.IsChecked = _settings.ShowCursorOverlay;

        foreach (ComboBoxItem item in HotkeyBox.Items)
        {
            var values = ((string)item.Tag).Split(',').Select(uint.Parse).ToArray();
            if (values[0] == _settings.HotkeyModifiers && values[1] == _settings.HotkeyVirtualKey)
            {
                HotkeyBox.SelectedItem = item;
                break;
            }
        }
        HotkeyBox.SelectedIndex = HotkeyBox.SelectedIndex < 0 ? 0 : HotkeyBox.SelectedIndex;
        UpdateProviderPresentation();
    }

    private AppSettings ReadForm()
    {
        var hotkey = ((string)((ComboBoxItem)HotkeyBox.SelectedItem).Tag)
            .Split(',').Select(uint.Parse).ToArray();

        var provider = ProviderBox.Text;
        var model = ModelBox.Text.Trim();
        return new AppSettings
        {
            AnthropicApiKey = AnthropicKeyBox.Password.Trim(),
            GeminiApiKey = GeminiKeyBox.Password.Trim(),
            AssemblyAiApiKey = AssemblyKeyBox.Password.Trim(),
            ElevenLabsApiKey = ElevenKeyBox.Password.Trim(),
            ElevenLabsVoiceId = VoiceIdBox.Text.Trim(),
            AiProvider = provider,
            ClaudeModel = string.Equals(provider, "Gemini", StringComparison.OrdinalIgnoreCase)
                ? _settings.ClaudeModel
                : model,
            GeminiModel = string.Equals(provider, "Gemini", StringComparison.OrdinalIgnoreCase)
                ? model
                : _settings.GeminiModel,
            ClaudeProxyUrl = ClaudeProxyBox.Text.Trim(),
            ElevenLabsProxyUrl = ElevenProxyBox.Text.Trim(),
            AssemblyAiTokenUrl = AssemblyTokenBox.Text.Trim(),
            HotkeyModifiers = hotkey[0],
            HotkeyVirtualKey = hotkey[1],
            ShowCursorOverlay = ShowOverlayBox.IsChecked == true,
        };
    }

    private void CopyToLiveSettings(AppSettings value)
    {
        _settings.AnthropicApiKey = value.AnthropicApiKey;
        _settings.GeminiApiKey = value.GeminiApiKey;
        _settings.AssemblyAiApiKey = value.AssemblyAiApiKey;
        _settings.ElevenLabsApiKey = value.ElevenLabsApiKey;
        _settings.ElevenLabsVoiceId = value.ElevenLabsVoiceId;
        _settings.ClaudeModel = value.ClaudeModel;
        _settings.GeminiModel = value.GeminiModel;
        _settings.AiProvider = value.AiProvider;
        _settings.ClaudeProxyUrl = value.ClaudeProxyUrl;
        _settings.ElevenLabsProxyUrl = value.ElevenLabsProxyUrl;
        _settings.AssemblyAiTokenUrl = value.AssemblyAiTokenUrl;
        _settings.HotkeyModifiers = value.HotkeyModifiers;
        _settings.HotkeyVirtualKey = value.HotkeyVirtualKey;
        _settings.ShowCursorOverlay = value.ShowCursorOverlay;
    }

    private void OnInputChanged(object sender, RoutedEventArgs e)
    {
        if (!_loading && IsLoaded)
            RefreshValidation();
    }

    private void OnProviderChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        UpdateProviderPresentation();
        RefreshValidation();
    }

    private void UpdateProviderPresentation()
    {
        var usesGemini = string.Equals(ProviderBox.Text, "Gemini", StringComparison.OrdinalIgnoreCase);
        ModelLabel.Text = usesGemini ? "Gemini model" : "Claude model";
        GeminiKeySection.Visibility = usesGemini ? Visibility.Visible : Visibility.Collapsed;
        AnthropicKeySection.Visibility = usesGemini ? Visibility.Collapsed : Visibility.Visible;
        ProviderHint.Text = usesGemini
            ? "Gemini supports screen images and is suitable for light free-tier use."
            : "Anthropic supports Clicky's full screen and precision-pointing flow.";
        if (string.IsNullOrWhiteSpace(ModelBox.Text) ||
            (usesGemini && ModelBox.Text.StartsWith("claude-")) ||
            (!usesGemini && ModelBox.Text.StartsWith("gemini-")))
        {
            ModelBox.Text = usesGemini ? AppSettings.DefaultGeminiModel : "claude-sonnet-4-6";
        }
    }

    private void RefreshValidation()
    {
        var errors = ReadForm().Validate();
        var ready = errors.Count == 0;
        StatusText.Text = ready ? "Ready" : $"{errors.Count} item{(errors.Count == 1 ? "" : "s")} left";
        StatusText.Foreground = new SolidColorBrush(ready
            ? System.Windows.Media.Color.FromRgb(0x8B, 0xE5, 0xAC)
            : System.Windows.Media.Color.FromRgb(0xFF, 0xC4, 0x6B));
        StatusDot.Fill = new SolidColorBrush(ready
            ? System.Windows.Media.Color.FromRgb(0x55, 0xD9, 0x8A)
            : System.Windows.Media.Color.FromRgb(0xF3, 0xA8, 0x3B));
        ValidationPanel.Visibility = Visibility.Collapsed;
    }

    private void SaveClick(object sender, RoutedEventArgs e)
    {
        var updated = ReadForm();
        var errors = updated.Validate();
        if (errors.Count > 0)
        {
            ValidationText.Text = string.Join(Environment.NewLine, errors.Select(error => $"• {error}"));
            ValidationPanel.Visibility = Visibility.Visible;
            return;
        }

        CopyToLiveSettings(updated);
        _settings.Save();
        SettingsSaved?.Invoke();
        Close();
    }

    private void OpenSettingsFolder(object sender, RoutedEventArgs e)
    {
        var directory = Path.GetDirectoryName(AppSettings.SettingsPath)!;
        Directory.CreateDirectory(directory);
        Process.Start(new ProcessStartInfo("explorer.exe", directory) { UseShellExecute = true });
    }

    private void CancelClick(object sender, RoutedEventArgs e) => Close();
}

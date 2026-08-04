using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClickyWindows.Settings;

public class AppSettings
{
    public const string DefaultGeminiModel = "gemini-3.6-flash";
    // API keys — loaded from settings file, never hardcoded
    public string AnthropicApiKey { get; set; } = "";
    public string GeminiApiKey { get; set; } = "";
    public string ElevenLabsApiKey { get; set; } = "";
    public string ElevenLabsVoiceId { get; set; } = "21m00Tcm4TlvDq8ikWAM"; // default ElevenLabs voice
    public string AssemblyAiApiKey { get; set; } = "";

    // Optional Cloudflare proxy URLs (leave empty to call APIs directly)
    public string ClaudeProxyUrl { get; set; } = "";
    public string ElevenLabsProxyUrl { get; set; } = "";
    public string AssemblyAiTokenUrl { get; set; } = "";

    // Push-to-talk hotkey — default: Ctrl+Shift+Space
    public uint HotkeyModifiers { get; set; } = 0x0002 | 0x0004; // MOD_CONTROL | MOD_SHIFT
    public uint HotkeyVirtualKey { get; set; } = 0x20; // VK_SPACE

    // Claude model
    public string ClaudeModel { get; set; } = "claude-sonnet-4-6";
    public string GeminiModel { get; set; } = DefaultGeminiModel;
    public string AiProvider { get; set; } = "Gemini";

    // Overlay settings
    public bool ShowCursorOverlay { get; set; } = true;

    // ── Persistence ────────────────────────────────────────────────────────

    public static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ClickyWindows",
        "settings.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOpts) ?? new AppSettings();
                if (string.Equals(settings.GeminiModel, "gemini-2.5-flash", StringComparison.OrdinalIgnoreCase))
                {
                    settings.GeminiModel = DefaultGeminiModel;
                    settings.Save();
                }
                return settings;
            }
        }
        catch { /* ignore, use defaults */ }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, JsonOpts));
        }
        catch { /* ignore */ }
    }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (UsesGemini)
        {
            if (string.IsNullOrWhiteSpace(GeminiApiKey))
                errors.Add("Add a Gemini API key.");
            if (string.IsNullOrWhiteSpace(GeminiModel))
                errors.Add("Choose a Gemini model.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(AnthropicApiKey) && !IsHttpUrl(ClaudeProxyUrl))
                errors.Add("Add an Anthropic API key or a valid Claude proxy URL.");
            if (string.IsNullOrWhiteSpace(ClaudeModel))
                errors.Add("Choose a Claude model.");
        }
        if (string.IsNullOrWhiteSpace(AssemblyAiApiKey))
            errors.Add("Add an AssemblyAI API key.");
        if (string.IsNullOrWhiteSpace(ElevenLabsApiKey) && !IsHttpUrl(ElevenLabsProxyUrl))
            errors.Add("Add an ElevenLabs API key or a valid proxy URL.");
        if (string.IsNullOrWhiteSpace(ElevenLabsVoiceId))
            errors.Add("Add an ElevenLabs voice ID.");
        if (HotkeyVirtualKey == 0)
            errors.Add("Choose a push-to-talk hotkey.");

        return errors;
    }

    public bool IsConfigured => Validate().Count == 0;

    public bool UsesGemini => string.Equals(AiProvider, "Gemini", StringComparison.OrdinalIgnoreCase);

    private static bool IsHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    // Convenience: which API to call for Claude
    public string ClaudeApiUrl =>
        !string.IsNullOrWhiteSpace(ClaudeProxyUrl)
            ? ClaudeProxyUrl
            : "https://api.anthropic.com/v1/messages";

    public string ElevenLabsApiUrl(string voiceId) =>
        !string.IsNullOrWhiteSpace(ElevenLabsProxyUrl)
            ? ElevenLabsProxyUrl
            : $"https://api.elevenlabs.io/v1/text-to-speech/{voiceId}";
}

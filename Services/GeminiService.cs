using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using ClickyWindows.Models;
using ClickyWindows.Settings;

namespace ClickyWindows.Services;

/// <summary>
/// Gemini vision client used when a user selects the low-volume Gemini API option.
/// It mirrors Clicky's streaming Claude path but does not provide Computer Use refinement.
/// </summary>
public sealed class GeminiService
{
    private static readonly HttpClient Http = new();
    private readonly AppSettings _settings;
    private readonly ConversationHistory _history;

    public GeminiService(AppSettings settings, ConversationHistory history)
    {
        _settings = settings;
        _history = history;
    }

    public async IAsyncEnumerable<string> StreamResponseAsync(
        string transcript,
        List<ScreenshotResult> screenshots,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var contents = new JsonArray();
        foreach (var turn in _history.Turns)
        {
            contents.Add(new JsonObject
            {
                ["role"] = turn.Role == "assistant" ? "model" : "user",
                ["parts"] = new JsonArray(new JsonObject { ["text"] = turn.Content }),
            });
        }

        var parts = new JsonArray();
        foreach (var screenshot in screenshots)
        {
            parts.Add(new JsonObject
            {
                ["inline_data"] = new JsonObject
                {
                    ["mime_type"] = "image/jpeg",
                    ["data"] = screenshot.Base64,
                },
            });
            parts.Add(new JsonObject { ["text"] = screenshot.Label });
        }
        parts.Add(new JsonObject { ["text"] = transcript });
        contents.Add(new JsonObject { ["role"] = "user", ["parts"] = parts });

        var body = new JsonObject
        {
            ["system_instruction"] = new JsonObject
            {
                ["parts"] = new JsonArray(new JsonObject { ["text"] = ClaudeService.SystemPrompt }),
            },
            ["contents"] = contents,
            ["generationConfig"] = new JsonObject { ["maxOutputTokens"] = 1024 },
        };

        var model = Uri.EscapeDataString(_settings.GeminiModel);
        var apiKey = Uri.EscapeDataString(_settings.GeminiApiKey);
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:streamGenerateContent?alt=sse&key={apiKey}";
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            throw new Exception($"Gemini API error {(int)response.StatusCode}: {error}");
        }

        var fullResponse = new StringBuilder();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);
        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null || !line.StartsWith("data: ")) continue;

            var text = ExtractText(line["data: ".Length..]);
            if (string.IsNullOrEmpty(text)) continue;
            fullResponse.Append(text);
            yield return text;
        }

        _history.AddUserMessage(transcript);
        _history.AddAssistantMessage(ClaudeService.StripPointTags(fullResponse.ToString()));
    }

    private static string? ExtractText(string json)
    {
        try
        {
            var node = JsonNode.Parse(json);
            return node?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.GetValue<string>();
        }
        catch (Exception)
        {
            return null;
        }
    }
}

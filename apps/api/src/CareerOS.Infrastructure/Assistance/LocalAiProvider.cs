using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using CareerOS.Application.Assistance;
using Microsoft.Extensions.Configuration;
namespace CareerOS.Infrastructure.Assistance;
public sealed class LocalAiOptions
{
    public bool Enabled { get; init; }
    public bool ConfigurationValid { get; init; } = true;
    public string Model { get; init; } = "";
    public bool Valid => Regex.IsMatch(Model, @"\A[a-zA-Z0-9][a-zA-Z0-9._-]{0,79}:[a-zA-Z0-9][a-zA-Z0-9._-]{0,39}\z") && !Model.Contains("cloud", StringComparison.OrdinalIgnoreCase);
    public static LocalAiOptions Read(IConfiguration c)
    {
        var text = c["LocalAi:Enabled"]; var valid = text is null || bool.TryParse(text, out _);
        return new() { Enabled = bool.TryParse(text, out var enabled) && enabled, ConfigurationValid = valid, Model = c["LocalAi:Model"] ?? "" };
    }
}
public sealed class LocalAiProvider(LocalAiOptions options, HttpClient client) : ILocalAiProvider, IDisposable
{
    public string Model => options.Model;
    private readonly SemaphoreSlim gate = new(1, 1);
    public static HttpClient CreateClient() => new(new SocketsHttpHandler
    {
        UseProxy = false,
        AllowAutoRedirect = false,
        UseCookies = false,
        Credentials = null,
        ConnectTimeout = TimeSpan.FromSeconds(2),
        MaxConnectionsPerServer = 2
    })
    { BaseAddress = new Uri("http://127.0.0.1:11434/"), Timeout = Timeout.InfiniteTimeSpan };
    private AiAvailability? Configuration() => !options.ConfigurationValid ? new("SetupRequired", "Local AI configuration is invalid. Use true or false for LocalAi:Enabled.") : !options.Enabled ? new("Disabled", "Local AI is off. Matching and Skills Intelligence remain available.") : !options.Valid ? new("SetupRequired", "Configure an installed local model with an explicit tag; cloud models are blocked.") : null;
    private async Task<JsonDocument> Send(HttpMethod method, string path, object? body, int max, CancellationToken ct)
    {
        // Fixed literal loopback destination. No request/config supplied URL, proxy, redirects, cookies, credentials, tools or pull endpoint.
        using var request = new HttpRequestMessage(method, new Uri(new Uri("http://127.0.0.1:11434/"), path));
        if (body != null) request.Content = JsonContent.Create(body);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode) throw new AiUnavailableException("Local AI is unavailable. Check the local server and installed model; no model was downloaded.");
        if (response.Content.Headers.ContentLength > max) throw new AiOutputException();
        await using var stream = await response.Content.ReadAsStreamAsync(ct); using var buffer = new MemoryStream(); var bytes = new byte[4096]; int read;
        while ((read = await stream.ReadAsync(bytes, ct)) > 0) { if (buffer.Length + read > max) throw new AiOutputException(); buffer.Write(bytes, 0, read); }
        return JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions { MaxDepth = 32 });
    }
    public static void ValidateLocal(JsonElement status, JsonElement show)
    {
        if (!status.TryGetProperty("cloud", out var cloud) || !cloud.TryGetProperty("disabled", out var disabled) || disabled.ValueKind != JsonValueKind.True)
            throw new AiUnavailableException("Local-only mode could not be verified. Set OLLAMA_NO_CLOUD=1 and restart Ollama; a server supporting /api/status is required.");
        if (show.TryGetProperty("remote_host", out var host) && !string.IsNullOrEmpty(host.GetString()) || show.TryGetProperty("remote_model", out var remote) && !string.IsNullOrEmpty(remote.GetString()) ||
            !show.TryGetProperty("details", out var details) || !details.TryGetProperty("format", out var format) || format.GetString() != "gguf" || !show.TryGetProperty("model_info", out var info) || info.ValueKind != JsonValueKind.Object || !info.EnumerateObject().Any())
            throw new AiUnavailableException("The selected model could not be verified as local GGUF weights. Remote and unknown model types are blocked.");
    }
    private async Task Preflight(CancellationToken ct)
    {
        using var status = await Send(HttpMethod.Get, "api/status", null, 8192, ct);
        // Reject cloud before asking even for model metadata.
        var root = status.RootElement;
        if (!root.TryGetProperty("cloud", out var c) || !c.TryGetProperty("disabled", out var d) || d.ValueKind != JsonValueKind.True)
            throw new AiUnavailableException("Local-only mode could not be verified. Set OLLAMA_NO_CLOUD=1 and restart Ollama.");
        using var show = await Send(HttpMethod.Post, "api/show", new { model = options.Model }, 262144, ct);
        ValidateLocal(status.RootElement, show.RootElement);
    }
    public async Task<AiAvailability> Status(CancellationToken ct)
    {
        if (Configuration() is { } state) return state;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(3));
        try { await Preflight(timeout.Token); return new("Ready", "Local-only server and model verified. Generation starts only when you request it.", Model); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return new("Unavailable", "Local AI did not respond in time. Other CareerOS features are unaffected."); }
        catch (Exception e) when (e is HttpRequestException or JsonException or AiOutputException or AiUnavailableException or InvalidOperationException) { return new("Unavailable", e is AiUnavailableException ? e.Message : "Local AI could not be verified. Check the local-only setup."); }
    }
    public async Task<string> Generate(AssistanceContext context, CancellationToken ct)
    {
        if (Configuration() is { } state) throw new AiUnavailableException(state.Message);
        if (!await gate.WaitAsync(0, ct)) throw new AiUnavailableException("Local AI is busy. Try again after the current request completes.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(90));
        try
        {
            await Preflight(timeout.Token);
            using var result = await Send(HttpMethod.Post, "api/chat", new
            {
                model = Model,
                stream = false,
                format = AssistanceRules.OutputSchema,
                keep_alive = 0,
                truncate = false,
                shift = false,
                options = new { temperature = 0, num_predict = 1200, num_ctx = 8192 },
                messages = new[] { new { role = "system", content = AssistanceRules.SystemPrompt }, new { role = "user", content = AssistanceRules.Serialize(context) } }
            }, 65536, timeout.Token);
            var root = result.RootElement;
            if (root.TryGetProperty("remote_host", out var host) && !string.IsNullOrEmpty(host.GetString()) || root.TryGetProperty("remote_model", out var remote) && !string.IsNullOrEmpty(remote.GetString()) || !root.TryGetProperty("done", out var done) || done.ValueKind != JsonValueKind.True || !root.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object || !message.TryGetProperty("role", out var role) || role.GetString() != "assistant" || message.TryGetProperty("tool_calls", out var tools) && (tools.ValueKind != JsonValueKind.Array || tools.GetArrayLength() > 0))
                throw new AiOutputException();
            return message.GetProperty("content").GetString() ?? throw new AiOutputException();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new AiUnavailableException("Local AI timed out after 90 seconds. Consider a smaller model or shorter description."); }
        catch (Exception e) when (e is HttpRequestException or JsonException or KeyNotFoundException or InvalidOperationException) { throw new AiUnavailableException("The local model returned an unavailable or invalid response. Your records were not changed."); }
        finally { gate.Release(); }
    }
    public void Dispose() { client.Dispose(); gate.Dispose(); }
}

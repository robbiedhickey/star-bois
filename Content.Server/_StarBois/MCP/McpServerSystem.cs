using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Content.Server._StarBois.MCP.Tools;
using Robust.Shared.Configuration;

namespace Content.Server._StarBois.MCP;

/// <summary>
/// Runs the star-bois MCP server alongside the game server.
/// Exposes game state and actions to agents via a single HTTP+SSE endpoint.
///
/// Transport: Claude Code connects to GET /mcp/sse, sends requests to POST /mcp/msg.
/// Thread safety: HTTP handlers enqueue tool calls; game thread dequeues and executes on Update().
///
/// Enable with: mcp.enabled true  (off by default, dev builds only).
/// </summary>
public sealed partial class McpServerSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;

    // Tool systems resolved lazily to avoid IoC dependency ordering issues.
    private WorldTools WorldTools => EntityManager.System<WorldTools>();
    private AdminTools AdminTools => EntityManager.System<AdminTools>();
    private ClientTools ClientTools => EntityManager.System<ClientTools>();
    private TestTools TestTools => EntityManager.System<TestTools>();

    private HttpListener? _listener;

    // Pending tool calls queued from HTTP thread, processed on game tick.
    private readonly ConcurrentQueue<PendingCall> _pending = new();

    // Active SSE connections to send responses back on.
    private readonly ConcurrentBag<SseClient> _sseClients = new();

    private List<McpToolDefinition> _toolDefs = new();

    public override void Initialize()
    {
        base.Initialize();

        Log.Info($"[MCP] System initializing. mcp.enabled={_cfg.GetCVar(McpCVars.McpEnabled)}");

        if (!_cfg.GetCVar(McpCVars.McpEnabled))
            return;

        _toolDefs = BuildToolDefs();

        var port = _cfg.GetCVar(McpCVars.McpPort);
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/");
        _listener.Start();

        Task.Run(ListenLoop);
        Log.Info($"[MCP] Server listening on http://localhost:{port}/");
    }

    public override void Shutdown()
    {
        _listener?.Stop();
        base.Shutdown();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        while (_pending.TryDequeue(out var call))
        {
            try
            {
                var result = DispatchTool(call.ToolName, call.Arguments);
                call.Completion.SetResult(result);
            }
            catch (Exception ex)
            {
                call.Completion.SetResult(McpToolResult.Err(ex.Message));
            }
        }
    }

    // --- HTTP listener ---

    private async Task ListenLoop()
    {
        while (_listener?.IsListening == true)
        {
            try
            {
                var ctx = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleRequest(ctx));
            }
            catch (HttpListenerException) { break; }
            catch (Exception ex) { Log.Error($"[MCP] Listener error: {ex.Message}"); }
        }
    }

    private async Task HandleRequest(HttpListenerContext ctx)
    {
        ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");

        var path = ctx.Request.Url?.AbsolutePath ?? "";

        if (ctx.Request.HttpMethod == "OPTIONS")
        {
            ctx.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            ctx.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
            ctx.Response.StatusCode = 204;
            ctx.Response.Close();
            return;
        }

        switch (path)
        {
            case "/mcp/sse":
                await HandleSse(ctx);
                break;
            case "/mcp/msg":
                await HandleMessage(ctx);
                break;
            default:
                ctx.Response.StatusCode = 404;
                ctx.Response.Close();
                break;
        }
    }

    private async Task HandleSse(HttpListenerContext ctx)
    {
        ctx.Response.ContentType = "text/event-stream";
        ctx.Response.Headers.Add("Cache-Control", "no-cache");
        ctx.Response.Headers.Add("X-Accel-Buffering", "no");
        ctx.Response.StatusCode = 200;

        // BOM-less UTF-8: a leading BOM before the first SSE event breaks strict MCP clients.
        var writer = new StreamWriter(ctx.Response.OutputStream, new UTF8Encoding(false)) { AutoFlush = true };
        var client = new SseClient(writer);
        _sseClients.Add(client);

        // Tell the MCP client where to POST messages.
        var port = _cfg.GetCVar(McpCVars.McpPort);
        await client.SendEvent("endpoint", $"http://localhost:{port}/mcp/msg");

        // Keep connection alive until client disconnects
        try { await Task.Delay(Timeout.Infinite, client.CancelToken); }
        catch (TaskCanceledException) { }
        finally { client.Dispose(); }
    }

    private async Task HandleMessage(HttpListenerContext ctx)
    {
        if (ctx.Request.HttpMethod != "POST")
        {
            ctx.Response.StatusCode = 405;
            ctx.Response.Close();
            return;
        }

        string body;
        using (var reader = new StreamReader(ctx.Request.InputStream))
            body = await reader.ReadToEndAsync();

        ctx.Response.StatusCode = 202;
        ctx.Response.Close();

        JsonRpcRequest? req;
        try { req = JsonSerializer.Deserialize<JsonRpcRequest>(body); }
        catch { return; }
        if (req == null) return;

        var response = await HandleJsonRpc(req);
        var json = JsonSerializer.Serialize(response, McpJsonOptions.Default);

        foreach (var client in _sseClients)
            _ = client.SendEvent("message", json);
    }

    // --- JSON-RPC dispatch ---

    private async Task<JsonRpcResponse> HandleJsonRpc(JsonRpcRequest req)
    {
        try
        {
            object? result = req.Method switch
            {
                "initialize" => HandleInitialize(),
                "tools/list" => new { tools = _toolDefs },
                "tools/call" => await HandleToolCall(req.Params),
                _ => null
            };

            return new JsonRpcResponse { Id = req.Id, Result = result };
        }
        catch (Exception ex)
        {
            return new JsonRpcResponse
            {
                Id = req.Id,
                Error = new JsonRpcError { Code = -32603, Message = ex.Message }
            };
        }
    }

    private object HandleInitialize() => new
    {
        protocolVersion = "2024-11-05",
        capabilities = new { tools = new { } },
        serverInfo = new { name = "star-bois", version = "0.1.0" }
    };

    private async Task<McpToolResult> HandleToolCall(JsonElement? paramsEl)
    {
        if (paramsEl == null)
            return McpToolResult.Err("Missing params");

        var name = paramsEl.Value.GetProperty("name").GetString() ?? "";
        var args = paramsEl.Value.TryGetProperty("arguments", out var a) ? a : default;

        // Client-side tools do not need the game thread.
        if (name.StartsWith("client_"))
            return await ClientTools.HandleAsync(name, args);

        // All other tools run on the game thread.
        var tcs = new TaskCompletionSource<McpToolResult>();
        _pending.Enqueue(new PendingCall(name, args, tcs));
        return await tcs.Task;
    }

    private McpToolResult DispatchTool(string name, JsonElement args)
    {
        return name switch
        {
            _ when name.StartsWith("world_") => WorldTools.Handle(name, args),
            _ when name.StartsWith("admin_") => AdminTools.Handle(name, args),
            _ when name.StartsWith("test_") => TestTools.Handle(name, args),
            _ => McpToolResult.Err($"Unknown tool: {name}")
        };
    }

    // --- Tool definitions ---

    private List<McpToolDefinition> BuildToolDefs()
    {
        var defs = new List<McpToolDefinition>();
        defs.AddRange(WorldTools.Definitions);
        defs.AddRange(AdminTools.Definitions);
        defs.AddRange(ClientTools.Definitions);
        defs.AddRange(TestTools.Definitions);
        return defs;
    }

    // --- Supporting types ---

    private sealed record PendingCall(
        string ToolName,
        JsonElement Arguments,
        TaskCompletionSource<McpToolResult> Completion);
}

internal sealed class SseClient : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public CancellationToken CancelToken => _cts.Token;

    public SseClient(StreamWriter writer) => _writer = writer;

    public async Task SendEvent(string eventType, string data)
    {
        await _lock.WaitAsync();
        try
        {
            await _writer.WriteAsync($"event: {eventType}\ndata: {data}\n\n");
        }
        catch { _cts.Cancel(); }
        finally { _lock.Release(); }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _writer.Dispose();
        _cts.Dispose();
        _lock.Dispose();
    }
}

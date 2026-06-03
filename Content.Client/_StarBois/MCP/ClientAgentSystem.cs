using System.IO;
using System.Net;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

namespace Content.Client._StarBois.MCP;

/// <summary>
/// Client-side agent HTTP API. Not exposed as MCP; the server MCP proxies to this.
/// Runs on localhost only.
///
/// Enable with: mcp.enabled true  (client config)
/// </summary>
public sealed partial class ClientAgentSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IInputManager _inputManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IGameTiming _timing = default!;

    private HttpListener? _listener;

    // UI actions must run on the main thread.
    private readonly Queue<Action> _mainQueue = new();
    private readonly object _queueLock = new();

    public override void Initialize()
    {
        base.Initialize();

        if (!_cfg.GetCVar(McpClientCVars.McpClientEnabled))
            return;

        var port = _cfg.GetCVar(McpClientCVars.McpClientPort);
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/");
        _listener.Start();

        Task.Run(ListenLoop);
        Log.Info($"[MCP Client] Agent API listening on http://localhost:{port}/");
    }

    public override void Shutdown()
    {
        _listener?.Stop();
        base.Shutdown();
    }

    public override void FrameUpdate(float frameTime)
    {
        lock (_queueLock)
        {
            while (_mainQueue.Count > 0)
                _mainQueue.Dequeue()();
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
        }
    }

    private async Task HandleRequest(HttpListenerContext ctx)
    {
        ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");

        var path = ctx.Request.Url?.AbsolutePath ?? "";
        string body;
        using (var reader = new StreamReader(ctx.Request.InputStream))
            body = await reader.ReadToEndAsync();

        byte[] responseBytes;
        string mime;
        int status;

        try
        {
            (status, responseBytes, mime) = path switch
            {
                "/agent/screenshot" => await TakeScreenshot(),
                "/agent/click_control" => await RunOnMain(() => ClickControl(body)),
                "/agent/set_control_value" => await RunOnMain(() => SetControlValue(body)),
                "/agent/get_control_tree" => (200, Encoding.UTF8.GetBytes(GetControlTree()), "application/json"),
                "/agent/get_player_info" => await RunOnMain(() => GetPlayerInfo()),
                "/agent/move" => await Move(body),
                "/agent/interact_entity" => await RunOnMain(() => InteractEntity(body)),
                _ => (404, Encoding.UTF8.GetBytes("Not found"), "text/plain")
            };
        }
        catch (Exception ex)
        {
            status = 500;
            responseBytes = Encoding.UTF8.GetBytes(ex.Message);
            mime = "text/plain";
        }

        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = mime;
        ctx.Response.ContentLength64 = responseBytes.Length;
        await ctx.Response.OutputStream.WriteAsync(responseBytes);
        ctx.Response.Close();
    }

    // --- Tool implementations ---

    private async Task<(int, byte[], string)> TakeScreenshot()
    {
        var tcs = new TaskCompletionSource<Image<SixLabors.ImageSharp.PixelFormats.Rgb24>>();

        lock (_queueLock)
            _mainQueue.Enqueue(() =>
            {
                _clyde.Screenshot(ScreenshotType.Final, img => tcs.SetResult(img));
            });

        var image = await tcs.Task;
        using var ms = new MemoryStream();
        image.Save(ms, new PngEncoder());
        return (200, ms.ToArray(), "image/png");
    }

    private (int, byte[], string) ClickControl(string body)
    {
        var args = JsonSerializer.Deserialize<JsonElement>(body);
        var agentId = args.GetProperty("agent_id").GetString() ?? "";

        var (control, onClick) = ControlRegistry.Resolve(agentId);
        if (control == null)
            return Err($"Control '{agentId}' not found. Use get_control_tree to see available controls.");

        if (!control.Visible)
            return Err($"Control '{agentId}' exists but is not visible.");

        if (onClick != null)
        {
            onClick();
            return Ok($"Clicked '{agentId}'");
        }

        // Fallback: grab keyboard focus (works for most interactive controls)
        control.GrabKeyboardFocus();
        return Ok($"Focused '{agentId}' (no click action registered)");
    }

    private (int, byte[], string) SetControlValue(string body)
    {
        var args = JsonSerializer.Deserialize<JsonElement>(body);
        var agentId = args.GetProperty("agent_id").GetString() ?? "";
        var value = args.GetProperty("value").GetString() ?? "";

        var (control, _) = ControlRegistry.Resolve(agentId);
        if (control == null)
            return Err($"Control '{agentId}' not found.");

        switch (control)
        {
            case LineEdit le:
                le.Text = value;
                return Ok($"Set '{agentId}' text = '{value}'");
            case SpinBox sb when int.TryParse(value, out var i):
                sb.OverrideValue(i);
                return Ok($"Set '{agentId}' value = {i}");
            default:
                return Err($"Control '{agentId}' ({control.GetType().Name}) does not support value setting.");
        }
    }

    private string GetControlTree()
    {
        var all = ControlRegistry.GetAll();
        return JsonSerializer.Serialize(all);
    }

    private (int, byte[], string) GetPlayerInfo()
    {
        if (_playerManager.LocalEntity is not { } player)
            return Err("No local player entity is attached.");

        var xform = Transform(player);
        var map = EntityManager.System<SharedTransformSystem>().GetMapCoordinates(player, xform);
        var meta = MetaData(player);

        var result = new
        {
            entity_id = player.Id,
            net_entity_id = GetNetEntity(player).Id,
            name = meta.EntityName,
            prototype = meta.EntityPrototype?.ID ?? "none",
            map_id = (int) map.MapId,
            x = MathF.Round(map.Position.X, 2),
            y = MathF.Round(map.Position.Y, 2),
            input_context = _inputManager.Contexts.ActiveContext.Name
        };

        return (200, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(result)), "application/json");
    }

    private async Task<(int, byte[], string)> Move(string body)
    {
        var args = JsonSerializer.Deserialize<JsonElement>(body);
        var direction = args.TryGetProperty("direction", out var d) ? d.GetString() ?? "" : "";
        var durationMs = args.TryGetProperty("duration_ms", out var ms) ? ms.GetInt32() : 500;
        durationMs = Math.Clamp(durationMs, 50, 5000);

        if (!TryGetMoveFunctions(direction, out var functions))
            return Err($"Unknown direction '{direction}'. Use north, south, east, west, or diagonals.");

        await RunOnMain(() =>
        {
            foreach (var function in functions)
                SendInput(function, BoundKeyState.Down);

            return Ok($"Started moving {direction}");
        });

        await Task.Delay(durationMs);

        await RunOnMain(() =>
        {
            foreach (var function in functions)
                SendInput(function, BoundKeyState.Up);

            return Ok($"Stopped moving {direction}");
        });

        return Ok($"Moved {direction} for {durationMs}ms");
    }

    private (int, byte[], string) InteractEntity(string body)
    {
        var args = JsonSerializer.Deserialize<JsonElement>(body);

        EntityUid uid;
        if (args.TryGetProperty("net_entity_id", out var netId))
        {
            var netEntity = new NetEntity(netId.GetInt32());
            if (!TryGetEntity(netEntity, out EntityUid? resolved) || resolved == null)
                return Err($"Net entity {netEntity.Id} is not available on this client.");

            uid = resolved.Value;
        }
        else
        {
            var id = args.GetProperty("entity_id").GetInt32();
            uid = new EntityUid(id);
        }

        if (!Exists(uid))
            return Err($"Entity {uid.Id} does not exist on this client.");

        SendInput(EngineKeyFunctions.Use, BoundKeyState.Down, uid);
        SendInput(EngineKeyFunctions.Use, BoundKeyState.Up, uid);

        return Ok($"Interacted with entity {uid.Id}");
    }

    private void SendInput(BoundKeyFunction function, BoundKeyState state, EntityUid? target = null)
    {
        if (_playerManager.LocalEntity is not { } player)
            throw new InvalidOperationException("No local player entity is attached.");

        var transform = EntityManager.System<SharedTransformSystem>();
        var targetUid = target ?? EntityUid.Invalid;
        var targetCoords = targetUid.Valid && Exists(targetUid)
            ? transform.GetMapCoordinates(targetUid)
            : transform.GetMapCoordinates(player);

        var coords = transform.ToCoordinates(player, targetCoords);
        var funcId = _inputManager.NetworkBindMap.KeyFunctionID(function);
        var input = new ClientFullInputCmdMessage(
            _timing.CurTick,
            _timing.TickFraction,
            funcId,
            coords,
            new ScreenCoordinates(Vector2.Zero, default),
            state,
            targetUid);

        EntityManager.System<InputSystem>().HandleInputCommand(_playerManager.LocalSession, function, input);
    }

    private static bool TryGetMoveFunctions(string direction, out BoundKeyFunction[] functions)
    {
        functions = direction.Trim().ToLowerInvariant() switch
        {
            "north" or "up" => [EngineKeyFunctions.MoveUp],
            "south" or "down" => [EngineKeyFunctions.MoveDown],
            "west" or "left" => [EngineKeyFunctions.MoveLeft],
            "east" or "right" => [EngineKeyFunctions.MoveRight],
            "northeast" or "north-east" or "up-right" => [EngineKeyFunctions.MoveUp, EngineKeyFunctions.MoveRight],
            "northwest" or "north-west" or "up-left" => [EngineKeyFunctions.MoveUp, EngineKeyFunctions.MoveLeft],
            "southeast" or "south-east" or "down-right" => [EngineKeyFunctions.MoveDown, EngineKeyFunctions.MoveRight],
            "southwest" or "south-west" or "down-left" => [EngineKeyFunctions.MoveDown, EngineKeyFunctions.MoveLeft],
            _ => []
        };

        return functions.Length > 0;
    }

    // Run an action on the main thread and await its result.
    private Task<(int, byte[], string)> RunOnMain(Func<(int, byte[], string)> action)
    {
        var tcs = new TaskCompletionSource<(int, byte[], string)>();
        lock (_queueLock)
            _mainQueue.Enqueue(() =>
            {
                try { tcs.SetResult(action()); }
                catch (Exception ex) { tcs.SetException(ex); }
            });
        return tcs.Task;
    }

    private static (int, byte[], string) Ok(string msg) =>
        (200, Encoding.UTF8.GetBytes(msg), "text/plain");

    private static (int, byte[], string) Err(string msg) =>
        (400, Encoding.UTF8.GetBytes(msg), "text/plain");
}

[CVarDefs]
public sealed class McpClientCVars
{
    public static readonly CVarDef<bool> McpClientEnabled =
        CVarDef.Create("mcp.enabled", false, CVar.CLIENTONLY);

    public static readonly CVarDef<int> McpClientPort =
        CVarDef.Create("mcp.client_port", 9223, CVar.CLIENTONLY);
}

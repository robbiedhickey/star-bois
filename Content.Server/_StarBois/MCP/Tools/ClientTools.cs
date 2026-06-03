using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Robust.Shared.Configuration;

namespace Content.Server._StarBois.MCP.Tools;

/// <summary>
/// Tools that proxy to the client's internal agent HTTP API.
/// These run on the HTTP thread; no game thread needed since they are pure network calls.
/// </summary>
public sealed partial class ClientTools : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public IReadOnlyList<McpToolDefinition> Definitions { get; } =
    [
        new()
        {
            Name = "client_screenshot",
            Description = "Capture the current game client frame. Returns a PNG image.",
            InputSchema = new { type = "object", properties = new { } }
        },
        new()
        {
            Name = "client_click_control",
            Description = "Click a named UI control by its AgentId. Use this to interact with menus, buttons, and consoles.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    agent_id = new { type = "string", description = "The AgentId of the control to click, e.g. 'starmap-warp-button'" }
                },
                required = new[] { "agent_id" }
            }
        },
        new()
        {
            Name = "client_set_control_value",
            Description = "Set the value of a named input control (text box, slider, dropdown).",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    agent_id = new { type = "string", description = "The AgentId of the control" },
                    value = new { type = "string", description = "Value to set" }
                },
                required = new[] { "agent_id", "value" }
            }
        },
        new()
        {
            Name = "client_get_control_tree",
            Description = "Get the current visible UI control tree with all AgentIds. Useful for discovering what controls are available.",
            InputSchema = new { type = "object", properties = new { } }
        },
        new()
        {
            Name = "client_get_player_info",
            Description = "Get the local player's attached entity, map position, and input context.",
            InputSchema = new { type = "object", properties = new { } }
        },
        new()
        {
            Name = "client_move",
            Description = "Move the local player or ghost in a cardinal/diagonal direction for a short duration.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    direction = new { type = "string", description = "Direction: north, south, east, west, northeast, northwest, southeast, southwest" },
                    duration_ms = new { type = "integer", description = "How long to hold movement input, default 500", @default = 500 }
                },
                required = new[] { "direction" }
            }
        },
        new()
        {
            Name = "client_interact_entity",
            Description = "Interact with an entity (equivalent to clicking on it in-world).",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    net_entity_id = new { type = "integer", description = "Network entity ID from world tools. Preferred for server-to-client MCP flows." },
                    entity_id = new { type = "integer", description = "Client-local Entity UID. Only use IDs from client_get_player_info or other client tools." }
                }
            }
        }
    ];

    public async Task<McpToolResult> HandleAsync(string name, JsonElement args)
    {
        var clientUrl = _cfg.GetCVar(McpCVars.McpClientUrl);

        try
        {
            var endpoint = name.Replace("client_", "");
            var payload = args.ValueKind == JsonValueKind.Undefined ? "{}" : args.GetRawText();
            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            var response = await Http.PostAsync($"{clientUrl}/agent/{endpoint}", content);

            if (!response.IsSuccessStatusCode)
                return McpToolResult.Err($"Client returned {response.StatusCode}");

            var body = await response.Content.ReadAsByteArrayAsync();

            // Screenshot returns raw PNG.
            if (name == "client_screenshot")
                return McpToolResult.Screenshot(body);

            var text = Encoding.UTF8.GetString(body);
            return McpToolResult.Ok(text);
        }
        catch (HttpRequestException ex)
        {
            return McpToolResult.Err($"Client unreachable: {ex.Message}. Is the game client running with mcp.enabled true?");
        }
        catch (TaskCanceledException)
        {
            return McpToolResult.Err("Client request timed out");
        }
    }
}

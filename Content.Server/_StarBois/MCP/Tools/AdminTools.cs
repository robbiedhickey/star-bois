using System.Numerics;
using System.Text.Json;
using Content.Server.Administration.Managers;
using Robust.Server.Console;
using Robust.Shared.Map;

namespace Content.Server._StarBois.MCP.Tools;

/// <summary>
/// Tools for admin-level game control: console commands, entity spawning.
/// All methods run on the game thread.
/// </summary>
public sealed partial class AdminTools : EntitySystem
{
    [Dependency] private IServerConsoleHost _console = default!;
    [Dependency] private IMapManager _mapManager = default!;

    public IReadOnlyList<McpToolDefinition> Definitions { get; } =
    [
        new()
        {
            Name = "admin_execute_command",
            Description = "Execute a server console command. Equivalent to typing in the server console.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    command = new { type = "string", description = "Command string to execute, e.g. 'spawn MobHuman 0,0,1'" }
                },
                required = new[] { "command" }
            }
        },
        new()
        {
            Name = "admin_spawn_entity",
            Description = "Spawn an entity prototype at a map position.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    prototype = new { type = "string", description = "Entity prototype ID" },
                    map_id = new { type = "integer", description = "Map ID to spawn on" },
                    x = new { type = "number", description = "X coordinate" },
                    y = new { type = "number", description = "Y coordinate" }
                },
                required = new[] { "prototype", "map_id", "x", "y" }
            }
        },
        new()
        {
            Name = "admin_delete_entity",
            Description = "Delete an entity by ID.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    entity_id = new { type = "integer", description = "Entity UID to delete" }
                },
                required = new[] { "entity_id" }
            }
        }
    ];

    public McpToolResult Handle(string name, JsonElement args) => name switch
    {
        "admin_execute_command" => ExecuteCommand(args),
        "admin_spawn_entity" => SpawnEntity(args),
        "admin_delete_entity" => DeleteEntity(args),
        _ => McpToolResult.Err($"Unknown admin tool: {name}")
    };

    private McpToolResult ExecuteCommand(JsonElement args)
    {
        var command = args.GetProperty("command").GetString() ?? "";
        if (string.IsNullOrWhiteSpace(command))
            return McpToolResult.Err("Command cannot be empty");

        _console.ExecuteCommand(command);
        return McpToolResult.Ok($"Executed: {command}");
    }

    private McpToolResult SpawnEntity(JsonElement args)
    {
        var prototype = args.GetProperty("prototype").GetString() ?? "";
        var mapId = new MapId(args.GetProperty("map_id").GetInt32());
        var x = args.GetProperty("x").GetSingle();
        var y = args.GetProperty("y").GetSingle();

        if (!_mapManager.MapExists(mapId))
            return McpToolResult.Err($"Map {(int)mapId} does not exist");

        var coords = new MapCoordinates(new Vector2(x, y), mapId);
        var uid = EntityManager.SpawnEntity(prototype, coords);

        return McpToolResult.Ok(new
        {
            entity_id = uid.Id,
            prototype,
            x,
            y,
            map_id = (int)mapId
        });
    }

    private McpToolResult DeleteEntity(JsonElement args)
    {
        var id = args.GetProperty("entity_id").GetInt32();
        var uid = new EntityUid((int)id);

        if (!Exists(uid))
            return McpToolResult.Err($"Entity {id} does not exist");

        Del(uid);
        return McpToolResult.Ok($"Deleted entity {id}");
    }
}

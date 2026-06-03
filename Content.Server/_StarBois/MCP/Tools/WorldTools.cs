using System.Numerics;
using System.Text.Json;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._StarBois.MCP.Tools;

/// <summary>
/// Tools for querying world state: entities, maps, ship systems.
/// All methods run on the game thread (called from McpServerSystem.Update).
/// </summary>
public sealed partial class WorldTools : EntitySystem
{
    [Dependency] private IMapManager _mapManager = default!;
    [Dependency] private MetaDataSystem _metaData = default!;

    public IReadOnlyList<McpToolDefinition> Definitions { get; } =
    [
        new()
        {
            Name = "world_get_entities_near",
            Description = "Get entities within a radius of a map position. Returns id, prototype, name, and position.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    map_id = new { type = "integer", description = "Map ID to search on" },
                    x = new { type = "number", description = "X coordinate" },
                    y = new { type = "number", description = "Y coordinate" },
                    radius = new { type = "number", description = "Search radius in tiles", @default = 10.0 }
                },
                required = new[] { "map_id", "x", "y" }
            }
        },
        new()
        {
            Name = "world_get_map_info",
            Description = "List all loaded maps and grids with entity counts.",
            InputSchema = new { type = "object", properties = new { } }
        },
        new()
        {
            Name = "world_get_entity_info",
            Description = "Get detailed info about a specific entity by ID.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    entity_id = new { type = "integer", description = "Entity UID" }
                },
                required = new[] { "entity_id" }
            }
        }
    ];

    public McpToolResult Handle(string name, JsonElement args) => name switch
    {
        "world_get_entities_near" => GetEntitiesNear(args),
        "world_get_map_info" => GetMapInfo(),
        "world_get_entity_info" => GetEntityInfo(args),
        _ => McpToolResult.Err($"Unknown world tool: {name}")
    };

    private McpToolResult GetEntitiesNear(JsonElement args)
    {
        var mapId = new MapId(args.GetProperty("map_id").GetInt32());
        var x = args.GetProperty("x").GetSingle();
        var y = args.GetProperty("y").GetSingle();
        var radius = args.TryGetProperty("radius", out var r) ? r.GetSingle() : 10f;

        var origin = new MapCoordinates(new Vector2(x, y), mapId);
        var results = new List<object>();

        var query = EntityQueryEnumerator<TransformComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var xform, out var meta))
        {
            if (xform.MapID != mapId) continue;
            var pos = xform.WorldPosition;
            var dist = Vector2.Distance(pos, origin.Position);
            if (dist > radius) continue;

            results.Add(new
            {
                id = uid.Id,
                net_entity_id = GetNetEntity(uid).Id,
                name = meta.EntityName,
                prototype = meta.EntityPrototype?.ID ?? "none",
                x = MathF.Round(pos.X, 2),
                y = MathF.Round(pos.Y, 2),
                distance = MathF.Round(dist, 2)
            });
        }

        results.Sort((a, b) =>
        {
            var da = (float)a.GetType().GetProperty("distance")!.GetValue(a)!;
            var db = (float)b.GetType().GetProperty("distance")!.GetValue(b)!;
            return da.CompareTo(db);
        });

        return McpToolResult.Ok(new { count = results.Count, entities = results });
    }

    private McpToolResult GetMapInfo()
    {
        var maps = new List<object>();

        foreach (var mapId in _mapManager.GetAllMapIds())
        {
            var mapUid = _mapManager.GetMapEntityId(mapId);
            var mapName = MetaData(mapUid).EntityName;

            var grids = new List<object>();
            var gridQuery = EntityQueryEnumerator<MapGridComponent, TransformComponent, MetaDataComponent>();
            while (gridQuery.MoveNext(out var gridUid, out _, out var xform, out var meta))
            {
                if (xform.MapID != mapId) continue;
                grids.Add(new
                {
                    id = gridUid.Id,
                    net_entity_id = GetNetEntity(gridUid).Id,
                    name = meta.EntityName,
                    prototype = meta.EntityPrototype?.ID ?? "none"
                });
            }

            maps.Add(new { map_id = (int)mapId, name = mapName, grids });
        }

        return McpToolResult.Ok(new { maps });
    }

    private McpToolResult GetEntityInfo(JsonElement args)
    {
        var id = args.GetProperty("entity_id").GetInt32();
        var uid = new EntityUid(id);

        if (!Exists(uid))
            return McpToolResult.Err($"Entity {id} does not exist");

        var meta = MetaData(uid);
        var xform = Transform(uid);

        var components = new List<string>();
        foreach (var comp in AllComps(uid))
            components.Add(comp.GetType().Name.Replace("Component", ""));

        return McpToolResult.Ok(new
        {
            id,
            net_entity_id = GetNetEntity(uid).Id,
            name = meta.EntityName,
            prototype = meta.EntityPrototype?.ID ?? "none",
            map_id = (int)xform.MapID,
            x = MathF.Round(xform.WorldPosition.X, 2),
            y = MathF.Round(xform.WorldPosition.Y, 2),
            components
        });
    }
}

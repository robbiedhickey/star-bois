using System.Linq;
using System.Numerics;
using System.Text.Json;
using Content.Server.Body.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Nutrition.Components;
using Content.Shared.StatusEffect;
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
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private DamageableSystem _damageable = default!;

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

        // Mob vitals — only present on living entities
        object? mobState = null;
        if (HasComp<MobStateComponent>(uid))
            mobState = _mobState.IsAlive(uid) ? "Alive" : _mobState.IsCritical(uid) ? "Critical" : "Dead";

        object? damage = null;
        if (TryComp<DamageableComponent>(uid, out _))
            damage = new { total = _damageable.GetTotalDamage(uid).Float() };

        object? respiration = null;
        if (TryComp<RespiratorComponent>(uid, out var resp))
        {
            // Read component fields into locals before calling any methods to avoid RA0002
            var status = resp.Status;
            var saturation = resp.Saturation;
            var maxSaturation = resp.MaxSaturation;
            var suffocThreshold = resp.SuffocationThreshold;
            var suffocCycles = resp.SuffocationCycles;
            var suffocCycleThreshold = resp.SuffocationCycleThreshold;
            respiration = new
            {
                status = Enum.GetName(typeof(RespiratorStatus), status) ?? status.ToString(),
                saturation = MathF.Round(saturation, 2),
                max_saturation = maxSaturation,
                suffocation_threshold = suffocThreshold,
                suffocation_cycles = suffocCycles,
                suffocation_cycle_threshold = suffocCycleThreshold
            };
        }

        object? hunger = null;
        if (TryComp<HungerComponent>(uid, out var h))
        {
            var threshold = h.CurrentThreshold;
            hunger = new { threshold = Enum.GetName(typeof(HungerThreshold), threshold) ?? threshold.ToString() };
        }

        object? thirst = null;
        if (TryComp<ThirstComponent>(uid, out var t))
        {
            var threshold = t.CurrentThirstThreshold;
            thirst = new { threshold = Enum.GetName(typeof(ThirstThreshold), threshold) ?? threshold.ToString() };
        }

        object? statusEffects = null;
        if (TryComp<StatusEffectsComponent>(uid, out var sfx))
            statusEffects = sfx.ActiveEffects.Keys.ToArray();

        return McpToolResult.Ok(new
        {
            id,
            net_entity_id = GetNetEntity(uid).Id,
            name = meta.EntityName,
            prototype = meta.EntityPrototype?.ID ?? "none",
            map_id = (int)xform.MapID,
            x = MathF.Round(xform.WorldPosition.X, 2),
            y = MathF.Round(xform.WorldPosition.Y, 2),
            mob_state = mobState,
            damage,
            respiration,
            hunger,
            thirst,
            status_effects = statusEffects,
            components
        });
    }
}

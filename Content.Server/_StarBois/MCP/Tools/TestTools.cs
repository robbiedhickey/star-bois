using System.Numerics;
using System.Text.Json;
using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server._StarBois.MCP.Tools;

/// <summary>
/// Deterministic scenario/test helpers. These are intentionally higher-level than
/// admin tools so MCP clients can arrange, act, and assert behavior changes.
/// </summary>
public sealed partial class TestTools : EntitySystem
{
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private StationSystem _station = default!;

    public IReadOnlyList<McpToolDefinition> Definitions { get; } =
    [
        new()
        {
            Name = "test_list_players",
            Description = "List connected player sessions and their attached entities.",
            InputSchema = new { type = "object", properties = new { } }
        },
        new()
        {
            Name = "test_get_player",
            Description = "Get one attached player entity. If name is omitted, returns the first attached in-game player.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    name = new { type = "string", description = "Optional session name to match exactly" }
                }
            }
        },
        new()
        {
            Name = "test_teleport_entity",
            Description = "Teleport an entity to map coordinates. Accepts server entity_id or net_entity_id.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    entity_id = new { type = "integer", description = "Server-local entity UID" },
                    net_entity_id = new { type = "integer", description = "Network entity ID" },
                    map_id = new { type = "integer", description = "Destination map ID" },
                    x = new { type = "number", description = "Destination X coordinate" },
                    y = new { type = "number", description = "Destination Y coordinate" }
                },
                required = new[] { "map_id", "x", "y" }
            }
        },
        new()
        {
            Name = "test_find_entities",
            Description = "Find entities by optional prototype, name substring, component, and/or radius.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    prototype = new { type = "string", description = "Optional exact prototype ID" },
                    name_contains = new { type = "string", description = "Optional case-insensitive entity name substring" },
                    component = new { type = "string", description = "Optional component name, e.g. Door, Apc, Transform" },
                    map_id = new { type = "integer", description = "Optional map ID for radius search" },
                    x = new { type = "number", description = "Optional X origin for radius search" },
                    y = new { type = "number", description = "Optional Y origin for radius search" },
                    radius = new { type = "number", description = "Optional search radius in tiles" },
                    limit = new { type = "integer", description = "Maximum results, default 50", @default = 50 }
                }
            }
        },
        new()
        {
            Name = "test_start_round",
            Description = "Force-start the round immediately, bypassing the lobby countdown. Equivalent to the 'startround' console command.",
            InputSchema = new { type = "object", properties = new { } }
        },
        new()
        {
            Name = "test_join_game",
            Description = "Spawn a player into the game as a crew member. If job is omitted, uses Passenger as fallback. Call after test_start_round when the player is still in the lobby.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    name = new { type = "string", description = "Session name to join (e.g. localhost@JoeGenero). If omitted, joins the first lobby player." },
                    job = new { type = "string", description = "Job prototype ID to spawn as, e.g. Pilot, StationEngineer, MedicalDoctor, Captain. Defaults to Pilot." }
                }
            }
        },
        new()
        {
            Name = "test_assert_entity",
            Description = "Assert that an entity exists and optionally has prototype, component, and/or is near a coordinate.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    entity_id = new { type = "integer", description = "Server-local entity UID" },
                    net_entity_id = new { type = "integer", description = "Network entity ID" },
                    prototype = new { type = "string", description = "Expected prototype ID" },
                    component = new { type = "string", description = "Expected component name" },
                    map_id = new { type = "integer", description = "Expected/current map ID for position check" },
                    x = new { type = "number", description = "Expected X coordinate for position check" },
                    y = new { type = "number", description = "Expected Y coordinate for position check" },
                    max_distance = new { type = "number", description = "Allowed position distance, default 0.5", @default = 0.5 }
                }
            }
        }
    ];

    public McpToolResult Handle(string name, JsonElement args) => name switch
    {
        "test_list_players" => ListPlayers(),
        "test_get_player" => GetPlayer(args),
        "test_teleport_entity" => TeleportEntity(args),
        "test_find_entities" => FindEntities(args),
        "test_assert_entity" => AssertEntity(args),
        "test_start_round" => StartRound(),
        "test_join_game" => JoinGame(args),
        _ => McpToolResult.Err($"Unknown test tool: {name}")
    };

    private McpToolResult StartRound()
    {
        if (_ticker.RunLevel == GameRunLevel.InRound)
            return McpToolResult.Ok(new { ok = true, message = "Round already running" });

        _ticker.StartRound(force: true);
        return McpToolResult.Ok(new { ok = true, message = "Round started" });
    }

    private McpToolResult JoinGame(JsonElement args)
    {
        var sessionName = args.TryGetProperty("name", out var n) ? n.GetString() : null;
        var jobId = args.TryGetProperty("job", out var j) ? j.GetString() : "Pilot";

        var session = _players.Sessions.FirstOrDefault(s =>
            sessionName == null ? s.Status == SessionStatus.Connected || s.Status == SessionStatus.InGame
                                : s.Name == sessionName);

        if (session == null)
            return McpToolResult.Err(sessionName == null ? "No connected player found" : $"No player named '{sessionName}' found");

        var stations = _station.GetStations();
        var station = stations.FirstOrDefault();
        if (station == EntityUid.Invalid)
            return McpToolResult.Err("No station found — is the round running?");

        try
        {
            _ticker.MakeJoinGame(session, station, jobId);
        }
        catch (Exception ex)
        {
            return McpToolResult.Err($"MakeJoinGame failed: {ex.Message} — player may have no saved character profile");
        }

        return McpToolResult.Ok(new { ok = true, session = session.Name, job = jobId });
    }

    private McpToolResult ListPlayers()
    {
        var players = _players.Sessions
            .Select(session => new
            {
                name = session.Name,
                status = session.Status.ToString(),
                attached = session.AttachedEntity != null,
                entity_id = session.AttachedEntity?.Id,
                net_entity_id = session.AttachedEntity is { } uid && Exists(uid)
                    ? GetNetEntity(uid).Id
                    : (int?) null
            })
            .ToList();

        return McpToolResult.Ok(new { count = players.Count, players });
    }

    private McpToolResult GetPlayer(JsonElement args)
    {
        var name = args.TryGetProperty("name", out var n) ? n.GetString() : null;
        var session = _players.Sessions.FirstOrDefault(s =>
            s.Status == SessionStatus.InGame &&
            s.AttachedEntity is { } attached &&
            Exists(attached) &&
            (name == null || s.Name == name));

        if (session == null || session.AttachedEntity is not { } uid)
            return McpToolResult.Err(name == null ? "No attached in-game player found" : $"No attached in-game player named '{name}' found");

        return McpToolResult.Ok(ToEntityInfo(uid, session));
    }

    private McpToolResult TeleportEntity(JsonElement args)
    {
        if (!TryResolveEntity(args, out var uid, out var err))
            return McpToolResult.Err(err);

        var mapId = new MapId(args.GetProperty("map_id").GetInt32());
        if (!_map.MapExists(mapId))
            return McpToolResult.Err($"Map {(int) mapId} does not exist");

        var x = args.GetProperty("x").GetSingle();
        var y = args.GetProperty("y").GetSingle();
        _transform.SetMapCoordinates(uid, new MapCoordinates(new Vector2(x, y), mapId));

        return McpToolResult.Ok(ToEntityInfo(uid));
    }

    private McpToolResult FindEntities(JsonElement args)
    {
        var prototype = args.TryGetProperty("prototype", out var p) ? p.GetString() : null;
        var nameContains = args.TryGetProperty("name_contains", out var n) ? n.GetString() : null;
        var component = args.TryGetProperty("component", out var c) ? c.GetString() : null;
        var limit = args.TryGetProperty("limit", out var l) ? Math.Clamp(l.GetInt32(), 1, 500) : 50;
        JsonElement m = default;
        JsonElement xEl = default;
        JsonElement yEl = default;
        JsonElement rEl = default;
        var hasRadius = args.TryGetProperty("map_id", out m)
                        && args.TryGetProperty("x", out xEl)
                        && args.TryGetProperty("y", out yEl)
                        && args.TryGetProperty("radius", out rEl);

        Type? componentType = null;
        if (!string.IsNullOrWhiteSpace(component))
        {
            if (!Factory.TryGetRegistration(component, out var registration, true))
                return McpToolResult.Err($"Unknown component '{component}'");

            componentType = registration.Type;
        }

        var origin = hasRadius
            ? new MapCoordinates(new Vector2(xEl.GetSingle(), yEl.GetSingle()), new MapId(m.GetInt32()))
            : default;
        var radius = hasRadius ? rEl.GetSingle() : 0f;

        var results = new List<object>();
        var query = EntityQueryEnumerator<TransformComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var xform, out var meta))
        {
            if (prototype != null && meta.EntityPrototype?.ID != prototype)
                continue;

            if (!string.IsNullOrWhiteSpace(nameContains) &&
                !meta.EntityName.Contains(nameContains, StringComparison.OrdinalIgnoreCase))
                continue;

            if (componentType != null && !HasComp(uid, componentType))
                continue;

            var mapCoords = _transform.GetMapCoordinates(uid, xform);
            if (hasRadius)
            {
                if (mapCoords.MapId != origin.MapId)
                    continue;

                if (Vector2.Distance(mapCoords.Position, origin.Position) > radius)
                    continue;
            }

            results.Add(ToEntityInfo(uid, null, mapCoords));
            if (results.Count >= limit)
                break;
        }

        return McpToolResult.Ok(new { count = results.Count, entities = results });
    }

    private McpToolResult AssertEntity(JsonElement args)
    {
        if (!TryResolveEntity(args, out var uid, out var err))
            return McpToolResult.Err(err);

        var failures = new List<string>();
        var meta = MetaData(uid);
        if (args.TryGetProperty("prototype", out var p))
        {
            var expected = p.GetString();
            var actual = meta.EntityPrototype?.ID ?? "none";
            if (actual != expected)
                failures.Add($"prototype expected '{expected}', got '{actual}'");
        }

        if (args.TryGetProperty("component", out var c))
        {
            var component = c.GetString() ?? "";
            if (!Factory.TryGetRegistration(component, out var registration, true))
                failures.Add($"unknown component '{component}'");
            else if (!HasComp(uid, registration.Type))
                failures.Add($"missing component '{registration.Name}'");
        }

        JsonElement m = default;
        JsonElement xEl = default;
        JsonElement yEl = default;
        var hasPosition = args.TryGetProperty("map_id", out m)
                          && args.TryGetProperty("x", out xEl)
                          && args.TryGetProperty("y", out yEl);
        if (hasPosition)
        {
            var expected = new MapCoordinates(new Vector2(xEl.GetSingle(), yEl.GetSingle()), new MapId(m.GetInt32()));
            var actual = _transform.GetMapCoordinates(uid);
            var maxDistance = args.TryGetProperty("max_distance", out var d) ? d.GetSingle() : 0.5f;
            var distance = actual.MapId == expected.MapId
                ? Vector2.Distance(actual.Position, expected.Position)
                : float.PositiveInfinity;

            if (distance > maxDistance)
                failures.Add($"position expected within {maxDistance} of map {(int) expected.MapId} ({expected.Position.X:0.##}, {expected.Position.Y:0.##}), got map {(int) actual.MapId} ({actual.Position.X:0.##}, {actual.Position.Y:0.##}), distance {distance:0.##}");
        }

        if (failures.Count > 0)
            return McpToolResult.Err(string.Join("; ", failures));

        return McpToolResult.Ok(new { ok = true, entity = ToEntityInfo(uid) });
    }

    private bool TryResolveEntity(JsonElement args, out EntityUid uid, out string error)
    {
        if (args.TryGetProperty("net_entity_id", out var netId))
        {
            var netEntity = new NetEntity(netId.GetInt32());
            if (TryGetEntity(netEntity, out EntityUid? resolved) && resolved != null && Exists(resolved.Value))
            {
                uid = resolved.Value;
                error = "";
                return true;
            }

            uid = EntityUid.Invalid;
            error = $"Net entity {netEntity.Id} does not exist";
            return false;
        }

        if (args.TryGetProperty("entity_id", out var id))
        {
            uid = new EntityUid(id.GetInt32());
            if (Exists(uid))
            {
                error = "";
                return true;
            }

            error = $"Entity {uid.Id} does not exist";
            return false;
        }

        uid = EntityUid.Invalid;
        error = "Expected entity_id or net_entity_id";
        return false;
    }

    private object ToEntityInfo(EntityUid uid, ICommonSession? session = null, MapCoordinates? coords = null)
    {
        var meta = MetaData(uid);
        var mapCoords = coords ?? _transform.GetMapCoordinates(uid);

        return new
        {
            entity_id = uid.Id,
            net_entity_id = GetNetEntity(uid).Id,
            name = meta.EntityName,
            prototype = meta.EntityPrototype?.ID ?? "none",
            map_id = (int) mapCoords.MapId,
            x = MathF.Round(mapCoords.Position.X, 2),
            y = MathF.Round(mapCoords.Position.Y, 2),
            session = session?.Name
        };
    }
}

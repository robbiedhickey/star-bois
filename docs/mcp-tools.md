# MCP Tool Reference

Server MCP runs on `http://localhost:9222` (SSE + POST).
Client agent runs on `http://localhost:9223` (proxied through server as `client_*` tools).

## SSE transport pattern

Always open the SSE stream **before** posting — the POST returns 202 empty, response arrives on the stream.

```bash
curl -s -N http://localhost:9222/mcp/sse > /tmp/mcp.log 2>&1 &
SSE_PID=$!
sleep 1
curl -s -X POST http://localhost:9222/mcp/msg \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"TOOL","arguments":{}}}'
sleep 2; kill $SSE_PID 2>/dev/null
python3 -c "
import json
with open('/tmp/mcp.log') as f:
    for line in f:
        if line.startswith('data:') and 'jsonrpc' in line:
            data = json.loads(line[5:].strip())
            for item in data.get('result',{}).get('content',[]):
                print(item.get('text',''))
"
```

---

## world_* — Game state queries (server-side, game thread)

### `world_get_map_info`
List all loaded maps and grids.
```json
{}
```
Returns: `{ maps: [{ map_id, name, grids: [{ id, net_entity_id, name, prototype }] }] }`

---

### `world_get_entities_near`
Get entities within a radius of a map position.
```json
{ "map_id": 1, "x": -378.95, "y": -551.42, "radius": 5.0 }
```
- `map_id` (int, required) — from `world_get_map_info`
- `x`, `y` (float, required) — world coordinates
- `radius` (float, default 10.0) — search radius in tiles

Returns: `{ count, entities: [{ id, net_entity_id, name, prototype, x, y, distance }] }`

> **Note:** Use `client_get_player_info` to get the player's current `map_id`, `x`, `y` — then pass those here.

---

### `world_get_entity_info`
Detailed info on a specific entity including vitals and components.
```json
{ "entity_id": 103 }
```
Returns: `{ id, net_entity_id, name, prototype, map_id, x, y, mob_state, damage, respiration, hunger, thirst, status_effects, components }`

---

## client_* — Client-side tools (proxied to port 9223)

### `client_screenshot`
Capture current frame as PNG.
```json
{}
```
Returns: image content block.

---

### `client_get_player_info`
Get local player entity, position, and input context.
```json
{}
```
Returns: `{ entity_id, net_entity_id, name, prototype, map_id, x, y, input_context }`

> **Always call this first** when you need player position or entity ID.

---

### `client_execute_command`
Run a client-side console command.
```json
{ "command": "togglelight" }
```
Useful commands: `togglelight`, `togglefov`, `togglehardfov`, `toggleshadows`

---

### `client_click_control`
Click a UI control by AgentId.
```json
{ "agent_id": "starmap-warp-button" }
```

---

### `client_set_control_value`
Set value on a text/spin input.
```json
{ "agent_id": "some-input", "value": "42" }
```

---

### `client_get_control_tree`
Get all visible UI controls and their AgentIds.
```json
{}
```

---

### `client_move`
Move the player in a direction.
```json
{ "direction": "north", "duration_ms": 500 }
```
Directions: `north`, `south`, `east`, `west`, `northeast`, `northwest`, `southeast`, `southwest`

---

### `client_interact_entity`
Interact with (click) an entity.
```json
{ "net_entity_id": 2999 }
```
Prefer `net_entity_id` over `entity_id` — it's stable across server/client.

---

## admin_* — Server admin tools

### `admin_execute_command`
Run a server console command.
```json
{ "command": "setambientlight 1 255 255 255 255" }
```

---

### `admin_spawn_entity`
Spawn an entity prototype at a position.
```json
{ "prototype": "MobCorgiSmall", "map_id": 1, "x": -378.0, "y": -551.0 }
```

---

### `admin_delete_entity`
Delete an entity by server-local ID.
```json
{ "entity_id": 103 }
```

---

## test_* — Round control and assertions

### `test_start_round`
Force-start the round (bypasses lobby countdown).
```json
{}
```

### `test_join_game`
Spawn the player as a crew member.
```json
{ "job": "Pilot" }
```
Jobs: `Pilot`, `StationEngineer`, `MedicalDoctor`, `Captain`, `Passenger`

### `test_get_player`
Get the first in-game player entity.
```json
{}
```

### `test_list_players`
List all connected sessions.
```json
{}
```

### `test_teleport_entity`
Teleport an entity to map coordinates.
```json
{ "net_entity_id": 2999, "map_id": 1, "x": -378.0, "y": -551.0 }
```

### `test_find_entities`
Find entities by prototype, name, or component.
```json
{ "prototype": "Girder", "map_id": 1 }
```

### `test_assert_entity`
Assert an entity exists with optional checks.
```json
{ "prototype": "Girder", "map_id": 1, "x": -378.0, "y": -551.0, "radius": 2.0 }
```

# star-bois MCP Server

An MCP server embedded in the game server that lets agents (and Claude Code) drive the game programmatically: querying world state, spawning entities, taking screenshots, and interacting with UI controls.

## Architecture

```
Claude Code
    | MCP (HTTP+SSE, port 9222)
McpServerSystem  <->  Game thread (EntityManager, systems)
    | internal HTTP (port 9223)
ClientAgentSystem  <->  Rendering, UI controls
```

The server MCP is the single interface Claude connects to. Client-side tools (screenshots, UI interaction) proxy internally to the client's HTTP API.

---

## Setup

### 1. Enable in server config

Edit `bin/Content.Server/server_config.toml` and add:

```toml
[vars]
mcp.enabled = true
mcp.port = 9222
mcp.client_url = "http://localhost:9223"
```

### 2. Enable in client config

Edit `bin/Content.Client/client_config.toml` (create if it doesn't exist) and add:

```toml
[vars]
mcp.enabled = true
mcp.client_port = 9223
```

### 3. Connect Claude Code

This repo includes a local Claude Code MCP registration at `.claude/settings.json`:

```json
{
  "mcpServers": {
    "star-bois": {
      "type": "sse",
      "url": "http://localhost:9222/mcp/sse"
    }
  }
}
```

### 4. Run the game

```bash
# Terminal 1
make server-dev

# Terminal 2
make client-dev
```

You should see in the server log:
```
[INFO] [MCP] Server listening on http://localhost:9222/
```

And in the client log:
```
[INFO] [MCP Client] Agent API listening on http://localhost:9223/
```

---

## API Surface Contract

The MCP server is a test harness API. Keep the surface cohesive by putting every tool into one of four prefixes:

| Prefix | Purpose | Rule of thumb |
| --- | --- | --- |
| `world_*` | Read-only server game state | Query what exists and where it is. No mutation. |
| `client_*` | Human-facing client observation/input | Screenshot, movement, UI controls, and normal interaction input. |
| `test_*` | Deterministic scenario setup/assertions | Arrange state and assert outcomes for behavior tests. |
| `admin_*` | Sharp privileged escape hatches | Raw server command/spawn/delete utilities. Prefer `test_*` for repeatable tests. |

Normal behavior tests should follow:

1. **Arrange** with `test_*` and narrow `admin_*` setup.
2. **Act** through `client_*` when testing player-facing behavior.
3. **Observe/assert** with `world_*` and `test_assert_entity`.

### Entity Identity

Server and client local entity IDs are not the same. Any server-originated entity response should include:

- `entity_id`: local entity UID in the process that produced the response.
- `net_entity_id`: network entity ID shared by server and client. Prefer this for MCP flows.

Use `net_entity_id` when passing an entity from `world_*` or `test_*` into `client_interact_entity`.

### Contract Check

With server/client running:

```bash
make mcp-register
make mcp-contract
```

`make mcp-register` validates the repo-local MCP registration. `make mcp-contract` validates that all tools belong to the documented prefix taxonomy and that core tools are present.

---

## Available Tools

### World Tools (server-side, game thread)

Read-only game-state queries. These should not mutate world state.

#### `world_get_map_info`
List all loaded maps and grids.
```json
{}
```

#### `world_get_entities_near`
Get entities within a radius of a position. Returns `entity_id`, `net_entity_id`, prototype, name, position, and distance.
```json
{
  "map_id": 1,
  "x": 0.0,
  "y": 0.0,
  "radius": 10.0
}
```

#### `world_get_entity_info`
Get detailed info about a specific entity including all components.
```json
{
  "entity_id": 1234
}
```

### Client Tools (proxied to game client)

Human-facing client actions and observations. These use the same input paths a player would use where practical.

#### `client_screenshot`
Capture the current rendered frame. Returns a PNG image.
```json
{}
```

#### `client_get_player_info`
Get the local player's attached entity, map position, and input context.
```json
{}
```

#### `client_move`
Move the local player or observer for a short duration by injecting the engine movement key functions.
```json
{
  "direction": "north",
  "duration_ms": 700
}
```

Directions: `north`, `south`, `east`, `west`, `northeast`, `northwest`, `southeast`, `southwest`.

#### `client_interact_entity`
Interact with a world entity by injecting the engine `Use` key function at the target entity. Prefer `net_entity_id`.
```json
{
  "net_entity_id": 1234
}
```

Normal SS14 interaction rules still apply. For example, a ghost may dispatch the input but be blocked from using some target entities.

#### `client_get_control_tree`
List all registered AgentIds and their current metadata. This is intentionally a registry of public automation controls, not a dump of every UI node. Controls only appear here after code registers them with `AgentControl.Register`.
```json
{}
```

Returns entries like:
```json
[
  {
    "agentId": "starmap-warp-button",
    "type": "Button",
    "visible": true,
    "disabled": false,
    "hasClickAction": true,
    "text": "Warp",
    "value": null
  }
]
```

Currently registered FTL controls:
- `starmap-warp-button`: clicks the selected starmap destination's warp button
- `power-apc-{net_entity_id}-button`: toggles one APC row in the power-control console

#### `client_click_control`
Click a named UI control.
```json
{
  "agent_id": "starmap-warp-button"
}
```

#### `client_set_control_value`
Set the value of an input control (LineEdit, SpinBox).
```json
{
  "agent_id": "starmap-destination-input",
  "value": "Cepheus-I-32"
}
```

### Test Tools (server-side, game thread)

Scenario helpers for behavior tests. Use these to make tests deterministic without forcing the agent to wander around for setup.

#### `test_list_players`
List connected player sessions and attached entities.
```json
{}
```

#### `test_get_player`
Get one attached in-game player. If `name` is omitted, returns the first attached player.
```json
{
  "name": "localhost@JoeGenero"
}
```

#### `test_teleport_entity`
Teleport an entity to map coordinates. Accepts either `entity_id` or `net_entity_id`.
```json
{
  "net_entity_id": 4491,
  "map_id": 1,
  "x": 10.0,
  "y": -3.0
}
```

#### `test_find_entities`
Find entities by optional prototype, name substring, component, and/or radius.
```json
{
  "component": "Transform",
  "map_id": 1,
  "x": 10.0,
  "y": -3.0,
  "radius": 8.0,
  "limit": 10
}
```

#### `test_assert_entity`
Assert that an entity exists and optionally has a prototype, component, and/or position.
```json
{
  "net_entity_id": 4491,
  "component": "Transform",
  "map_id": 1,
  "x": 10.0,
  "y": -3.0,
  "max_distance": 0.5
}
```

Successful assertions return `{ "ok": true, "entity": ... }`. Failed assertions return an MCP error with the specific mismatch.

### Admin Tools (server-side, game thread)

Privileged escape hatches. Use sparingly in tests and prefer `test_*` tools when a deterministic helper exists.

#### `admin_execute_command`
Run any server console command.
```json
{
  "command": "spawn MobHuman 0,0,1"
}
```

#### `admin_spawn_entity`
Spawn an entity prototype at a position.
```json
{
  "prototype": "MobCorgiSmall",
  "map_id": 1,
  "x": 5.0,
  "y": 3.0
}
```

#### `admin_delete_entity`
Delete an entity by ID.
```json
{
  "entity_id": 1234
}
```

---

## Making Controls Agent-Addressable

Register any UI control you want agents to interact with using `AgentControl.Register`. Do this in the control's constructor or `OnInitialized`:

```csharp
// In a window or control class
protected override void OnInitialized()
{
    base.OnInitialized();

    var warpButton = new Button { Text = "Warp" };
    AgentControl.Register("starmap-warp-button", warpButton, () =>
    {
        // Same logic as OnPressed handler
        OnWarpPressed();
    });
}
```

**Naming convention:** `{system}-{control-type}`, e.g.:
- `starmap-warp-button`
- `starmap-destination-list`
- `gunner-fire-button`
- `power-shields-slider`

AgentIds are part of the agent interface. Treat them like public API. Do not rename without updating tests and documentation.

Unregister in `Dispose` if the control is short-lived:
```csharp
protected override void Dispose(bool disposing)
{
    AgentControl.Unregister("starmap-warp-button");
    base.Dispose(disposing);
}
```

---

## Testing Manually

Once running, you can verify each tool layer independently:

**1. Test the HTTP server is up:**
```bash
curl http://localhost:9222/mcp/sse
# Should stream SSE events
```

**2. Test world tools via Claude Code:**
Ask Claude: *"Call world_get_map_info and tell me what maps are loaded"*

**3. Test screenshot:**
Ask Claude: *"Take a screenshot with client_screenshot"*

**4. Test spawning:**
Ask Claude: *"Spawn a MobCorgiSmall at position 0,0 on map 1, then take a screenshot to confirm"*

**5. Test UI interaction:**
Open the star map console in the game client, then ask Claude: *"What controls are visible in client_get_control_tree?"*

**6. Validate API shape:**
```bash
make mcp-contract
```

**7. Run scenario smoke test:**
```bash
make mcp-scenario
```

---

## Phase 4b Test Checklist

These are the scenarios to verify once the MCP server is connected. Run them in order:

- [ ] `world_get_map_info` returns at least one map
- [ ] `admin_spawn_entity` spawns a visible entity (confirm with screenshot)
- [ ] `client_screenshot` returns a valid PNG
- [ ] `client_get_control_tree` lists controls when a UI is open
- [ ] Star map opens and `starmap-*` controls appear in tree
- [ ] `client_click_control("starmap-warp-button")` triggers a jump
- [ ] AI ship spawns via `admin_spawn_entity` and transitions to Hostile
- [ ] Gunner console opens and `gunner-fire-button` fires weapons

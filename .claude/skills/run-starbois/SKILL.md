---
description: Launch and drive the star-bois game server + client, and interact with the MCP test harness
---

# star-bois Run Skill

Use this skill to launch the game, verify it's healthy, and drive gameplay via the MCP server.

## Port Map

| Port | What |
|---|---|
| 1212 | Game server (RobustToolbox networking) |
| 9222 | MCP server (HTTP+SSE, server-side tools) |
| 9223 | MCP client agent (HTTP, client-side tools) |

## Step 1 — Build

```bash
dotnet build --nologo -consoleLoggerParameters:NoSummary 2>&1 | tail -3
```

Expect `0 Error(s)`. Fix errors before continuing. Warnings are fine.

## Step 2 — Launch server and client

Both must run simultaneously. Launch in background:

```bash
make server-dev > /tmp/starbois-server.log 2>&1 &
make client-dev > /tmp/starbois-client.log 2>&1 &
```

**Server ready** — poll until this appears:
```bash
grep -q "Server Version.*Ready" /tmp/starbois-server.log && echo "ready"
```
Full ready sequence in log:
```
[INFO] [MCP] Server listening on http://localhost:9222/
[INFO] ticker: Started game rule  EndOnShipDestruction
[INFO] ticker: Started game rule  GeneratePoints
[INFO] station: Set up station CS Cestoda-Class Budget Cruiser
[INFO] root: Server Version 277.0.0.0 -> Ready
```

**Client ready** — poll until:
```bash
grep -q "Agent API listening" /tmp/starbois-client.log && echo "ready"
```

**Verify ports are bound:**
```bash
lsof -i :9222 -i :9223 -i :1212 | grep LISTEN
```

## Step 3 — Get a player in-game

The server starts with the lobby enabled. Players spawn when the round starts and they join. Use MCP to drive this entirely — **do not use osascript or GUI automation**.

```bash
# Helper: run an MCP tool call and get the response
mcp_call() {
  curl -s -N http://localhost:9222/mcp/sse > /tmp/mcp-sse.log 2>&1 &
  local PID=$!
  sleep 1
  curl -s -X POST http://localhost:9222/mcp/msg -H "Content-Type: application/json" -d "$1"
  sleep 2
  kill $PID 2>/dev/null
  python3 -c "
import json
with open('/tmp/mcp-sse.log') as f:
    for line in f:
        if line.startswith('data:') and 'jsonrpc' in line:
            data = json.loads(line[5:].strip())
            for item in data.get('result',{}).get('content',[]):
                print(item.get('text') or '[image]')
"
}

# 1. Start the round (bypasses lobby countdown)
mcp_call '{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"test_start_round","arguments":{}}}'

# 2. Spawn the first connected player as Passenger (default fallback job)
mcp_call '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"test_join_game","arguments":{}}}'

# Or spawn as a specific job:
mcp_call '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"test_join_game","arguments":{"job":"Pilot"}}}'
```

Available crew jobs: `Pilot`, `StationEngineer`, `MedicalDoctor`, `Captain`, `Passenger`.

## SSE Transport Pattern (critical)

**Always open the SSE stream before posting.** The POST to `/mcp/msg` returns 202 with an empty body — the actual response arrives on the SSE stream. If you open SSE after posting, you miss the response.

```bash
curl -s -N http://localhost:9222/mcp/sse > /tmp/mcp-sse.log 2>&1 &
SSE_PID=$!
sleep 1  # wait for SSE connection to establish

curl -s -X POST http://localhost:9222/mcp/msg \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"world_get_map_info","arguments":{}}}'

sleep 2
kill $SSE_PID 2>/dev/null

python3 -c "
import json
with open('/tmp/mcp-sse.log') as f:
    for line in f:
        if line.startswith('data:') and 'jsonrpc' in line:
            data = json.loads(line[5:].strip())
            for item in data.get('result',{}).get('content',[]):
                print(item.get('text',''))
"
```

## Screenshots

The `client_screenshot` response delivers a base64 PNG over SSE. Always use `json.loads` — never `sed`/regex on the raw line (JSON unicode escapes corrupt base64).

```bash
curl -s -N http://localhost:9222/mcp/sse > /tmp/mcp-ss.log 2>&1 &
SSE_PID=$!
sleep 1
curl -s -X POST http://localhost:9222/mcp/msg -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"client_screenshot","arguments":{}}}'
sleep 3
kill $SSE_PID 2>/dev/null

python3 -c "
import json, base64
with open('/tmp/mcp-ss.log') as f:
    for line in f:
        if line.startswith('data:') and 'jsonrpc' in line:
            data = json.loads(line[5:].strip())
            for item in data.get('result',{}).get('content',[]):
                if item.get('type') == 'image':
                    open('/tmp/screenshot.png','wb').write(base64.b64decode(item['data']))
                    print('Saved screenshot.png')
                else:
                    print(item.get('text',''))
"
```

## Common Tool Sequences

**Get map IDs (do this first — needed for spawn/teleport calls):**
```json
{ "name": "world_get_map_info", "arguments": {} }
```

**Spawn an entity near the player:**
```json
{ "name": "admin_spawn_entity", "arguments": { "prototype": "MobCorgiSmall", "map_id": 2, "x": 0.0, "y": 0.0 } }
```

**Teleport player:**
```json
{ "name": "test_teleport_entity", "arguments": { "net_entity_id": 1234, "map_id": 2, "x": 10.0, "y": -3.0 } }
```

## Entity IDs

Server and client use different local entity IDs. Always prefer `net_entity_id` when passing entities between tools. Tool responses include both `entity_id` (process-local) and `net_entity_id` (shared across server/client).

## Restart a round

```bash
# Full round restart (keeps players in lobby, they need test_start_round + test_join_game again)
mcp_call '{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"admin_execute_command","arguments":{"command":"restartroundnow"}}}'
```

## Kill and restart everything

```bash
pkill -f "Content.Server"; pkill -f "Content.Client"
make server-dev > /tmp/starbois-server.log 2>&1 &
make client-dev > /tmp/starbois-client.log 2>&1 &
```

## Make Test Targets

```bash
make mcp-register   # validates .claude/settings.json MCP registration
make mcp-contract   # checks all tools belong to world_/client_/test_/admin_ prefixes
make mcp-smoke      # connects SSE, calls tools/list, verifies expected tools exist
make mcp-scenario   # full arrange-act-assert scenario test
```

## Checking for startup errors

```bash
grep -E "FATL|ERRO.*system\." /tmp/starbois-server.log | head -20
grep -E "FATL|Unhandled" /tmp/starbois-client.log | head -20
```

Fatal (`FATL`) errors crash the process. Non-fatal (`ERRO`) errors are logged and continue — they are expected for some map migration artifacts and can be ignored unless the process crashes.

## Preset options

```bash
make server-dev            # Exploration preset (FTL game loop) — default
make server-dev PRESET=Sandbox  # Sandbox preset (no rules, free build/test)
```

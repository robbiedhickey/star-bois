# star-bois — Claude Instructions

## Workflow rules

- **Never commit without explicit approval.** After making changes, build and test first, then ask before running `git commit`.
- **Never push without explicit approval.**

## MCP — always use it when the game is running

When the game is running, **use MCP for all game state queries** — do not guess, infer from code, or ask the user. This includes player position, nearby entities, map info, UI state, and screenshots.

Full tool reference with exact params: [`docs/mcp-tools.md`](docs/mcp-tools.md)

**Common starting sequence:**
1. `client_get_player_info` → get `map_id`, `x`, `y`, `net_entity_id`
2. `world_get_entities_near` → query what's around the player (takes `map_id`/`x`/`y`, NOT entity ID)
3. `client_screenshot` → see what the player sees

## Running the game

```
make server-dev   # terminal 1
make client-dev   # terminal 2
```

First run takes a few minutes (NuGet restore). Server prints `Server Version ... Ready` when up.

## Key ports

| Port | What |
|------|------|
| 1212 | Game server (RobustToolbox) |
| 9222 | MCP server (HTTP+SSE) |
| 9223 | MCP client agent (HTTP) |

## MCP pattern

Always open SSE before posting — the POST returns 202 empty, response arrives on the stream.

## Environment

- `ROBUST_DISABLE_SANDBOX=1` must be set for the client (handled by `make client-dev`)
- Set via Make's `export` directive — processed by Make, not the shell, so works on Windows too

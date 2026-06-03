#!/usr/bin/env python3
"""Run a deterministic arrange-act-assert scenario through StarBois MCP."""

from __future__ import annotations

import argparse
import json
import sys
import urllib.error

from starbois_mcp_smoke import McpClient, parse_tool_json, tool_text


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--sse-url", default="http://localhost:9222/mcp/sse")
    parser.add_argument("--move-direction", default="east")
    parser.add_argument("--move-ms", type=int, default=700)
    args = parser.parse_args()

    client = McpClient(args.sse_url)
    try:
        client.start()
        client.request("initialize")

        player = parse_tool_json(client.call_tool("test_get_player"))
        print(f"arrange: player entity={player['entity_id']} net={player['net_entity_id']} pos=({player['x']}, {player['y']})")

        teleported = parse_tool_json(client.call_tool("test_teleport_entity", {
            "net_entity_id": player["net_entity_id"],
            "map_id": player["map_id"],
            "x": player["x"],
            "y": player["y"],
        }))
        print(f"arrange: teleported to ({teleported['x']}, {teleported['y']})")

        assertion = parse_tool_json(client.call_tool("test_assert_entity", {
            "net_entity_id": player["net_entity_id"],
            "component": "Transform",
            "map_id": teleported["map_id"],
            "x": teleported["x"],
            "y": teleported["y"],
            "max_distance": 0.25,
        }))
        print(f"assert: positioned ok={assertion['ok']}")

        before = parse_tool_json(client.call_tool("client_get_player_info"))
        print(f"act: before ({before['x']}, {before['y']})")
        print(f"act: {tool_text(client.call_tool('client_move', {'direction': args.move_direction, 'duration_ms': args.move_ms}))}")
        after = parse_tool_json(client.call_tool("client_get_player_info"))
        print(f"assert: after ({after['x']}, {after['y']})")

        if before["x"] == after["x"] and before["y"] == after["y"]:
            raise RuntimeError("client_move did not change player position")

        found = parse_tool_json(client.call_tool("test_find_entities", {
            "map_id": after["map_id"],
            "x": after["x"],
            "y": after["y"],
            "radius": 8,
            "component": "Transform",
            "limit": 10,
        }))
        print(f"assert: found {found['count']} nearby transform entities")
        if found["count"] < 1:
            raise RuntimeError("test_find_entities returned no nearby entities")

        print("scenario ok")
        return 0
    except (TimeoutError, RuntimeError, urllib.error.URLError, KeyError, json.JSONDecodeError) as exc:
        print(f"scenario failed: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())

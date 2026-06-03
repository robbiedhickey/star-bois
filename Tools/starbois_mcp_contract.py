#!/usr/bin/env python3
"""Validate the StarBois MCP tool surface against the public taxonomy."""

from __future__ import annotations

import argparse
import json
import sys
import urllib.error

from starbois_mcp_smoke import McpClient


GROUPS = {
    "world": "read-only server game state",
    "client": "human-facing client observation and input",
    "test": "deterministic scenario setup and assertions",
    "admin": "sharp privileged escape hatches",
}

REQUIRED = {
    "world_get_map_info",
    "world_get_entities_near",
    "world_get_entity_info",
    "client_get_player_info",
    "client_move",
    "client_interact_entity",
    "client_screenshot",
    "test_list_players",
    "test_get_player",
    "test_teleport_entity",
    "test_find_entities",
    "test_assert_entity",
    "admin_spawn_entity",
    "admin_delete_entity",
    "admin_execute_command",
}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--sse-url", default="http://localhost:9222/mcp/sse")
    args = parser.parse_args()

    client = McpClient(args.sse_url)
    try:
        client.start()
        client.request("initialize")
        tools = client.request("tools/list").get("tools", [])
    except (TimeoutError, RuntimeError, urllib.error.URLError, json.JSONDecodeError) as exc:
        print(f"contract failed: {exc}", file=sys.stderr)
        return 1

    names = [tool.get("name") for tool in tools]
    errors: list[str] = []

    missing = sorted(REQUIRED - set(names))
    if missing:
        errors.append(f"missing required tools: {', '.join(missing)}")

    grouped: dict[str, list[str]] = {group: [] for group in GROUPS}
    for name in names:
        if not isinstance(name, str) or "_" not in name:
            errors.append(f"tool has invalid name: {name!r}")
            continue

        prefix, _ = name.split("_", 1)
        if prefix not in GROUPS:
            errors.append(f"tool '{name}' uses unknown prefix '{prefix}'")
            continue

        grouped[prefix].append(name)

        schema = next(tool for tool in tools if tool.get("name") == name).get("inputSchema")
        if not isinstance(schema, dict) or schema.get("type") != "object":
            errors.append(f"tool '{name}' must expose object inputSchema")

    for group, description in GROUPS.items():
        values = sorted(grouped[group])
        print(f"{group}_* ({description}): {len(values)}")
        for name in values:
            print(f"  - {name}")

    if errors:
        print("\ncontract failed:", file=sys.stderr)
        for error in errors:
            print(f"- {error}", file=sys.stderr)
        return 1

    print("\ncontract ok")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

#!/usr/bin/env python3
"""Validate the repo-local MCP registration for StarBois."""

from __future__ import annotations

import argparse
import json
from pathlib import Path


DEFAULT_SETTINGS = Path(".claude/settings.json")
DEFAULT_NAME = "star-bois"
DEFAULT_URL = "http://localhost:9222/mcp/sse"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--settings", type=Path, default=DEFAULT_SETTINGS)
    parser.add_argument("--name", default=DEFAULT_NAME)
    parser.add_argument("--url", default=DEFAULT_URL)
    args = parser.parse_args()

    with args.settings.open(encoding="utf-8") as file:
        settings = json.load(file)

    servers = settings.get("mcpServers")
    if not isinstance(servers, dict):
        raise RuntimeError(f"{args.settings} does not contain an mcpServers object")

    server = servers.get(args.name)
    if not isinstance(server, dict):
        raise RuntimeError(f"{args.settings} does not register {args.name!r}")

    actual_type = server.get("type")
    actual_url = server.get("url")
    if actual_type != "sse":
        raise RuntimeError(f"{args.name} should use type 'sse', got {actual_type!r}")
    if actual_url != args.url:
        raise RuntimeError(f"{args.name} should point at {args.url}, got {actual_url!r}")

    print(f"registration ok: {args.name} -> {actual_url}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

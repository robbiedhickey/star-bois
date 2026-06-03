#!/usr/bin/env python3
"""Smoke-test the StarBois MCP bridge against a running dev server/client."""

from __future__ import annotations

import argparse
import json
import queue
import sys
import threading
import time
import urllib.error
import urllib.request


class McpClient:
    def __init__(self, sse_url: str) -> None:
        self.sse_url = sse_url
        self.msg_url: str | None = None
        self.responses: "queue.Queue[dict]" = queue.Queue()
        self._next_id = 1
        self._ready = threading.Event()

    def start(self) -> None:
        thread = threading.Thread(target=self._read_sse, daemon=True)
        thread.start()
        if not self._ready.wait(timeout=10):
            raise TimeoutError(f"MCP endpoint event not received from {self.sse_url}")

    def request(self, method: str, params: dict | None = None, timeout: float = 20) -> dict:
        if self.msg_url is None:
            raise RuntimeError("MCP message endpoint is not ready")

        req_id = self._next_id
        self._next_id += 1
        payload = {"jsonrpc": "2.0", "id": req_id, "method": method}
        if params is not None:
            payload["params"] = params

        data = json.dumps(payload).encode()
        request = urllib.request.Request(
            self.msg_url,
            data=data,
            headers={"Content-Type": "application/json"},
            method="POST",
        )
        with urllib.request.urlopen(request, timeout=timeout) as response:
            if response.status != 202:
                raise RuntimeError(f"MCP POST returned HTTP {response.status}")

        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            try:
                item = self.responses.get(timeout=0.25)
            except queue.Empty:
                continue
            if item.get("id") == req_id:
                if item.get("error"):
                    raise RuntimeError(item["error"])
                return item.get("result") or {}

        raise TimeoutError(f"Timed out waiting for MCP response to {method}")

    def call_tool(self, name: str, arguments: dict | None = None) -> dict:
        return self.request(
            "tools/call",
            {"name": name, "arguments": arguments or {}},
            timeout=30,
        )

    def _read_sse(self) -> None:
        try:
            with urllib.request.urlopen(self.sse_url, timeout=30) as response:
                event_type: str | None = None
                data_parts: list[str] = []

                for raw_line in response:
                    line = raw_line.decode("utf-8").rstrip("\n")
                    if line.startswith("event: "):
                        event_type = line[7:]
                    elif line.startswith("data: "):
                        data_parts.append(line[6:])
                    elif line == "" and event_type is not None:
                        data = "\n".join(data_parts)
                        if event_type == "endpoint":
                            self.msg_url = data
                            self._ready.set()
                        elif event_type == "message":
                            self.responses.put(json.loads(data))
                        event_type = None
                        data_parts = []
        except Exception as exc:
            self.responses.put({"id": -1, "error": str(exc)})


def tool_text(result: dict) -> str:
    content = result.get("content") or []
    if not content:
        return ""
    return content[0].get("text") or ""


def parse_tool_json(result: dict) -> dict:
    text = tool_text(result)
    return json.loads(text) if text else {}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--sse-url", default="http://localhost:9222/mcp/sse")
    parser.add_argument("--move-direction", default="east")
    parser.add_argument("--move-ms", type=int, default=700)
    parser.add_argument("--near-radius", type=float, default=6.0)
    args = parser.parse_args()

    client = McpClient(args.sse_url)
    try:
        client.start()
        init = client.request("initialize")
        print(f"initialized: {init.get('serverInfo', {}).get('name', 'unknown')}")

        tools = client.request("tools/list").get("tools", [])
        tool_names = {tool.get("name") for tool in tools}
        required = {"client_get_player_info", "client_move", "world_get_entities_near", "client_interact_entity"}
        missing = sorted(required - tool_names)
        if missing:
            raise RuntimeError(f"missing tools: {', '.join(missing)}")
        print(f"tools: {len(tools)} available")

        before = parse_tool_json(client.call_tool("client_get_player_info"))
        print(f"before: entity={before['entity_id']} pos=({before['x']}, {before['y']}) context={before['input_context']}")

        move = tool_text(client.call_tool("client_move", {
            "direction": args.move_direction,
            "duration_ms": args.move_ms,
        }))
        print(f"move: {move}")

        after = parse_tool_json(client.call_tool("client_get_player_info"))
        print(f"after:  entity={after['entity_id']} pos=({after['x']}, {after['y']}) context={after['input_context']}")

        if before["x"] == after["x"] and before["y"] == after["y"]:
            raise RuntimeError("position did not change after movement")

        nearby = parse_tool_json(client.call_tool("world_get_entities_near", {
            "map_id": after["map_id"],
            "x": after["x"],
            "y": after["y"],
            "radius": args.near_radius,
        }))
        entities = nearby.get("entities") or []
        print(f"nearby: {len(entities)} entities within {args.near_radius:g}")

        target = next((ent for ent in entities if ent.get("id") != after["entity_id"]), None)
        if target is None:
            print("interact: skipped, no nearby target found")
            return 0

        interaction = tool_text(client.call_tool("client_interact_entity", {"net_entity_id": target["net_entity_id"]}))
        print(f"interact: {interaction} target={target.get('name', target['id'])}")
        return 0
    except (TimeoutError, RuntimeError, urllib.error.URLError, KeyError, json.JSONDecodeError) as exc:
        print(f"smoke failed: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())

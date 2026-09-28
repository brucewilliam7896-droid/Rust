#!/usr/bin/env python3
"""
Driver for the Rust+ Unity project's MCP for Unity bridge.

Talks JSON-RPC 2.0 (MCP "streamable HTTP" transport) directly to the
Unity Editor's bridge at http://127.0.0.1:8080/mcp. No Claude Code MCP
client needed - this works standalone with only the Python stdlib, from
any agent or shell that can run Python 3.

Requires: the Rust+ project already open in the Unity Editor, with the
MCP for Unity package installed (it is - see Packages/manifest.json)
and its bridge running (it auto-starts with the Editor once configured;
see SKILL.md "Prerequisites").

Usage:
    python driver.py state                          # editor readiness snapshot
    python driver.py instances                       # connected Unity instances
    python driver.py resource <uri>                  # raw resource read
    python driver.py tool <name> ['<json-args>']      # raw tool call
    python driver.py screenshot [name] [--resolution N]
    python driver.py console [count] [error|warning|log|all]
    python driver.py test [EditMode|PlayMode] [--timeout SECONDS]
"""
import json
import sys
import time
import urllib.error
import urllib.request

URL = "http://127.0.0.1:8080/mcp"
HEADERS = {
    "Content-Type": "application/json",
    "Accept": "application/json, text/event-stream",
}


def _parse_sse(body: str):
    data_lines = [line[len("data:"):].strip() for line in body.splitlines() if line.startswith("data:")]
    if not data_lines:
        raise ValueError(f"No SSE data in response body: {body!r}")
    return json.loads(data_lines[-1])


def _post(payload, session_id=None):
    headers = dict(HEADERS)
    if session_id:
        headers["Mcp-Session-Id"] = session_id
    req = urllib.request.Request(URL, data=json.dumps(payload).encode(), headers=headers, method="POST")
    try:
        with urllib.request.urlopen(req, timeout=60) as resp:
            return resp.read().decode(), resp.headers.get("Mcp-Session-Id", session_id)
    except urllib.error.URLError as exc:
        raise SystemExit(
            f"Could not reach Unity MCP bridge at {URL}: {exc}\n"
            "Is the Rust+ project open in the Unity Editor? See SKILL.md Prerequisites."
        ) from exc


class UnityMcp:
    def __init__(self):
        self.session_id = None
        self._req_id = 1
        self._initialize()

    def _initialize(self):
        body, session_id = _post({
            "jsonrpc": "2.0",
            "id": self._req_id,
            "method": "initialize",
            "params": {
                "protocolVersion": "2024-11-05",
                "capabilities": {},
                "clientInfo": {"name": "rustplus-driver", "version": "1.0"},
            },
        })
        self.session_id = session_id
        _parse_sse(body)  # raises if the handshake itself failed
        _post({"jsonrpc": "2.0", "method": "notifications/initialized"}, self.session_id)

    def _call(self, method, params=None):
        self._req_id += 1
        body, _ = _post({"jsonrpc": "2.0", "id": self._req_id, "method": method, "params": params or {}}, self.session_id)
        result = _parse_sse(body)
        if "error" in result:
            raise SystemExit(f"MCP error calling {method}: {result['error']}")
        return result["result"]

    def read_resource(self, uri):
        result = self._call("resources/read", {"uri": uri})
        text = result["contents"][0]["text"]
        try:
            return json.loads(text)
        except json.JSONDecodeError:
            return text

    def call_tool(self, name, arguments=None):
        result = self._call("tools/call", {"name": name, "arguments": arguments or {}})
        text = result["content"][0]["text"]
        try:
            return json.loads(text)
        except json.JSONDecodeError:
            return text


def cmd_state(mcp, args):
    print(json.dumps(mcp.read_resource("mcpforunity://editor/state"), indent=2))


def cmd_instances(mcp, args):
    print(json.dumps(mcp.read_resource("mcpforunity://instances"), indent=2))


def cmd_resource(mcp, args):
    print(json.dumps(mcp.read_resource(args[0]), indent=2))


def cmd_tool(mcp, args):
    name = args[0]
    arguments = json.loads(args[1]) if len(args) > 1 else {}
    print(json.dumps(mcp.call_tool(name, arguments), indent=2))


def cmd_screenshot(mcp, args):
    name = None
    resolution = 512
    positional = []
    it = iter(args)
    for a in it:
        if a == "--resolution":
            resolution = int(next(it))
        else:
            positional.append(a)
    if positional:
        name = positional[0]

    call_args = {"action": "screenshot", "include_image": False, "max_resolution": resolution}
    if name:
        call_args["screenshot_file_name"] = name
    result = mcp.call_tool("manage_camera", call_args)
    print(json.dumps(result, indent=2))
    full_path = result.get("data", {}).get("fullPath")
    if full_path:
        # Screenshot capture is async in Unity; give it a moment to land on disk.
        time.sleep(2)
        print(f"\nSaved to: {full_path}", file=sys.stderr)


def cmd_console(mcp, args):
    count = 20
    level = "all"
    if len(args) > 0:
        count = int(args[0])
    if len(args) > 1:
        level = args[1]
    types = None if level == "all" else [level]
    result = mcp.call_tool("read_console", {"types": types, "count": count})
    print(json.dumps(result, indent=2))


def cmd_test(mcp, args):
    mode = "EditMode"
    timeout = 120
    positional = []
    it = iter(args)
    for a in it:
        if a == "--timeout":
            timeout = int(next(it))
        else:
            positional.append(a)
    if positional:
        mode = positional[0]

    started = mcp.call_tool("run_tests", {"mode": mode})
    job_id = started["data"]["job_id"]
    print(f"Started {mode} test job {job_id}...", file=sys.stderr)

    deadline = time.time() + timeout
    while time.time() < deadline:
        job = mcp.call_tool("get_test_job", {"job_id": job_id, "wait_timeout": 10, "include_failed_tests": True})
        status = job["data"]["status"]
        if status in ("succeeded", "failed", "error"):
            print(json.dumps(job, indent=2))
            summary = job["data"].get("result", {}).get("summary", {})
            if summary:
                print(
                    f"\n{mode}: {summary.get('passed', 0)} passed, "
                    f"{summary.get('failed', 0)} failed, "
                    f"{summary.get('skipped', 0)} skipped "
                    f"({summary.get('durationSeconds', 0):.2f}s)",
                    file=sys.stderr,
                )
            return
    raise SystemExit(f"Test job {job_id} did not finish within {timeout}s")


COMMANDS = {
    "state": cmd_state,
    "instances": cmd_instances,
    "resource": cmd_resource,
    "tool": cmd_tool,
    "screenshot": cmd_screenshot,
    "console": cmd_console,
    "test": cmd_test,
}


def main():
    if len(sys.argv) < 2 or sys.argv[1] not in COMMANDS:
        print(__doc__)
        sys.exit(1)
    mcp = UnityMcp()
    COMMANDS[sys.argv[1]](mcp, sys.argv[2:])


if __name__ == "__main__":
    main()

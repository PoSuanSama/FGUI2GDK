#!/usr/bin/env python3
"""运行受保护的 Unity Agent Bridge 固定槽位 JSON 会话。"""

from __future__ import annotations

import argparse
import json
import os
import sys
import tempfile
import threading
import time
import uuid
from pathlib import Path
from typing import Any


MAX_ENVELOPE_BYTES = 1024 * 1024
SLOT_NAMES = (
    "request.json.tmp",
    "request.json",
    "processing.json",
    "response.json.tmp",
    "response.json",
)
SKIP_DIRECTORIES = {".git", "Library", "Logs", "Temp", "obj", "bin"}


class ChineseArgumentParser(argparse.ArgumentParser):
    def __init__(self, *args: Any, **kwargs: Any):
        super().__init__(*args, add_help=False, **kwargs)
        self._positionals.title = "位置参数"
        self._optionals.title = "选项"
        self.add_argument("-h", "--help", action="help", help="显示此帮助信息并退出")

    def format_usage(self) -> str:
        return super().format_usage().replace("usage: ", "用法：", 1)

    def format_help(self) -> str:
        return super().format_help().replace("usage: ", "用法：", 1)

    def error(self, message: str) -> None:
        self.print_usage(sys.stderr)
        self.exit(2, f"{self.prog}: 错误：{self._localize_error(message)}\n")

    @staticmethod
    def _localize_error(message: str) -> str:
        if message.startswith("unrecognized arguments: "):
            return "无法识别的参数：" + message.removeprefix("unrecognized arguments: ")
        if message.startswith("argument ") and message.endswith(": expected one argument"):
            argument = message.removeprefix("argument ").removesuffix(": expected one argument")
            return f"参数 {argument} 缺少取值"
        marker = ": not allowed with argument "
        if message.startswith("argument ") and marker in message:
            argument, conflicting = message.removeprefix("argument ").split(marker, 1)
            return f"参数 {argument} 不能与参数 {conflicting} 同时使用"
        return "命令行参数无效；请使用 --help 查看正确用法"


class BridgeError(RuntimeError):
    pass


def has_bridge(project: Path) -> bool:
    return (project / "Assets").is_dir() and (project / ".agentbridge").is_dir()


def discover_project(start: Path, explicit: Path | None) -> Path:
    if explicit is not None:
        project = explicit.resolve()
        if not has_bridge(project):
            raise BridgeError(
                f"{project} 未同时包含 Assets/ 和既存的同级 .agentbridge/；"
                "Unity 尚未安装或启动 AgentBridge"
            )
        return project

    current = start.resolve()
    for candidate in (current, *current.parents):
        if has_bridge(candidate):
            return candidate

    frontier = [(current, 0)]
    while frontier:
        candidate, depth = frontier.pop(0)
        if depth >= 3:
            continue
        try:
            children = [child for child in candidate.iterdir() if child.is_dir() and child.name not in SKIP_DIRECTORIES]
        except OSError:
            continue
        for child in children:
            if has_bridge(child):
                return child
            frontier.append((child, depth + 1))
    raise BridgeError("Unity 尚未安装或启动 AgentBridge；未找到既存的 Bridge 根目录")


def find_contract(project: Path) -> Path:
    candidates: list[Path] = []
    package_cache = project / "Library" / "PackageCache"
    if package_cache.is_dir():
        for package in package_cache.iterdir():
            if package.is_dir() and "unityagentbridge" in package.name.lower():
                candidates.append(package / "AGENT.md")

    packages = project / "Packages"
    if packages.is_dir():
        for package in packages.iterdir():
            if package.is_dir() and "unityagentbridge" in package.name.lower():
                candidates.append(package / "AGENT.md")

    existing = [path.resolve() for path in candidates if path.is_file()]
    if not existing:
        raise BridgeError(
            "未找到已安装的 UnityAgentBridge AGENT.md；使用 Bridge 前请先在 Unity 中导入或解析该包"
        )
    if len(existing) > 1:
        raise BridgeError("找到多个已安装的 UnityAgentBridge 契约：" + ", ".join(map(str, existing)))
    return existing[0]


def json_line(value: Any, stream: Any = sys.stdout) -> None:
    print(json.dumps(value, ensure_ascii=False, separators=(",", ":")), file=stream, flush=True)


def configure_output_encoding() -> None:
    for stream in (sys.stdout, sys.stderr):
        reconfigure = getattr(stream, "reconfigure", None)
        if reconfigure is not None:
            reconfigure(encoding="utf-8")


def extract_command_names(value: Any) -> set[str]:
    names: set[str] = set()
    if isinstance(value, dict):
        commands = value.get("commands")
        if isinstance(commands, dict):
            names.update(key for key in commands if isinstance(key, str))
        descriptor_name = value.get("command") or value.get("name")
        if isinstance(descriptor_name, str) and (
            "paramsSchema" in value or "batchAllowed" in value or "supportsUndoCollapse" in value
        ):
            names.add(descriptor_name)
        for child in value.values():
            names.update(extract_command_names(child))
    elif isinstance(value, list):
        for child in value:
            names.update(extract_command_names(child))
    return names


class BridgeSession:
    def __init__(self, project: Path):
        self.project = project
        self.root = project / ".agentbridge"
        self.first_request = True
        self.discovery_required = True
        self.commands_version: str | None = None
        self.command_names: set[str] = set()

    def _assert_idle(self) -> None:
        occupied = [name for name in SLOT_NAMES if (self.root / name).exists()]
        if occupied:
            raise BridgeError(
                "Bridge 未处于空闲状态；不要覆盖或删除已占用的槽位：" + ", ".join(occupied)
            )

    def _publish(self, envelope: dict[str, Any]) -> None:
        encoded = json.dumps(envelope, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
        if len(encoded) > MAX_ENVELOPE_BYTES:
            raise BridgeError(f"请求大小为 {len(encoded)} 字节；上限为 {MAX_ENVELOPE_BYTES} 字节")
        temp_path = self.root / "request.json.tmp"
        final_path = self.root / "request.json"
        try:
            with temp_path.open("xb") as output:
                output.write(encoded)
                output.flush()
                os.fsync(output.fileno())
            os.replace(temp_path, final_path)
        except FileExistsError as error:
            raise BridgeError("请求槽位已被占用；请保留现场并停止操作") from error

    def _wait_for_response(self, request_id: str, budget: float) -> dict[str, Any]:
        response_path = self.root / "response.json"
        processing_path = self.root / "processing.json"
        started = time.monotonic()
        next_notice = started + budget
        while not response_path.is_file():
            now = time.monotonic()
            if now >= next_notice:
                json_line(
                    {
                        "event": "wait_budget_exceeded",
                        "id": request_id,
                        "elapsedSeconds": round(now - started, 1),
                        "message": "继续轮询当前交换；不要发布其他请求",
                    },
                    sys.stderr,
                )
                next_notice = now + 30.0
            time.sleep(0.2)

        size = response_path.stat().st_size
        if size > MAX_ENVELOPE_BYTES:
            raise BridgeError(f"响应大小为 {size} 字节；请勿确认该响应，并检查宿主状态")
        raw = response_path.read_bytes()
        try:
            response = json.loads(raw.decode("utf-8"))
        except (UnicodeDecodeError, json.JSONDecodeError) as error:
            raise BridgeError("response.json 不是有效的 UTF-8 JSON；请勿确认该响应") from error
        if not isinstance(response, dict):
            raise BridgeError("响应信封不是对象；请勿确认该响应")
        if response.get("id") != request_id:
            raise BridgeError(
                f"响应 ID {response.get('id')!r} 与请求 ID {request_id!r} 不匹配；请勿确认该响应"
            )

        while processing_path.exists():
            time.sleep(0.1)
        response_path.unlink()
        return response

    def exchange(self, command: str, params: dict[str, Any], timeout: float = 30.0) -> dict[str, Any]:
        if not isinstance(command, str) or not command:
            raise BridgeError("command 必须是非空字符串")
        if not isinstance(params, dict):
            raise BridgeError("params 必须是对象")
        if timeout <= 0 or timeout > 3600:
            raise BridgeError("timeout 必须满足 0 < timeout <= 3600 秒")
        if self.first_request and command != "list_commands":
            raise BridgeError("会话的首个请求必须是 list_commands")
        if self.discovery_required and command != "list_commands":
            raise BridgeError("命令发现结果已失效；执行其他命令前请先运行 list_commands")
        if self.command_names and command != "list_commands" and command not in self.command_names:
            raise BridgeError(f"已发现的命令集中不存在 {command!r}")

        self._assert_idle()
        request_id = "req-" + uuid.uuid4().hex[:24]
        envelope = {"v": 1, "id": request_id, "command": command, "params": params}
        self._publish(envelope)
        response = self._wait_for_response(request_id, timeout)
        self.first_request = False

        version = response.get("commandsVersion")
        error = response.get("error")
        error_code = error.get("code") if isinstance(error, dict) else None
        if command == "list_commands" and response.get("status") == "ok":
            self.commands_version = version if isinstance(version, str) else None
            self.command_names = extract_command_names(response.get("result"))
            self.discovery_required = False
        elif error_code == "UNKNOWN_COMMAND":
            self.discovery_required = True
        elif isinstance(version, str) and self.commands_version is not None and version != self.commands_version:
            self.discovery_required = True
        return response


def run_repl(session: BridgeSession, contract: Path) -> int:
    json_line(
        {
            "event": "ready",
            "project": str(session.project),
            "bridgeRoot": str(session.root),
            "contract": str(contract),
            "next": {"command": "list_commands", "params": {}},
        }
    )
    for raw_line in sys.stdin:
        line = raw_line.strip()
        if not line:
            continue
        try:
            request = json.loads(line)
            if not isinstance(request, dict):
                raise BridgeError("每个输入行必须是一个 JSON 对象")
            if request.get("action") == "quit":
                json_line({"event": "closed"})
                return 0
            command = request.get("command")
            params = request.get("params", {})
            timeout = float(request.get("timeout", 30))
            response = session.exchange(command, params, timeout)
            json_line({"event": "response", "response": response, "discoveryRequired": session.discovery_required})
        except (BridgeError, ValueError, json.JSONDecodeError) as error:
            json_line({"event": "input_error", "message": str(error)}, sys.stderr)
    return 0


def run_self_test() -> int:
    with tempfile.TemporaryDirectory(prefix="gdk-bridge-session-") as temp:
        project = Path(temp) / "Unity"
        root = project / ".agentbridge"
        package = project / "Library" / "PackageCache" / "me.xw.unityagentbridge@test"
        (project / "Assets").mkdir(parents=True)
        root.mkdir()
        package.mkdir(parents=True)
        (package / "AGENT.md").write_text("# 测试契约\n", encoding="utf-8")
        host_errors: list[BaseException] = []

        def fake_host() -> None:
            try:
                for _ in range(2):
                    request_path = root / "request.json"
                    processing_path = root / "processing.json"
                    while not request_path.is_file():
                        time.sleep(0.01)
                    os.replace(request_path, processing_path)
                    request = json.loads(processing_path.read_text(encoding="utf-8"))
                    if request["command"] == "list_commands":
                        result = {
                            "commands": [
                                {
                                    "name": "ping",
                                    "paramsSchema": {"type": "object"},
                                    "batchAllowed": True,
                                }
                            ]
                        }
                    else:
                        result = {"message": "pong"}
                    response = {
                        "v": 1,
                        "id": request["id"],
                        "status": "ok",
                        "result": result,
                        "error": None,
                        "commandsVersion": "test-v1",
                    }
                    response_temp = root / "response.json.tmp"
                    response_temp.write_text(json.dumps(response), encoding="utf-8")
                    os.replace(response_temp, root / "response.json")
                    time.sleep(0.03)
                    processing_path.unlink()
            except BaseException as error:  # pragma: no cover - surfaced below
                host_errors.append(error)

        worker = threading.Thread(target=fake_host, daemon=True)
        worker.start()
        session = BridgeSession(discover_project(project, project))
        contract = find_contract(project)
        if contract.name != "AGENT.md":
            print("失败：未发现已安装契约", file=sys.stderr)
            return 1
        discovery = session.exchange("list_commands", {}, 1)
        ping = session.exchange("ping", {}, 1)
        worker.join(timeout=2)
        if worker.is_alive() or host_errors:
            print(f"失败：模拟宿主未正常结束：{host_errors}", file=sys.stderr)
            return 1
        if discovery.get("status") != "ok" or ping.get("result", {}).get("message") != "pong":
            print("失败：收到非预期响应", file=sys.stderr)
            return 1
        if any((root / name).exists() for name in SLOT_NAMES):
            print("失败：交换槽位未被确认并清理", file=sys.stderr)
            return 1
        try:
            BridgeSession(project).exchange("ping", {}, 1)
        except BridgeError as error:
            if "首个请求" not in str(error):
                print(f"失败：首个请求守卫返回了错误结果：{error}", file=sys.stderr)
                return 1
        else:
            print("失败：接受了未执行发现的首个请求", file=sys.stderr)
            return 1

    print("通过：Bridge 发现、原子交换、确认顺序和首个请求守卫")
    return 0


def parse_args() -> argparse.Namespace:
    parser = ChineseArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, metavar="工程路径", help="Unity 工程路径；必须已包含 Assets 和 .agentbridge")
    parser.add_argument("--show-contract", action="store_true", help="输出已安装的 AGENT.md 路径并退出")
    parser.add_argument("--ack-contract", action="store_true", help="确认主代理已完整阅读安装的 AGENT.md")
    parser.add_argument("--self-test", action="store_true", help="运行隔离的模拟宿主协议测试")
    return parser.parse_args()


def main() -> int:
    configure_output_encoding()
    args = parse_args()
    if args.self_test:
        return run_self_test()
    try:
        project = discover_project(Path.cwd(), args.project)
        contract = find_contract(project)
        if args.show_contract:
            print(contract)
            return 0
        if not args.ack_contract:
            raise BridgeError(
                f"请完整阅读位于 {contract} 的已安装契约，然后使用 --ack-contract 重新运行"
            )
        return run_repl(BridgeSession(project), contract)
    except BridgeError as error:
        print(f"错误：{error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())

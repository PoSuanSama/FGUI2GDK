#!/usr/bin/env python3
"""校验 GDK 变更路径和 Unity 资源不变量。"""

from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import sys
import tempfile
from dataclasses import asdict, dataclass
from pathlib import Path, PurePosixPath


UNITY_ASSETS = "Unity/Assets/"
SERIALIZED_EXTENSIONS = {
    ".anim",
    ".asset",
    ".controller",
    ".lighting",
    ".mat",
    ".overridecontroller",
    ".physicsmaterial",
    ".physicsmaterial2d",
    ".playable",
    ".prefab",
    ".preset",
    ".unity",
}
SECRET_EXTENSIONS = {".jks", ".key", ".keystore", ".p12", ".pem", ".pfx"}
GUID_PATTERN = re.compile(r"(?m)^guid:\s*([0-9a-fA-F]{32})\s*$")
PRIVATE_KEY_PATTERN = re.compile(r"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----")
TOKEN_PATTERNS = (
    re.compile(r"\bAKIA[0-9A-Z]{16}\b"),
    re.compile(r"\bsk-[A-Za-z0-9_-]{20,}\b"),
    re.compile(r"(?i)(?:api[_-]?key|access[_-]?token|client[_-]?secret)\s*[:=]\s*['\"][^'\"]{12,}['\"]"),
)
FORBIDDEN_PARTS = (
    "Unity/Library/",
    "Unity/Temp/",
    "Unity/Logs/",
    "Unity/UserSettings/",
    "Temp/",
    "Logs/",
    ".vs/",
    ".idea/",
)
THIRD_PARTY_PREFIXES = (
    "DotNet/ThirdParty/",
    "Tools/Luban/Luban-Extension/",
    "Unity/Assets/Scripts/Library/",
)
GENERATOR_SOURCE_PREFIXES = (
    "Design/Excel/",
    "Design/Proto/",
    "Design/FairyGUI/",
    "Tools/FairyGUI/",
    "Share/SourceGenerator/",
    "Share/Tool/ExcelExporter/",
    "Share/Tool/Proto2CS/",
    "Tools/Luban/CustomTemplates/",
)


class ChineseArgumentParser(argparse.ArgumentParser):
    def __init__(self, *args: object, **kwargs: object):
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


@dataclass(frozen=True)
class Change:
    status: str
    path: str


@dataclass(frozen=True)
class Issue:
    severity: str
    code: str
    path: str
    message: str


def run_git(repo: Path, *args: str, check: bool = True) -> bytes:
    result = subprocess.run(
        ["git", *args],
        cwd=repo,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
    )
    if check and result.returncode != 0:
        raise RuntimeError(result.stderr.decode("utf-8", errors="replace").strip())
    return result.stdout


def repo_root(start: Path) -> Path:
    output = subprocess.run(
        ["git", "rev-parse", "--show-toplevel"],
        cwd=start,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    if output.returncode != 0:
        raise RuntimeError("当前路径不在 Git 仓库内")
    return Path(output.stdout.strip()).resolve()


def normalize_path(raw: bytes | str) -> str:
    value = raw.decode("utf-8", errors="surrogateescape") if isinstance(raw, bytes) else raw
    return PurePosixPath(value.replace("\\", "/")).as_posix()


def parse_name_status(data: bytes) -> list[Change]:
    tokens = data.split(b"\0")
    changes: list[Change] = []
    index = 0
    while index < len(tokens) and tokens[index]:
        status = tokens[index].decode("ascii", errors="replace")
        index += 1
        if status.startswith(("R", "C")):
            old_path = normalize_path(tokens[index])
            new_path = normalize_path(tokens[index + 1])
            index += 2
            changes.append(Change("D", old_path))
            changes.append(Change("A", new_path))
        else:
            path = normalize_path(tokens[index])
            index += 1
            changes.append(Change(status[:1], path))
    return changes


def merge_changes(groups: list[list[Change]]) -> list[Change]:
    merged: dict[str, str] = {}
    for group in groups:
        for change in group:
            previous = merged.get(change.path)
            if previous == "A" and change.status == "M":
                continue
            if previous == "D" and change.status == "A":
                merged[change.path] = "M"
            else:
                merged[change.path] = change.status
    return [Change(status, path) for path, status in sorted(merged.items())]


def collect_changes(repo: Path, staged: bool, base: str | None) -> list[Change]:
    if base:
        data = run_git(repo, "diff", "--name-status", "-z", "--find-renames", f"{base}...HEAD")
        return parse_name_status(data)
    if staged:
        data = run_git(repo, "diff", "--cached", "--name-status", "-z", "--find-renames")
        return parse_name_status(data)

    cached = parse_name_status(run_git(repo, "diff", "--cached", "--name-status", "-z", "--find-renames"))
    working = parse_name_status(run_git(repo, "diff", "--name-status", "-z", "--find-renames"))
    untracked = [
        Change("A", normalize_path(path))
        for path in run_git(repo, "ls-files", "--others", "--exclude-standard", "-z").split(b"\0")
        if path
    ]
    return merge_changes([cached, working, untracked])


def all_repository_files(repo: Path) -> list[str]:
    return [
        normalize_path(path)
        for path in run_git(repo, "ls-files", "--cached", "--others", "--exclude-standard", "-z").split(b"\0")
        if path
    ]


def is_generated(path: str) -> bool:
    if path.endswith(".Bind.cs"):
        return True
    parts = PurePosixPath(path).parts
    return path.startswith("Unity/Assets/Scripts/Game/") and "Generate" in parts


def issue(severity: str, code: str, path: str, message: str) -> Issue:
    return Issue(severity, code, path, message)


def configure_output_encoding() -> None:
    for stream in (sys.stdout, sys.stderr):
        reconfigure = getattr(stream, "reconfigure", None)
        if reconfigure is not None:
            reconfigure(encoding="utf-8")


def check_path_policy(repo: Path, changes: list[Change]) -> list[Issue]:
    issues: list[Issue] = []
    changed_paths = {change.path for change in changes}
    generated = [change.path for change in changes if change.status != "D" and is_generated(change.path)]
    has_generator_source = any(
        path.startswith(GENERATOR_SOURCE_PREFIXES) for path in changed_paths
    )

    if generated and not has_generator_source:
        for path in generated:
            issues.append(issue("error", "GEN001", path, "生成输出发生变更，但生成器或输入没有对应变更"))
    elif generated:
        for path in generated:
            issues.append(issue("warning", "GEN002", path, "请确认此文件由生成流程重新生成，而非手动编辑"))

    for change in changes:
        path = change.path
        lower = path.lower()
        if (
            any(path.startswith(prefix) for prefix in FORBIDDEN_PARTS)
            or "/__pycache__/" in path
            or "/.pytest_cache/" in path
            or "/.mypy_cache/" in path
            or lower.endswith((".pyc", ".pyo"))
        ):
            issues.append(issue("error", "PATH001", path, "不得提交本地缓存、构建输出或 Editor 输出"))
        if path.startswith("Unity/") and PurePosixPath(path).name in {"Unity.sln", "Unity.Editor.csproj", "Assembly-CSharp.csproj"}:
            issues.append(issue("error", "PATH002", path, "不得提交 Unity 生成的 IDE 项目文件"))
        if PurePosixPath(path).name.startswith("~$") or lower.endswith((".tmp", ".bak", ".orig")):
            issues.append(issue("error", "PATH003", path, "不得提交临时文件或备份文件"))
        if any(path.startswith(prefix) for prefix in THIRD_PARTY_PREFIXES):
            issues.append(issue("warning", "OWN001", path, "第三方或框架代码发生变更；请确认使用扩展点不是更合适的方案"))

        if change.status == "D":
            continue
        file_path = repo / Path(path)
        if not file_path.is_file():
            continue
        size = file_path.stat().st_size
        if size >= 100 * 1024 * 1024:
            issues.append(issue("error", "SIZE002", path, f"文件大小为 {size / 1024 / 1024:.1f} MiB；超过常规 Git 托管限制"))
        elif size >= 20 * 1024 * 1024:
            issues.append(issue("warning", "SIZE001", path, f"大型文件大小为 {size / 1024 / 1024:.1f} MiB；请审查必要性和仓库策略"))

        name = PurePosixPath(path).name.lower()
        suffix = PurePosixPath(path).suffix.lower()
        if suffix in SECRET_EXTENSIONS or name == ".env" or name.startswith(".env.") or re.search(r"(?:^|[-_.])(secret|credentials?)(?:[-_.]|$)", name):
            issues.append(issue("error", "SEC001", path, "不得提交可能包含秘密信息的文件类型或名称"))
        if size <= 1024 * 1024:
            try:
                content = file_path.read_text(encoding="utf-8")
            except (UnicodeDecodeError, OSError):
                content = ""
            if PRIVATE_KEY_PATTERN.search(content):
                issues.append(issue("error", "SEC002", path, "检测到私钥内容"))
            elif any(pattern.search(content) for pattern in TOKEN_PATTERNS):
                issues.append(issue("warning", "SEC003", path, "检测到疑似凭据的令牌；请确认它是不含秘密的测试数据"))

    manifest = "Unity/Packages/manifest.json"
    lock = "Unity/Packages/packages-lock.json"
    if manifest in changed_paths and lock not in changed_paths:
        issues.append(issue("error", "PKG001", manifest, "包清单发生变更，但 packages-lock.json 没有对应变更"))
    if lock in changed_paths and manifest not in changed_paths:
        issues.append(issue("warning", "PKG002", lock, "只有锁文件发生包变更；必须说明原因并在 Unity 中验证"))
    return issues


def check_case_collisions(files: list[str]) -> list[Issue]:
    issues: list[Issue] = []
    seen: dict[str, str] = {}
    for path in files:
        key = path.casefold()
        previous = seen.get(key)
        if previous and previous != path:
            issues.append(issue("error", "CASE001", path, f"与 {previous} 存在不区分大小写的路径冲突"))
        else:
            seen[key] = path
    return issues


def changed_status_map(changes: list[Change]) -> dict[str, str]:
    return {change.path: change.status for change in changes}


def expected_folder_metas(path: str) -> list[str]:
    pure = PurePosixPath(path)
    parents: list[str] = []
    parent = pure.parent
    assets_root = PurePosixPath("Unity/Assets")
    while parent != assets_root and assets_root in parent.parents:
        parents.append(parent.as_posix() + ".meta")
        parent = parent.parent
    return parents


def meta_guid(path: Path) -> str | None:
    try:
        head = path.read_text(encoding="utf-8", errors="ignore")[:4096]
    except OSError:
        return None
    match = GUID_PATTERN.search(head)
    return match.group(1).lower() if match else None


def check_unity_metadata(repo: Path, changes: list[Change], files: list[str]) -> list[Issue]:
    issues: list[Issue] = []
    statuses = changed_status_map(changes)
    changed_meta_paths: list[str] = []

    for change in changes:
        path = change.path
        if not path.startswith(UNITY_ASSETS):
            continue
        is_meta = path.endswith(".meta")
        asset_path = path[:-5] if is_meta else path
        paired_path = asset_path if is_meta else path + ".meta"

        if change.status == "D":
            if statuses.get(paired_path) != "D":
                # 文件夹的 .meta 在 Git 中没有对应的目录记录，删除空生成目录时允许只删除其 .meta。
                if is_meta and not Path(asset_path).suffix:
                    continue
                kind = "资源" if is_meta else ".meta"
                issues.append(issue("error", "META002", path, f"删除 Unity 路径时遗漏了配对的 {kind} 删除操作"))
            continue

        disk_path = repo / Path(path)
        if is_meta:
            changed_meta_paths.append(path)
            if not (repo / Path(asset_path)).exists():
                issues.append(issue("error", "META003", path, "元数据存在，但对应资源或文件夹不存在"))
        elif disk_path.is_file() and not (repo / Path(paired_path)).is_file():
            issues.append(issue("error", "META001", path, "Unity 资源缺少对应的 .meta 文件"))

        if change.status == "A" and not is_meta:
            for folder_meta in expected_folder_metas(path):
                if not (repo / Path(folder_meta)).is_file():
                    issues.append(issue("error", "META005", path, f"新增资源的父文件夹缺少 {folder_meta}"))

        if not is_meta and PurePosixPath(path).suffix.lower() in SERIALIZED_EXTENSIONS:
            issues.append(issue("warning", "UNITY001", path, "Unity 序列化资源发生变更；需要 Bridge 回读、导入、编译和日志证据"))

    if changed_meta_paths:
        guid_owners: dict[str, list[str]] = {}
        for path in files:
            if not path.startswith(UNITY_ASSETS) or not path.endswith(".meta"):
                continue
            guid = meta_guid(repo / Path(path))
            if guid:
                guid_owners.setdefault(guid, []).append(path)
        for path in changed_meta_paths:
            guid = meta_guid(repo / Path(path))
            if not guid:
                issues.append(issue("error", "META006", path, "Unity 元数据中没有有效的 32 字符 guid"))
                continue
            owners = guid_owners.get(guid, [])
            if len(owners) > 1:
                others = ", ".join(owner for owner in owners if owner != path)
                issues.append(issue("error", "META004", path, f"guid {guid} 同时被 {others} 使用"))
    return issues


def analyze(repo: Path, changes: list[Change]) -> list[Issue]:
    files = all_repository_files(repo)
    issues = []
    issues.extend(check_path_policy(repo, changes))
    issues.extend(check_case_collisions(files))
    issues.extend(check_unity_metadata(repo, changes, files))
    return sorted(set(issues), key=lambda item: (item.severity != "error", item.code, item.path, item.message))


def run_self_test() -> int:
    with tempfile.TemporaryDirectory(prefix="gdk-change-guard-") as temp:
        repo = Path(temp)
        run_git(repo, "init", "-q")
        run_git(repo, "config", "user.email", "guard@example.invalid")
        run_git(repo, "config", "user.name", "GDK 变更守卫")
        assets = repo / "Unity" / "Assets"
        assets.mkdir(parents=True)
        (assets / "Good.txt").write_text("正常\n", encoding="utf-8")
        (assets / "Good.txt.meta").write_text("fileFormatVersion: 2\nguid: 11111111111111111111111111111111\n", encoding="utf-8")
        run_git(repo, "add", ".")
        run_git(repo, "commit", "-q", "-m", "test(repo): 初始化仓库")

        (assets / "Missing.txt").write_text("缺少元数据\n", encoding="utf-8")
        issues = analyze(repo, collect_changes(repo, staged=False, base=None))
        if not any(item.code == "META001" for item in issues):
            print("失败：未检测到缺失的 .meta", file=sys.stderr)
            return 1

        (assets / "Missing.txt.meta").write_text("fileFormatVersion: 2\nguid: 11111111111111111111111111111111\n", encoding="utf-8")
        issues = analyze(repo, collect_changes(repo, staged=False, base=None))
        if not any(item.code == "META004" for item in issues):
            print("失败：未检测到重复 GUID", file=sys.stderr)
            return 1

        generated = assets / "Scripts" / "Game" / "Generate"
        generated.mkdir(parents=True)
        (generated / "Generated.cs").write_text("// 生成文件\n", encoding="utf-8")
        issues = analyze(repo, collect_changes(repo, staged=False, base=None))
        if not any(item.code == "GEN001" for item in issues):
            print("失败：未检测到仅修改生成文件的变更", file=sys.stderr)
            return 1

    print("通过：变更守卫可检测元数据缺失、GUID 重复和仅修改生成文件的情况")
    return 0


def parse_args() -> argparse.Namespace:
    parser = ChineseArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--staged", action="store_true", help="检查暂存区差异")
    mode.add_argument("--base", metavar="基准引用", help="检查从 <base>...HEAD 开始的已提交差异")
    parser.add_argument("--repo", type=Path, default=Path.cwd(), metavar="仓库路径", help="Git 仓库内的路径")
    parser.add_argument("--strict", action="store_true", help="将警告视为失败")
    parser.add_argument("--json", action="store_true", dest="as_json", help="输出机器可读的 JSON")
    parser.add_argument("--self-test", action="store_true", help="运行内置的隔离测试")
    return parser.parse_args()


def main() -> int:
    configure_output_encoding()
    args = parse_args()
    if args.self_test:
        return run_self_test()
    try:
        repo = repo_root(args.repo)
        changes = collect_changes(repo, args.staged, args.base)
        issues = analyze(repo, changes)
    except (OSError, RuntimeError) as error:
        print(f"错误：{error}", file=sys.stderr)
        return 2

    errors = [item for item in issues if item.severity == "error"]
    warnings = [item for item in issues if item.severity == "warning"]
    if args.as_json:
        print(json.dumps({"changes": [asdict(item) for item in changes], "issues": [asdict(item) for item in issues]}, ensure_ascii=False, indent=2))
    else:
        severity_labels = {"error": "错误", "warning": "警告"}
        for item in issues:
            print(f"{severity_labels.get(item.severity, item.severity)} {item.code} {item.path}：{item.message}")
        print(f"已检查 {len(changes)} 个变更路径：{len(errors)} 个错误，{len(warnings)} 个警告")

    if errors or (args.strict and warnings):
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

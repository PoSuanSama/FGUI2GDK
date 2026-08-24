#!/usr/bin/env python3
"""无需外部依赖地校验 GDK Conventional Commit 信息。"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path


ALLOWED_TYPES = (
    "feat",
    "fix",
    "refactor",
    "perf",
    "test",
    "docs",
    "build",
    "ci",
    "chore",
    "revert",
)
SUBJECT_PATTERN = re.compile(
    rf"^({'|'.join(ALLOWED_TYPES)})\(([a-z0-9][a-z0-9-]*)\)(!)?: (\S.*)$"
)
TRAILING_PUNCTUATION = (".", "!", "?", ":", ";", "。", "！", "？", "：", "；")


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


def meaningful_lines(message: str) -> list[str]:
    return [line.rstrip() for line in message.replace("\r\n", "\n").split("\n") if not line.startswith("#")]


def validate(message: str) -> list[str]:
    errors: list[str] = []
    lines = meaningful_lines(message)
    while lines and not lines[0]:
        lines.pop(0)
    while lines and not lines[-1]:
        lines.pop()

    if not lines:
        return ["提交信息为空"]

    subject = lines[0]
    if subject.startswith("Merge "):
        return []

    match = SUBJECT_PATTERN.fullmatch(subject)
    if not match:
        errors.append(
            "标题必须符合 '<type>(<小写-scope>): <描述>' 格式；"
            f"type 必须是 {', '.join(ALLOWED_TYPES)} 之一"
        )
    else:
        breaking = bool(match.group(3))
        description = match.group(4)
        if description.endswith(TRAILING_PUNCTUATION):
            errors.append("标题末尾不得使用标点")
        if description in {"update", "fix", "changes", "优化代码", "更新文件", "修改代码"}:
            errors.append("标题过于含糊；请描述变更后的行为")
        if breaking and not any(line.startswith("BREAKING CHANGE:") for line in lines[1:]):
            errors.append("破坏性变更标题必须包含 'BREAKING CHANGE:' 页脚")

    if len(subject) > 72:
        errors.append(f"标题长度为 {len(subject)} 个字符；上限为 72 个字符")

    if len(lines) > 1 and lines[1] != "":
        errors.append("标题与正文之间必须空一行")

    for index, line in enumerate(lines[2:], start=3):
        if len(line) > 100 and "http://" not in line and "https://" not in line:
            errors.append(f"第 {index} 行长度为 {len(line)} 个字符；上限为 100 个字符")

    return errors


def configure_output_encoding() -> None:
    for stream in (sys.stdout, sys.stderr):
        reconfigure = getattr(stream, "reconfigure", None)
        if reconfigure is not None:
            reconfigure(encoding="utf-8")


def run_self_test() -> int:
    cases = {
        "fix(et): 修复重连状态未清理": True,
        "build(bridge): 升级 AgentBridge 至 2.0.2": True,
        "优化代码": False,
        "fix(ET): 修复问题": False,
        "fix(et): 修复问题。": False,
        "feat(api)!: 调整会话协议\n\nBREAKING CHANGE: 需要重新生成客户端": True,
        "feat(api)!: 调整会话协议": False,
    }
    failures = []
    for message, expected in cases.items():
        actual = not validate(message)
        if actual != expected:
            failures.append((message, expected, validate(message)))
    if failures:
        for message, expected, errors in failures:
            print(f"失败：预期={expected} 信息={message!r} 错误={errors}", file=sys.stderr)
        return 1
    print(f"通过：{len(cases)} 个提交信息用例")
    return 0


def parse_args() -> argparse.Namespace:
    parser = ChineseArgumentParser(description=__doc__)
    source = parser.add_mutually_exclusive_group()
    source.add_argument("message", nargs="?", metavar="提交信息", help="提交信息文本")
    source.add_argument("--file", type=Path, metavar="文件路径", help="从文件读取提交信息")
    parser.add_argument("--self-test", action="store_true", help="运行内置测试")
    return parser.parse_args()


def main() -> int:
    configure_output_encoding()
    args = parse_args()
    if args.self_test:
        return run_self_test()
    if args.file:
        message = args.file.read_text(encoding="utf-8-sig")
    elif args.message is not None:
        message = args.message
    else:
        print("错误：请提供提交信息或 --file", file=sys.stderr)
        return 2

    errors = validate(message)
    if errors:
        for error in errors:
            print(f"错误：{error}", file=sys.stderr)
        return 1
    print("通过：提交信息符合 GDK 规范")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

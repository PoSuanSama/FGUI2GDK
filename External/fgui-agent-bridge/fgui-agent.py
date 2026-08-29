#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""免安装启动 fgui-agent CLI。

把随仓库内置的 FairGUI Agent Bridge 源码(src/fairygui_agent)加入 sys.path 后
直接运行 cli.main,不需要 uv / pip install / venv。CLI 路径(不含 MCP 服务)只依赖
Python 3.10+ 标准库;MCP 服务(mcp_server.py)仍需安装 mcp 包,见 Book/FairyGUI接入.md。

用法(与安装版 fgui-agent 一致):
    python External/fgui-agent-bridge/fgui-agent.py status --project <工程路径>
    python External/fgui-agent-bridge/fgui-agent.py publish --scope packages --package Package1 --project <工程路径>

也可以把本文件所在目录加进 PATH,或设 FGUI_AGENT_EXE 指向 python + 本文件。
"""

from __future__ import annotations

import sys
from pathlib import Path

_SELF = Path(__file__).resolve()
_SRC = _SELF.parent / "src"

if not _SRC.is_dir():
    raise SystemExit(f"fairygui_agent 源码目录缺失: {_SRC}")

if str(_SRC) not in sys.path:
    sys.path.insert(0, str(_SRC))

from fairygui_agent.cli import main  # noqa: E402

if __name__ == "__main__":
    raise SystemExit(main())

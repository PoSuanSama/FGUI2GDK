# Implement — 第一批清理 UGUI 基础层

1. 删除 vendor `UnityGameFramework.Runtime.UI` 目录及其 `.meta`。
2. 删除 `Game/UI/Common` 下 UGUI 基类与 loader/helper。
3. 删除 `ET/Loader/UGF/UIForm` 与 `ET/Loader/UGF/UIWidget`。
4. 删除相关 ModelView/HotfixView 旧 UIWidget 测试。
5. 精简 `UGFSystemSingleton` 与 `GameEntry.Builtin`。
6. 清理 Editor 生成工具与 CodeBind 映射。
7. 编译 ET、GameHot，运行 ET 冒烟，运行 `rg` 检查。

验证命令：
- Unity Bridge `recompile` + `get_compile_result`
- `invoke_agent_method ET.FairyInventorySmokeTest::RunFairyInventorySmokeTest`
- `python .agents/skills/gdk-development-workflow/scripts/validate_changes.py`
- `git diff --cached --check`

## 收口证据

1-7 项全部落地（提交 8b39d6cc 及阶段 F 批次 b2f4ed4e/86b5f27e）：
UGUI 基础层/UIForm/UIWidget/生成工具全部删除；ET/GameHot 双模式编译 0/0；
ET 冒烟与零 UGUI 静态扫描通过。

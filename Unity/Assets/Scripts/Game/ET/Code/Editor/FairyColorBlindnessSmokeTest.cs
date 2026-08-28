using System;
using System.Collections.Generic;
using System.IO;
using AgentBridge;
using Cysharp.Threading.Tasks;
using Game;
using UnityEditor;

namespace ET
{
    /// <summary>
    /// 色觉能力冒烟(阶段 D,design §10.5):
    /// 验证语义颜色检查器:对仓库 FairyGUI 组件 XML 跑 lint,
    /// 断言返回稳定发现(存在只靠颜色表达状态的节点是设计快照事实,
    /// 本测试只验证检查器可运行、发现可定位,不把发现数当回归门槛);
    /// 并验证纯图形节点(无颜色)不误报。
    /// </summary>
    public static class FairyColorBlindnessSmokeTest
    {
        [AgentCallable("FairyGUI 色觉语义检查冒烟:lint 可运行且发现可定位。", 30)]
        public static async UniTask RunFairyColorBlindnessSmokeTest()
        {
            await UniTask.CompletedTask;

            // 样本 1:纯颜色状态节点 -> 应报发现。
            const string colorOnlyXml =
                "<component>\n  <displayList>\n    <graph id=\"dot\" name=\"dot\" fillColor=\"#ff34d399\"/>\n  </displayList>\n</component>";
            IReadOnlyList<FairyColorBlindness.SemanticColorFinding> findings =
                FairyColorBlindness.CheckSemanticColor(colorOnlyXml);
            if (findings.Count == 0)
            {
                throw new InvalidOperationException(
                    "Semantic color lint did not report a color-only element.");
            }

            bool hasDotFinding = false;
            foreach (FairyColorBlindness.SemanticColorFinding finding in findings)
            {
                if (finding.ElementId == "dot" && finding.Reason.Contains("dot", StringComparison.Ordinal))
                {
                    hasDotFinding = true;
                }
            }

            if (!hasDotFinding)
            {
                throw new InvalidOperationException(
                    "Semantic color lint finding does not locate the color-only element.");
            }

            // 样本 2:带文本的节点 -> 不误报。
            const string textNodeXml =
                "<component>\n  <displayList>\n    <text id=\"label\" name=\"label\" color=\"#ffffff\" text=\"状态\"/>\n  </displayList>\n</component>";
            IReadOnlyList<FairyColorBlindness.SemanticColorFinding> textFindings =
                FairyColorBlindness.CheckSemanticColor(textNodeXml);
            if (textFindings.Count != 0)
            {
                throw new InvalidOperationException(
                    "Semantic color lint reported a text-bearing element as color-only.");
            }

            // 仓库快照:对 Package1 组件跑一遍,确认可运行(不设数量门槛)。
            string[] componentFiles =
            {
                "Design/FairyGUI/GDK_FGUI/assets/Package1/MainView.xml",
                "Design/FairyGUI/GDK_FGUI/assets/Package1/InventoryView.xml",
            };
            foreach (string file in componentFiles)
            {
                string fullPath = Path.GetFullPath(file);
                if (!File.Exists(fullPath))
                {
                    continue;
                }

                _ = FairyColorBlindness.CheckSemanticColor(File.ReadAllText(fullPath));
            }
        }
    }
}

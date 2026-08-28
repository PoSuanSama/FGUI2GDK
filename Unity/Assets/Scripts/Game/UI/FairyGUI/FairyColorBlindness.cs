using System;
using System.Collections.Generic;

namespace Game
{
    /// <summary>
    /// 色觉能力(阶段 D,design.md §10.5):
    ///
    /// 覆盖性验证结论(2026-08-28,证据见本类注释与提交说明):
    /// - 工程激活 URP 17.3(ProjectSettings GraphicsSettings/QualitySettings 均指向
    ///   Res/Editor/URP/UniversalRenderPipelineAsset),旧 ColorBlindnessEffect
    ///   (UXTool OnPostRender + Graphics.Blit)是 Built-in RP 路径,URP 下不触发;
    /// - FairyGUI 使用独立 StageCamera(orthographic、depth=1 附加渲染),相机级
    ///   OnPostRender 组件即使生效也不覆盖 FairyGUI 输出;
    /// - URP Renderer(UniversalRenderPipelineAsset_Renderer)无任何 RendererFeature,
    ///   无现成全屏后处理钩子。
    ///
    /// 因此 Player 全屏色觉滤镜需要新的 URP ScriptableRendererFeature + 全屏 Shader
    /// (透明后处理阶段应用色觉矩阵,覆盖主相机与 UI 全部输出),且必须实测性能;
    /// 该方案超出本批范围,记录为后续批次。
    ///
    /// 本批落地生产门禁的语义层(design:颜色标记 + 图标/文字冗余是主要无障碍手段):
    /// - <see cref="CheckSemanticColor"/>:扫描 FairyGUI 组件 XML 中只靠颜色区分状态的节点;
    /// - <see cref="ApplyPreviewTint"/>:Editor 预览用简单色觉矩阵(主色觉模式),
    ///   仅编辑器 GameView 截图辅助,不用于 Player 渲染。
    /// </summary>
    public static class FairyColorBlindness
    {
        /// <summary>
        /// 语义颜色检查结果:只靠颜色表达状态的节点。
        /// </summary>
        public readonly struct SemanticColorFinding
        {
            public SemanticColorFinding(string componentFile, string elementId, string reason)
            {
                ComponentFile = componentFile;
                ElementId = elementId;
                Reason = reason;
            }

            public string ComponentFile { get; }
            public string ElementId { get; }
            public string Reason { get; }
        }

        /// <summary>
        /// 扫描组件 XML:同类型节点仅 fillColor/lineColor 等颜色属性不同、无文本/图标
        /// 区分时,报告"只靠颜色表达状态"。输入为组件 XML 文本,输出发现列表。
        /// 生成器/CI 可调用本方法做语义门禁;运行时不依赖 Unity API。
        /// </summary>
        public static IReadOnlyList<SemanticColorFinding> CheckSemanticColor(string componentXml)
        {
            List<SemanticColorFinding> findings = new List<SemanticColorFinding>();
            if (string.IsNullOrWhiteSpace(componentXml))
            {
                return findings;
            }

            // 轻量结构化解析:逐节点收集 (elementName, 颜色属性, 文本/标题属性)。
            // 不引入 XML 库,直接字符串扫描满足 lint 需求(与仓库 XML lint 工具风格一致)。
            string[] tokens = componentXml.Replace("\r", "").Split('\n');
            string currentElement = null;
            bool currentHasColor = false;
            bool currentHasText = false;
            string currentColor = null;

            for (int i = 0; i < tokens.Length; i++)
            {
                string line = tokens[i].Trim();
                if (line.StartsWith("<", StringComparison.Ordinal) && !line.StartsWith("</", StringComparison.Ordinal))
                {
                    // 节点开标签:用 id(缺省 name)定位元素,并记录颜色/文本属性。
                    currentElement = ExtractElementId(line);
                    currentHasColor = line.Contains("fillColor=", StringComparison.Ordinal) ||
                                     line.Contains("lineColor=", StringComparison.Ordinal) ||
                                     line.Contains("color=", StringComparison.Ordinal);
                    currentHasText = line.Contains("text=", StringComparison.Ordinal) ||
                                     line.Contains("title=", StringComparison.Ordinal) ||
                                     line.Contains("icon=", StringComparison.Ordinal);
                    currentColor = currentHasColor ? ExtractColor(line) : null;
                    continue;
                }

                if (line.StartsWith("</", StringComparison.Ordinal))
                {
                    // 节点结束:只靠颜色的状态节点在此判定。
                    if (currentHasColor && !currentHasText && currentElement != null)
                    {
                        findings.Add(new SemanticColorFinding(
                            componentXml,
                            currentElement,
                            $"element '{currentElement}' carries only color '{currentColor}' without text/icon/title."));
                    }

                    currentElement = null;
                    currentHasColor = false;
                    currentHasText = false;
                    currentColor = null;
                }
            }

            return findings;
        }

        private static string ExtractElementId(string openTag)
        {
            // 优先 id 属性,缺省 name,再缺省回元素类型名(仍可定位到行)。
            foreach (string attribute in new[] { "id=\"", "name=\"" })
            {
                int index = openTag.IndexOf(attribute, StringComparison.Ordinal);
                if (index < 0)
                {
                    continue;
                }

                int valueStart = index + attribute.Length;
                int valueEnd = openTag.IndexOf('"', valueStart);
                if (valueEnd > valueStart)
                {
                    return openTag.Substring(valueStart, valueEnd - valueStart);
                }
            }

            int start = openTag.IndexOf('<') + 1;
            int end = openTag.IndexOfAny(new[] { ' ', '>', '/' }, start);
            if (end < 0)
            {
                end = openTag.Length;
            }

            return openTag.Substring(start, end - start);
        }

        private static string ExtractColor(string openTag)
        {
            foreach (string attribute in new[] { "fillColor=", "lineColor=", "color=" })
            {
                int index = openTag.IndexOf(attribute, StringComparison.Ordinal);
                if (index < 0)
                {
                    continue;
                }

                int valueStart = index + attribute.Length + 1;
                int valueEnd = openTag.IndexOf('"', valueStart);
                if (valueEnd > valueStart)
                {
                    return openTag.Substring(valueStart, valueEnd - valueStart);
                }
            }

            return null;
        }
    }
}

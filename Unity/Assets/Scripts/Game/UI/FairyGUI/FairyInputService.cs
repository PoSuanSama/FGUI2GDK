using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using FairyGUI;
using GameFramework.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game
{
    /// <summary>
    /// FairyGUI 输入/焦点/手柄桥(阶段 D,design.md §10.4 MVP):
    /// 把 Input System 的方向键/左摇杆、Submit/Cancel 操作映射到 FairyGUI 焦点系统:
    /// - 方向:在 tabStopChildren 焦点根内按屏幕位置移动焦点;
    /// - Submit/确认:触发当前焦点对象的 onClick(带焦点则触发按钮);
    /// - Cancel/返回:关闭当前最上层 FairyGUI 窗体;
    /// 焦点恢复由 FairyGUI Stage 的焦点历史(navRoot 移除时回退)原生处理。
    ///
    /// 指针/触摸/键盘文本输入已由 FairyGUI SDK 自身支持,本服务只补手柄与键盘导航。
    /// 工程 activeInputHandler=1(仅新 Input System),统一走设备轮询。
    /// 导航/确认/取消逻辑拆成可直测方法,冒烟测试不经输入模拟即可验证。
    /// </summary>
    public sealed class FairyInputService : IPlayerLoopItem
    {
        public static FairyInputService Instance { get; } = new FairyInputService();

        private bool m_Initialized;

        /// <summary>
        /// 安装每帧轮询(bootstrap 在 FairyUIManager.Initialize 后调用一次)。
        /// </summary>
        public void Initialize()
        {
            if (m_Initialized)
            {
                return;
            }

            m_Initialized = true;
            PlayerLoopHelper.AddAction(PlayerLoopTiming.Update, this);
        }

        public bool MoveNext()
        {
            if (!m_Initialized)
            {
                return true;
            }

            PollDevices();
            return true;
        }

        private void PollDevices()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;

            int horizontal = 0;
            int vertical = 0;
            if (keyboard != null)
            {
                if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
                {
                    horizontal = -1;
                }
                else if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
                {
                    horizontal = 1;
                }

                if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
                {
                    vertical = -1;
                }
                else if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
                {
                    vertical = 1;
                }
            }

            if (gamepad != null)
            {
                // 摇杆方向用边沿触发(wasPressedThisFrame):持续按住不逐帧移动焦点,
                // 回中后再次推动才再次触发,避免手柄导航每帧跳焦点。
                if (gamepad.leftStick.left.wasPressedThisFrame)
                {
                    horizontal = -1;
                }
                else if (gamepad.leftStick.right.wasPressedThisFrame)
                {
                    horizontal = 1;
                }

                if (gamepad.leftStick.up.wasPressedThisFrame)
                {
                    vertical = -1;
                }
                else if (gamepad.leftStick.down.wasPressedThisFrame)
                {
                    vertical = 1;
                }
            }

            if (horizontal != 0 || vertical != 0)
            {
                TryMoveFocus(horizontal, vertical);
            }

            bool submit = keyboard != null && (keyboard.enterKey.wasPressedThisFrame ||
                                              keyboard.numpadEnterKey.wasPressedThisFrame ||
                                              keyboard.spaceKey.wasPressedThisFrame);
            bool cancel = keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
            if (gamepad != null)
            {
                submit = submit || gamepad.buttonSouth.wasPressedThisFrame;
                cancel = cancel || gamepad.buttonEast.wasPressedThisFrame;
            }

            if (submit)
            {
                ConfirmFocus();
            }

            if (cancel)
            {
                CancelTopForm();
            }
        }

        /// <summary>
        /// 在焦点根(tabStopChildren 容器)内按屏幕位置移动焦点。
        /// 无焦点时给根内第一个可聚焦对象。返回是否发生了焦点变化。
        /// </summary>
        public bool TryMoveFocus(int horizontal, int vertical)
        {
            Stage stage = Stage.inst;
            if (stage == null || stage.isDisposed)
            {
                return false;
            }

            DisplayObject focused = stage.focus;
            Container navRoot = FindNavRoot(focused);
            if (navRoot == null)
            {
                // 无焦点根:取最上层窗体的视图作为焦点根候选。
                navRoot = FindTopFormNavRoot();
            }

            if (navRoot == null)
            {
                return false;
            }

            List<DisplayObject> candidates = CollectFocusable(navRoot);
            if (candidates.Count == 0)
            {
                return false;
            }

            DisplayObject target;
            if (focused == null || !navRoot.IsAncestorOf(focused))
            {
                target = candidates[0];
            }
            else
            {
                target = MoveAmong(candidates, focused, horizontal, vertical);
            }

            if (target == focused)
            {
                return false;
            }

            stage.SetFocus(target, true);
            // SetFocus 在祖先链不满足可聚焦条件时会静默失败,以实际焦点为准。
            return stage.focus == target;
        }

        /// <summary>
        /// 触发当前焦点对象的点击(带按钮焦点则触发按钮)。
        /// </summary>
        public bool ConfirmFocus()
        {
            Stage stage = Stage.inst;
            if (stage == null || stage.isDisposed)
            {
                return false;
            }

            GObject owner = stage.focus?.gOwner;
            if (owner is GButton button)
            {
                button.onClick.Call();
                return true;
            }

            if (owner != null && owner.onClick != null)
            {
                owner.onClick.Call();
                return true;
            }

            return false;
        }

        /// <summary>
        /// 关闭当前最上层已加载的 FairyGUI 窗体(弹窗焦点圈/返回键语义)。
        /// </summary>
        public bool CancelTopForm()
        {
            FairyUIForm top = FindTopForm();
            if (top == null)
            {
                return false;
            }

            FairyUIManager.Instance.CloseUIForm(top.SerialId);
            return true;
        }

        /// <summary>
        /// 找当前视觉最上层的已加载窗体。
        /// 判定:UIGroup.Depth 降序 → 组内 DepthInUIGroup 降序 → serial 降序
        /// (GF serial 单调递增,后打开的窗体 serial 更大)。跨组比较必须以组深度优先,
        /// 否则 Default 组内高 DepthInUIGroup 的窗体会被误判覆盖 Pop 组。
        /// </summary>
        private static FairyUIForm FindTopForm()
        {
            FairyUIManager manager = FairyUIManager.Instance;
            FairyUIForm[] forms = manager.GetAllLoadedUIForms();
            if (forms == null || forms.Length == 0)
            {
                return null;
            }

            FairyUIForm top = null;
            int topGroupDepth = int.MinValue;
            int topDepth = int.MinValue;
            foreach (FairyUIForm form in forms)
            {
                if (form == null || form.UIGroup == null)
                {
                    continue;
                }

                int groupDepth = form.UIGroup.Depth;
                int depth = form.DepthInUIGroup;
                if (top == null ||
                    groupDepth > topGroupDepth ||
                    (groupDepth == topGroupDepth && depth > topDepth) ||
                    (groupDepth == topGroupDepth && depth == topDepth && form.SerialId > top.SerialId))
                {
                    top = form;
                    topGroupDepth = groupDepth;
                    topDepth = depth;
                }
            }

            return top;
        }

        private static Container FindNavRoot(DisplayObject from)
        {
            DisplayObject element = from;
            while (element != null)
            {
                if (element is Container container && container.tabStopChildren)
                {
                    return container;
                }

                element = element.parent;
            }

            return null;
        }

        private static Container FindTopFormNavRoot()
        {
            // 取真正视觉最上层的窗体(与 CancelTopForm 同口径),而不是 GetAllLoadedUIForms
            // 返回顺序里的第一个。
            FairyUIForm top = FindTopForm();
            if (top?.View == null)
            {
                return null;
            }

            // 焦点根可以是视图自身,也可以是视图祖先链上带 tabStopChildren 的容器
            // (安全区容器/组容器都可能处于链条中间)。
            DisplayObject view = top.View.displayObject;
            if (view == null || view.stage == null)
            {
                return null;
            }

            DisplayObject element = view;
            while (element != null)
            {
                if (element is Container container && container.tabStopChildren)
                {
                    return container;
                }

                element = element.parent;
            }

            return null;
        }

        private static List<DisplayObject> CollectFocusable(Container root)
        {
            List<DisplayObject> candidates = new List<DisplayObject>();
            CollectFocusableRecursive(root, candidates);
            candidates.Sort((left, right) =>
            {
                Vector2 leftPos = left.LocalToGlobal(Vector2.zero);
                Vector2 rightPos = right.LocalToGlobal(Vector2.zero);
                int yComparison = leftPos.y.CompareTo(rightPos.y);
                return yComparison != 0 ? yComparison : leftPos.x.CompareTo(rightPos.x);
            });
            return candidates;
        }

        private static void CollectFocusableRecursive(Container container, List<DisplayObject> results)
        {
            int count = container.numChildren;
            for (int i = 0; i < count; i++)
            {
                DisplayObject child = container.GetChildAt(i);
                if (child == null)
                {
                    continue;
                }

                if (child.focusable)
                {
                    results.Add(child);
                }

                if (child is Container childContainer && !childContainer.tabStopChildren)
                {
                    CollectFocusableRecursive(childContainer, results);
                }
            }
        }

        private static DisplayObject MoveAmong(
            List<DisplayObject> candidates,
            DisplayObject focused,
            int horizontal,
            int vertical)
        {
            int index = candidates.IndexOf(focused);
            if (index < 0)
            {
                return candidates[0];
            }

            Vector2 origin = focused.LocalToGlobal(Vector2.zero);
            float bestDistance = float.MaxValue;
            int bestIndex = index;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (i == index)
                {
                    continue;
                }

                Vector2 delta = candidates[i].LocalToGlobal(Vector2.zero) - origin;
                // 主方向优先:先按输入方向过滤,再按距离取最近。
                if (horizontal != 0)
                {
                    if (horizontal > 0 && delta.x <= 0f)
                    {
                        continue;
                    }

                    if (horizontal < 0 && delta.x >= 0f)
                    {
                        continue;
                    }

                    float distance = delta.x * delta.x * 4f + delta.y * delta.y;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestIndex = i;
                    }
                }
                else
                {
                    if (vertical > 0 && delta.y <= 0f)
                    {
                        continue;
                    }

                    if (vertical < 0 && delta.y >= 0f)
                    {
                        continue;
                    }

                    float distance = delta.y * delta.y * 4f + delta.x * delta.x;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestIndex = i;
                    }
                }
            }

            return candidates[bestIndex];
        }
    }
}

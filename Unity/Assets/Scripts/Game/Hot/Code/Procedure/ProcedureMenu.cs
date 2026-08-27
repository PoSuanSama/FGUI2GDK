//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameFramework.Fsm;
using UnityGameFramework.Runtime;

namespace Game.Hot
{
    public class ProcedureMenu : ProcedureBase
    {
        private bool m_StartGame = false;
        private CancellationTokenSource m_FairyOpenCancellation;
        private UIForm m_FairyDemoUIForm;

        public void StartGame()
        {
            m_StartGame = true;
        }

        protected override void OnEnter(IFsm<ProcedureComponent> procedureOwner)
        {
            base.OnEnter(procedureOwner);

            m_StartGame = false;
            m_FairyOpenCancellation = new CancellationTokenSource();
            OpenFairyDemoAsync(m_FairyOpenCancellation.Token).Forget();
        }

        protected override void OnLeave(IFsm<ProcedureComponent> procedureOwner, bool isShutdown)
        {
            CancellationTokenSource cancellation = m_FairyOpenCancellation;
            m_FairyOpenCancellation = null;
            cancellation?.Cancel();
            cancellation?.Dispose();

            if (!isShutdown && m_FairyDemoUIForm != null)
            {
                if (GameEntry.UI.HasUIForm(m_FairyDemoUIForm.SerialId))
                {
                    GameEntry.UI.CloseUIForm(m_FairyDemoUIForm.SerialId);
                }

                m_FairyDemoUIForm = null;
            }

            base.OnLeave(procedureOwner, isShutdown);
        }

        protected override void OnUpdate(IFsm<ProcedureComponent> procedureOwner, float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(procedureOwner, elapseSeconds, realElapseSeconds);

            if (m_StartGame)
            {
                procedureOwner.SetData<VarInt32>("NextSceneId", HotEntry.Tables.DTOneConfig.SceneMain);
                procedureOwner.SetData<VarByte>("GameMode", (byte)GameMode.Survival);
                ChangeState<ProcedureChangeScene>(procedureOwner);
            }
        }

        private async UniTaskVoid OpenFairyDemoAsync(CancellationToken cancellationToken)
        {
            try
            {
                m_FairyDemoUIForm = await FairyUIFormService.OpenFairyUIFormAsync(
                    UIFormId.FairyDemoForm,
                    this,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                Log.Error("Failed to open the FairyGUI demo form: {0}", exception);
            }
        }
    }
}

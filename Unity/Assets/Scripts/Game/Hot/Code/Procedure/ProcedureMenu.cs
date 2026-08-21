//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework.Event;
using GameFramework.Fsm;
using UnityGameFramework.Runtime;

namespace Game.Hot
{
    public class ProcedureMenu : ProcedureBase
    {
        private bool m_StartGame = false;
        private FairyDemoForm m_FairyDemoForm = null;

        public void StartGame()
        {
            m_StartGame = true;
        }

        protected override void OnEnter(IFsm<ProcedureComponent> procedureOwner)
        {
            base.OnEnter(procedureOwner);

            GameEntry.Event.Subscribe(OpenUIFormSuccessEventArgs.EventId, OnOpenUIFormSuccess);

            m_StartGame = false;
            GameEntry.UI.OpenUIForm(UIFormId.FairyDemoForm, this);
        }

        protected override void OnLeave(IFsm<ProcedureComponent> procedureOwner, bool isShutdown)
        {
            if (!isShutdown)
            {
                GameEntry.Event.Unsubscribe(OpenUIFormSuccessEventArgs.EventId, OnOpenUIFormSuccess);

                if (m_FairyDemoForm != null)
                {
                    m_FairyDemoForm.Close();
                    m_FairyDemoForm = null;
                }
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

        private void OnOpenUIFormSuccess(object sender, GameEventArgs e)
        {
            OpenUIFormSuccessEventArgs ne = (OpenUIFormSuccessEventArgs)e;
            if (ne.UserData != this)
            {
                return;
            }

            m_FairyDemoForm = (FairyDemoForm)ne.UIForm.Logic;
        }
    }
}

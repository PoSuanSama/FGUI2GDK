using Cysharp.Threading.Tasks;
using UnityGameFramework.Runtime;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

namespace Game
{
    public class ProcedurePreload : ProcedureBase
    {
        protected override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);
            
            PreloadAsync(procedureOwner).Forget();
        }

        private async UniTaskVoid PreloadAsync(ProcedureOwner procedureOwner)
        {
            Log.Info("Start load Game Tables!");
            await GameEntry.Tables.LoadAllAsync();
            Log.Info("Finish load Game Tables!");
            
            Log.Info("Start load Localization!");
            await GameEntry.Localization.LoadLanguageAsync(GameEntry.Localization.Language);
            Log.Info("Finish load Localization!");

#if UNITY_HOTFIX && ENABLE_IL2CPP
            await HybridCLRHelper.LoadAsync();
#endif
#if UNITY_EDITOR
            Check();
#endif
            ChangeState<ProcedurePreset>(procedureOwner);
        }

        protected override void OnDestroy(ProcedureOwner procedureOwner)
        {
            base.OnDestroy(procedureOwner);
        }

#if UNITY_EDITOR
        private void Check()
        {
            // 双符号模式下 GameHot 流程启动时 GameEntry.Tables 可能尚未就绪,防御性
            // 跳过检查,避免 DTEntity.DataList 空引用。
            if (GameEntry.Tables == null ||
                GameEntry.Tables.DTEntity == null ||
                GameEntry.Tables.DTEntity.DataList == null)
            {
                return;
            }

            foreach (var drEntity in GameEntry.Tables.DTEntity.DataList)
            {
                GameFramework.Entity.IEntityGroup entityGroup = GameEntry.Entity.GetEntityGroup(drEntity.EntityGroupName);
                if (entityGroup == null)
                {
                    Log.Error(GameFramework.Utility.Text.Format("DREntity '{0}' - entity group '{1}' is not exist.", drEntity.AssetName, drEntity.EntityGroupName));
                }
            }
        }
#endif
    }
}

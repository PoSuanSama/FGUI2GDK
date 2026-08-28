using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

namespace Game
{
    public class ProcedurePreset : ProcedureBase
    {
        protected override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);
            
            // UGUI 全局按钮音效已随 ExButton 移除;FairyGUI 按钮音效由 FairySound 映射处理。
            
#if UNITY_ET
            ChangeState<ProcedureET>(procedureOwner);
#elif UNITY_GAMEHOT
            ChangeState<ProcedureGameHot>(procedureOwner);
#else
            ChangeState<ProcedureGame>(procedureOwner);
#endif
        }
    }
}

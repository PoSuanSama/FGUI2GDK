using Cysharp.Threading.Tasks;
using GameFramework.Fsm;
using UnityEngine;
using UnityGameFramework.Extension;
using UnityGameFramework.Runtime;

namespace Game.Hot
{
    public class ProcedurePreload : ProcedureBase
    {
        protected override void OnEnter(IFsm<ProcedureComponent> procedureOwner)
        {
            base.OnEnter(procedureOwner);
            PreloadAsync(procedureOwner).Forget();
        }

        private async UniTaskVoid PreloadAsync(IFsm<ProcedureComponent> procedureOwner)
        {
            await HotEntry.Tables.LoadAllAsync();
            Log.Info("Game.Hot.Code Load Config!");
            
            ChangeState<ProcedureGame>(procedureOwner);
        }

    }
}

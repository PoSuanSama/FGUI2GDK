using GameFramework.UI;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace Game
{
    /// <summary>
    /// Migration UI form helper: a descriptor TextAsset routes to the FairyGUI host; a GameObject
    /// still routes to the legacy prefab path.
    /// </summary>
    public class GDKUIFormHelper : UIFormHelperBase
    {
        public override object InstantiateUIForm(object uiFormAsset)
        {
            if (uiFormAsset is TextAsset descriptorAsset)
            {
                GameObject host = new GameObject(descriptorAsset.name);
                host.hideFlags = HideFlags.HideInHierarchy;
                FairyUIFormHost hostComponent = host.AddComponent<FairyUIFormHost>();
                hostComponent.DescriptorKey = descriptorAsset.name;
                return host;
            }

            return Instantiate((Object)uiFormAsset);
        }

        public override IUIForm CreateUIForm(object uiFormInstance, IUIGroup uiGroup, object userData)
        {
            GameObject gameObject = uiFormInstance as GameObject;
            if (gameObject == null)
            {
                Log.Error("UI form instance is invalid.");
                return null;
            }

            Transform transform = gameObject.transform;
            transform.SetParent(((MonoBehaviour)uiGroup.Helper).transform);
            transform.localScale = Vector3.one;

            FairyUIFormHost host = gameObject.GetComponent<FairyUIFormHost>();
            if (host != null)
            {
                UIForm uiForm = gameObject.GetComponent<UIForm>();
                bool isPooledInstance = uiForm != null;
                uiForm ??= gameObject.AddComponent<UIForm>();
                FairyUIFormLogic logic = gameObject.GetOrAddComponent<FairyUIFormLogic>();
                if (isPooledInstance)
                {
                    FairyUIFormPreparedState preparedState =
                        FairyUIFormPreparedRegistry.ConsumePooledInstance(
                            host.DescriptorKey,
                            userData);
                    try
                    {
                        logic.Adopt(preparedState, uiGroup);
                    }
                    catch
                    {
                        preparedState.Dispose();
                        throw;
                    }
                }

                return uiForm;
            }

            return gameObject.GetOrAddComponent<UIForm>();
        }

        public override void ReleaseUIForm(object uiFormAsset, object uiFormInstance)
        {
            if (uiFormAsset is Object asset)
            {
                GameEntry.Resource.UnloadAsset(asset);
            }

            if (uiFormInstance is Object instance)
            {
                Destroy(instance);
            }
        }
    }
}

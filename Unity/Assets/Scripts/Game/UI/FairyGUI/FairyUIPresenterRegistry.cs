using System;

namespace Game
{
    /// <summary>
    /// Hot-update side injects package binding and presenter factories here.
    /// </summary>
    public static class FairyUIPresenterRegistry
    {
        /// <summary>
        /// Called before creating the GComponent so the hot-update side can register package item extensions.
        /// </summary>
        public static Action<FairyUIFormDescriptor> PreparePackage;

        /// <summary>
        /// Called after creating the GComponent; must return a presenter for the descriptor.
        /// </summary>
        public static Func<FairyUIFormDescriptor, IFairyUIPresenter> CreatePresenter;
    }
}
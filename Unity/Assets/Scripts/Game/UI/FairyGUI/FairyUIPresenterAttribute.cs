using System;

namespace Game
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class FairyUIPresenterAttribute : Attribute
    {
        public FairyUIPresenterAttribute(int uiFormId)
        {
            UiFormId = uiFormId;
        }

        public int UiFormId { get; }
    }
}

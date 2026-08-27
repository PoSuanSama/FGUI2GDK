using System;

namespace Game.Hot
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class FairyUIPresenterAttribute : Attribute
    {
        public FairyUIPresenterAttribute(string csName)
        {
            CsName = csName;
        }

        public string CsName { get; }
    }
}
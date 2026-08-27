using FairyGUI;
using GameFramework;

namespace Game
{
    public sealed class FairyGuideStep : IReference
    {
        public string Id { get; private set; }
        public string Text { get; private set; }
        public GObject Target { get; private set; }
        public GComponent Content { get; private set; }

        public static FairyGuideStep Create(string id, string text, GObject target = null, GComponent content = null)
        {
            FairyGuideStep step = ReferencePool.Acquire<FairyGuideStep>();
            step.Id = id;
            step.Text = text;
            step.Target = target;
            step.Content = content;
            return step;
        }

        public void Clear()
        {
            Id = null;
            Text = null;
            Target = null;
            if (Content != null)
            {
                Content.Dispose();
                Content = null;
            }
        }

        public void Dispose()
        {
            ReferencePool.Release(this);
        }
    }
}

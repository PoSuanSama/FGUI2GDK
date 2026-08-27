using FairyGUI;

namespace Game
{
    public sealed class FairyGuideStep
    {
        public FairyGuideStep(string id, string text, GObject target = null, GComponent content = null)
        {
            Id = id;
            Text = text;
            Target = target;
            Content = content;
        }

        public string Id { get; }

        public string Text { get; }

        public GObject Target { get; }

        public GComponent Content { get; }
    }
}

namespace Game
{
    /// <summary>
    /// Keeps the framework-created UI group GameObject as the shared FairyGUI group host.
    /// </summary>
    public sealed class GDKUIGroupHelper : UGuiGroupHelper
    {
        public override void SetDepth(int depth)
        {
            base.SetDepth(depth);
            FairyUIRootService.Instance.SetGroupDepth(this, depth);
        }

        private void OnDestroy()
        {
            FairyUIRootService.Instance.ReleaseGroup(this);
        }
    }
}

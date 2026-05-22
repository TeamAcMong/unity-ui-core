namespace DreamTech.UICore.Animations.Backends
{
    /// <summary>
    /// Service locator nhẹ cho IAnimationBackend.
    /// Default = UniTaskAnimationBackend (lazy-initialized).
    /// Override tại Bootstrap để dùng custom backend (DOTween wrapper, v.v.):
    /// <code>
    /// AnimationBackendRegistry.Current = new DOTweenAnimationBackend();
    /// </code>
    /// </summary>
    public static class AnimationBackendRegistry
    {
        private static IAnimationBackend _current;

        /// <summary>
        /// Backend hiện tại. Lazy-init thành UniTaskAnimationBackend nếu chưa được set.
        /// Set tại Bootstrap trước khi bất kỳ UI component nào khởi động.
        /// </summary>
        public static IAnimationBackend Current
        {
            get => _current ??= new UniTaskAnimationBackend();
            set => _current = value;
        }
    }
}

namespace DreamTech.UICore.Animations.Backends
{
    /// <summary>
    /// Service locator nhẹ cho IAnimationBackend.
    /// Default = <see cref="DefaultAnimationBackend"/> (lazy-initialized, không cần package ngoài).
    /// Override tại Bootstrap để dùng backend khác (UniTaskAnimationBackend khi project có UniTask, DOTween wrapper, v.v.):
    /// <code>
    /// AnimationBackendRegistry.Current = new DOTweenAnimationBackend();
    /// </code>
    /// </summary>
    public static class AnimationBackendRegistry
    {
        private static IAnimationBackend _current;

        /// <summary>
        /// Backend hiện tại. Lazy-init thành <see cref="DefaultAnimationBackend"/> nếu chưa được set.
        /// Set tại Bootstrap trước khi bất kỳ UI component nào khởi động.
        /// </summary>
        public static IAnimationBackend Current
        {
            get => _current ??= new DefaultAnimationBackend();
            set => _current = value;
        }
    }
}

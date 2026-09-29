namespace DreamTech.UICore.Animations.Backends
{
    /// <summary>
    /// Backend chạy được tween theo một đồng hồ khác đồng hồ chung của nó (theo <c>Time.timeScale</c> hay theo giờ thật). Module
    /// cần UI phản hồi đúng nhịp cả lúc game slow-motion / pause (vd <see cref="Modules.PressScaleModule"/>) hỏi qua đây; backend
    /// không implement thì module chạy theo đồng hồ chung của backend.
    /// </summary>
    public interface ITimeModeAnimationBackend : IAnimationBackend
    {
        /// <summary>Backend (có thể chính nó) chạy tween theo <paramref name="mode"/>.</summary>
        IAnimationBackend WithTimeMode(AnimationTimeMode mode);
    }
}

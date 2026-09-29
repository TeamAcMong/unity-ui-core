namespace DreamTech.UICore.Animations.Backends
{
    /// <summary>Đồng hồ mà backend dùng để chạy tween.</summary>
    public enum AnimationTimeMode
    {
        /// <summary><c>Time.deltaTime</c> — chậm/dừng theo <c>Time.timeScale</c>.</summary>
        Scaled = 0,

        /// <summary><c>Time.unscaledDeltaTime</c> — giờ thật, không bị slow-motion hay pause ảnh hưởng.</summary>
        Unscaled = 1,
    }
}

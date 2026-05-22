using System;

namespace DreamTech.UICore.Animations.Backends
{
    /// <summary>
    /// Token trả về cho animation đang chạy.
    /// Cho phép Stop, check trạng thái, và chain callback OnComplete.
    /// </summary>
    public interface IAnimationHandle
    {
        /// <summary>True khi animation đang chạy (chưa complete và chưa bị stop).</summary>
        bool IsPlaying { get; }

        /// <summary>True khi animation đã chạy xong tự nhiên (không bị cancel).</summary>
        bool IsCompleted { get; }

        /// <summary>Dừng animation ngay lập tức. Target sẽ được restore về original value nếu backend hỗ trợ.</summary>
        void Stop();

        /// <summary>
        /// Đăng ký callback chạy khi animation complete (tự nhiên, không phải cancel).
        /// Nếu đã complete khi gọi thì callback được invoke ngay.
        /// </summary>
        IAnimationHandle OnComplete(Action callback);
    }
}

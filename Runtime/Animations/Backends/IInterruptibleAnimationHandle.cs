namespace DreamTech.UICore.Animations.Backends
{
    /// <summary>
    /// Handle dừng được theo kiểu "ngắt, giữ nguyên chỗ": target đứng ở giá trị đang có, KHÔNG trả về <c>from</c> như
    /// <see cref="IAnimationHandle.Stop"/>.
    /// <para>
    /// Dùng khi một animation mới sắp nối tiếp từ đúng chỗ animation cũ dừng — ví dụ nút đang co (Pressed) thì người chơi
    /// nhả tay: animation nhả phải bắt đầu từ cỡ đang co dở, không giật về cỡ gốc rồi mới chạy.
    /// </para>
    /// Interface riêng (không thêm vào <see cref="IAnimationHandle"/>) để backend tự viết không bị vỡ.
    /// </summary>
    public interface IInterruptibleAnimationHandle : IAnimationHandle
    {
        /// <summary>Dừng ngay, giữ target ở giá trị hiện tại. OnComplete không được gọi.</summary>
        void Interrupt();
    }
}

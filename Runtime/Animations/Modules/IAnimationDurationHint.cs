namespace DreamTech.UICore.Animations.Modules
{
    /// <summary>
    /// Tuỳ chọn cho animation module: tự báo mình chạy bao lâu khi chuyển sang một state. Bảng xem trước của editor dùng để biết
    /// lúc nào xong. Module kế thừa <see cref="AnimationModuleBase"/> đã có sẵn <c>duration</c> nên không cần; module có thời lượng
    /// khác nhau theo state (vd <see cref="PressScaleModule"/>: nhấn nhanh, nhả chậm) thì nên implement.
    /// </summary>
    public interface IAnimationDurationHint
    {
        /// <summary>Số giây animation chạy khi component chuyển sang <paramref name="state"/>.</summary>
        float GetDuration(UIState state);
    }
}

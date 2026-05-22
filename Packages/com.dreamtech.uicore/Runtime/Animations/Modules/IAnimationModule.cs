using DreamTech.UICore.Animations.Backends;
using UnityEngine;

namespace DreamTech.UICore.Animations.Modules
{
    /// <summary>
    /// Plug-in animation module. Implement interface này, mark class với [System.Serializable],
    /// và class sẽ tự xuất hiện trong Inspector dropdown của [SerializeReference, SubclassSelector] List.
    /// Không cần đăng ký ở đâu — Editor drawer tự discover qua reflection.
    /// </summary>
    public interface IAnimationModule
    {
        /// <summary>
        /// Tên hiển thị trong Inspector dropdown.
        /// Override để có friendly name thay vì class name đầy đủ.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Module có được enable không. Khi false, Play() không chạy animation.
        /// Designer toggle qua Inspector checkbox.
        /// </summary>
        bool Enabled { get; }

        /// <summary>
        /// Cache initial value từ target. Gọi 1 lần khi component Awake/Start.
        /// Module nên lưu giá trị vào internal field để dùng làm baseline cho UIState.Normal.
        /// Ví dụ ScaleModule lưu target.localScale, ColorModule lưu graphic.color.
        /// </summary>
        /// <param name="target">MonoBehaviour chứa component cần animate (button, toggle, v.v.).</param>
        void CaptureInitialValue(MonoBehaviour target);

        /// <summary>
        /// Play animation chuyển sang state mới. Backend abstraction cho phép swap UniTask/DOTween.
        /// </summary>
        /// <param name="target">MonoBehaviour host cung cấp lifecycle context.</param>
        /// <param name="newState">State UI element vừa chuyển sang.</param>
        /// <param name="backend">Backend để tạo tween. Thường là AnimationBackendRegistry.Current.</param>
        /// <returns>
        /// IAnimationHandle để track/stop/chain, hoặc null nếu module skip state này.
        /// Caller không cần null-check — null handle được bỏ qua silently.
        /// </returns>
        IAnimationHandle Play(MonoBehaviour target, UIState newState, IAnimationBackend backend);
    }
}

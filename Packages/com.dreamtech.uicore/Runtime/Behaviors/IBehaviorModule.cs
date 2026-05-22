using DreamTech.UICore.Animations;
using DreamTech.UICore.Base;

namespace DreamTech.UICore.Behaviors
{
    /// <summary>
    /// Plug-in behavior module. Hook vào click flow của <see cref="InteractiveUIComponent"/>.
    /// User custom: implement interface, mark class với <c>[Serializable]</c> và auto xuất hiện
    /// trong Inspector dropdown của <c>[SerializeReference, SubclassSelector]</c> List.
    /// Pattern đối xứng với <see cref="DreamTech.UICore.Animations.Modules.IAnimationModule"/> —
    /// designer add/remove behaviors qua Inspector mà không cần đăng ký ở đâu.
    /// </summary>
    public interface IBehaviorModule
    {
        /// <summary>Tên hiển thị trong Inspector dropdown.</summary>
        string DisplayName { get; }

        /// <summary>Behavior có được enable không. Khi false, hooks không chạy.</summary>
        bool Enabled { get; }

        /// <summary>Gọi 1 lần khi host Awake. Lưu host reference vào field internal.</summary>
        /// <param name="host">InteractiveUIComponent chứa behavior module.</param>
        void Initialize(InteractiveUIComponent host);

        /// <summary>
        /// Gọi TRƯỚC khi click event fire. Return false để CANCEL click
        /// (ví dụ: cooldown chưa xong, double-click chưa đủ).
        /// </summary>
        /// <returns>true = cho phép click fire; false = block click.</returns>
        bool OnBeforeClick();

        /// <summary>Gọi SAU khi click đã pass tất cả guards và OnInteract đã chạy.</summary>
        void OnAfterClick();

        /// <summary>Hook vào state change (Normal/Hover/Pressed/Disabled/Selected).</summary>
        /// <param name="newState">State mà host vừa chuyển sang.</param>
        void OnPointerStateChanged(UIState newState);

        /// <summary>Cleanup khi host bị destroy. Cancel pending UniTask, unsubscribe events.</summary>
        void Dispose();
    }
}

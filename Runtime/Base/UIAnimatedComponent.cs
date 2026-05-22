using System.Collections.Generic;
using DreamTech.UICore.Animations;
using DreamTech.UICore.Animations.Backends;
using DreamTech.UICore.Animations.Events;
using DreamTech.UICore.Animations.Modules;
using UnityEngine;

namespace DreamTech.UICore.Base
{
    /// <summary>
    /// Base class cho UI component có animation modular.
    /// Designer add/remove animation modules qua Inspector List với SubclassSelector dropdown.
    /// Component cụ thể (AnimatedButton, AnimatedToggle, ...) kế thừa class này và gọi
    /// <see cref="PlayAnimationsForState"/> khi state thay đổi.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class UIAnimatedComponent : MonoBehaviour
    {
        [Header("Animation Modules")]
        [Tooltip("Add animation modules from dropdown. Custom modules tự xuất hiện nếu implement IAnimationModule và mark [Serializable].")]
        [SerializeReference, SubclassSelector]
        protected List<IAnimationModule> animationModules = new List<IAnimationModule>();

        [Header("Animation Events")]
        [SerializeField] protected AnimationEventHooks animationEvents = new AnimationEventHooks();

        /// <summary>RectTransform của component. Null nếu không phải RectTransform (rare trong UI).</summary>
        protected RectTransform rectTransform;

        /// <summary>CanvasGroup trên cùng GameObject. Null nếu không có — CanvasGroup là optional.</summary>
        protected CanvasGroup canvasGroup;

        // Active handles per state-change để stop animation cũ khi state mới đến.
        // private để đảm bảo chỉ PlayAnimationsForState/StopActiveAnimations quản lý list này.
        private readonly List<IAnimationHandle> _activeHandles = new List<IAnimationHandle>();

        /// <summary>
        /// Cache component refs và gọi <see cref="CaptureInitialValues"/> cho tất cả modules.
        /// Subclass override nên gọi <c>base.Awake()</c> trước.
        /// </summary>
        protected virtual void Awake()
        {
            rectTransform = transform as RectTransform;
            // CanvasGroup là optional — không dùng RequireComponent để tránh force thêm component
            // lên những UI element không cần fade (ProgressBar fill, CooldownButton icon, v.v.)
            canvasGroup = GetComponent<CanvasGroup>();
            CaptureInitialValues();
        }

        /// <summary>
        /// Gọi <see cref="IAnimationModule.CaptureInitialValue"/> cho mọi module.
        /// Override để cache thêm refs riêng của component, nhưng nhớ gọi <c>base.CaptureInitialValues()</c>.
        /// </summary>
        protected virtual void CaptureInitialValues()
        {
            foreach (var module in animationModules)
                module?.CaptureInitialValue(this);
        }

        /// <summary>
        /// Gọi khi state UI thay đổi (Normal → Hover, Pressed, Disabled, ...).
        /// Dừng mọi animation đang chạy, sau đó play tất cả modules đã enable cho state mới.
        /// <para>
        /// Modules mà <see cref="IAnimationModule.Play"/> trả <c>null</c> (do triggerOnState mismatch
        /// hoặc logic nội bộ) sẽ bị bỏ qua — không được thêm vào _activeHandles, không ảnh hưởng
        /// đến việc fire <see cref="AnimationEventHooks.InvokeComplete"/>.
        /// </para>
        /// </summary>
        /// <param name="newState">State mà component vừa chuyển sang.</param>
        protected void PlayAnimationsForState(UIState newState)
        {
            StopActiveAnimations();

            if (animationModules.Count == 0)
            {
                // Không có module nào — vẫn fire Complete để event chain không bị treo.
                animationEvents.InvokeStart();
                animationEvents.InvokeComplete();
                return;
            }

            var backend = AnimationBackendRegistry.Current;
            animationEvents.InvokeStart();

            // Pass 1: collect all valid handles first, then register OnComplete callbacks.
            // Lý do tách 2 pass: nếu 1 pass duy nhất, handle đầu tiên có thể complete ngay lập tức
            // (synchronous backend hoặc duration=0) trước khi loop add đủ handles vào list —
            // completedCount >= totalCount sẽ fire sớm và InvokeComplete bị gọi nhiều lần.
            var newHandles = new List<IAnimationHandle>();
            foreach (var module in animationModules)
            {
                if (module == null || !module.Enabled) continue;
                var handle = module.Play(this, newState, backend);
                // null handle = module bỏ qua state này (triggerOnState mismatch, v.v.)
                if (handle == null) continue;
                newHandles.Add(handle);
                _activeHandles.Add(handle);
            }

            int totalCount = newHandles.Count;

            // Không có module nào thực sự chạy (tất cả trả null) → fire Complete ngay.
            if (totalCount == 0)
            {
                animationEvents.InvokeComplete();
                return;
            }

            // Pass 2: register OnComplete với totalCount đã biết chính xác.
            // Capture handle in local var so the closure removes the right one (not the loop var).
            int completedCount = 0;
            foreach (var handle in newHandles)
            {
                var capturedHandle = handle;
                capturedHandle.OnComplete(() =>
                {
                    completedCount++;
                    // Remove completed handle from _activeHandles so future StopActiveAnimations
                    // doesn't call Stop() on a handle whose CancellationTokenSource is already
                    // disposed (UniTask backend disposes CTS in MarkCompleted).
                    _activeHandles.Remove(capturedHandle);
                    if (completedCount >= totalCount)
                        animationEvents.InvokeComplete();
                });
            }
        }

        /// <summary>
        /// Dừng tất cả animation đang chạy và clear danh sách handles.
        /// Tự động gọi ở đầu <see cref="PlayAnimationsForState"/> và trong <see cref="OnDisable"/>/<see cref="OnDestroy"/>.
        /// </summary>
        protected void StopActiveAnimations()
        {
            foreach (var handle in _activeHandles)
                handle?.Stop();
            _activeHandles.Clear();
        }

        /// <summary>Dừng animation khi component bị disable (scene transition, pooling, v.v.).</summary>
        protected virtual void OnDisable()
        {
            StopActiveAnimations();
        }

        /// <summary>Cleanup khi component bị destroy — tránh zombie tween tiếp tục chạy sau khi object đã bị destroy.</summary>
        protected virtual void OnDestroy()
        {
            StopActiveAnimations();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public API — runtime module management
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Thêm animation module vào list và cache initial value ngay lập tức.
        /// Dùng để add module programmatically ở runtime (ví dụ từ tutorial system).
        /// </summary>
        /// <param name="module">Module cần thêm. Null sẽ bị bỏ qua silently.</param>
        public void AddModule(IAnimationModule module)
        {
            if (module == null) return;
            animationModules.Add(module);
            module.CaptureInitialValue(this);
        }

        /// <summary>
        /// Xóa tất cả modules thuộc type <typeparamref name="T"/> khỏi list.
        /// Animation đang chạy KHÔNG bị stop — gọi <see cref="StopActiveAnimations"/> trước nếu cần.
        /// </summary>
        /// <typeparam name="T">Type của module cần xóa. Phải implement <see cref="IAnimationModule"/>.</typeparam>
        /// <returns>Số module đã xóa.</returns>
        public int RemoveModulesOfType<T>() where T : IAnimationModule
        {
            return animationModules.RemoveAll(m => m is T);
        }

        /// <summary>
        /// Xóa toàn bộ modules. Stop mọi animation đang chạy trước khi clear.
        /// </summary>
        public void ClearModules()
        {
            StopActiveAnimations();
            animationModules.Clear();
        }

        /// <summary>
        /// Lấy module đầu tiên thuộc type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Type của module. Phải implement <see cref="IAnimationModule"/>.</typeparam>
        /// <returns>Module đầu tiên tìm được, hoặc <c>null</c> nếu không có.</returns>
        public T GetModule<T>() where T : class, IAnimationModule
        {
            foreach (var m in animationModules)
                if (m is T t) return t;
            return null;
        }
    }
}

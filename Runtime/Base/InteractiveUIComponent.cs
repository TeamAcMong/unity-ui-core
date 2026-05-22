using System.Collections.Generic;
using DreamTech.UICore.Animations;
using DreamTech.UICore.Animations.Modules;
using DreamTech.UICore.Behaviors;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DreamTech.UICore.Base
{
    /// <summary>
    /// Base class cho component có pointer interaction + behavior modules.
    /// Subclass override <see cref="OnInteract"/> để xử lý click event (fire UnityEvent, toggle, ...).
    /// <para>
    /// Behavior modules (Cooldown, LongPress, MultiClick, ...) hook vào click flow qua
    /// <see cref="IBehaviorModule.OnBeforeClick"/> (gate) và <see cref="IBehaviorModule.OnAfterClick"/>
    /// (post-action). Pattern đối xứng với animation modules trên <see cref="UIAnimatedComponent"/>.
    /// </para>
    /// </summary>
    public abstract class InteractiveUIComponent : UIAnimatedComponent,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler,
        IPointerClickHandler
    {
        [Header("Interaction")]
        [SerializeField] protected bool interactable = true;

        [Tooltip("Anti-double-click guard (giây, unscaled). 0 = không guard.")]
        [SerializeField, Range(0f, 1f)] protected float clickCooldown = 0.1f;

        [Header("Behavior Modules")]
        [Tooltip("Add behaviors (Cooldown, LongPress, MultiClick, ...) qua dropdown. Custom modules tự xuất hiện nếu implement IBehaviorModule và mark [Serializable].")]
        [SerializeReference, SubclassSelector]
        protected List<IBehaviorModule> behaviorModules = new List<IBehaviorModule>();

        protected UIState currentUIState = UIState.Normal;
        protected bool isPointerInside;
        protected bool isPointerDown;
        protected float lastClickTime = -999f;

        public bool IsInteractable => interactable;
        public UIState CurrentState => currentUIState;
        public IReadOnlyList<IBehaviorModule> Behaviors => behaviorModules;

        /// <summary>
        /// Initialize behavior modules và set initial UIState.
        /// Subclass override nên gọi <c>base.Awake()</c> trước (hoặc set toggle/internal state TRƯỚC
        /// nếu cần initial state khác Normal — vì <see cref="ComputeStateForInteractable"/> sẽ được gọi).
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            for (int i = 0; i < behaviorModules.Count; i++)
                behaviorModules[i]?.Initialize(this);
            UIState initial = interactable ? ComputeStateForInteractable() : UIState.Disabled;
            // ApplyState skip nếu equal → force set field để initial state đúng (Normal là default).
            currentUIState = initial;
            // animate:false → không chạy animation lúc Awake (giữ behavior cũ).
        }

        /// <summary>Dispose tất cả behaviors trước khi base cleanup animation.</summary>
        protected override void OnDestroy()
        {
            for (int i = 0; i < behaviorModules.Count; i++)
                behaviorModules[i]?.Dispose();
            base.OnDestroy();
        }

        /// <summary>
        /// Set interactable flag. Khi false → state chuyển sang Disabled.
        /// Khi true → recompute state dựa trên pointer (Normal/Hover/Pressed/Selected).
        /// </summary>
        public virtual void SetInteractable(bool value)
        {
            if (interactable == value) return;
            interactable = value;
            ApplyState(value ? ComputeStateForInteractable() : UIState.Disabled);
        }

        /// <summary>
        /// Override để custom UIState mapping (ví dụ Toggle thêm Selected khi On).
        /// Default: trả về Pressed/Hover/Normal dựa trên pointer flags.
        /// </summary>
        protected virtual UIState ComputeStateForInteractable()
        {
            if (isPointerDown) return UIState.Pressed;
            if (isPointerInside) return UIState.Hover;
            return UIState.Normal;
        }

        /// <summary>Subclass implement: xử lý click action (fire onClick, toggle, ...).</summary>
        protected abstract void OnInteract();

        // ─────────────────────────────────────────────────────────────────────
        // Pointer event handlers
        // ─────────────────────────────────────────────────────────────────────

        public virtual void OnPointerEnter(PointerEventData eventData)
        {
            isPointerInside = true;
            if (!interactable) return;
            if (!isPointerDown) ApplyState(UIState.Hover);
        }

        public virtual void OnPointerExit(PointerEventData eventData)
        {
            isPointerInside = false;
            if (!interactable) return;
            if (!isPointerDown) ApplyState(ComputeStateForInteractable());
        }

        public virtual void OnPointerDown(PointerEventData eventData)
        {
            if (!interactable) return;
            isPointerDown = true;
            ApplyState(UIState.Pressed);
        }

        public virtual void OnPointerUp(PointerEventData eventData)
        {
            if (!interactable) return;
            isPointerDown = false;
            ApplyState(ComputeStateForInteractable());
        }

        public virtual void OnPointerClick(PointerEventData eventData)
        {
            if (!interactable) return;
            if (Time.unscaledTime - lastClickTime < clickCooldown) return;
            lastClickTime = Time.unscaledTime;

            // Run behavior guards — any returns false cancels click.
            for (int i = 0; i < behaviorModules.Count; i++)
            {
                var b = behaviorModules[i];
                if (b == null || !b.Enabled) continue;
                if (!b.OnBeforeClick()) return;
            }

            OnInteract();

            for (int i = 0; i < behaviorModules.Count; i++)
            {
                var b = behaviorModules[i];
                if (b == null || !b.Enabled) continue;
                b.OnAfterClick();
            }
        }

        /// <summary>
        /// Set internal state và play animations (nếu khác current).
        /// Notify behavior modules về state change.
        /// </summary>
        protected void ApplyState(UIState newState, bool animate = true)
        {
            if (currentUIState == newState) return;
            currentUIState = newState;

            for (int i = 0; i < behaviorModules.Count; i++)
            {
                var b = behaviorModules[i];
                if (b == null || !b.Enabled) continue;
                b.OnPointerStateChanged(newState);
            }

            if (animate) PlayAnimationsForState(newState);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public behavior API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Lấy behavior module đầu tiên thuộc type <typeparamref name="T"/>.</summary>
        public T GetBehavior<T>() where T : class, IBehaviorModule
        {
            for (int i = 0; i < behaviorModules.Count; i++)
                if (behaviorModules[i] is T t) return t;
            return null;
        }

        /// <summary>Add behavior module ở runtime và initialize ngay lập tức.</summary>
        public void AddBehavior(IBehaviorModule behavior)
        {
            if (behavior == null) return;
            behaviorModules.Add(behavior);
            behavior.Initialize(this);
        }

        /// <summary>Remove behavior modules thuộc type <typeparamref name="T"/>, dispose từng cái.</summary>
        public int RemoveBehaviorsOfType<T>() where T : IBehaviorModule
        {
            int removed = 0;
            for (int i = behaviorModules.Count - 1; i >= 0; i--)
            {
                if (behaviorModules[i] is T)
                {
                    behaviorModules[i]?.Dispose();
                    behaviorModules.RemoveAt(i);
                    removed++;
                }
            }
            return removed;
        }
    }
}

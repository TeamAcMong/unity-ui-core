using DreamTech.UICore.Animations;
using DreamTech.UICore.Base;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DreamTech.UICore.Buttons
{
    public enum ToggleState
    {
        Off,
        On,
    }

    /// <summary>
    /// Animated toggle với 2 stable state (On/Off). Click chuyển state.
    /// Optional sync với Unity Toggle component qua <see cref="linkedToggle"/>.
    /// <para>
    /// State mapping: ToggleState.Off → <see cref="offState"/> (default Normal),
    /// ToggleState.On → <see cref="onState"/> (default Selected).
    /// Override <see cref="ComputeStateForInteractable"/> để custom thêm.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class AnimatedToggle : InteractiveUIComponent
    {
        [Header("Toggle Settings")]
        [SerializeField] private ToggleState initialState = ToggleState.Off;
        [SerializeField] private Toggle linkedToggle;

        [Header("State Mapping")]
        [Tooltip("UIState khi ToggleState = Off (default: Normal)")]
        [SerializeField] private UIState offState = UIState.Normal;
        [Tooltip("UIState khi ToggleState = On (default: Selected)")]
        [SerializeField] private UIState onState = UIState.Selected;

        [Header("Events")]
        public UnityEvent<bool> onValueChanged = new();

        private ToggleState _currentToggleState;

        public ToggleState CurrentToggleState => _currentToggleState;
        public bool IsOn => _currentToggleState == ToggleState.On;

        protected override void Awake()
        {
            // Set toggle state TRƯỚC base.Awake() để base's ComputeStateForInteractable trả về đúng mapping.
            _currentToggleState = initialState;
            base.Awake();
            if (linkedToggle != null)
            {
                linkedToggle.isOn = IsOn;
                linkedToggle.onValueChanged.AddListener(OnLinkedToggleChanged);
            }
        }

        protected override void OnDestroy()
        {
            if (linkedToggle != null)
                linkedToggle.onValueChanged.RemoveListener(OnLinkedToggleChanged);
            base.OnDestroy();
        }

        private void OnLinkedToggleChanged(bool value)
        {
            SetToggleState(value ? ToggleState.On : ToggleState.Off, fireEvent: false);
        }

        protected override void OnInteract() => Toggle();

        /// <summary>Flip toggle state. Fires <see cref="onValueChanged"/>.</summary>
        public void Toggle() => SetToggleState(IsOn ? ToggleState.Off : ToggleState.On);

        /// <summary>Set toggle state programmatically.</summary>
        public void SetToggleState(ToggleState state, bool animate = true, bool fireEvent = true)
        {
            if (_currentToggleState == state) return;
            _currentToggleState = state;
            if (linkedToggle != null) linkedToggle.SetIsOnWithoutNotify(IsOn);
            ApplyState(ComputeStateForInteractable(), animate);
            if (fireEvent) onValueChanged?.Invoke(IsOn);
        }

        /// <summary>
        /// Force re-trigger animation cho state hiện tại (bypass equality guard).
        /// </summary>
        public void ForceTriggerAnimation() => PlayAnimationsForState(currentUIState);

        /// <summary>Override mapping: thêm Selected state khi toggle On.</summary>
        protected override UIState ComputeStateForInteractable()
        {
            if (isPointerDown) return UIState.Pressed;
            if (isPointerInside) return UIState.Hover;
            return IsOn ? onState : offState;
        }
    }
}

using DreamTech.UICore.Animations;
using DreamTech.UICore.Base;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace DreamTech.UICore.Buttons
{
    /// <summary>
    /// Animated push button. Single-action control — click fires <see cref="onClick"/> UnityEvent.
    /// <para>
    /// Animation modules cho visual feedback per state (hover/press/disabled).
    /// Behavior modules cho gating (Cooldown, LongPress, MultiClick) — orthogonal với animation.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class AnimatedButton : InteractiveUIComponent
    {
        [Header("Audio (optional)")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip hoverSound;
        [SerializeField] private AudioClip clickSound;

        [Header("Click Event")]
        public UnityEvent onClick = new();

        protected override void Awake()
        {
            base.Awake();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            if (interactable && !isPointerDown) PlaySound(hoverSound);
        }

        protected override void OnInteract()
        {
            PlaySound(clickSound);
            onClick?.Invoke();
        }

        /// <summary>
        /// Force re-trigger animation cho state hiện tại (bypass equality guard).
        /// Hữu ích cho Editor play-mode test hoặc re-flash animation.
        /// </summary>
        public void ForceTriggerAnimation() => PlayAnimationsForState(currentUIState);

        /// <summary>
        /// Reset về Normal state (hoặc Disabled nếu !interactable). Stop active animations.
        /// </summary>
        public void ResetButton()
        {
            isPointerInside = false;
            isPointerDown = false;
            StopActiveAnimations();
            UIState target = interactable ? UIState.Normal : UIState.Disabled;
            // ApplyState chỉ animate khi state thay đổi — bypass guard bằng force re-trigger.
            if (currentUIState != target)
            {
                ApplyState(target);
            }
            else
            {
                PlayAnimationsForState(target);
            }
        }

        private void PlaySound(AudioClip clip)
        {
            if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
        }
    }
}

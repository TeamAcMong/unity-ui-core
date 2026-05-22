using System;
using UnityEngine;
using UnityEngine.UI;
using DreamTech.UICore.Animations.Backends;

namespace DreamTech.UICore.Animations.Modules
{
    /// <summary>
    /// Animates alpha (opacity) between state-specific values.
    /// <para>
    /// <b>Priority:</b> <see cref="CanvasGroup"/> is preferred because it fades all child
    /// graphics simultaneously without touching individual <see cref="Graphic.color"/> values.
    /// If <see cref="targetCanvasGroup"/> is null, the module falls back to animating
    /// <see cref="Graphic.color.a"/> on <see cref="targetGraphic"/>.
    /// </para>
    /// <para>
    /// If both are null at runtime, <see cref="CaptureInitialValue"/> attempts
    /// <c>GetComponent&lt;CanvasGroup&gt;()</c> on the host. If still not found,
    /// the module silently skips (Play returns null).
    /// </para>
    /// </summary>
    [Serializable]
    public class FadeModule : AnimationModuleBase
    {
        [Tooltip("CanvasGroup to fade (preferred — fades all children). Assign explicitly or leave null for auto-detect.")]
        [SerializeField] private CanvasGroup targetCanvasGroup;

        [Tooltip("Fallback Graphic to fade (only alpha channel of color is animated). Used when targetCanvasGroup is null.")]
        [SerializeField] private Graphic targetGraphic;

        [Tooltip("Alpha for Normal state.")]
        [SerializeField, Range(0f, 1f)] private float normalAlpha = 1f;

        [Tooltip("Alpha for Hover state.")]
        [SerializeField, Range(0f, 1f)] private float hoverAlpha = 1f;

        [Tooltip("Alpha for Pressed state.")]
        [SerializeField, Range(0f, 1f)] private float pressedAlpha = 1f;

        [Tooltip("Alpha for Disabled state (0.5 = semi-transparent by default).")]
        [SerializeField, Range(0f, 1f)] private float disabledAlpha = 0.5f;

        [Tooltip("Alpha for Selected state.")]
        [SerializeField, Range(0f, 1f)] private float selectedAlpha = 1f;

        /// <inheritdoc/>
        public override string DisplayName => "Fade";

        /// <inheritdoc/>
        public override void CaptureInitialValue(MonoBehaviour target)
        {
            // Auto-detect CanvasGroup if not assigned
            if (targetCanvasGroup == null && targetGraphic == null && target != null)
                targetCanvasGroup = target.GetComponent<CanvasGroup>();
        }

        /// <inheritdoc/>
        public override IAnimationHandle Play(MonoBehaviour target, UIState newState, IAnimationBackend backend)
        {
            if (!enabled || target == null) return null;

            // Determine current alpha from whichever target is available
            float from;
            if (targetCanvasGroup != null)
                from = targetCanvasGroup.alpha;
            else if (targetGraphic != null)
                from = targetGraphic.color.a;
            else
                return null; // Nothing to fade

            float to = GetTargetAlpha(newState);

            // Capture references for closure (avoid repeated null checks on serialized field)
            CanvasGroup cg = targetCanvasGroup;
            Graphic g = targetGraphic;

            return backend.TweenFloat(target, from, to, duration,
                a =>
                {
                    if (cg != null)
                    {
                        cg.alpha = a;
                    }
                    else if (g != null)
                    {
                        Color c = g.color;
                        c.a = a;
                        g.color = c;
                    }
                },
                curve);
        }

        private float GetTargetAlpha(UIState state) => state switch
        {
            UIState.Normal   => normalAlpha,
            UIState.Hover    => hoverAlpha,
            UIState.Pressed  => pressedAlpha,
            UIState.Disabled => disabledAlpha,
            UIState.Selected => selectedAlpha,
            _                => normalAlpha,
        };
    }
}

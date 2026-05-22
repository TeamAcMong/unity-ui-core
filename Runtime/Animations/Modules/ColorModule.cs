using System;
using UnityEngine;
using UnityEngine.UI;
using DreamTech.UICore.Animations.Backends;

namespace DreamTech.UICore.Animations.Modules
{
    /// <summary>
    /// Animates the color of a <see cref="Graphic"/> component (Image, Text, TextMeshProUGUI, etc.)
    /// between state-specific color values.
    /// <para>
    /// Designer workflow: assign <c>targetGraphic</c> explicitly in the Inspector for clarity.
    /// If left null, <see cref="CaptureInitialValue"/> will fall back to
    /// <c>target.GetComponent&lt;Graphic&gt;()</c> at runtime.
    /// </para>
    /// </summary>
    [Serializable]
    public class ColorModule : AnimationModuleBase
    {
        [Tooltip("Graphic to animate. Assign explicitly for clarity; falls back to GetComponent<Graphic>() if null.")]
        [SerializeField] private Graphic targetGraphic;

        [Tooltip("Color for Normal state.")]
        [SerializeField] private Color normalColor = Color.white;

        [Tooltip("Color for Hover state.")]
        [SerializeField] private Color hoverColor = new Color(0.9f, 0.9f, 0.9f, 1f);

        [Tooltip("Color for Pressed state.")]
        [SerializeField] private Color pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);

        [Tooltip("Color for Disabled state (semi-transparent grey by default).")]
        [SerializeField] private Color disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

        [Tooltip("Color for Selected state.")]
        [SerializeField] private Color selectedColor = Color.white;

        private Color _initialColor = Color.white;

        /// <inheritdoc/>
        public override string DisplayName => "Color";

        /// <inheritdoc/>
        public override void CaptureInitialValue(MonoBehaviour target)
        {
            if (targetGraphic == null && target != null)
                targetGraphic = target.GetComponent<Graphic>();

            if (targetGraphic != null)
                _initialColor = targetGraphic.color;
        }

        /// <inheritdoc/>
        public override IAnimationHandle Play(MonoBehaviour target, UIState newState, IAnimationBackend backend)
        {
            if (!enabled || targetGraphic == null) return null;

            Color from = targetGraphic.color;
            Color to = GetTargetColor(newState);
            Graphic g = targetGraphic;

            return backend.TweenColor(target, from, to, duration,
                c => { if (g != null) g.color = c; },
                curve);
        }

        private Color GetTargetColor(UIState state) => state switch
        {
            UIState.Normal   => normalColor,
            UIState.Hover    => hoverColor,
            UIState.Pressed  => pressedColor,
            UIState.Disabled => disabledColor,
            UIState.Selected => selectedColor,
            _                => normalColor,
        };
    }
}

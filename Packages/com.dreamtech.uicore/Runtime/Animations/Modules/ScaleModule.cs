using System;
using UnityEngine;
using DreamTech.UICore.Animations.Backends;

namespace DreamTech.UICore.Animations.Modules
{
    /// <summary>
    /// Animates <see cref="Transform.localScale"/> between state-specific values.
    /// Supports both uniform (single float multiplier) and non-uniform (full Vector3) modes.
    /// The scale values are treated as <b>multipliers</b> applied to the initial captured scale,
    /// so Normal=1 always preserves whatever scale the object had at runtime start.
    /// </summary>
    [Serializable]
    public class ScaleModule : AnimationModuleBase
    {
        // ─────────────────────────────────────────────────────────────────────
        // Uniform mode
        // ─────────────────────────────────────────────────────────────────────

        [Tooltip("When true, a single float multiplier is used for each state instead of per-axis Vector3.")]
        [SerializeField] private bool useUniformScale = true;

        [Tooltip("Scale multiplier for Normal state (1 = original scale).")]
        [SerializeField] private float normalScale = 1f;

        [Tooltip("Scale multiplier for Hover state.")]
        [SerializeField] private float hoverScale = 1.1f;

        [Tooltip("Scale multiplier for Pressed state.")]
        [SerializeField] private float pressedScale = 0.9f;

        [Tooltip("Scale multiplier for Disabled state.")]
        [SerializeField] private float disabledScale = 1f;

        [Tooltip("Scale multiplier for Selected state.")]
        [SerializeField] private float selectedScale = 1f;

        // ─────────────────────────────────────────────────────────────────────
        // Non-uniform mode
        // ─────────────────────────────────────────────────────────────────────

        [Tooltip("Per-axis scale multiplier for Normal state (active when useUniformScale = false).")]
        [SerializeField] private Vector3 normalScaleV = Vector3.one;

        [Tooltip("Per-axis scale multiplier for Hover state.")]
        [SerializeField] private Vector3 hoverScaleV = new Vector3(1.1f, 1.1f, 1.1f);

        [Tooltip("Per-axis scale multiplier for Pressed state.")]
        [SerializeField] private Vector3 pressedScaleV = new Vector3(0.9f, 0.9f, 0.9f);

        [Tooltip("Per-axis scale multiplier for Disabled state.")]
        [SerializeField] private Vector3 disabledScaleV = Vector3.one;

        [Tooltip("Per-axis scale multiplier for Selected state.")]
        [SerializeField] private Vector3 selectedScaleV = Vector3.one;

        // ─────────────────────────────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────────────────────────────

        private Vector3 _initialScale = Vector3.one;

        /// <inheritdoc/>
        public override string DisplayName => "Scale";

        /// <inheritdoc/>
        public override void CaptureInitialValue(MonoBehaviour target)
        {
            if (target != null)
                _initialScale = target.transform.localScale;
        }

        /// <inheritdoc/>
        public override IAnimationHandle Play(MonoBehaviour target, UIState newState, IAnimationBackend backend)
        {
            if (!enabled || target == null) return null;

            Vector3 from = target.transform.localScale;
            Vector3 to = GetTargetScale(newState);
            Transform t = target.transform;

            return backend.TweenVector3(target, from, to, duration,
                v => { if (t != null) t.localScale = v; },
                curve);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private Vector3 GetTargetScale(UIState state)
        {
            if (useUniformScale)
            {
                float multiplier = state switch
                {
                    UIState.Normal   => normalScale,
                    UIState.Hover    => hoverScale,
                    UIState.Pressed  => pressedScale,
                    UIState.Disabled => disabledScale,
                    UIState.Selected => selectedScale,
                    _                => normalScale,
                };
                // Apply multiplier per-axis against initial scale so non-uniform initial scales are preserved.
                return new Vector3(
                    _initialScale.x * multiplier,
                    _initialScale.y * multiplier,
                    _initialScale.z * multiplier);
            }

            // Non-uniform: per-axis multiplier scaled against initial
            Vector3 axisMultiplier = state switch
            {
                UIState.Normal   => normalScaleV,
                UIState.Hover    => hoverScaleV,
                UIState.Pressed  => pressedScaleV,
                UIState.Disabled => disabledScaleV,
                UIState.Selected => selectedScaleV,
                _                => normalScaleV,
            };

            return Vector3.Scale(_initialScale, axisMultiplier);
        }
    }
}

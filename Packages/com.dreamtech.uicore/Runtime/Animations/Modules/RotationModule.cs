using System;
using UnityEngine;
using DreamTech.UICore.Animations.Backends;

namespace DreamTech.UICore.Animations.Modules
{
    /// <summary>
    /// Animates <see cref="Transform.localEulerAngles"/> between state-specific values.
    /// <para>
    /// <b>Edge case — Euler wrap-around:</b>
    /// This module lerps raw Euler angle vectors via <see cref="IAnimationBackend.TweenVector3"/>.
    /// This works correctly for small angles (under 180°) and for the common UI pattern of
    /// spinning only the Z axis (e.g. arrow icon flip ±90°). If you need cross-axis or
    /// greater-than-180° rotations, prefer a dedicated Quaternion Slerp approach outside
    /// this module to avoid gimbal-lock or shortest-path artifacts.
    /// </para>
    /// </summary>
    [Serializable]
    public class RotationModule : AnimationModuleBase
    {
        [Tooltip("Local Euler angles for Normal state (degrees). Z-axis is most common for UI icons.")]
        [SerializeField] private Vector3 normalRotation = Vector3.zero;

        [Tooltip("Local Euler angles for Hover state.")]
        [SerializeField] private Vector3 hoverRotation = Vector3.zero;

        [Tooltip("Local Euler angles for Pressed state.")]
        [SerializeField] private Vector3 pressedRotation = Vector3.zero;

        [Tooltip("Local Euler angles for Disabled state.")]
        [SerializeField] private Vector3 disabledRotation = Vector3.zero;

        [Tooltip("Local Euler angles for Selected state.")]
        [SerializeField] private Vector3 selectedRotation = Vector3.zero;

        [SerializeField, Tooltip("Optional override — animate transform này thay vì target root. Null = transform của component (root).")]
        private Transform targetTransform;

        private Vector3 _initialEuler;

        /// <inheritdoc/>
        public override string DisplayName => "Rotation";

        /// <inheritdoc/>
        public override void CaptureInitialValue(MonoBehaviour target)
        {
            var t = ResolveTargetTransform(target);
            if (t != null)
                _initialEuler = t.localEulerAngles;
        }

        /// <inheritdoc/>
        public override IAnimationHandle Play(MonoBehaviour target, UIState newState, IAnimationBackend backend)
        {
            if (!enabled || target == null) return null;
            var t = ResolveTargetTransform(target);
            if (t == null) return null;

            Vector3 from = t.localEulerAngles;
            Vector3 to = GetTargetEuler(newState);

            return backend.TweenVector3(target, from, to, duration,
                v => { if (t != null) t.localEulerAngles = v; },
                curve);
        }

        private Transform ResolveTargetTransform(MonoBehaviour target)
        {
            return targetTransform != null ? targetTransform : (target != null ? target.transform : null);
        }

        private Vector3 GetTargetEuler(UIState state) => state switch
        {
            UIState.Normal   => normalRotation,
            UIState.Hover    => hoverRotation,
            UIState.Pressed  => pressedRotation,
            UIState.Disabled => disabledRotation,
            UIState.Selected => selectedRotation,
            _                => normalRotation,
        };
    }
}

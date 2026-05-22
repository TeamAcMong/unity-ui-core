using System;
using UnityEngine;
using DreamTech.UICore.Animations.Backends;

namespace DreamTech.UICore.Animations.Modules
{
    /// <summary>
    /// Animates <see cref="Transform.localPosition"/> by applying a per-state offset
    /// relative to the initial captured position.
    /// <para>
    /// All offset values are additive offsets from the initial position captured at
    /// <see cref="CaptureInitialValue"/>. Setting all offsets to <see cref="Vector3.zero"/>
    /// means no movement in any state.
    /// </para>
    /// </summary>
    [Serializable]
    public class PositionModule : AnimationModuleBase
    {
        [Tooltip("Local-position offset from initial position for Normal state.")]
        [SerializeField] private Vector3 normalOffset = Vector3.zero;

        [Tooltip("Local-position offset from initial position for Hover state.")]
        [SerializeField] private Vector3 hoverOffset = Vector3.zero;

        [Tooltip("Local-position offset from initial position for Pressed state (e.g. (0, -2, 0) for a press-down feel).")]
        [SerializeField] private Vector3 pressedOffset = Vector3.zero;

        [Tooltip("Local-position offset from initial position for Disabled state.")]
        [SerializeField] private Vector3 disabledOffset = Vector3.zero;

        [Tooltip("Local-position offset from initial position for Selected state.")]
        [SerializeField] private Vector3 selectedOffset = Vector3.zero;

        [SerializeField, Tooltip("Optional override — animate transform này thay vì target root. Null = transform của component (root).")]
        private Transform targetTransform;

        private Vector3 _initialPosition;

        /// <inheritdoc/>
        public override string DisplayName => "Position";

        /// <inheritdoc/>
        public override void CaptureInitialValue(MonoBehaviour target)
        {
            var t = ResolveTargetTransform(target);
            if (t != null)
                _initialPosition = t.localPosition;
        }

        /// <inheritdoc/>
        public override IAnimationHandle Play(MonoBehaviour target, UIState newState, IAnimationBackend backend)
        {
            if (!enabled || target == null) return null;
            var t = ResolveTargetTransform(target);
            if (t == null) return null;

            Vector3 from = t.localPosition;
            Vector3 to = _initialPosition + GetOffset(newState);

            return backend.TweenVector3(target, from, to, duration,
                v => { if (t != null) t.localPosition = v; },
                curve);
        }

        private Transform ResolveTargetTransform(MonoBehaviour target)
        {
            return targetTransform != null ? targetTransform : (target != null ? target.transform : null);
        }

        private Vector3 GetOffset(UIState state) => state switch
        {
            UIState.Normal   => normalOffset,
            UIState.Hover    => hoverOffset,
            UIState.Pressed  => pressedOffset,
            UIState.Disabled => disabledOffset,
            UIState.Selected => selectedOffset,
            _                => normalOffset,
        };
    }
}

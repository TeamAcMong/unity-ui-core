using System;
using UnityEngine;
using DreamTech.UICore.Animations.Backends;

namespace DreamTech.UICore.Animations.Modules
{
    /// <summary>
    /// Abstract base class cho tất cả built-in animation modules.
    /// Chứa các field chung: enabled, duration, curve.
    /// Subclass thêm state-specific fields (ví dụ ScaleModule thêm normalScale, pressedScale).
    /// </summary>
    [Serializable]
    public abstract class AnimationModuleBase : IAnimationModule
    {
        [SerializeField] protected bool enabled = true;

        [Tooltip("Thời gian transition (giây).")]
        [SerializeField] protected float duration = 0.2f;

        [Tooltip("Easing curve. Default = EaseInOut.")]
        [SerializeField] protected AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        /// <inheritdoc/>
        public bool Enabled => enabled;

        /// <inheritdoc/>
        public abstract string DisplayName { get; }

        /// <inheritdoc/>
        public abstract void CaptureInitialValue(MonoBehaviour target);

        /// <inheritdoc/>
        public abstract IAnimationHandle Play(MonoBehaviour target, UIState newState, IAnimationBackend backend);
    }
}

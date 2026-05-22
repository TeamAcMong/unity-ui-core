using System;
using UnityEngine;
using DreamTech.UICore.Animations.Backends;

namespace DreamTech.UICore.Animations.Modules
{
    /// <summary>
    /// One-shot position shake effect triggered when the UI element enters a specific state.
    /// Unlike state-interpolating modules, ShakeModule fires a decaying random-offset animation
    /// from the current position and returns automatically without holding a persistent target.
    /// <para>
    /// Common usage: set <see cref="triggerOnState"/> to <see cref="UIState.Pressed"/> or
    /// <see cref="UIState.Disabled"/> for a "can't do that" denial shake.
    /// </para>
    /// </summary>
    [Serializable]
    public class ShakeModule : AnimationModuleBase
    {
        [Tooltip("Maximum position offset amplitude at the start of the shake.")]
        [SerializeField] private float strength = 10f;

        [Tooltip("Number of shakes per second. Higher = more rapid rattling.")]
        [SerializeField] private int vibrato = 10;

        [Tooltip("Random angle spread in degrees (0 = purely horizontal, 90 = fully random direction).")]
        [SerializeField, Range(0f, 180f)] private float randomness = 90f;

        [Tooltip("The UIState transition that triggers this shake. Only fires when entering this state.")]
        [SerializeField] private UIState triggerOnState = UIState.Pressed;

        /// <inheritdoc/>
        public override string DisplayName => "Shake (one-shot on state)";

        /// <inheritdoc/>
        public override void CaptureInitialValue(MonoBehaviour target) { }

        /// <inheritdoc/>
        public override IAnimationHandle Play(MonoBehaviour target, UIState newState, IAnimationBackend backend)
        {
            if (!enabled || target == null) return null;
            if (newState != triggerOnState) return null;

            return backend.Shake(target, target.transform, strength, duration, vibrato, randomness);
        }
    }
}

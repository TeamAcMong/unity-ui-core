using System;
using UnityEngine;
using DreamTech.UICore.Animations.Backends;

namespace DreamTech.UICore.Animations.Modules
{
    /// <summary>
    /// One-shot punch scale effect triggered when the UI element enters a specific state.
    /// Unlike state-interpolating modules, PunchModule does not hold a target value —
    /// it fires a spring-decay animation from the current scale and returns automatically.
    /// <para>
    /// Common usage: set <see cref="triggerOnState"/> to <see cref="UIState.Pressed"/> for
    /// a satisfying press-down bounce effect.
    /// </para>
    /// </summary>
    [Serializable]
    public class PunchModule : AnimationModuleBase
    {
        [Tooltip("Punch offset applied to each axis of localScale. (0.1, 0.1, 0) gives a 2D scale pop.")]
        [SerializeField] private Vector3 punchAmount = new Vector3(0.1f, 0.1f, 0f);

        [Tooltip("Number of oscillations before the punch decays to zero. Higher = more bouncy.")]
        [SerializeField] private int vibrato = 10;

        [Tooltip("Elasticity of the bounce. 0 = no spring-back, 1 = full elastic.")]
        [SerializeField, Range(0f, 1f)] private float elasticity = 1f;

        [Tooltip("The UIState transition that triggers this punch. Only fires when entering this state.")]
        [SerializeField] private UIState triggerOnState = UIState.Pressed;

        /// <inheritdoc/>
        public override string DisplayName => "Punch (one-shot on state)";

        /// <inheritdoc/>
        public override void CaptureInitialValue(MonoBehaviour target) { }

        /// <inheritdoc/>
        public override IAnimationHandle Play(MonoBehaviour target, UIState newState, IAnimationBackend backend)
        {
            if (!enabled || target == null) return null;
            if (newState != triggerOnState) return null;

            return backend.Punch(target, target.transform, punchAmount, duration, vibrato, elasticity);
        }
    }
}

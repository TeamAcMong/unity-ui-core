using System;
using UnityEngine;
using UnityEngine.Events;

namespace DreamTech.UICore.Animations.Events
{
    /// <summary>
    /// Inspector-serializable event hooks that fire at key moments during an animation.
    /// Attach this as a field on a MonoBehaviour and call the <c>Invoke*</c> methods
    /// from animation play/update/complete code.
    /// <para>
    /// Example — wiring hooks to a module play call:
    /// <code>
    /// var handle = module.Play(target, state, backend);
    /// hooks.InvokeStart();
    /// handle?.OnComplete(() => hooks.InvokeComplete());
    /// </code>
    /// </para>
    /// </summary>
    [Serializable]
    public class AnimationEventHooks
    {
        [Tooltip("Fired once immediately before the first animation frame.")]
        public UnityEvent onAnimationStart = new UnityEvent();

        [Tooltip("Fired every frame with normalized progress t in [0..1].")]
        public UnityEvent<float> onAnimationStep = new UnityEvent<float>();

        [Tooltip("Fired once when the animation completes naturally (not when stopped/cancelled).")]
        public UnityEvent onAnimationComplete = new UnityEvent();

        // ─────────────────────────────────────────────────────────────────────
        // Invoke helpers (null-safe wrappers for caller convenience)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Fires <see cref="onAnimationStart"/>.</summary>
        public void InvokeStart() => onAnimationStart?.Invoke();

        /// <summary>Fires <see cref="onAnimationStep"/> with normalized progress <paramref name="t"/> (0..1).</summary>
        /// <param name="t">Normalized animation progress.</param>
        public void InvokeStep(float t) => onAnimationStep?.Invoke(t);

        /// <summary>Fires <see cref="onAnimationComplete"/>.</summary>
        public void InvokeComplete() => onAnimationComplete?.Invoke();
    }
}

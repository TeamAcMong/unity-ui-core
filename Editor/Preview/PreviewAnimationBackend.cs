using System;
using System.Collections.Generic;
using DreamTech.UICore.Animations.Backends;
using UnityEditor;
using UnityEngine;

namespace DreamTech.UICore.Editor.Preview
{
    /// <summary>
    /// Edit-mode IAnimationBackend driven by <see cref="EditorApplication.update"/> instead of UniTask.
    /// UniTask PlayerLoopTiming.Update does not fire in Edit mode, so this backend replaces it for
    /// preview sessions initiated from custom inspectors or the Preview panel.
    ///
    /// Lifecycle:
    /// - Tweens are registered via the five IAnimationBackend methods.
    /// - <see cref="EditorApplication.update"/> is subscribed lazily on first tween and unsubscribed
    ///   automatically when all active tweens finish.
    /// - <see cref="StopAll"/> cancels every in-flight tween immediately and unsubscribes the tick.
    /// </summary>
    internal sealed class PreviewAnimationBackend : IAnimationBackend
    {
        // ─────────────────────────────────────────────────────────────────────
        // Internal types
        // ─────────────────────────────────────────────────────────────────────

        private enum TweenType { Float, Vector3, Color, Punch, Shake }

        private sealed class PreviewTween
        {
            public PreviewAnimationHandle Handle;
            public float ElapsedTime;
            public float Duration;
            public AnimationCurve Curve;
            public TweenType Type;

            // Generic tween value support
            public float FromFloat;
            public float ToFloat;
            public Vector3 FromVector3;
            public Vector3 ToVector3;
            public Color FromColor;
            public Color ToColor;

            // Callbacks
            public Action OnStartCallback;
            public Action<float> OnStepCallback;
            public Action OnCompleteCallback;
            public Action<float> OnUpdateFloat;
            public Action<Vector3> OnUpdateVector3;
            public Action<Color> OnUpdateColor;

            // Punch/Shake target data
            public Transform Target;
            public Vector3 OriginalLocalPos;
            public Vector3 OriginalLocalScale;

            // Punch-specific
            public Vector3 PunchAmount;
            public int Vibrato;
            public float Elasticity;
            // Derived punch state (computed once when tween starts)
            public float HalfPeriod;
            public float Decay;
            public float ElapsedSinceLastHalfPeriod;
            public int HalfPeriodIndex;

            // Shake-specific
            public float Strength;
            public float Randomness;
            public float TimePerShake;
            public float ElapsedSinceLastShake;
            public Vector3 CurrentShakeOffset;

            public bool Started;
        }

        // ─────────────────────────────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────────────────────────────

        private readonly List<PreviewTween> _activeTweens = new List<PreviewTween>();
        private bool _updateSubscribed;
        private double _lastTickTime;
        // Re-entrancy guard: when true, Tick is mid-iteration. StopAll defers mutation
        // and we use _pendingRemoval to safely remove finished/cancelled tweens after the loop.
        private bool _isTicking;
        private readonly List<PreviewTween> _pendingRemoval = new List<PreviewTween>();

        // ─────────────────────────────────────────────────────────────────────
        // IAnimationBackend — TweenFloat
        // ─────────────────────────────────────────────────────────────────────

        public IAnimationHandle TweenFloat(
            MonoBehaviour host,
            float from, float to, float duration,
            Action<float> onUpdate,
            AnimationCurve curve = null,
            Action onStart = null,
            Action<float> onStep = null,
            Action onComplete = null)
        {
            var handle = new PreviewAnimationHandle();
            var tween = new PreviewTween
            {
                Handle = handle,
                Duration = Mathf.Max(0f, duration),
                Curve = curve,
                Type = TweenType.Float,
                FromFloat = from,
                ToFloat = to,
                OnUpdateFloat = onUpdate,
                OnStartCallback = onStart,
                OnStepCallback = onStep,
                OnCompleteCallback = onComplete,
            };
            _activeTweens.Add(tween);
            EnsureSubscribed();
            return handle;
        }

        // ─────────────────────────────────────────────────────────────────────
        // IAnimationBackend — TweenVector3
        // ─────────────────────────────────────────────────────────────────────

        public IAnimationHandle TweenVector3(
            MonoBehaviour host,
            Vector3 from, Vector3 to, float duration,
            Action<Vector3> onUpdate,
            AnimationCurve curve = null,
            Action onStart = null,
            Action<float> onStep = null,
            Action onComplete = null)
        {
            var handle = new PreviewAnimationHandle();
            var tween = new PreviewTween
            {
                Handle = handle,
                Duration = Mathf.Max(0f, duration),
                Curve = curve,
                Type = TweenType.Vector3,
                FromVector3 = from,
                ToVector3 = to,
                OnUpdateVector3 = onUpdate,
                OnStartCallback = onStart,
                OnStepCallback = onStep,
                OnCompleteCallback = onComplete,
            };
            _activeTweens.Add(tween);
            EnsureSubscribed();
            return handle;
        }

        // ─────────────────────────────────────────────────────────────────────
        // IAnimationBackend — TweenColor
        // ─────────────────────────────────────────────────────────────────────

        public IAnimationHandle TweenColor(
            MonoBehaviour host,
            Color from, Color to, float duration,
            Action<Color> onUpdate,
            AnimationCurve curve = null,
            Action onStart = null,
            Action<float> onStep = null,
            Action onComplete = null)
        {
            var handle = new PreviewAnimationHandle();
            var tween = new PreviewTween
            {
                Handle = handle,
                Duration = Mathf.Max(0f, duration),
                Curve = curve,
                Type = TweenType.Color,
                FromColor = from,
                ToColor = to,
                OnUpdateColor = onUpdate,
                OnStartCallback = onStart,
                OnStepCallback = onStep,
                OnCompleteCallback = onComplete,
            };
            _activeTweens.Add(tween);
            EnsureSubscribed();
            return handle;
        }

        // ─────────────────────────────────────────────────────────────────────
        // IAnimationBackend — Punch
        // ─────────────────────────────────────────────────────────────────────

        public IAnimationHandle Punch(
            MonoBehaviour host,
            Transform target,
            Vector3 punchAmount,
            float duration,
            int vibrato = 10,
            float elasticity = 1f,
            Action onComplete = null)
        {
            var handle = new PreviewAnimationHandle();

            if (target == null)
            {
                handle.MarkCompleted();
                onComplete?.Invoke();
                return handle;
            }

            int safeVibrato = Mathf.Max(1, vibrato);
            float safeDuration = Mathf.Max(0.001f, duration);
            float halfPeriod = safeDuration / (safeVibrato * 2f);

            var tween = new PreviewTween
            {
                Handle = handle,
                Duration = safeDuration,
                Type = TweenType.Punch,
                Target = target,
                OriginalLocalScale = target.localScale,
                PunchAmount = punchAmount,
                Vibrato = safeVibrato,
                Elasticity = elasticity,
                OnCompleteCallback = onComplete,
                // Punch state
                HalfPeriod = halfPeriod,
                Decay = 1f / safeVibrato,
                ElapsedSinceLastHalfPeriod = 0f,
                HalfPeriodIndex = 0,
            };
            _activeTweens.Add(tween);
            EnsureSubscribed();
            return handle;
        }

        // ─────────────────────────────────────────────────────────────────────
        // IAnimationBackend — Shake
        // ─────────────────────────────────────────────────────────────────────

        public IAnimationHandle Shake(
            MonoBehaviour host,
            Transform target,
            float strength,
            float duration,
            int vibrato = 10,
            float randomness = 90f,
            Action onComplete = null)
        {
            var handle = new PreviewAnimationHandle();

            if (target == null)
            {
                handle.MarkCompleted();
                onComplete?.Invoke();
                return handle;
            }

            int safeVibrato = Mathf.Max(1, vibrato);
            float safeDuration = Mathf.Max(0.001f, duration);
            float timePerShake = safeDuration / safeVibrato;

            var tween = new PreviewTween
            {
                Handle = handle,
                Duration = safeDuration,
                Type = TweenType.Shake,
                Target = target,
                OriginalLocalPos = target.localPosition,
                Strength = strength,
                Randomness = randomness,
                OnCompleteCallback = onComplete,
                // Shake state — trigger first shake offset immediately on first tick
                TimePerShake = timePerShake,
                ElapsedSinceLastShake = timePerShake,
                CurrentShakeOffset = Vector3.zero,
            };
            _activeTweens.Add(tween);
            EnsureSubscribed();
            return handle;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public control
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Cancel and remove every in-flight tween. Restores Punch/Shake targets.
        /// Unsubscribes from <see cref="EditorApplication.update"/>.
        /// Safe to call mid-Tick — mutation is deferred when re-entrant.
        /// </summary>
        public void StopAll()
        {
            // Restore targets and mark cancelled (always safe — these are pure value writes)
            foreach (var t in _activeTweens)
            {
                RestoreTarget(t);
                t.Handle.MarkCancelled();
            }

            // If we're currently mid-Tick (callback re-entered into StopAll), DO NOT mutate the list.
            // The Tick loop's defensive guards will skip already-cancelled tweens and clean up
            // pending removals at the end. This avoids ArgumentOutOfRangeException from index shift.
            if (!_isTicking)
            {
                _activeTweens.Clear();
                Unsubscribe();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Tick loop
        // ─────────────────────────────────────────────────────────────────────

        private void EnsureSubscribed()
        {
            if (_updateSubscribed) return;
            _lastTickTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
            _updateSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_updateSubscribed) return;
            EditorApplication.update -= Tick;
            _updateSubscribed = false;
        }

        private void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            float dt = (float)(now - _lastTickTime);
            _lastTickTime = now;

            // Guard against absurd dt values (editor paused/step, sleep, etc.)
            dt = Mathf.Clamp(dt, 0f, 0.1f);

            _isTicking = true;
            try
            {
            for (int i = _activeTweens.Count - 1; i >= 0; i--)
            {
                // Defensive: list may have shrunk via re-entrant StopAll (deferred removal),
                // or the index could be out of range due to a removed-during-callback case.
                if (i >= _activeTweens.Count) continue;
                var tween = _activeTweens[i];

                // Handle cancelled (Stop() called externally)
                if (tween.Handle.IsCancelled)
                {
                    RestoreTarget(tween);
                    _pendingRemoval.Add(tween);
                    continue;
                }

                // OnStart fires once before the first value update
                if (!tween.Started)
                {
                    tween.OnStartCallback?.Invoke();
                    tween.Started = true;
                }

                tween.ElapsedTime += dt;
                float t = tween.Duration > 0f ? Mathf.Clamp01(tween.ElapsedTime / tween.Duration) : 1f;

                bool finished = (t >= 1f);

                switch (tween.Type)
                {
                    case TweenType.Float:
                        TickFloat(tween, t);
                        break;
                    case TweenType.Vector3:
                        TickVector3(tween, t);
                        break;
                    case TweenType.Color:
                        TickColor(tween, t);
                        break;
                    case TweenType.Punch:
                        TickPunch(tween, dt, finished);
                        break;
                    case TweenType.Shake:
                        TickShake(tween, dt, finished);
                        break;
                }

                tween.OnStepCallback?.Invoke(t);

                if (finished)
                {
                    // Restore Punch/Shake to exact original on natural completion
                    RestoreTarget(tween);

                    tween.Handle.MarkCompleted();
                    _pendingRemoval.Add(tween);
                    // Callback may re-enter (e.g. PreviewSession.RestoreAndEnd → StopAll).
                    // Marked _isTicking so re-entrant mutations are deferred and merged
                    // into the same _pendingRemoval batch below.
                    tween.OnCompleteCallback?.Invoke();
                }
            }
            }
            finally
            {
                _isTicking = false;
                // Apply all deferred removals safely (by reference, not by index).
                if (_pendingRemoval.Count > 0)
                {
                    foreach (var t in _pendingRemoval) _activeTweens.Remove(t);
                    _pendingRemoval.Clear();
                }
            }

            // Force editor repaint at high cadence so preview looks smooth (not throttled
            // to EditorApplication.update's idle rate of ~10 Hz). QueuePlayerLoopUpdate
            // triggers a full player-loop tick in edit mode, driving Unity's repaint pipeline.
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            // Also nudge any focused InspectorWindow which doesn't always pick up RepaintAllViews
            var focused = EditorWindow.focusedWindow;
            if (focused != null) focused.Repaint();

            if (_activeTweens.Count == 0)
                Unsubscribe();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Per-type tick helpers
        // ─────────────────────────────────────────────────────────────────────

        private static void TickFloat(PreviewTween tween, float t)
        {
            float eased = EvaluateCurve(tween.Curve, t);
            float value = Mathf.LerpUnclamped(tween.FromFloat, tween.ToFloat, eased);
            tween.OnUpdateFloat?.Invoke(value);
        }

        private static void TickVector3(PreviewTween tween, float t)
        {
            float eased = EvaluateCurve(tween.Curve, t);
            Vector3 value = Vector3.LerpUnclamped(tween.FromVector3, tween.ToVector3, eased);
            tween.OnUpdateVector3?.Invoke(value);
        }

        private static void TickColor(PreviewTween tween, float t)
        {
            float eased = EvaluateCurve(tween.Curve, t);
            Color value = Color.LerpUnclamped(tween.FromColor, tween.ToColor, eased);
            tween.OnUpdateColor?.Invoke(value);
        }

        /// <summary>
        /// Mirrors UniTaskAnimationBackend Punch: accumulator-based half-period alternation with
        /// sine easing per half-period and amplitude decay.
        /// </summary>
        private static void TickPunch(PreviewTween tween, float dt, bool finished)
        {
            if (finished || tween.Target == null) return;

            tween.ElapsedSinceLastHalfPeriod += dt;
            if (tween.ElapsedSinceLastHalfPeriod >= tween.HalfPeriod)
            {
                tween.ElapsedSinceLastHalfPeriod -= tween.HalfPeriod;
                tween.HalfPeriodIndex++;
            }

            float halfT = Mathf.Clamp01(tween.HalfPeriod > 0f
                ? tween.ElapsedSinceLastHalfPeriod / tween.HalfPeriod
                : 1f);
            float amplitudeScale = Mathf.Max(0f, 1f - tween.HalfPeriodIndex * tween.Decay);
            float sign = (tween.HalfPeriodIndex % 2 == 0) ? 1f : -tween.Elasticity;
            float sineT = Mathf.Sin(halfT * Mathf.PI);
            Vector3 offset = tween.PunchAmount * (sign * amplitudeScale * sineT);

            tween.Target.localScale = tween.OriginalLocalScale + offset;
        }

        /// <summary>
        /// Mirrors UniTaskAnimationBackend Shake: interval-based random offset with decay.
        /// </summary>
        private static void TickShake(PreviewTween tween, float dt, bool finished)
        {
            if (finished || tween.Target == null) return;

            tween.ElapsedSinceLastShake += dt;
            if (tween.ElapsedSinceLastShake >= tween.TimePerShake)
            {
                tween.ElapsedSinceLastShake -= tween.TimePerShake;

                float decayFactor = 1f - Mathf.Clamp01(tween.ElapsedTime / tween.Duration);
                float currentStrength = tween.Strength * decayFactor;
                float randomnessRad = tween.Randomness * Mathf.Deg2Rad;
                float angle = UnityEngine.Random.Range(-randomnessRad, randomnessRad);
                Vector3 dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                tween.CurrentShakeOffset = dir * currentStrength;
            }

            tween.Target.localPosition = tween.OriginalLocalPos + tween.CurrentShakeOffset;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private static float EvaluateCurve(AnimationCurve curve, float t)
        {
            return curve != null ? curve.Evaluate(t) : t;
        }

        /// <summary>
        /// Restore Punch/Shake target to their captured original values.
        /// Safe to call even if target was destroyed (Unity null check).
        /// </summary>
        private static void RestoreTarget(PreviewTween tween)
        {
            try
            {
                if (tween.Type == TweenType.Punch && tween.Target != null)
                    tween.Target.localScale = tween.OriginalLocalScale;
                else if (tween.Type == TweenType.Shake && tween.Target != null)
                    tween.Target.localPosition = tween.OriginalLocalPos;
            }
            catch
            {
                // Target may have been destroyed — silently swallow
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PreviewAnimationHandle
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// IAnimationHandle implementation for <see cref="PreviewAnimationBackend"/>.
    /// Thread-safety is not required — always called from the main thread (EditorApplication.update).
    /// </summary>
    internal sealed class PreviewAnimationHandle : IAnimationHandle
    {
        private List<Action> _onCompleteCallbacks;

        /// <inheritdoc/>
        public bool IsPlaying => !IsCompleted && !IsCancelled;

        /// <inheritdoc/>
        public bool IsCompleted { get; private set; }

        /// <summary>True when <see cref="Stop"/> was called or the session cancelled externally.</summary>
        public bool IsCancelled { get; private set; }

        /// <inheritdoc/>
        public void Stop() => IsCancelled = true;

        /// <inheritdoc/>
        public IAnimationHandle OnComplete(Action callback)
        {
            if (callback == null) return this;
            if (IsCompleted)
            {
                // Already completed — invoke inline, mirroring UniTaskAnimationHandle behaviour
                callback.Invoke();
                return this;
            }
            (_onCompleteCallbacks ??= new List<Action>()).Add(callback);
            return this;
        }

        /// <summary>Called by the backend when the tween finishes naturally.</summary>
        internal void MarkCompleted()
        {
            if (IsCompleted || IsCancelled) return;
            IsCompleted = true;
            if (_onCompleteCallbacks == null) return;
            foreach (var cb in _onCompleteCallbacks)
                cb?.Invoke();
            _onCompleteCallbacks.Clear();
        }

        /// <summary>Called by the backend or session when the tween is cancelled externally.</summary>
        internal void MarkCancelled() => IsCancelled = true;
    }
}

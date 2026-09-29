using System;
using System.Collections.Generic;
using DreamTech.UICore.Base;
using UnityEngine;

namespace DreamTech.UICore.Animations.Backends
{
    /// <summary>
    /// Backend mặc định — không phụ thuộc package nào ngoài Unity. Tween chạy trên vòng Update của package (<see cref="FrameLoop"/>):
    /// khung đầu ghi giá trị ở t = 0, mỗi khung sau cộng thời gian rồi ghi, khung cuối ghi đúng giá trị đích. Tween tự dừng khi
    /// host bị destroy.
    /// <para>Project có UniTask có thể dùng <c>UniTaskAnimationBackend</c> (assembly <c>DreamTech.UICore.UniTask</c>) — cùng hành vi.</para>
    /// </summary>
    public sealed class DefaultAnimationBackend : ITimeModeAnimationBackend
    {
        private static readonly AnimationCurve LinearCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        /// <summary>
        /// Đồng hồ chạy tween. <see cref="AnimationTimeMode.Scaled"/> (mặc định) chậm/dừng theo <c>Time.timeScale</c>;
        /// <see cref="AnimationTimeMode.Unscaled"/> chạy theo giờ thật — UI mở lúc game đang slow-motion hay pause vẫn đúng nhịp.
        /// <code>AnimationBackendRegistry.Current = new DefaultAnimationBackend { TimeMode = AnimationTimeMode.Unscaled };</code>
        /// </summary>
        public AnimationTimeMode TimeMode { get; set; } = AnimationTimeMode.Scaled;

        private DefaultAnimationBackend _scaledClock;
        private DefaultAnimationBackend _unscaledClock;

        /// <summary>Chính nó nếu cùng đồng hồ, không thì một backend anh em (tạo một lần) chạy theo <paramref name="mode"/>.</summary>
        public IAnimationBackend WithTimeMode(AnimationTimeMode mode)
        {
            if (mode == TimeMode) return this;
            return mode == AnimationTimeMode.Unscaled
                ? _unscaledClock ??= new DefaultAnimationBackend { TimeMode = AnimationTimeMode.Unscaled }
                : _scaledClock ??= new DefaultAnimationBackend { TimeMode = AnimationTimeMode.Scaled };
        }

        // ─────────────────────────────────────────────────────────────────────
        // Handle
        // ─────────────────────────────────────────────────────────────────────

        private sealed class Handle : IInterruptibleAnimationHandle
        {
            private readonly List<Action> _onCompleteCallbacks = new List<Action>();
            private bool _stopped;

            internal FrameLoopHandle Loop;

            /// <summary><see cref="Stop"/> trả target về <c>from</c>; <see cref="Interrupt"/> giữ nguyên giá trị đang có.</summary>
            internal bool RestoreOnCancel { get; private set; } = true;

            /// <summary>Chưa xong, chưa bị dừng, và vòng còn chạy (vòng dừng vì callback ném lỗi thì thôi chạy).</summary>
            public bool IsPlaying => !_stopped && !IsCompleted && (Loop == null || Loop.IsRunning);
            public bool IsCompleted { get; private set; }

            public void Stop()
            {
                if (_stopped || IsCompleted) return;
                _stopped = true;
                Loop?.Cancel();
            }

            public void Interrupt()
            {
                if (_stopped || IsCompleted) return;
                RestoreOnCancel = false;
                Stop();
            }

            public IAnimationHandle OnComplete(Action callback)
            {
                if (callback == null) return this;
                if (IsCompleted) callback.Invoke();
                else _onCompleteCallbacks.Add(callback);
                return this;
            }

            internal void MarkCompleted()
            {
                IsCompleted = true;
                foreach (Action callback in _onCompleteCallbacks) callback?.Invoke();
                _onCompleteCallbacks.Clear();
            }
        }

        private float DeltaTime(float deltaTime, float unscaledDeltaTime) =>
            TimeMode == AnimationTimeMode.Unscaled ? unscaledDeltaTime : deltaTime;

        // ─────────────────────────────────────────────────────────────────────
        // Tween (float / Vector3 / Color dùng chung một nhịp)
        // ─────────────────────────────────────────────────────────────────────

        public IAnimationHandle TweenFloat(MonoBehaviour host, float from, float to, float duration, Action<float> onUpdate,
                                           AnimationCurve curve = null, Action onStart = null, Action<float> onStep = null,
                                           Action onComplete = null)
        {
            return RunTween(host, duration, curve, onStart, onStep, onComplete,
                            eased => onUpdate?.Invoke(Mathf.LerpUnclamped(from, to, eased)),
                            () => onUpdate?.Invoke(to),
                            () => onUpdate?.Invoke(from));
        }

        public IAnimationHandle TweenVector3(MonoBehaviour host, Vector3 from, Vector3 to, float duration, Action<Vector3> onUpdate,
                                             AnimationCurve curve = null, Action onStart = null, Action<float> onStep = null,
                                             Action onComplete = null)
        {
            return RunTween(host, duration, curve, onStart, onStep, onComplete,
                            eased => onUpdate?.Invoke(Vector3.LerpUnclamped(from, to, eased)),
                            () => onUpdate?.Invoke(to),
                            () => onUpdate?.Invoke(from));
        }

        public IAnimationHandle TweenColor(MonoBehaviour host, Color from, Color to, float duration, Action<Color> onUpdate,
                                           AnimationCurve curve = null, Action onStart = null, Action<float> onStep = null,
                                           Action onComplete = null)
        {
            return RunTween(host, duration, curve, onStart, onStep, onComplete,
                            eased => onUpdate?.Invoke(Color.LerpUnclamped(from, to, eased)),
                            () => onUpdate?.Invoke(to),
                            () => onUpdate?.Invoke(from));
        }

        private IAnimationHandle RunTween(MonoBehaviour host, float duration, AnimationCurve curve, Action onStart,
                                          Action<float> onStep, Action onComplete, Action<float> applyEased, Action applyEnd,
                                          Action applyStart)
        {
            var handle = new Handle();
            AnimationCurve easing = curve ?? LinearCurve;
            float elapsed = 0f;

            void Complete()
            {
                applyEnd();
                onStep?.Invoke(1f);
                handle.MarkCompleted();
                onComplete?.Invoke();
            }

            try
            {
                onStart?.Invoke();
                if (elapsed >= duration)
                {
                    Complete();
                    return handle;
                }

                applyEased(easing.Evaluate(0f));
                onStep?.Invoke(0f);
                handle.Loop = FrameLoop.Run(host, (deltaTime, unscaledDeltaTime) =>
                {
                    elapsed += DeltaTime(deltaTime, unscaledDeltaTime);
                    if (elapsed < duration)
                    {
                        float progress = Mathf.Clamp01(elapsed / duration);
                        applyEased(easing.Evaluate(progress));
                        onStep?.Invoke(progress);
                        return true;
                    }
                    Complete();
                    return false;
                }, () =>
                {
                    if (!handle.RestoreOnCancel) return;
                    try { applyStart(); }
                    catch { /* host có thể đã bị destroy */ }
                });
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                handle.Loop?.Cancel();
            }
            return handle;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Punch (scale) / Shake (position) — luôn trả về giá trị gốc khi xong hoặc bị dừng
        // ─────────────────────────────────────────────────────────────────────

        public IAnimationHandle Punch(MonoBehaviour host, Transform target, Vector3 punchAmount, float duration, int vibrato = 10,
                                      float elasticity = 1f, Action onComplete = null)
        {
            var handle = new Handle();
            if (target == null) return handle;

            Vector3 originalScale = target.localScale;
            int safeVibrato = Mathf.Max(1, vibrato);
            float halfPeriod = duration / (safeVibrato * 2f);
            float decay = 1f / safeVibrato;
            float sinceHalfPeriod = 0f;
            int halfPeriodIndex = 0;

            void Apply(float deltaTime, float elapsedBefore)
            {
                sinceHalfPeriod += deltaTime;
                if (sinceHalfPeriod >= halfPeriod)
                {
                    sinceHalfPeriod -= halfPeriod;
                    halfPeriodIndex++;
                }
                float halfProgress = halfPeriod > 0f ? Mathf.Clamp01(sinceHalfPeriod / halfPeriod) : 1f;
                float amplitude = Mathf.Max(0f, 1f - halfPeriodIndex * decay);
                float sign = halfPeriodIndex % 2 == 0 ? 1f : -elasticity;
                target.localScale = originalScale + punchAmount * (sign * amplitude * Mathf.Sin(halfProgress * Mathf.PI));
            }

            void Restore()
            {
                try { if (target != null) target.localScale = originalScale; }
                catch { /* target có thể đã bị destroy */ }
            }

            return RunOffset(host, handle, duration, onComplete, Apply, Restore);
        }

        public IAnimationHandle Shake(MonoBehaviour host, Transform target, float strength, float duration, int vibrato = 10,
                                      float randomness = 90f, Action onComplete = null)
        {
            var handle = new Handle();
            if (target == null) return handle;

            Vector3 originalPosition = target.localPosition;
            int safeVibrato = Mathf.Max(1, vibrato);
            float timePerShake = duration / safeVibrato;
            float sinceShake = timePerShake; // lắc ngay khung đầu
            float randomnessRadians = randomness * Mathf.Deg2Rad;
            Vector3 offset = Vector3.zero;

            void Apply(float deltaTime, float elapsedBefore)
            {
                sinceShake += deltaTime;
                if (sinceShake >= timePerShake)
                {
                    sinceShake -= timePerShake;
                    float currentStrength = strength * (duration > 0f ? 1f - elapsedBefore / duration : 0f);
                    float angle = UnityEngine.Random.Range(-randomnessRadians, randomnessRadians);
                    offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * currentStrength;
                }
                target.localPosition = originalPosition + offset;
            }

            void Restore()
            {
                try { if (target != null) target.localPosition = originalPosition; }
                catch { /* target có thể đã bị destroy */ }
            }

            return RunOffset(host, handle, duration, onComplete, Apply, Restore);
        }

        /// <summary>
        /// Nhịp chung của Punch / Shake. Như bản UniTask: khung gọi áp độ lệch với delta của chính khung đó; mỗi khung sau cộng delta
        /// đã áp vào tổng thời gian, hết giờ thì trả về gốc, còn thì áp tiếp với delta mới. <paramref name="apply"/> nhận
        /// (delta khung này, tổng thời gian trước khung này).
        /// </summary>
        private IAnimationHandle RunOffset(MonoBehaviour host, Handle handle, float duration, Action onComplete,
                                           Action<float, float> apply, Action restore)
        {
            try
            {
                if (duration <= 0f)
                {
                    restore();
                    handle.MarkCompleted();
                    onComplete?.Invoke();
                    return handle;
                }

                float elapsed = 0f;
                float appliedDelta = DeltaTime(Time.deltaTime, Time.unscaledDeltaTime);
                apply(appliedDelta, elapsed);
                handle.Loop = FrameLoop.Run(host, (deltaTime, unscaledDeltaTime) =>
                {
                    elapsed += appliedDelta;
                    if (elapsed >= duration)
                    {
                        restore();
                        handle.MarkCompleted();
                        onComplete?.Invoke();
                        return false;
                    }
                    appliedDelta = DeltaTime(deltaTime, unscaledDeltaTime);
                    apply(appliedDelta, elapsed);
                    return true;
                }, restore);
            }
            catch (Exception exception)
            {
                restore();
                Debug.LogException(exception);
                handle.Loop?.Cancel();
            }
            return handle;
        }
    }
}

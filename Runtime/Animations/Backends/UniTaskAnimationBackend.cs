using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DreamTech.UICore.Animations.Backends
{
    /// <summary>
    /// Default IAnimationBackend implementation dùng UniTask thay coroutine.
    /// Zero DOTween dependency. Tất cả tween chạy trên PlayerLoopTiming.Update.
    /// </summary>
    public sealed class UniTaskAnimationBackend : IAnimationBackend
    {
        // ─────────────────────────────────────────────────────────────────────
        // Handle
        // ─────────────────────────────────────────────────────────────────────

        private sealed class UniTaskAnimationHandle : IAnimationHandle
        {
            private readonly CancellationTokenSource _cts = new CancellationTokenSource();
            private readonly List<Action> _onCompleteCallbacks = new List<Action>();
            private bool _completed;

            public CancellationToken Token => _cts.Token;
            public bool IsPlaying => !_cts.IsCancellationRequested && !_completed;
            public bool IsCompleted => _completed;

            public void Stop()
            {
                // Idempotent: handle naturally completed (CTS disposed in MarkCompleted)
                // or already cancelled — both no-op safely.
                if (_completed) return;
                try
                {
                    if (!_cts.IsCancellationRequested)
                        _cts.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // CTS disposed between MarkCompleted and our IsCancellationRequested check
                    // (race only possible if MarkCompleted runs across thread). Safe to ignore.
                }
            }

            public IAnimationHandle OnComplete(Action callback)
            {
                if (callback == null) return this;
                if (_completed)
                {
                    // Already done — invoke immediately
                    callback.Invoke();
                }
                else
                {
                    _onCompleteCallbacks.Add(callback);
                }
                return this;
            }

            /// <summary>Gọi bởi RunAsync khi tween kết thúc tự nhiên.</summary>
            public void MarkCompleted()
            {
                _completed = true;
                foreach (var cb in _onCompleteCallbacks)
                    cb?.Invoke();
                _onCompleteCallbacks.Clear();
                // Dispose CTS để giải phóng resource
                _cts.Dispose();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // TweenFloat
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
            var handle = new UniTaskAnimationHandle();
            var linkedCts = CancellationTokenSource
                .CreateLinkedTokenSource(handle.Token, host.GetCancellationTokenOnDestroy());
            RunTweenFloat(handle, from, to, duration, onUpdate, curve, onStart, onStep, onComplete, linkedCts).Forget();
            return handle;
        }

        private static async UniTaskVoid RunTweenFloat(
            UniTaskAnimationHandle handle,
            float from, float to, float duration,
            Action<float> onUpdate,
            AnimationCurve curve,
            Action onStart,
            Action<float> onStep,
            Action onComplete,
            CancellationTokenSource linkedCts)
        {
            var ct = linkedCts.Token;
            try
            {
                onStart?.Invoke();
                var c = curve ?? AnimationCurve.Linear(0f, 0f, 1f, 1f);
                float elapsed = 0f;

                while (elapsed < duration)
                {
                    ct.ThrowIfCancellationRequested();
                    float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
                    float v = Mathf.LerpUnclamped(from, to, c.Evaluate(t));
                    onUpdate?.Invoke(v);
                    onStep?.Invoke(t);
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    elapsed += Time.deltaTime; // deltaTime read after yield = same frame
                }

                // Final exact value
                onUpdate?.Invoke(to);
                onStep?.Invoke(1f);
                handle.MarkCompleted();
                onComplete?.Invoke();
            }
            catch (OperationCanceledException)
            {
                // Restore to 'from' on cancel so caller can decide; silent exit
                try { onUpdate?.Invoke(from); } catch { /* host may be destroyed */ }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                linkedCts.Dispose();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // TweenVector3
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
            var handle = new UniTaskAnimationHandle();
            var linkedCts = CancellationTokenSource
                .CreateLinkedTokenSource(handle.Token, host.GetCancellationTokenOnDestroy());
            RunTweenVector3(handle, from, to, duration, onUpdate, curve, onStart, onStep, onComplete, linkedCts).Forget();
            return handle;
        }

        private static async UniTaskVoid RunTweenVector3(
            UniTaskAnimationHandle handle,
            Vector3 from, Vector3 to, float duration,
            Action<Vector3> onUpdate,
            AnimationCurve curve,
            Action onStart,
            Action<float> onStep,
            Action onComplete,
            CancellationTokenSource linkedCts)
        {
            var ct = linkedCts.Token;
            try
            {
                onStart?.Invoke();
                var c = curve ?? AnimationCurve.Linear(0f, 0f, 1f, 1f);
                float elapsed = 0f;

                while (elapsed < duration)
                {
                    ct.ThrowIfCancellationRequested();
                    float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
                    Vector3 v = Vector3.LerpUnclamped(from, to, c.Evaluate(t));
                    onUpdate?.Invoke(v);
                    onStep?.Invoke(t);
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    elapsed += Time.deltaTime;
                }

                onUpdate?.Invoke(to);
                onStep?.Invoke(1f);
                handle.MarkCompleted();
                onComplete?.Invoke();
            }
            catch (OperationCanceledException)
            {
                try { onUpdate?.Invoke(from); } catch { }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                linkedCts.Dispose();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // TweenColor
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
            var handle = new UniTaskAnimationHandle();
            var linkedCts = CancellationTokenSource
                .CreateLinkedTokenSource(handle.Token, host.GetCancellationTokenOnDestroy());
            RunTweenColor(handle, from, to, duration, onUpdate, curve, onStart, onStep, onComplete, linkedCts).Forget();
            return handle;
        }

        private static async UniTaskVoid RunTweenColor(
            UniTaskAnimationHandle handle,
            Color from, Color to, float duration,
            Action<Color> onUpdate,
            AnimationCurve curve,
            Action onStart,
            Action<float> onStep,
            Action onComplete,
            CancellationTokenSource linkedCts)
        {
            var ct = linkedCts.Token;
            try
            {
                onStart?.Invoke();
                var c = curve ?? AnimationCurve.Linear(0f, 0f, 1f, 1f);
                float elapsed = 0f;

                while (elapsed < duration)
                {
                    ct.ThrowIfCancellationRequested();
                    float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
                    Color v = Color.LerpUnclamped(from, to, c.Evaluate(t));
                    onUpdate?.Invoke(v);
                    onStep?.Invoke(t);
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    elapsed += Time.deltaTime;
                }

                onUpdate?.Invoke(to);
                onStep?.Invoke(1f);
                handle.MarkCompleted();
                onComplete?.Invoke();
            }
            catch (OperationCanceledException)
            {
                try { onUpdate?.Invoke(from); } catch { }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                linkedCts.Dispose();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Punch
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
            var handle = new UniTaskAnimationHandle();
            var linkedCts = CancellationTokenSource
                .CreateLinkedTokenSource(handle.Token, host.GetCancellationTokenOnDestroy());
            RunPunch(handle, target, punchAmount, duration, vibrato, elasticity, onComplete, linkedCts).Forget();
            return handle;
        }

        private static async UniTaskVoid RunPunch(
            UniTaskAnimationHandle handle,
            Transform target,
            Vector3 punchAmount,
            float duration,
            int vibrato,
            float elasticity,
            Action onComplete,
            CancellationTokenSource linkedCts)
        {
            if (target == null)
            {
                linkedCts.Dispose();
                return;
            }

            Vector3 originalScale = target.localScale;
            var ct = linkedCts.Token;

            try
            {
                float elapsed = 0f;
                int safeVibrato = Mathf.Max(1, vibrato);
                float halfPeriod = duration / (safeVibrato * 2f);
                // Decay: each half-period the amplitude reduces
                float decay = 1f / safeVibrato;
                float elapsedSinceLastHalfPeriod = 0f;
                int halfPeriodIndex = 0;

                while (elapsed < duration)
                {
                    ct.ThrowIfCancellationRequested();

                    // Advance accumulator BEFORE applying — both use same Time.deltaTime snapshot
                    float dt = Time.deltaTime;
                    elapsedSinceLastHalfPeriod += dt;

                    if (elapsedSinceLastHalfPeriod >= halfPeriod)
                    {
                        elapsedSinceLastHalfPeriod -= halfPeriod;
                        halfPeriodIndex++;
                    }

                    // Progress within current half-period: 0..1
                    float halfT = Mathf.Clamp01(elapsedSinceLastHalfPeriod / halfPeriod);

                    // Amplitude decays each half-period
                    float amplitudeScale = Mathf.Max(0f, 1f - halfPeriodIndex * decay);

                    // Direction: alternates sign each half-period, scaled by elasticity
                    float sign = (halfPeriodIndex % 2 == 0) ? 1f : -elasticity;

                    // Sine easing within each half-period for smoothness
                    float sineT = Mathf.Sin(halfT * Mathf.PI);
                    Vector3 offset = punchAmount * (sign * amplitudeScale * sineT);

                    target.localScale = originalScale + offset;

                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    elapsed += dt;
                }

                // Restore exact original scale
                target.localScale = originalScale;
                handle.MarkCompleted();
                onComplete?.Invoke();
            }
            catch (OperationCanceledException)
            {
                // Restore on cancel — target may still be alive
                try { if (target != null) target.localScale = originalScale; } catch { }
            }
            catch (Exception e)
            {
                try { if (target != null) target.localScale = originalScale; } catch { }
                Debug.LogException(e);
            }
            finally
            {
                linkedCts.Dispose();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Shake
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
            var handle = new UniTaskAnimationHandle();
            var linkedCts = CancellationTokenSource
                .CreateLinkedTokenSource(handle.Token, host.GetCancellationTokenOnDestroy());
            RunShake(handle, target, strength, duration, vibrato, randomness, onComplete, linkedCts).Forget();
            return handle;
        }

        private static async UniTaskVoid RunShake(
            UniTaskAnimationHandle handle,
            Transform target,
            float strength,
            float duration,
            int vibrato,
            float randomness,
            Action onComplete,
            CancellationTokenSource linkedCts)
        {
            if (target == null)
            {
                linkedCts.Dispose();
                return;
            }

            Vector3 originalPosition = target.localPosition;
            var ct = linkedCts.Token;

            try
            {
                float elapsed = 0f;
                int safeVibrato = Mathf.Max(1, vibrato);
                // Time per shake interval — accumulator pattern, no WaitForSeconds alloc
                float timePerShake = duration / safeVibrato;
                float elapsedSinceLastShake = timePerShake; // Trigger immediately on first frame

                Vector3 currentOffset = Vector3.zero;
                float randomnessRad = randomness * Mathf.Deg2Rad;

                while (elapsed < duration)
                {
                    ct.ThrowIfCancellationRequested();

                    float dt = Time.deltaTime;
                    elapsedSinceLastShake += dt;

                    if (elapsedSinceLastShake >= timePerShake)
                    {
                        elapsedSinceLastShake -= timePerShake;

                        // Decay strength over time
                        float decayFactor = 1f - (elapsed / duration);
                        float currentStrength = strength * decayFactor;

                        // Random direction with randomness angle
                        float angle = UnityEngine.Random.Range(-randomnessRad, randomnessRad);
                        Vector3 dir = new Vector3(
                            Mathf.Cos(angle),
                            Mathf.Sin(angle),
                            0f
                        );
                        currentOffset = dir * currentStrength;
                    }

                    target.localPosition = originalPosition + currentOffset;

                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    elapsed += dt;
                }

                // Restore exact original position
                target.localPosition = originalPosition;
                handle.MarkCompleted();
                onComplete?.Invoke();
            }
            catch (OperationCanceledException)
            {
                // Restore on cancel
                try { if (target != null) target.localPosition = originalPosition; } catch { }
            }
            catch (Exception e)
            {
                try { if (target != null) target.localPosition = originalPosition; } catch { }
                Debug.LogException(e);
            }
            finally
            {
                linkedCts.Dispose();
            }
        }
    }
}

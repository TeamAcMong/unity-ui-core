using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DreamTech.UICore.Animations;
using UnityEngine;
using UnityEngine.Events;

namespace DreamTech.UICore.Behaviors
{
    /// <summary>
    /// Giữ button → fire click event lặp lại theo interval.
    /// Bắt đầu sau initialDelay, repeat mỗi repeatInterval. Optional acceleration.
    /// </summary>
    [Serializable]
    public class HoldRepeatBehavior : BehaviorModuleBase
    {
        [SerializeField, Min(0.05f), Tooltip("Delay trước repeat đầu tiên (giây).")]
        private float initialDelay = 0.4f;

        [SerializeField, Min(0.01f), Tooltip("Interval giữa các repeat (giây).")]
        private float repeatInterval = 0.1f;

        [SerializeField, Tooltip("Bật để giảm interval dần theo thời gian giữ (acceleration).")]
        private bool accelerate = false;

        [SerializeField, Range(0.1f, 1f), Tooltip("Hệ số giảm interval. 0.3 = sau acceleration interval còn 30%.")]
        private float minIntervalRatio = 0.3f;

        [SerializeField, Min(0.5f), Tooltip("Thời gian để đạt min interval (giây).")]
        private float accelerateDuration = 2f;

        [Header("Events")]
        public UnityEvent onRepeat = new();

        public override string DisplayName => "Hold Repeat";

        private CancellationTokenSource _repeatCts;

        public override void OnPointerStateChanged(UIState newState)
        {
            if (!enabled || host == null) return;
            if (newState == UIState.Pressed)
            {
                StartRepeating();
            }
            else
            {
                StopRepeating();
            }
        }

        public override void Dispose()
        {
            StopRepeating();
        }

        private void StartRepeating()
        {
            StopRepeating();
            _repeatCts = CancellationTokenSource.CreateLinkedTokenSource(
                host != null ? host.GetCancellationTokenOnDestroy() : default);
            RunRepeatAsync(_repeatCts).Forget();
        }

        private void StopRepeating()
        {
            if (_repeatCts != null)
            {
                try { if (!_repeatCts.IsCancellationRequested) _repeatCts.Cancel(); }
                catch (ObjectDisposedException) { }
                _repeatCts = null;
            }
        }

        private async UniTaskVoid RunRepeatAsync(CancellationTokenSource cts)
        {
            var ct = cts.Token;
            try
            {
                // Initial delay before first repeat
                await UniTask.Delay(TimeSpan.FromSeconds(initialDelay), ignoreTimeScale: true, cancellationToken: ct);

                float elapsedSinceStart = 0f;
                while (true)
                {
                    ct.ThrowIfCancellationRequested();
                    onRepeat?.Invoke();

                    float currentInterval = repeatInterval;
                    if (accelerate)
                    {
                        float t = Mathf.Clamp01(elapsedSinceStart / accelerateDuration);
                        currentInterval = Mathf.Lerp(repeatInterval, repeatInterval * minIntervalRatio, t);
                    }

                    await UniTask.Delay(TimeSpan.FromSeconds(currentInterval), ignoreTimeScale: true, cancellationToken: ct);
                    elapsedSinceStart += currentInterval;
                }
            }
            catch (OperationCanceledException) { /* released */ }
            finally
            {
                cts.Dispose();
                if (ReferenceEquals(_repeatCts, cts)) _repeatCts = null;
            }
        }
    }
}

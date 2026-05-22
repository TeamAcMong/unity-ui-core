using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DreamTech.UICore.Animations;
using UnityEngine;
using UnityEngine.Events;

namespace DreamTech.UICore.Behaviors
{
    /// <summary>
    /// Detect user hold button >= threshold → fire OnLongPress.
    /// Designer chọn có cancel click event không khi long-press triggered.
    /// </summary>
    [Serializable]
    public class LongPressBehavior : BehaviorModuleBase
    {
        [SerializeField, Min(0.1f), Tooltip("Thời gian giữ (giây) trước khi fire long-press.")]
        private float threshold = 0.7f;

        [SerializeField, Tooltip("Nếu true: long-press triggered sẽ cancel regular click.")]
        private bool consumeClick = true;

        [Header("Events")]
        public UnityEvent onLongPress = new();
        public UnityEvent<float> onProgress = new();  // 0..1 progress

        public override string DisplayName => "Long Press";

        private bool _longPressTriggered;
        private CancellationTokenSource _detectCts;

        public override void OnPointerStateChanged(UIState newState)
        {
            if (!enabled || host == null) return;
            if (newState == UIState.Pressed)
            {
                StartDetection();
            }
            else
            {
                StopDetection();
            }
        }

        public override bool OnBeforeClick()
        {
            if (!enabled) return true;
            // Nếu long-press đã fire VÀ designer set consumeClick → cancel click
            if (_longPressTriggered && consumeClick)
            {
                _longPressTriggered = false;  // reset cho lần sau
                return false;
            }
            _longPressTriggered = false;
            return true;
        }

        public override void Dispose()
        {
            StopDetection();
        }

        private void StartDetection()
        {
            StopDetection();
            _longPressTriggered = false;
            _detectCts = CancellationTokenSource.CreateLinkedTokenSource(
                host != null ? host.GetCancellationTokenOnDestroy() : default);
            RunDetectionAsync(_detectCts).Forget();
        }

        private void StopDetection()
        {
            if (_detectCts != null)
            {
                try { if (!_detectCts.IsCancellationRequested) _detectCts.Cancel(); }
                catch (ObjectDisposedException) { }
                _detectCts = null;
            }
        }

        private async UniTaskVoid RunDetectionAsync(CancellationTokenSource cts)
        {
            var ct = cts.Token;
            try
            {
                float elapsed = 0f;
                onProgress?.Invoke(0f);
                while (elapsed < threshold)
                {
                    ct.ThrowIfCancellationRequested();
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    elapsed += Time.unscaledDeltaTime;
                    onProgress?.Invoke(Mathf.Clamp01(elapsed / threshold));
                }
                _longPressTriggered = true;
                onLongPress?.Invoke();
                onProgress?.Invoke(1f);
            }
            catch (OperationCanceledException)
            {
                onProgress?.Invoke(0f);
            }
            finally
            {
                cts.Dispose();
                if (ReferenceEquals(_detectCts, cts)) _detectCts = null;
            }
        }
    }
}

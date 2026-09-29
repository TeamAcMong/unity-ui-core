using System;
using DreamTech.UICore.Animations;
using DreamTech.UICore.Base;
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
        private FrameLoopHandle _detectLoop;

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

            // Giờ thật: giữ đủ threshold giây thì bắn. Nhả / rời nút / host bị destroy trước đó thì tiến độ về 0.
            float elapsed = 0f;
            onProgress?.Invoke(0f);
            _detectLoop = FrameLoop.Run(host, (deltaTime, unscaledDeltaTime) =>
            {
                elapsed += unscaledDeltaTime;
                onProgress?.Invoke(Mathf.Clamp01(elapsed / threshold));
                if (elapsed < threshold) return true;

                _longPressTriggered = true;
                onLongPress?.Invoke();
                onProgress?.Invoke(1f);
                return false;
            }, () => onProgress?.Invoke(0f));
        }

        private void StopDetection()
        {
            _detectLoop?.Cancel();
            _detectLoop = null;
        }
    }
}

using System;
using DreamTech.UICore.Animations;
using DreamTech.UICore.Base;
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

        private FrameLoopHandle _repeatLoop;

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

            // Giờ thật (không theo timeScale): chờ initialDelay, bắn, rồi chờ từng khoảng (nhanh dần nếu bật accelerate).
            float waitRemaining = initialDelay;
            float elapsedSinceStart = 0f;
            float lastInterval = 0f;
            bool fired = false;

            _repeatLoop = FrameLoop.Run(host, (deltaTime, unscaledDeltaTime) =>
            {
                waitRemaining -= unscaledDeltaTime;
                if (waitRemaining > 0f) return true;

                if (fired) elapsedSinceStart += lastInterval;
                fired = true;
                onRepeat?.Invoke();

                lastInterval = repeatInterval;
                if (accelerate)
                {
                    float t = Mathf.Clamp01(elapsedSinceStart / accelerateDuration);
                    lastInterval = Mathf.Lerp(repeatInterval, repeatInterval * minIntervalRatio, t);
                }
                waitRemaining = lastInterval;
                return true;
            });
        }

        private void StopRepeating()
        {
            _repeatLoop?.Cancel();
            _repeatLoop = null;
        }
    }
}

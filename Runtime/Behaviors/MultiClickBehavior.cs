using System;
using UnityEngine;
using UnityEngine.Events;

namespace DreamTech.UICore.Behaviors
{
    /// <summary>
    /// Yêu cầu N clicks liên tiếp trong khoảng time window. Single click bị consume,
    /// chỉ click thứ N mới fire host's OnInteract. Useful cho double-click pattern.
    /// </summary>
    [Serializable]
    public class MultiClickBehavior : BehaviorModuleBase
    {
        [SerializeField, Range(2, 10), Tooltip("Số click cần để trigger.")]
        private int requiredClicks = 2;

        [SerializeField, Min(0.1f), Tooltip("Time window giữa các click (giây). Quá window → reset counter.")]
        private float windowDuration = 0.4f;

        [Header("Events")]
        public UnityEvent<int> onClickCountChanged = new();  // current count
        public UnityEvent onMultiClickTriggered = new();

        public override string DisplayName => "Multi-Click";

        private int _clickCount;
        private float _lastClickTime = -999f;

        public override bool OnBeforeClick()
        {
            if (!enabled) return true;

            float now = Time.unscaledTime;
            if (now - _lastClickTime > windowDuration)
            {
                _clickCount = 0;  // expired, reset
            }

            _clickCount++;
            _lastClickTime = now;
            onClickCountChanged?.Invoke(_clickCount);

            if (_clickCount >= requiredClicks)
            {
                _clickCount = 0;
                onMultiClickTriggered?.Invoke();
                return true;  // pass click through → host's OnInteract fire
            }

            return false;  // consume click, chưa đủ N
        }
    }
}

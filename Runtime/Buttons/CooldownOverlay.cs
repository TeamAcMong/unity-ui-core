using DreamTech.UICore.Base;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DreamTech.UICore.Buttons
{
    /// <summary>
    /// Cooldown progress overlay. Set target progress, hiển thị mượt với MoveTowards.
    /// Hỗ trợ Image filled hoặc Sliced fill mode.
    /// KHÔNG dùng Update — vòng làm mượt (<see cref="FrameLoop"/>) chỉ chạy khi cần catch-up.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class CooldownOverlay : UIAnimatedComponent
    {
        [Header("Overlay Settings")]
        [SerializeField] private Image fillImage;
        [SerializeField, Tooltip("Units per second (0..1 progress). Higher = faster catch-up.")]
        private float smoothSpeed = 10f;

        [Header("Optional Text")]
        [SerializeField] private TextMeshProUGUI countdownText;
        [SerializeField] private bool showCountdown = false;
        [SerializeField] private float maxDuration = 3f;  // dùng để tính seconds từ progress

        private float _currentDisplayProgress = 1f;
        private float _targetProgress = 1f;

        private FrameLoopHandle _smoothLoop;
        private bool IsSmoothing => _smoothLoop != null && _smoothLoop.IsRunning;

        protected override void Awake()
        {
            base.Awake();
            if (fillImage == null) fillImage = GetComponent<Image>();
            ApplyVisual(1f);
        }

        protected override void OnDestroy()
        {
            CancelSmoothLoop();
            base.OnDestroy();
        }

        /// <summary>Set target progress 0..1 (0 = empty/cooling, 1 = full/ready).</summary>
        public void SetProgress(float value)
        {
            _targetProgress = Mathf.Clamp01(value);
            if (Mathf.Approximately(_currentDisplayProgress, _targetProgress)) return;
            if (IsSmoothing) return;  // loop đang chạy sẽ tự catch up
            StartSmoothLoop();
        }

        /// <summary>Set immediately without smoothing.</summary>
        public void SetProgressImmediate(float value)
        {
            CancelSmoothLoop();
            _targetProgress = Mathf.Clamp01(value);
            _currentDisplayProgress = _targetProgress;
            ApplyVisual(_currentDisplayProgress);
        }

        private void StartSmoothLoop()
        {
            CancelSmoothLoop();
            _smoothLoop = FrameLoop.Run(this, TickSmooth);
        }

        private bool TickSmooth(float deltaTime, float unscaledDeltaTime)
        {
            _currentDisplayProgress = Mathf.MoveTowards(_currentDisplayProgress, _targetProgress, smoothSpeed * deltaTime);
            ApplyVisual(_currentDisplayProgress);
            if (!Mathf.Approximately(_currentDisplayProgress, _targetProgress)) return true;

            // snap để loại bỏ epsilon drift
            _currentDisplayProgress = _targetProgress;
            ApplyVisual(_currentDisplayProgress);
            return false;
        }

        private void CancelSmoothLoop()
        {
            _smoothLoop?.Cancel();
            _smoothLoop = null;
        }

        private void ApplyVisual(float progress)
        {
            if (fillImage != null) fillImage.fillAmount = progress;
            if (countdownText != null && showCountdown)
            {
                float remaining = (1f - progress) * maxDuration;
                countdownText.text = remaining > 0.1f ? $"{remaining:F1}" : "";
            }
        }
    }
}

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DreamTech.UICore.Animations.Backends;
using DreamTech.UICore.Base;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DreamTech.UICore.ProgressBars
{
    /// <summary>Cách hiển thị fill bar.</summary>
    public enum FillMode
    {
        /// <summary><see cref="Image.fillAmount"/> trên Image type=Filled.</summary>
        Filled,
        /// <summary>Stretch qua anchor (9-patch friendly).</summary>
        Sliced,
        /// <summary>Scale child trong mask để clip.</summary>
        Masked,
        /// <summary>Set localScale.x/y trực tiếp.</summary>
        Scaled,
    }

    /// <summary>Trục fill cho mode <see cref="FillMode.Sliced"/>/<see cref="FillMode.Masked"/>/<see cref="FillMode.Scaled"/>.</summary>
    public enum FillDirection
    {
        Horizontal,
        Vertical,
    }

    /// <summary>Mode lerp <c>currentValue → displayValue</c>.</summary>
    public enum ValueAnimationMode
    {
        /// <summary>Snap ngay không animate.</summary>
        Instant,
        /// <summary>Linear theo duration.</summary>
        Smooth,
        /// <summary><see cref="Mathf.SmoothStep"/> easing.</summary>
        EaseInOut,
        /// <summary>Mass-spring-damper physics (overshoot/bounce).</summary>
        Spring,
    }

    /// <summary>Cách quyết định màu fill.</summary>
    public enum ColorMode
    {
        /// <summary>1 màu cố định.</summary>
        Solid,
        /// <summary>3 màu theo threshold (critical/warning/healthy).</summary>
        Threshold,
        /// <summary><see cref="Gradient"/> theo normalized value.</summary>
        Gradient,
    }

    /// <summary>Format hiển thị giá trị trên text.</summary>
    public enum TextFormat
    {
        None,
        Integer,
        Decimal1,
        Percent,
        Custom,
    }

    /// <summary>Cờ trigger flash effect — có thể combine nhiều flag.</summary>
    [Flags]
    public enum FlashTrigger
    {
        None = 0,
        OnReachMax = 1 << 0,
        OnReachMin = 1 << 1,
        OnDamage = 1 << 2,
        OnHeal = 1 << 3,
    }

    /// <summary>
    /// Progress bar đa năng: animated value lerp, 4 fill mode, 3 color mode,
    /// optional text, flash + pulse effect, full UniTask-based — KHÔNG coroutine.
    /// <para>
    /// Kế thừa <see cref="UIAnimatedComponent"/> nên vẫn có thể attach
    /// animation modules ngoài qua Inspector (ví dụ punch khi value đổi).
    /// </para>
    /// <para>
    /// Auto-find component (fillRect/fillImage/backgroundImage/overlayImage/valueText)
    /// trong <see cref="Awake"/> chỉ khi field null — không overwrite assignment của designer.
    /// </para>
    /// </summary>
    [AddComponentMenu("DreamTech UI/Advanced Progress Bar")]
    public class AdvancedProgressBar : UIAnimatedComponent
    {
        [Header("Value")]
        [SerializeField] private float currentValue = 0.5f;
        [SerializeField] private float maxValue = 1f;
        [SerializeField] private float minValue = 0f;

        [Header("References (auto-find if null)")]
        [SerializeField] private RectTransform fillRect;
        [SerializeField] private Image fillImage;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image overlayImage;
        [SerializeField] private TMP_Text valueText;

        [Header("Fill Settings")]
        [SerializeField] private FillMode fillMode = FillMode.Filled;
        [SerializeField] private FillDirection fillDirection = FillDirection.Horizontal;

        [Header("Value Animation")]
        [SerializeField] private ValueAnimationMode valueAnimationMode = ValueAnimationMode.Smooth;
        [SerializeField] private float animationDuration = 0.3f;
        [SerializeField] private float springDamping = 0.5f;
        [SerializeField] private float springFrequency = 6f;

        [Header("Color")]
        [SerializeField] private ColorMode colorMode = ColorMode.Solid;
        [SerializeField] private Color solidColor = Color.green;
        [SerializeField, Range(0f, 1f)] private float lowThreshold = 0.25f;
        [SerializeField, Range(0f, 1f)] private float midThreshold = 0.5f;
        [SerializeField] private Color criticalColor = Color.red;
        [SerializeField] private Color warningColor = Color.yellow;
        [SerializeField] private Color healthyColor = Color.green;
        [SerializeField] private Gradient gradient;

        [Header("Text")]
        [SerializeField] private TextFormat textFormat = TextFormat.Percent;
        [SerializeField] private string textPrefix = "";
        [SerializeField] private string textSuffix = "";
        [SerializeField] private string customFormat = "{0:F1}";

        [Header("Flash Effect")]
        [SerializeField] private FlashTrigger flashTrigger = FlashTrigger.None;
        [SerializeField] private Color flashColor = Color.white;
        [SerializeField] private float flashDuration = 0.2f;

        [Header("Pulse Effect")]
        [SerializeField] private bool pulseOnMax = false;
        [SerializeField] private float pulseScale = 1.05f;
        [SerializeField] private float pulseDuration = 0.5f;

        // ─────────────────────────────────────────────────────────────────────
        // Events
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Fire mỗi khi <see cref="SetValue"/> đổi value (sau clamp, trước animate).</summary>
        public UnityEngine.Events.UnityEvent<float> onValueChanged = new();

        /// <summary>Fire khi value chạm <see cref="maxValue"/>.</summary>
        public UnityEngine.Events.UnityEvent onReachMax = new();

        /// <summary>Fire khi value chạm <see cref="minValue"/>.</summary>
        public UnityEngine.Events.UnityEvent onReachMin = new();

        // ─────────────────────────────────────────────────────────────────────
        // Runtime state
        // ─────────────────────────────────────────────────────────────────────

        private float _displayValue;
        private float _targetValue;
        private float _springVelocity;
        private float _previousValue;

        // Issue #7 fix — cache last applied sliced fill state để skip rebuild RectTransform anchor.
        private float _lastSlicedNormalizedValue = -1f;
        private FillDirection _lastFillDirection;

        // Issue #2 fix — track active flash/pulse handle để Stop trước khi start cái mới
        // (tránh multi-tween race condition).
        private IAnimationHandle _flashHandle;
        private IAnimationHandle _pulseHandle;

        private CancellationTokenSource _valueAnimCts;
        private Color _originalFillColor;
        private Vector3 _overlayInitialScale = Vector3.one;

        // ─────────────────────────────────────────────────────────────────────
        // Public read-only props
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Giá trị target (đã clamp). KHÔNG phải display value.</summary>
        public float CurrentValue => currentValue;

        /// <summary>Giá trị đang hiển thị (theo animation lerp). Khác <see cref="CurrentValue"/> khi đang animate.</summary>
        public float DisplayValue => _displayValue;

        /// <summary>Display value normalized về [0,1] theo min/max.</summary>
        public float NormalizedValue => Mathf.InverseLerp(minValue, maxValue, _displayValue);

        public bool IsFull => Mathf.Approximately(_displayValue, maxValue);
        public bool IsEmpty => Mathf.Approximately(_displayValue, minValue);

        // ─────────────────────────────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();

            // Issue #8 fix — designer-friendly auto-find. Chỉ chạy khi null, không destructive.
            AutoFindComponents();

            _displayValue = currentValue;
            _targetValue = currentValue;
            _previousValue = currentValue;

            if (fillImage != null) _originalFillColor = fillImage.color;
            if (overlayImage != null) _overlayInitialScale = overlayImage.transform.localScale;

            UpdateVisual(_displayValue);
        }

        protected override void OnDestroy()
        {
            CancelValueAnimation();
            _flashHandle?.Stop();
            _pulseHandle?.Stop();
            _flashHandle = null;
            _pulseHandle = null;
            base.OnDestroy();
        }

        private void AutoFindComponents()
        {
            if (fillRect == null)
            {
                var fillTr = transform.Find("Fill");
                if (fillTr != null) fillRect = fillTr as RectTransform;
            }
            if (fillImage == null && fillRect != null)
                fillImage = fillRect.GetComponent<Image>();

            if (backgroundImage == null)
            {
                var bgTr = transform.Find("Background");
                if (bgTr != null) backgroundImage = bgTr.GetComponent<Image>();
            }
            if (overlayImage == null)
            {
                var ovTr = transform.Find("Overlay");
                if (ovTr != null) overlayImage = ovTr.GetComponent<Image>();
            }
            if (valueText == null) valueText = GetComponentInChildren<TMP_Text>();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public API — set value
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Set giá trị target. Clamp tự động vào [<see cref="minValue"/>, <see cref="maxValue"/>].
        /// </summary>
        /// <param name="value">Giá trị mới.</param>
        /// <param name="animate">
        /// <c>true</c> = lerp theo <see cref="valueAnimationMode"/>.
        /// <c>false</c> = snap ngay (override mode).
        /// </param>
        public void SetValue(float value, bool animate = true)
        {
            value = Mathf.Clamp(value, minValue, maxValue);
            if (Mathf.Approximately(currentValue, value)) return;

            _previousValue = currentValue;
            currentValue = value;
            _targetValue = value;
            onValueChanged?.Invoke(currentValue);

            if (!animate || valueAnimationMode == ValueAnimationMode.Instant)
            {
                CancelValueAnimation();
                _displayValue = value;
                UpdateVisual(_displayValue);
            }
            else
            {
                StartValueAnimation();
            }

            CheckFlashTriggers(_previousValue, value);

            if (Mathf.Approximately(value, maxValue))
            {
                onReachMax?.Invoke();
                if (pulseOnMax) StartPulse();
            }
            else
            {
                // Value đã rời khỏi max → đảm bảo pulse stop (không cần check IsFull bên trong loop chỉ).
                StopPulse();
            }

            if (Mathf.Approximately(value, minValue))
                onReachMin?.Invoke();
        }

        /// <summary>Set theo normalized 0..1 (auto Lerp về min/max).</summary>
        public void SetValueNormalized(float normalized, bool animate = true)
        {
            SetValue(Mathf.Lerp(minValue, maxValue, Mathf.Clamp01(normalized)), animate);
        }

        /// <summary>
        /// Đổi max value. Nếu <paramref name="keepRatio"/>=true, giữ nguyên tỉ lệ display
        /// (ví dụ HP 50/100 → upgrade max=200 sẽ thành 100/200).
        /// </summary>
        public void SetMaxValue(float max, bool keepRatio = true)
        {
            if (keepRatio && maxValue > 0)
            {
                float ratio = currentValue / maxValue;
                maxValue = max;
                SetValue(max * ratio, animate: false);
            }
            else
            {
                maxValue = max;
                SetValue(Mathf.Min(currentValue, max), animate: false);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Value animation (UniTask-based)
        // ─────────────────────────────────────────────────────────────────────

        private void StartValueAnimation()
        {
            CancelValueAnimation();
            _valueAnimCts = CancellationTokenSource.CreateLinkedTokenSource(
                this.GetCancellationTokenOnDestroy());
            RunValueAnimationAsync(_valueAnimCts.Token).Forget();
        }

        private async UniTaskVoid RunValueAnimationAsync(CancellationToken ct)
        {
            try
            {
                if (valueAnimationMode == ValueAnimationMode.Spring)
                {
                    // Mass-spring-damper: F = -kx - cv, with k = ω², c = 2ζω.
                    while (!Mathf.Approximately(_displayValue, _targetValue)
                           || Mathf.Abs(_springVelocity) > 0.001f)
                    {
                        ct.ThrowIfCancellationRequested();
                        await UniTask.Yield(PlayerLoopTiming.Update, ct);
                        float dt = Time.deltaTime;
                        float displacement = _targetValue - _displayValue;
                        float springForce = displacement * springFrequency * springFrequency;
                        float dampingForce = -2f * springDamping * springFrequency * _springVelocity;
                        _springVelocity += (springForce + dampingForce) * dt;
                        _displayValue += _springVelocity * dt;
                        UpdateVisual(_displayValue);
                    }
                    _displayValue = _targetValue;
                    _springVelocity = 0f;
                }
                else
                {
                    float startValue = _displayValue;
                    float endValue = _targetValue;
                    float elapsed = 0f;
                    while (elapsed < animationDuration
                           && !Mathf.Approximately(_displayValue, endValue))
                    {
                        ct.ThrowIfCancellationRequested();
                        await UniTask.Yield(PlayerLoopTiming.Update, ct);
                        elapsed += Time.deltaTime;
                        float t = animationDuration > 0f
                            ? Mathf.Clamp01(elapsed / animationDuration)
                            : 1f;
                        if (valueAnimationMode == ValueAnimationMode.EaseInOut)
                            t = Mathf.SmoothStep(0f, 1f, t);
                        _displayValue = Mathf.Lerp(startValue, endValue, t);
                        UpdateVisual(_displayValue);
                    }
                    _displayValue = endValue;
                }
                UpdateVisual(_displayValue);
            }
            catch (OperationCanceledException)
            {
                // Cancel là expected khi: object destroyed, SetValue gọi lại, hoặc SetValue(animate:false).
            }
        }

        private void CancelValueAnimation()
        {
            if (_valueAnimCts == null) return;
            _valueAnimCts.Cancel();
            _valueAnimCts.Dispose();
            _valueAnimCts = null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Visual update
        // ─────────────────────────────────────────────────────────────────────

        private void UpdateVisual(float value)
        {
            float normalized = Mathf.InverseLerp(minValue, maxValue, value);

            switch (fillMode)
            {
                case FillMode.Filled:
                    if (fillImage != null) fillImage.fillAmount = normalized;
                    break;
                case FillMode.Sliced:
                    UpdateSlicedFill(normalized);
                    break;
                case FillMode.Masked:
                    UpdateMaskedFill(normalized);
                    break;
                case FillMode.Scaled:
                    UpdateScaledFill(normalized);
                    break;
            }

            UpdateColor(normalized);
            UpdateText(value, normalized);
        }

        private void UpdateSlicedFill(float normalized)
        {
            // Issue #7 fix — skip rebuild khi normalized + direction unchanged.
            // Tránh dirty layout pass mỗi frame khi value đứng yên (ví dụ spring đã settled
            // nhưng visual loop vẫn chạy 1 frame thừa do tolerance).
            if (Mathf.Approximately(normalized, _lastSlicedNormalizedValue)
                && fillDirection == _lastFillDirection)
                return;

            if (fillRect == null) return;

            if (fillDirection == FillDirection.Horizontal)
            {
                fillRect.anchorMin = new Vector2(0, 0);
                fillRect.anchorMax = new Vector2(normalized, 1);
                fillRect.pivot = new Vector2(0, 0.5f);
            }
            else
            {
                fillRect.anchorMin = new Vector2(0, 0);
                fillRect.anchorMax = new Vector2(1, normalized);
                fillRect.pivot = new Vector2(0.5f, 0);
            }
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            _lastSlicedNormalizedValue = normalized;
            _lastFillDirection = fillDirection;
        }

        private void UpdateMaskedFill(float normalized)
        {
            if (fillRect == null) return;
            fillRect.localScale = fillDirection == FillDirection.Horizontal
                ? new Vector3(normalized, 1f, 1f)
                : new Vector3(1f, normalized, 1f);
        }

        private void UpdateScaledFill(float normalized)
        {
            if (fillRect == null) return;
            fillRect.localScale = fillDirection == FillDirection.Horizontal
                ? new Vector3(normalized, 1f, 1f)
                : new Vector3(1f, normalized, 1f);
        }

        private void UpdateColor(float normalized)
        {
            if (fillImage == null) return;
            Color target = colorMode switch
            {
                ColorMode.Solid => solidColor,
                ColorMode.Threshold => normalized < lowThreshold ? criticalColor
                                     : normalized < midThreshold ? warningColor
                                     : healthyColor,
                ColorMode.Gradient => gradient != null ? gradient.Evaluate(normalized) : solidColor,
                _ => solidColor,
            };
            fillImage.color = target;
            // Update _originalFillColor để Flash trở về đúng màu base hiện tại
            // (KHÔNG về màu Awake nếu mode khác Solid hoặc threshold đã đổi tier).
            _originalFillColor = target;
        }

        private void UpdateText(float value, float normalized)
        {
            if (valueText == null) return;
            string formatted = textFormat switch
            {
                TextFormat.None => string.Empty,
                TextFormat.Integer => Mathf.RoundToInt(value).ToString(),
                TextFormat.Decimal1 => value.ToString("F1"),
                TextFormat.Percent => $"{Mathf.RoundToInt(normalized * 100f)}%",
                TextFormat.Custom => string.Format(customFormat, value),
                _ => value.ToString(),
            };
            valueText.text = $"{textPrefix}{formatted}{textSuffix}";
        }

        // ─────────────────────────────────────────────────────────────────────
        // Flash effect — Issue #11 fix: 2-phase original → flash → original.
        // ─────────────────────────────────────────────────────────────────────

        private void CheckFlashTriggers(float previous, float current)
        {
            if (flashTrigger == FlashTrigger.None) return;

            bool shouldFlash = false;
            if ((flashTrigger & FlashTrigger.OnReachMax) != 0
                && Mathf.Approximately(current, maxValue))
                shouldFlash = true;
            if ((flashTrigger & FlashTrigger.OnReachMin) != 0
                && Mathf.Approximately(current, minValue))
                shouldFlash = true;
            if ((flashTrigger & FlashTrigger.OnDamage) != 0 && current < previous)
                shouldFlash = true;
            if ((flashTrigger & FlashTrigger.OnHeal) != 0 && current > previous)
                shouldFlash = true;

            if (shouldFlash) Flash();
        }

        /// <summary>
        /// Trigger flash effect 1 lần — 2-phase original → flash → original (Issue #11 fix).
        /// Nếu đang flash dở thì stop và bắt đầu cycle mới (không multi-stack).
        /// </summary>
        public void Flash()
        {
            if (fillImage == null) return;

            _flashHandle?.Stop();

            var backend = AnimationBackendRegistry.Current;
            float halfDuration = Mathf.Max(0.0001f, flashDuration * 0.5f);
            Color startColor = fillImage.color;
            // Snapshot lại _originalFillColor TẠI THỜI ĐIỂM trigger — Phase 2 trở về màu này.
            // (UpdateColor có thể đã refresh _originalFillColor giữa chừng cho mode Threshold/Gradient.)
            Color savedColor = _originalFillColor;

            // Phase 1: start → flash
            _flashHandle = backend.TweenColor(this, startColor, flashColor, halfDuration,
                c => { if (fillImage != null) fillImage.color = c; });

            _flashHandle.OnComplete(() =>
            {
                // Phase 2: flash → savedColor. Lưu vào _flashHandle để Stop() ở OnDestroy bao trùm cả phase 2.
                _flashHandle = backend.TweenColor(this, flashColor, savedColor, halfDuration,
                    c => { if (fillImage != null) fillImage.color = c; });
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        // Pulse effect — Issue #2 fix: track handle, no multi-coroutine race.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Start pulse loop trên overlayImage (scale base → peak → base, lặp khi <see cref="IsFull"/>).
        /// No-op nếu đã đang pulse hoặc overlay null.
        /// </summary>
        public void StartPulse()
        {
            if (overlayImage == null) return;
            if (_pulseHandle != null && _pulseHandle.IsPlaying) return;

            PulseLoop(this.GetCancellationTokenOnDestroy()).Forget();
        }

        private async UniTaskVoid PulseLoop(CancellationToken ct)
        {
            var backend = AnimationBackendRegistry.Current;
            try
            {
                while (IsFull && !ct.IsCancellationRequested && overlayImage != null)
                {
                    Vector3 baseScale = _overlayInitialScale;
                    Vector3 peakScale = baseScale * pulseScale;
                    float halfDuration = Mathf.Max(0.0001f, pulseDuration * 0.5f);

                    // ↑ base → peak
                    _pulseHandle = backend.TweenVector3(this, baseScale, peakScale, halfDuration,
                        v => { if (overlayImage != null) overlayImage.transform.localScale = v; });
                    {
                        // Capture local để closure WaitUntil ổn định kể cả khi _pulseHandle bị overwrite.
                        var h = _pulseHandle;
                        await UniTask.WaitUntil(
                            () => h == null || !h.IsPlaying || h.IsCompleted,
                            cancellationToken: ct);
                    }

                    if (ct.IsCancellationRequested || !IsFull || overlayImage == null) break;

                    // ↓ peak → base
                    _pulseHandle = backend.TweenVector3(this, peakScale, baseScale, halfDuration,
                        v => { if (overlayImage != null) overlayImage.transform.localScale = v; });
                    {
                        var h = _pulseHandle;
                        await UniTask.WaitUntil(
                            () => h == null || !h.IsPlaying || h.IsCompleted,
                            cancellationToken: ct);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Destroy hoặc StopPulse — expected.
            }
            finally
            {
                if (overlayImage != null) overlayImage.transform.localScale = _overlayInitialScale;
                _pulseHandle = null;
            }
        }

        /// <summary>Force stop pulse loop và restore overlay scale.</summary>
        public void StopPulse()
        {
            _pulseHandle?.Stop();
            _pulseHandle = null;
            if (overlayImage != null) overlayImage.transform.localScale = _overlayInitialScale;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Editor preview
        // ─────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying) return;

            // Live preview: clamp + apply visual. KHÔNG fire event, KHÔNG animate trong Editor.
            currentValue = Mathf.Clamp(currentValue, minValue, maxValue);
            _displayValue = currentValue;

            // Auto-find ở Edit mode để designer thấy result ngay khi gắn component.
            AutoFindComponents();

            // Invalidate sliced cache để force apply mới khi designer đổi direction trong inspector.
            _lastSlicedNormalizedValue = -1f;

            UpdateVisual(_displayValue);
        }
#endif
    }
}

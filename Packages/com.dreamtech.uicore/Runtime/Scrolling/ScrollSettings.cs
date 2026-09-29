using System;
using UnityEngine;

namespace DreamTech.UICore.Scrolling
{
    /// <summary>
    /// Khoảng đệm quanh các ô, theo hướng cuộn: <see cref="Start"/>/<see cref="End"/> dọc trục cuộn (trước ô đầu / sau ô cuối),
    /// <see cref="CrossStart"/>/<see cref="CrossEnd"/> theo trục phụ (trái/phải khi cuộn dọc, trên/dưới khi cuộn ngang).
    /// </summary>
    [Serializable]
    public struct ScrollPadding
    {
        public float Start;
        public float End;
        public float CrossStart;
        public float CrossEnd;

        public ScrollPadding(float start, float end, float crossStart, float crossEnd)
        {
            Start = start;
            End = end;
            CrossStart = crossStart;
            CrossEnd = crossEnd;
        }
    }

    /// <summary>Kiểu hút về ô sau khi thả tay.</summary>
    public enum ScrollSnapMode
    {
        /// <summary>Không hút — trôi theo quán tính của ScrollRect.</summary>
        Off = 0,

        /// <summary>Quán tính chậm lại dưới <see cref="ScrollSnapSettings.SettleSpeed"/> thì hút về ô gần điểm neo nhất.</summary>
        Nearest = 1,

        /// <summary>
        /// Lật trang: vuốt nhanh hơn <see cref="ScrollSnapSettings.FlickSpeed"/> sang đúng một ô kế bên, chậm hơn thì về ô gần nhất.
        /// Hợp với carousel / trang tutorial / chọn level theo trang.
        /// </summary>
        Paged = 2,
    }

    /// <summary>
    /// Cài đặt hút. Điểm neo tính theo trục cuộn, 0 = mép đầu (ô 0), 1 = mép cuối: <see cref="ViewportPivot"/> 0,5 và
    /// <see cref="ItemPivot"/> 0,5 = tâm ô nằm giữa khung nhìn; 0 và 0 = mép đầu ô dính mép đầu khung nhìn.
    /// </summary>
    [Serializable]
    public sealed class ScrollSnapSettings
    {
        [SerializeField] private ScrollSnapMode _mode = ScrollSnapMode.Off;

        [Tooltip("Điểm neo trên khung nhìn theo trục cuộn (0 = mép đầu, 1 = mép cuối).")]
        [SerializeField, Range(0f, 1f)] private float _viewportPivot = 0.5f;

        [Tooltip("Điểm trên ô được đưa tới điểm neo (0 = mép đầu ô, 1 = mép cuối ô).")]
        [SerializeField, Range(0f, 1f)] private float _itemPivot = 0.5f;

        [Tooltip("Nearest: quán tính chậm dưới mức này (px/giây) thì bắt đầu hút.")]
        [SerializeField, Min(0f)] private float _settleSpeed = 250f;

        [Tooltip("Paged: thả tay nhanh hơn mức này (px/giây) thì sang ô kế bên theo hướng vuốt.")]
        [SerializeField, Min(0f)] private float _flickSpeed = 500f;

        [Tooltip("Giây hút tới ô.")]
        [SerializeField, Min(0f)] private float _duration = 0.25f;

        [SerializeField] private AnimationCurve _curve = ScrollAnimationSettings.EaseOutCubic();

        public ScrollSnapMode Mode => _mode;
        public bool Enabled => _mode != ScrollSnapMode.Off;
        public float ViewportPivot => _viewportPivot;
        public float ItemPivot => _itemPivot;
        public float SettleSpeed => _settleSpeed;
        public float FlickSpeed => _flickSpeed;
        public float Duration => _duration;
        public AnimationCurve Curve => _curve;

        /// <summary>Đặt bằng code. Tham số bỏ trống = giữ nguyên.</summary>
        public ScrollSnapSettings Configure(ScrollSnapMode? mode = null, float? viewportPivot = null, float? itemPivot = null,
                                            float? settleSpeed = null, float? flickSpeed = null, float? duration = null,
                                            AnimationCurve curve = null)
        {
            if (mode.HasValue) _mode = mode.Value;
            if (viewportPivot.HasValue) _viewportPivot = Mathf.Clamp01(viewportPivot.Value);
            if (itemPivot.HasValue) _itemPivot = Mathf.Clamp01(itemPivot.Value);
            if (settleSpeed.HasValue) _settleSpeed = Mathf.Max(0f, settleSpeed.Value);
            if (flickSpeed.HasValue) _flickSpeed = Mathf.Max(0f, flickSpeed.Value);
            if (duration.HasValue) _duration = Mathf.Max(0f, duration.Value);
            if (curve != null) _curve = curve;
            return this;
        }
    }

    /// <summary>Cài đặt cho cuộn bằng code (<c>ScrollToIndex</c>) và cho mọi chuyển động list tự chạy (kể cả hút).</summary>
    [Serializable]
    public sealed class ScrollAnimationSettings
    {
        [Tooltip("Giây cuộn tới ô khi gọi ScrollToIndex không truyền thời lượng.")]
        [SerializeField, Min(0f)] private float _duration = 0.35f;

        [SerializeField] private AnimationCurve _curve = EaseOutCubic();

        [Tooltip("Chạy theo giờ thật: list vẫn cuộn đúng nhịp khi game slow-motion / pause.")]
        [SerializeField] private bool _useUnscaledTime = true;

        public float Duration => _duration;
        public AnimationCurve Curve => _curve;
        public bool UseUnscaledTime => _useUnscaledTime;

        public ScrollAnimationSettings Configure(float? duration = null, AnimationCurve curve = null, bool? useUnscaledTime = null)
        {
            if (duration.HasValue) _duration = Mathf.Max(0f, duration.Value);
            if (curve != null) _curve = curve;
            if (useUnscaledTime.HasValue) _useUnscaledTime = useUnscaledTime.Value;
            return this;
        }

        /// <summary>Nhanh lúc đầu, chậm dần khi tới nơi.</summary>
        public static AnimationCurve EaseOutCubic()
        {
            // Tiếp tuyến của 1 - (1 - t)^3: 3 ở đầu, 0 ở cuối.
            return new AnimationCurve(new Keyframe(0f, 0f, 3f, 3f), new Keyframe(1f, 1f, 0f, 0f));
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace DreamTech.UICore.Feedback
{
    /// <summary>Cách cỡ nút đi tới cỡ đích.</summary>
    public enum PressMotion
    {
        /// <summary>
        /// Theo thời lượng: nhấn đi hết <see cref="ButtonFeedbackProfile.PressDuration"/> theo <see cref="ButtonFeedbackProfile.PressCurve"/>,
        /// nhả đi hết <see cref="ButtonFeedbackProfile.ReleaseDuration"/> theo <see cref="ButtonFeedbackProfile.ReleaseCurve"/> (đường nhả
        /// vượt 1 = nút vọt quá cỡ gốc rồi mới về). Hợp với nút bấm.
        /// </summary>
        Timed = 0,

        /// <summary>
        /// Đuổi theo: cỡ tiến về cỡ đích theo lerp mũ với tốc độ <see cref="ButtonFeedbackProfile.FollowSpeed"/>, không vọt. Hợp với vật
        /// "mềm" như rương, thẻ.
        /// </summary>
        Follow = 1,
    }

    /// <summary>
    /// Bộ số của phản hồi nhấn, dùng chung cho nhiều nút — chỉnh một chỗ, mọi nút cùng đổi. Nút không gán profile dùng
    /// <see cref="ButtonFeedback.DefaultProfile"/> (game đặt lúc khởi động) hoặc <see cref="BuiltInDefault"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "DreamTech/UI Core/Button Feedback Profile", fileName = "ButtonFeedbackProfile")]
    public sealed class ButtonFeedbackProfile : ScriptableObject
    {
        [SerializeField] private PressMotion _motion = PressMotion.Timed;

        [Tooltip("Cỡ lúc nhấn giữ, nhân với cỡ gốc.")]
        [SerializeField, Range(0.5f, 1f)] private float _pressedScale = 0.9f;

        [Tooltip("Timed: giây co về cỡ nhấn.")]
        [SerializeField, Min(0f)] private float _pressDuration = 0.1f;

        [Tooltip("Timed: tiến độ co (0→1) theo thời gian (0→1). Bỏ trống = tuyến tính.")]
        [SerializeField] private AnimationCurve _pressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Tooltip("Timed: giây nhả về cỡ gốc.")]
        [SerializeField, Min(0f)] private float _releaseDuration = 0.2f;

        [Tooltip("Timed: tiến độ nhả (0 = cỡ lúc nhả, 1 = cỡ gốc). Vượt 1 = vọt quá cỡ gốc. Bỏ trống = tuyến tính.")]
        [SerializeField] private AnimationCurve _releaseCurve = DefaultReleaseCurve();

        [Tooltip("Follow: tốc độ đuổi theo cỡ đích (/giây).")]
        [SerializeField, Min(0.1f)] private float _followSpeed = 20f;

        [Tooltip("Chạy theo giờ thật: UI mở lúc game slow-motion / pause vẫn nhún đúng nhịp.")]
        [SerializeField] private bool _useUnscaledTime = true;

        [Tooltip("Chờ bấy nhiêu giây mới nhún nút nằm trong ScrollRect — nếu ngón tay kéo trong lúc chờ thì là cuộn, không " +
                 "nhún. 0 = nhún ngay.")]
        [SerializeField, Min(0f)] private float _scrollPressDelay = 0.06f;

        [Tooltip("ScrollRect đang trôi nhanh hơn mức này (px/giây) thì chạm là để dừng cuộn, không nhún.")]
        [SerializeField, Min(0f)] private float _scrollVelocityThreshold = 10f;

        [Tooltip("Tắt Animator và đặt Transition của Selectable về None khi gắn — tránh hai thứ cùng co một nút.")]
        [SerializeField] private bool _takeOverTransition = true;

        [Tooltip("Khoá gửi cho ButtonFeedbackCues.Handler khi nút được bấm. Trống = không phát.")]
        [SerializeField] private string _clickCueKey = "click";

        [Tooltip("Object có tên kết thúc bằng hậu tố này: RegisterAll bỏ qua, chế độ Children không co.")]
        [SerializeField] private string _excludeSuffix = "_noFeedback";

        private static ButtonFeedbackProfile _builtInDefault;

        public PressMotion Motion => _motion;
        public float PressedScale => _pressedScale;
        public float PressDuration => _pressDuration;
        public AnimationCurve PressCurve => _pressCurve;
        public float ReleaseDuration => _releaseDuration;
        public AnimationCurve ReleaseCurve => _releaseCurve;
        public float FollowSpeed => _followSpeed;
        public bool UseUnscaledTime => _useUnscaledTime;
        public float ScrollPressDelay => _scrollPressDelay;
        public float ScrollVelocityThreshold => _scrollVelocityThreshold;
        public bool TakeOverTransition => _takeOverTransition;
        public string ClickCueKey => _clickCueKey;
        public string ExcludeSuffix => _excludeSuffix;

        /// <summary>Profile dựng từ giá trị mặc định trong code — dùng khi game không đặt gì.</summary>
        public static ButtonFeedbackProfile BuiltInDefault
        {
            get
            {
                if (_builtInDefault == null)
                {
                    _builtInDefault = CreateInstance<ButtonFeedbackProfile>();
                    _builtInDefault.name = "ButtonFeedbackProfile (built-in)";
                    _builtInDefault.hideFlags = HideFlags.DontSave;
                }
                return _builtInDefault;
            }
        }

        /// <summary>Đặt số bằng code (test, hoặc game muốn dựng profile lúc chạy). Tham số bỏ trống = giữ nguyên.</summary>
        public ButtonFeedbackProfile Configure(PressMotion? motion = null, float? pressedScale = null, float? pressDuration = null,
                                               float? releaseDuration = null, AnimationCurve pressCurve = null,
                                               AnimationCurve releaseCurve = null, float? followSpeed = null,
                                               bool? useUnscaledTime = null, float? scrollPressDelay = null,
                                               float? scrollVelocityThreshold = null, bool? takeOverTransition = null,
                                               string clickCueKey = null, string excludeSuffix = null)
        {
            if (motion.HasValue) _motion = motion.Value;
            if (pressedScale.HasValue) _pressedScale = Mathf.Clamp(pressedScale.Value, 0.01f, 1f);
            if (pressDuration.HasValue) _pressDuration = Mathf.Max(0f, pressDuration.Value);
            if (releaseDuration.HasValue) _releaseDuration = Mathf.Max(0f, releaseDuration.Value);
            if (pressCurve != null) _pressCurve = pressCurve;
            if (releaseCurve != null) _releaseCurve = releaseCurve;
            if (followSpeed.HasValue) _followSpeed = Mathf.Max(0.1f, followSpeed.Value);
            if (useUnscaledTime.HasValue) _useUnscaledTime = useUnscaledTime.Value;
            if (scrollPressDelay.HasValue) _scrollPressDelay = Mathf.Max(0f, scrollPressDelay.Value);
            if (scrollVelocityThreshold.HasValue) _scrollVelocityThreshold = Mathf.Max(0f, scrollVelocityThreshold.Value);
            if (takeOverTransition.HasValue) _takeOverTransition = takeOverTransition.Value;
            if (clickCueKey != null) _clickCueKey = clickCueKey;
            if (excludeSuffix != null) _excludeSuffix = excludeSuffix;
            return this;
        }

        /// <summary>Nhả có vọt nhẹ: đi quá cỡ gốc khoảng một nửa quãng nhún ở ~40 % thời lượng rồi về.</summary>
        public static AnimationCurve DefaultReleaseCurve()
        {
            return new AnimationCurve(new Keyframe(0f, 0f, 0f, 4f), new Keyframe(0.4f, 1.5f, 0f, 0f), new Keyframe(1f, 1f, 0f, 0f));
        }

        // ─────────────────────────────────────────────────────────────────────
        // Nhịp — MỘT chỗ tính cho ButtonFeedback, biểu đồ của inspector và test
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Dưới ngưỡng này coi như đã tới cỡ đích (lerp mũ không bao giờ tới đúng).</summary>
        internal const float SettleThreshold = 0.0005f;

        /// <summary>
        /// Hệ số cỡ sau một bước của pha nhấn (<paramref name="pressing"/>) hoặc pha nhả. <paramref name="elapsed"/> là thời gian của
        /// pha tính cả bước này, <paramref name="startFactor"/> là hệ số lúc pha bắt đầu. <paramref name="finished"/>: đã co tới cỡ
        /// nhấn (pha nhấn) / đã về nghỉ (pha nhả).
        /// </summary>
        internal float Step(bool pressing, float startFactor, float currentFactor, float elapsed, float deltaTime, out bool finished)
        {
            if (_motion == PressMotion.Follow)
            {
                float goal = pressing ? _pressedScale : 1f;
                float next = Mathf.Lerp(currentFactor, goal, Mathf.Clamp01(deltaTime * _followSpeed));
                finished = Mathf.Abs(next - goal) < SettleThreshold;
                return finished ? goal : next;
            }

            if (pressing)
            {
                float progress = _pressDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / _pressDuration);
                finished = progress >= 1f;
                return Mathf.LerpUnclamped(startFactor, _pressedScale, Evaluate(_pressCurve, progress));
            }

            float releaseProgress = _releaseDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / _releaseDuration);
            finished = releaseProgress >= 1f;
            return finished ? 1f : Mathf.LerpUnclamped(startFactor, 1f, Evaluate(_releaseCurve, releaseProgress));
        }

        private static float Evaluate(AnimationCurve curve, float progress)
        {
            return curve != null && curve.length > 0 ? curve.Evaluate(progress) : progress;
        }

        /// <summary>Hình dạng một cú chạm (kết quả của <see cref="SampleTap"/>).</summary>
        public readonly struct TapShape
        {
            public TapShape(float lowest, float peak, float peakAfterRelease, float releaseTime, float endTime)
            {
                Lowest = lowest;
                Peak = peak;
                PeakAfterRelease = peakAfterRelease;
                ReleaseTime = releaseTime;
                EndTime = endTime;
            }

            /// <summary>Cỡ nhỏ nhất lúc đang giữ — chạm nhanh thì có thể chưa co tới <see cref="PressedScale"/>.</summary>
            public float Lowest { get; }

            /// <summary>Cỡ lớn nhất sau khi nhả; 1 = không vọt.</summary>
            public float Peak { get; }

            /// <summary>Giây từ lúc nhả tới đỉnh.</summary>
            public float PeakAfterRelease { get; }

            /// <summary>Giây (tính từ lúc chạm) lúc nhả.</summary>
            public float ReleaseTime { get; }

            /// <summary>Giây (tính từ lúc chạm) lúc về nghỉ hẳn.</summary>
            public float EndTime { get; }

            /// <summary>Nhả có vọt quá cỡ gốc không.</summary>
            public bool Overshoots => Peak > 1f + SettleThreshold;
        }

        /// <summary>
        /// Mô phỏng một cú chạm đúng như <see cref="ButtonFeedback"/> chạy: chạm, giữ <paramref name="holdSeconds"/> giây, nhả, tới lúc
        /// về nghỉ — mỗi bước <paramref name="step"/> giây. <paramref name="samples"/> (nếu có) nhận từng điểm (giây, hệ số cỡ).
        /// Dùng cho biểu đồ của inspector và cho test; không đụng tới scene.
        /// </summary>
        public TapShape SampleTap(float holdSeconds, float step = 1f / 120f, List<Vector2> samples = null)
        {
            const float MaxSeconds = 10f;
            holdSeconds = Mathf.Max(0f, holdSeconds);
            step = Mathf.Max(0.0005f, step);
            samples?.Clear();
            samples?.Add(new Vector2(0f, 1f));

            float time = 0f;
            float factor = 1f;
            float startFactor = 1f;
            float elapsed = 0f;
            bool pressing = true;
            float lowest = 1f;
            float releaseTime = holdSeconds;
            float peak = 1f;
            float peakTime = 0f;

            while (time < MaxSeconds)
            {
                if (pressing && time >= holdSeconds - 0.00001f)
                {
                    pressing = false;
                    startFactor = factor;
                    elapsed = 0f;
                    releaseTime = time;
                    peak = factor;
                }

                time += step;
                elapsed += step;
                factor = Step(pressing, startFactor, factor, elapsed, step, out bool finished);
                samples?.Add(new Vector2(time, factor));

                if (pressing)
                {
                    lowest = Mathf.Min(lowest, factor);
                    continue;
                }
                if (factor > peak)
                {
                    peak = factor;
                    peakTime = time - releaseTime;
                }
                if (finished) break;
            }

            return new TapShape(lowest, Mathf.Max(peak, 1f), peakTime, releaseTime, time);
        }
    }
}

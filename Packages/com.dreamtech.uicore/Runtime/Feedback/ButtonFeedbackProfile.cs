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
        [Header("Nhún")]
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

        [Header("Thời gian")]
        [Tooltip("Chạy theo giờ thật: UI mở lúc game slow-motion / pause vẫn nhún đúng nhịp.")]
        [SerializeField] private bool _useUnscaledTime = true;

        [Header("Nút trong vùng cuộn (ScrollRect)")]
        [Tooltip("Chờ bấy nhiêu giây mới nhún nút nằm trong ScrollRect — nếu ngón tay kéo trong lúc chờ thì là cuộn, không " +
                 "nhún. 0 = nhún ngay.")]
        [SerializeField, Min(0f)] private float _scrollPressDelay = 0.06f;

        [Tooltip("ScrollRect đang trôi nhanh hơn mức này (px/giây) thì chạm là để dừng cuộn, không nhún.")]
        [SerializeField, Min(0f)] private float _scrollVelocityThreshold = 10f;

        [Header("Nút có sẵn")]
        [Tooltip("Tắt Animator và đặt Transition của Selectable về None khi gắn — tránh hai thứ cùng co một nút.")]
        [SerializeField] private bool _takeOverTransition = true;

        [Header("Âm / rung")]
        [Tooltip("Khoá gửi cho ButtonFeedbackCues.Handler khi nút được bấm. Trống = không phát.")]
        [SerializeField] private string _clickCueKey = "click";

        [Header("Loại trừ")]
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
    }
}

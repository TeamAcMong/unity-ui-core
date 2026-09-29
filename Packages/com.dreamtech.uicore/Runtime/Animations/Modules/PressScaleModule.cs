using System;
using DreamTech.UICore.Animations.Backends;
using DreamTech.UICore.Feedback;
using UnityEngine;

namespace DreamTech.UICore.Animations.Modules
{
    /// <summary>
    /// Nhún khi nhấn theo một <see cref="ButtonFeedbackProfile"/> — cùng bộ số với <see cref="ButtonFeedback"/>, nên AnimatedButton
    /// và Button thường gắn ButtonFeedback nhún giống hệt nhau khi dùng chung một profile.
    /// <para>Khác <see cref="ScaleModule"/>: nhấn và nhả có thời lượng + đường cong riêng (nhả vọt quá cỡ gốc rồi về), và chạy theo
    /// giờ thật nếu profile bảo vậy. Pressed → co về <see cref="ButtonFeedbackProfile.PressedScale"/>; mọi state khác → về cỡ gốc
    /// theo nhịp nhả.</para>
    /// <para>Module chỉ lo chuyển động. Chờ một chút khi nút nằm trong ScrollRect và giữ nguyên vùng chạm lúc co là việc của
    /// <see cref="ButtonFeedback"/> — nút trong danh sách cuộn nên dùng Button + ButtonFeedback.</para>
    /// </summary>
    [Serializable]
    public class PressScaleModule : IAnimationModule, IAnimationDurationHint
    {
        /// <summary>
        /// Profile kiểu Follow không có thời lượng (đuổi theo tới khi sát đích). Module quy về một tween dài bấy nhiêu lần hằng số thời
        /// gian (1 / tốc độ) với đường cong mũ — tới ~99 % quãng đường, đủ giống mắt thường.
        /// </summary>
        private const float FollowTimeConstants = 5f;

        [SerializeField] private bool enabled = true;

        [Tooltip("Bộ số nhún, dùng chung với ButtonFeedback. Trống = ButtonFeedback.DefaultProfile (game đặt) hoặc profile mặc định của package.")]
        [SerializeField] private ButtonFeedbackProfile profile;

        [Tooltip("Optional override — co transform này thay vì root của component.")]
        [SerializeField] private Transform targetTransform;

        private Vector3 _initialScale = Vector3.one;
        private static AnimationCurve _followCurve;

        /// <inheritdoc/>
        public string DisplayName => "Press Scale";

        /// <inheritdoc/>
        public bool Enabled => enabled;

        /// <summary>Profile đang dùng: của module, không có thì mặc định của game, không có nữa thì của package.</summary>
        public ButtonFeedbackProfile Profile
        {
            get
            {
                if (profile != null) return profile;
                if (ButtonFeedback.DefaultProfile != null) return ButtonFeedback.DefaultProfile;
                return ButtonFeedbackProfile.BuiltInDefault;
            }
        }

        /// <summary>Gán profile bằng code (test, hoặc dựng nút lúc chạy).</summary>
        public void SetProfile(ButtonFeedbackProfile value) => profile = value;

        /// <inheritdoc/>
        public void CaptureInitialValue(MonoBehaviour target)
        {
            Transform t = ResolveTarget(target);
            if (t != null) _initialScale = t.localScale;
        }

        /// <inheritdoc/>
        public IAnimationHandle Play(MonoBehaviour target, UIState newState, IAnimationBackend backend)
        {
            if (!enabled || target == null || backend == null) return null;
            Transform t = ResolveTarget(target);
            if (t == null) return null;

            ButtonFeedbackProfile p = Profile;
            bool pressing = newState == UIState.Pressed;
            if (backend is ITimeModeAnimationBackend clocked)
            {
                backend = clocked.WithTimeMode(p.UseUnscaledTime ? AnimationTimeMode.Unscaled : AnimationTimeMode.Scaled);
            }

            Vector3 to = _initialScale * (pressing ? p.PressedScale : 1f);
            return backend.TweenVector3(target, t.localScale, to, DurationFor(p, pressing),
                                        v => { if (t != null) t.localScale = v; },
                                        CurveFor(p, pressing));
        }

        /// <inheritdoc/>
        public float GetDuration(UIState state) => DurationFor(Profile, state == UIState.Pressed);

        /// <summary>Thời lượng tween của pha nhấn / nhả theo profile.</summary>
        internal static float DurationFor(ButtonFeedbackProfile p, bool pressing)
        {
            if (p.Motion == PressMotion.Follow) return FollowTimeConstants / p.FollowSpeed;
            return pressing ? p.PressDuration : p.ReleaseDuration;
        }

        /// <summary>Đường cong tween của pha nhấn / nhả theo profile; null = tuyến tính.</summary>
        internal static AnimationCurve CurveFor(ButtonFeedbackProfile p, bool pressing)
        {
            if (p.Motion == PressMotion.Follow) return FollowCurve;
            AnimationCurve curve = pressing ? p.PressCurve : p.ReleaseCurve;
            return curve != null && curve.length > 0 ? curve : null;
        }

        /// <summary>1 − e^(−5x), chuẩn hoá để tới đúng 1 ở x = 1: nhanh lúc đầu, chậm dần — dáng của "đuổi theo".</summary>
        private static AnimationCurve FollowCurve
        {
            get
            {
                if (_followCurve != null) return _followCurve;
                float[] points = { 0f, 0.05f, 0.1f, 0.2f, 0.3f, 0.45f, 0.6f, 0.8f, 1f };
                float scale = 1f / (1f - Mathf.Exp(-FollowTimeConstants));
                var keys = new Keyframe[points.Length];
                for (int index = 0; index < points.Length; index++)
                {
                    float x = points[index];
                    float value = (1f - Mathf.Exp(-FollowTimeConstants * x)) * scale;
                    float slope = FollowTimeConstants * Mathf.Exp(-FollowTimeConstants * x) * scale;
                    keys[index] = new Keyframe(x, value, slope, slope);
                }
                _followCurve = new AnimationCurve(keys);
                return _followCurve;
            }
        }

        private Transform ResolveTarget(MonoBehaviour target)
        {
            return targetTransform != null ? targetTransform : (target != null ? target.transform : null);
        }
    }
}

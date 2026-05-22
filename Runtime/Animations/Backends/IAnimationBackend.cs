using System;
using UnityEngine;

namespace DreamTech.UICore.Animations.Backends
{
    /// <summary>
    /// Interface backend animation. Cho phép swap implementation (UniTask, DOTween, LeanTween, v.v.)
    /// mà không thay đổi module code.
    /// </summary>
    public interface IAnimationBackend
    {
        /// <summary>
        /// Generic float tween — primitive cho mọi custom animation.
        /// </summary>
        /// <param name="host">MonoBehaviour dùng làm context lifecycle. Cancel khi host bị destroy.</param>
        /// <param name="from">Giá trị bắt đầu.</param>
        /// <param name="to">Giá trị kết thúc.</param>
        /// <param name="duration">Thời gian tween (giây).</param>
        /// <param name="onUpdate">Callback mỗi frame với giá trị hiện tại.</param>
        /// <param name="curve">Easing curve. Null = linear.</param>
        /// <param name="onStart">Gọi 1 lần trước frame đầu tiên.</param>
        /// <param name="onStep">Gọi mỗi frame với normalized time 0..1.</param>
        /// <param name="onComplete">Gọi khi tween hoàn tất tự nhiên.</param>
        IAnimationHandle TweenFloat(
            MonoBehaviour host,
            float from, float to, float duration,
            Action<float> onUpdate,
            AnimationCurve curve = null,
            Action onStart = null,
            Action<float> onStep = null,
            Action onComplete = null);

        /// <summary>
        /// Generic Vector3 tween (cho scale, position, rotation euler).
        /// </summary>
        IAnimationHandle TweenVector3(
            MonoBehaviour host,
            Vector3 from, Vector3 to, float duration,
            Action<Vector3> onUpdate,
            AnimationCurve curve = null,
            Action onStart = null,
            Action<float> onStep = null,
            Action onComplete = null);

        /// <summary>
        /// Generic Color tween.
        /// </summary>
        IAnimationHandle TweenColor(
            MonoBehaviour host,
            Color from, Color to, float duration,
            Action<Color> onUpdate,
            AnimationCurve curve = null,
            Action onStart = null,
            Action<float> onStep = null,
            Action onComplete = null);

        /// <summary>
        /// Punch: scale tăng vọt rồi dao động về original.
        /// </summary>
        /// <param name="host">Lifecycle context.</param>
        /// <param name="target">Transform cần punch.</param>
        /// <param name="punchAmount">Offset đỉnh của punch (ví dụ Vector3.one * 0.3f).</param>
        /// <param name="duration">Tổng thời gian hiệu ứng.</param>
        /// <param name="vibrato">Số dao động (default 10).</param>
        /// <param name="elasticity">Độ đàn hồi 0..1 (default 1 = full elastic).</param>
        /// <param name="onComplete">Callback khi xong.</param>
        IAnimationHandle Punch(
            MonoBehaviour host,
            Transform target,
            Vector3 punchAmount,
            float duration,
            int vibrato = 10,
            float elasticity = 1f,
            Action onComplete = null);

        /// <summary>
        /// Shake: rung vị trí ngẫu nhiên với decay.
        /// </summary>
        /// <param name="host">Lifecycle context.</param>
        /// <param name="target">Transform cần shake.</param>
        /// <param name="strength">Biên độ rung tối đa.</param>
        /// <param name="duration">Tổng thời gian.</param>
        /// <param name="vibrato">Số rung per second (default 10).</param>
        /// <param name="randomness">Góc ngẫu nhiên 0..90 (default 90).</param>
        /// <param name="onComplete">Callback khi xong.</param>
        IAnimationHandle Shake(
            MonoBehaviour host,
            Transform target,
            float strength,
            float duration,
            int vibrato = 10,
            float randomness = 90f,
            Action onComplete = null);
    }
}

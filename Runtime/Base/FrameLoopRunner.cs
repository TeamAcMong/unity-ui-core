using UnityEngine;

namespace DreamTech.UICore.Base
{
    /// <summary>Chạy <see cref="FrameLoop"/> trong Play mode — tạo tự động, ẩn khỏi Hierarchy, không cần gắn tay.</summary>
    [AddComponentMenu("")]
    [DefaultExecutionOrder(-32000)]
    internal sealed class FrameLoopRunner : MonoBehaviour
    {
        private void Update()
        {
            FrameLoop.TickAll(Time.deltaTime, Time.unscaledDeltaTime);
        }
    }
}

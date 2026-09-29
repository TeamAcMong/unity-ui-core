using System;
using UnityEngine;

namespace DreamTech.UICore.Feedback
{
    /// <summary>
    /// Điểm nối âm thanh / rung của <see cref="ButtonFeedback"/>. Package không tự phát âm: game đăng ký MỘT handler lúc khởi
    /// động và tự quyết khoá nào ra âm / kiểu rung nào — nhờ vậy dùng được với bất kỳ hệ audio/haptic nào.
    /// <code>
    /// ButtonFeedbackCues.Handler = (source, key) =>
    /// {
    ///     if (key == "click") { MyAudio.Play("ui_click"); MyHaptics.Light(); }
    /// };
    /// </code>
    /// </summary>
    public static class ButtonFeedbackCues
    {
        /// <summary>Gọi khi một nút có <see cref="ButtonFeedback"/> được bấm thật (click hợp lệ, nút đang cho bấm).</summary>
        public static Action<ButtonFeedback, string> Handler { get; set; }

        internal static void Raise(ButtonFeedback source, string cueKey)
        {
            if (string.IsNullOrEmpty(cueKey)) return;
            Action<ButtonFeedback, string> handler = Handler;
            if (handler == null) return;
            try
            {
                handler(source, cueKey);
            }
            catch (Exception exception)
            {
                // Một handler lỗi không được nuốt mất click của nút.
                Debug.LogException(exception, source);
            }
        }
    }
}

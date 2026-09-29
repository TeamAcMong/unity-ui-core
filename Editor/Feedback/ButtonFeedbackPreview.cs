using DreamTech.UICore.Editor.Preview;
using DreamTech.UICore.Feedback;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DreamTech.UICore.Editor.Feedback
{
    /// <summary>
    /// Cho nút nhún thử trong Edit mode, bằng chính <see cref="ButtonFeedback"/> của nút: nhấn, chạy <see cref="ButtonFeedback.Advance"/>
    /// theo đồng hồ editor, nhả. Xong (hoặc bị ngắt) thì trả lại y nguyên cỡ, vùng chạm và các ô tham chiếu — xem thử không để lại
    /// thay đổi nào. Ngắt khi: lưu scene / prefab, vào-ra Play mode, reload script, đổi selection, thoát editor.
    /// </summary>
    [InitializeOnLoad]
    internal static class ButtonFeedbackPreview
    {
        private static ButtonFeedback _target;
        private static double _startTime;
        private static double _lastTickTime;
        private static float _releaseAfter = -1f;
        private static bool _released;
        private static bool _fastTickScheduled;

        static ButtonFeedbackPreview()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.playModeStateChanged += _ => Stop();
            EditorApplication.quitting += Stop;
            EditorSceneManager.sceneSaving += (scene, path) => Stop();
            PrefabStage.prefabSaving += _ => Stop();
            Selection.selectionChanged += Stop;
        }

        /// <summary>Đang cho <paramref name="feedback"/> nhún thử.</summary>
        internal static bool IsPreviewing(ButtonFeedback feedback) => feedback != null && ReferenceEquals(_target, feedback);

        /// <summary>Đang giữ (đã nhấn, chưa nhả).</summary>
        internal static bool IsHolding => !ReferenceEquals(_target, null) && !_released;

        /// <summary>Nhún thử được không; không thì <paramref name="reason"/> nói vì sao.</summary>
        internal static bool CanPreview(ButtonFeedback feedback, out string reason)
        {
            reason = null;
            if (feedback == null)
            {
                reason = "No button to preview.";
                return false;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                reason = "In Play mode, use the Play Mode Tools below.";
                return false;
            }
            var scene = feedback.gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                reason = "Preview works in a scene or in Prefab Mode — open the prefab or drag it into a scene.";
                return false;
            }
            if (!feedback.isActiveAndEnabled)
            {
                reason = "The object is inactive or this component is disabled.";
                return false;
            }
            return true;
        }

        /// <summary>Nhấn và giữ tới khi gọi <see cref="Release"/> (để ngắm cỡ lúc đang nhấn).</summary>
        internal static void Press(ButtonFeedback feedback) => Begin(feedback, -1f);

        /// <summary>Nhấn, giữ <paramref name="holdSeconds"/> giây, nhả, xem tới khi về nghỉ.</summary>
        internal static void Tap(ButtonFeedback feedback, float holdSeconds) => Begin(feedback, Mathf.Max(0f, holdSeconds));

        /// <summary>Nhả cú nhấn đang giữ; phiên tự kết thúc khi nút về nghỉ.</summary>
        internal static void Release()
        {
            if (ReferenceEquals(_target, null) || _released) return;
            _released = true;
            if (_target != null) _target.OnPointerUp(null);
            RepaintViews();
        }

        /// <summary>Dừng ngay và trả nút về y như trước lúc xem.</summary>
        internal static void Stop()
        {
            if (ReferenceEquals(_target, null)) return;
            ButtonFeedback target = _target;
            _target = null;
            EditorApplication.update -= Tick;
            if (target != null) target.EndEditorPreview();
            RepaintViews();
        }

        private static void Begin(ButtonFeedback feedback, float releaseAfter)
        {
            Stop();
            if (!CanPreview(feedback, out _)) return;

            // Mỗi lúc chỉ một thứ xem trước: bảng xem trước của component module (AnimatedButton…) cũng dừng.
            PreviewSession.CancelActive();

            feedback.BeginEditorPreview();
            feedback.OnPointerDown(null);
            if (!feedback.IsPressed && !feedback.IsPressPending)
            {
                // Nút không cho bấm (interactable = false) — không có gì để xem.
                feedback.EndEditorPreview();
                return;
            }

            _target = feedback;
            _released = false;
            _releaseAfter = releaseAfter;
            _startTime = _lastTickTime = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            ScheduleFastTick();
        }

        private static void Tick()
        {
            if (_target == null)
            {
                Stop();
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            float deltaTime = Mathf.Min((float)(now - _lastTickTime), 0.1f);
            if (deltaTime <= 0f) return;
            _lastTickTime = now;

            if (!_released && _releaseAfter >= 0f && now - _startTime >= _releaseAfter) Release();
            _target.Advance(deltaTime);
            if (_released && _target.PhaseLabel == "Idle")
            {
                Stop();
                return;
            }

            RepaintViews();
            ScheduleFastTick();
        }

        /// <summary><c>EditorApplication.update</c> lúc rảnh chỉ ~10 lần/giây — thêm nhịp <c>delayCall</c> mỗi khung editor cho mượt.</summary>
        private static void ScheduleFastTick()
        {
            if (_fastTickScheduled) return;
            _fastTickScheduled = true;
            EditorApplication.delayCall += () =>
            {
                _fastTickScheduled = false;
                if (!ReferenceEquals(_target, null)) Tick();
            };
        }

        private static void RepaintViews()
        {
            EditorApplication.QueuePlayerLoopUpdate();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
    }
}

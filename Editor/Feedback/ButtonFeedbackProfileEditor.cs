using System;
using DreamTech.UICore.Editor.Base;
using DreamTech.UICore.Editor.Styles;
using DreamTech.UICore.Feedback;
using UnityEditor;
using UnityEngine;

namespace DreamTech.UICore.Editor.Feedback
{
    /// <summary>
    /// Inspector của <see cref="ButtonFeedbackProfile"/> — bộ số nhún dùng chung. Chọn nhanh một kiểu nhún mẫu, xem biểu đồ cỡ theo thời
    /// gian (kéo thanh "Hold" để thấy cả chạm nhanh), chỉnh số theo nhóm. <see cref="ButtonFeedbackEditor"/> nhúng phần chỉnh số này
    /// để chỉnh ngay từ nút.
    /// </summary>
    [CustomEditor(typeof(ButtonFeedbackProfile))]
    public class ButtonFeedbackProfileEditor : UIComponentEditorBase
    {
        private const float GraphHeight = 132f;

        private static readonly (string Label, string Tooltip, Action<ButtonFeedbackProfile> Apply)[] Presets =
        {
            ("Punchy", "Squeeze to ×0.80 in 0.12 s; on release spring past the rest size (×1.12) and settle — a lively game button.",
                profile => profile.Configure(PressMotion.Timed, 0.8f, 0.12f, 0.2f, AnimationCurve.Linear(0f, 0f, 1f, 1f),
                                             new AnimationCurve(new Keyframe(0f, 0.005f, 0f, 0f), new Keyframe(0.35f, 1.6f, 0f, 0f),
                                                                new Keyframe(1f, 1f, 0f, 0f)))),
            ("Default", "The package default: ×0.90 in 0.10 s, a small spring on release.",
                profile => profile.Configure(PressMotion.Timed, 0.9f, 0.1f, 0.2f, AnimationCurve.Linear(0f, 0f, 1f, 1f),
                                             ButtonFeedbackProfile.DefaultReleaseCurve())),
            ("Subtle", "×0.95 in 0.08 s, almost no spring — for dense lists and small icons.",
                profile => profile.Configure(PressMotion.Timed, 0.95f, 0.08f, 0.15f, AnimationCurve.Linear(0f, 0f, 1f, 1f),
                                             new AnimationCurve(new Keyframe(0f, 0f, 0f, 4f), new Keyframe(0.4f, 1.3f, 0f, 0f),
                                                                new Keyframe(1f, 1f, 0f, 0f)))),
            ("Soft", "Follow motion: eases towards ×0.92 and back with no spring — for soft things like cards and chests.",
                profile => profile.Configure(PressMotion.Follow, 0.92f, followSpeed: 18f)),
        };

        private bool _foldPresets = true;
        private bool _foldGraph = true;
        private bool _foldMotion = true;
        private bool _foldClock = true;
        private bool _foldScroll = true;
        private bool _foldExisting = true;
        private bool _foldCue = true;
        private bool _foldExclude = true;
        private float _holdSeconds = -1f;

        private ButtonFeedbackProfile Profile => (ButtonFeedbackProfile)target;

        protected override string[] TabNames => new[] { "Motion", "Timing & Input", "Sound & Filters" };
        protected override string HeaderTitle => "Button Feedback Profile";
        protected override string HeaderSubtitle => "Shared press feel — every button using this profile changes with it";
        protected override GUIContent HeaderIcon => UIEditorStyles.IconSettings;

        protected override void DrawTabContent(int tabIndex)
        {
            switch (tabIndex)
            {
                case 0:
                    DrawPresets();
                    DrawGraphCard();
                    DrawMotionCard();
                    break;
                case 1:
                    DrawTimingCards();
                    break;
                case 2:
                    DrawSoundCards();
                    break;
            }
        }

        protected override void DrawPlayModeContent()
        {
            EditorGUILayout.LabelField("Edits apply live to every button using this profile.", EditorStyles.wordWrappedMiniLabel);
        }

        /// <summary>Phần chỉnh số (không header, không tab) — để <see cref="ButtonFeedbackEditor"/> nhúng vào inspector của nút.</summary>
        internal void DrawEmbedded()
        {
            serializedObject.Update();
            DrawPresets();
            DrawMotionCard();
            DrawTimingCards();
            serializedObject.ApplyModifiedProperties();
        }

        // ── Motion ─────────────────────────────────────────────────────────────────────────────────────────────

        private void DrawPresets()
        {
            if (DrawSectionCard("Presets", ref _foldPresets))
            {
                EditorGUILayout.BeginHorizontal();
                foreach (var preset in Presets)
                {
                    if (!GUILayout.Button(new GUIContent(preset.Label, preset.Tooltip), GUILayout.Height(22f))) continue;
                    serializedObject.ApplyModifiedProperties();
                    Undo.RecordObject(Profile, "Apply " + preset.Label + " preset");
                    preset.Apply(Profile);
                    EditorUtility.SetDirty(Profile);
                    serializedObject.Update();
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField("Sets the motion numbers only; timing, scroll, sound and filter settings stay.",
                                           EditorStyles.wordWrappedMiniLabel);
            }
            EndSectionCard();
        }

        private void DrawGraphCard()
        {
            if (DrawSectionCard("Scale over time", ref _foldGraph))
            {
                DrawGraph(Profile, ref _holdSeconds);
            }
            EndSectionCard();
        }

        /// <summary>Biểu đồ + thanh "Hold" + dòng tóm tắt — dùng chung cho inspector của profile và của nút.</summary>
        internal static void DrawGraph(ButtonFeedbackProfile profile, ref float holdSeconds)
        {
            if (holdSeconds < 0f) holdSeconds = DefaultHold(profile);
            Rect area = GUILayoutUtility.GetRect(0f, GraphHeight, GUILayout.ExpandWidth(true));
            ButtonFeedbackProfile.TapShape shape = ButtonFeedbackGraph.Draw(area, profile, holdSeconds);

            holdSeconds = EditorGUILayout.Slider(new GUIContent("Hold", "How long the finger stays down in this graph. Shorter than the " +
                                                                       "press time = a quick tap, released mid-squeeze."),
                                                 holdSeconds, 0.02f, 1f);
            EditorGUILayout.LabelField(Summary(profile, shape), EditorStyles.wordWrappedMiniLabel);
        }

        /// <summary>Giữ đủ lâu để thấy nút co hết rồi mới nhả.</summary>
        internal static float DefaultHold(ButtonFeedbackProfile profile)
        {
            float press = profile.Motion == PressMotion.Follow ? 4f / Mathf.Max(0.1f, profile.FollowSpeed) : profile.PressDuration;
            return Mathf.Clamp(press + 0.1f, 0.02f, 1f);
        }

        internal static string Summary(ButtonFeedbackProfile profile, ButtonFeedbackProfile.TapShape shape)
        {
            string press = profile.Motion == PressMotion.Follow
                ? "Press: follows ×" + profile.PressedScale.ToString("0.00") + " at speed " + profile.FollowSpeed.ToString("0.#")
                : "Press: ×" + profile.PressedScale.ToString("0.00") + " in " + profile.PressDuration.ToString("0.00") + " s";
            string release = shape.Overshoots
                ? "release peaks at ×" + shape.Peak.ToString("0.00") + " (+" + shape.PeakAfterRelease.ToString("0.00") + " s)"
                : "release has no spring";
            string settle = "back at rest " + (shape.EndTime - shape.ReleaseTime).ToString("0.00") + " s after release";
            string clock = profile.UseUnscaledTime ? "real time" : "game time";
            return press + " · " + release + " · " + settle + " · " + clock + ".";
        }

        private void DrawMotionCard()
        {
            if (DrawSectionCard("Squeeze", ref _foldMotion))
            {
                DrawProperty("_motion", "Motion");
                DrawProperty("_pressedScale", "Pressed Scale");
                if (Profile.Motion == PressMotion.Timed)
                {
                    DrawProperty("_pressDuration", "Press Duration");
                    DrawProperty("_pressCurve", "Press Curve");
                    DrawProperty("_releaseDuration", "Release Duration");
                    DrawProperty("_releaseCurve", "Release Curve");
                    EditorGUILayout.LabelField("Release Curve above 1 = the button springs past its rest size, then settles.",
                                               EditorStyles.wordWrappedMiniLabel);
                }
                else
                {
                    DrawProperty("_followSpeed", "Follow Speed");
                }
            }
            EndSectionCard();
        }

        // ── Timing & input ─────────────────────────────────────────────────────────────────────────────────────

        private void DrawTimingCards()
        {
            if (DrawSectionCard("Clock", ref _foldClock))
            {
                DrawProperty("_useUnscaledTime", "Use Unscaled Time");
            }
            EndSectionCard();

            if (DrawSectionCard("Buttons inside a scroll list", ref _foldScroll))
            {
                DrawProperty("_scrollPressDelay", "Press Delay");
                DrawProperty("_scrollVelocityThreshold", "Stop-Scroll Speed");
                EditorGUILayout.LabelField("A drag during the delay scrolls instead of squeezing. Touching a list that is still moving " +
                                           "faster than the stop-scroll speed only stops it.", EditorStyles.wordWrappedMiniLabel);
            }
            EndSectionCard();

            if (DrawSectionCard("Existing button effects", ref _foldExisting))
            {
                DrawProperty("_takeOverTransition", "Take Over Transition");
                EditorGUILayout.LabelField("On: the Button's own transition and Animator are switched off, so only the squeeze plays.",
                                           EditorStyles.wordWrappedMiniLabel);
            }
            EndSectionCard();
        }

        // ── Sound & filters ────────────────────────────────────────────────────────────────────────────────────

        private void DrawSoundCards()
        {
            if (DrawSectionCard("Click cue", ref _foldCue))
            {
                DrawProperty("_clickCueKey", "Click Cue Key");
                DrawHelpCard("Each click sends this key to ButtonFeedbackCues.Handler — hook your sound / haptics there once at startup. " +
                             "Leave it empty when buttons already play their own sound.", HelpType.Info);
            }
            EndSectionCard();

            if (DrawSectionCard("Exclusions", ref _foldExclude))
            {
                DrawProperty("_excludeSuffix", "Exclude Suffix");
                EditorGUILayout.LabelField("Objects whose name ends with this are skipped by \"Add Button Feedback\" and by Children " +
                                           "scale mode.", EditorStyles.wordWrappedMiniLabel);
            }
            EndSectionCard();
        }
    }

    /// <summary>Tạo asset profile mới, chép số từ một profile có sẵn.</summary>
    internal static class ButtonFeedbackProfileAssets
    {
        /// <summary>Hỏi chỗ lưu rồi tạo profile chép số từ <paramref name="source"/>; huỷ hộp thoại thì trả null.</summary>
        internal static ButtonFeedbackProfile CreateCopyOf(ButtonFeedbackProfile source)
        {
            string path = EditorUtility.SaveFilePanelInProject("New Button Feedback Profile", "ButtonFeedbackProfile", "asset",
                                                               "Where should the shared press settings be saved?");
            if (string.IsNullOrEmpty(path)) return null;

            var profile = ScriptableObject.CreateInstance<ButtonFeedbackProfile>();
            if (source != null) CopyValues(source, profile);
            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssets();
            return profile;
        }

        internal static void CopyValues(ButtonFeedbackProfile source, ButtonFeedbackProfile destination)
        {
            destination.Configure(source.Motion, source.PressedScale, source.PressDuration, source.ReleaseDuration,
                                  CopyCurve(source.PressCurve), CopyCurve(source.ReleaseCurve), source.FollowSpeed,
                                  source.UseUnscaledTime, source.ScrollPressDelay, source.ScrollVelocityThreshold,
                                  source.TakeOverTransition, source.ClickCueKey ?? string.Empty, source.ExcludeSuffix ?? string.Empty);
        }

        private static AnimationCurve CopyCurve(AnimationCurve curve) => curve == null ? null : new AnimationCurve(curve.keys);
    }
}

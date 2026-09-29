using System.Collections.Generic;
using DreamTech.UICore.Editor.Base;
using DreamTech.UICore.Editor.Styles;
using DreamTech.UICore.Feedback;
using UnityEditor;
using UnityEngine;

namespace DreamTech.UICore.Editor.Feedback
{
    /// <summary>
    /// Inspector của <see cref="ButtonFeedback"/> theo khuôn của package (header, tab, thẻ như Animated Button):
    /// kiểm cấu hình kèm nút sửa, cho nhún thử ngay trong Scene (Press / Release / Tap), biểu đồ cỡ theo thời gian, chỉnh profile
    /// dùng chung ngay tại chỗ, và bảng thử lúc Play.
    /// </summary>
    [CustomEditor(typeof(ButtonFeedback))]
    [CanEditMultipleObjects]
    public class ButtonFeedbackEditor : UIComponentEditorBase
    {
        private bool _foldChecks = true;
        private bool _foldPreview = true;
        private bool _foldProfile = true;
        private bool _foldGraph = true;
        private bool _foldProfileSettings;
        private bool _foldScale = true;
        private bool _foldTouch = true;
        private bool _foldCue = true;
        private float _holdSeconds = -1f;
        private UnityEditor.Editor _profileEditor;

        private ButtonFeedback Feedback => (ButtonFeedback)target;
        private bool SingleTarget => targets.Length == 1;

        protected override string[] TabNames => new[] { "Feedback", "Target", "Sound" };
        protected override string HeaderTitle => "Button Feedback";
        protected override string HeaderSubtitle => "Press feedback for an existing Button or Toggle";
        protected override GUIContent HeaderIcon => UIEditorStyles.IconAnimation;

        public override bool RequiresConstantRepaint() =>
            Application.isPlaying || (SingleTarget && ButtonFeedbackPreview.IsPreviewing(Feedback));

        private void OnDisable()
        {
            if (_profileEditor != null) DestroyImmediate(_profileEditor);
        }

        protected override void DrawTabContent(int tabIndex)
        {
            switch (tabIndex)
            {
                case 0: DrawFeedbackTab(); break;
                case 1: DrawTargetTab(); break;
                case 2: DrawSoundTab(); break;
            }
        }

        // ── Feedback tab ───────────────────────────────────────────────────────────────────────────────────────

        private void DrawFeedbackTab()
        {
            if (SingleTarget)
            {
                DrawChecks(ButtonFeedbackValidator.Validate(Feedback));
                DrawPreviewCard();
            }
            else
            {
                DrawHelpCard("Preview, checks and the graph show one button at a time — select a single button to use them.",
                             HelpType.Info);
            }

            DrawProfileCard();

            if (SingleTarget)
            {
                if (DrawSectionCard("Scale over time", ref _foldGraph))
                {
                    ButtonFeedbackProfileEditor.DrawGraph(Feedback.Profile, ref _holdSeconds);
                }
                EndSectionCard();
                DrawProfileSettings();
            }
        }

        private void DrawChecks(List<ButtonFeedbackValidator.Issue> issues)
        {
            if (issues.Count == 0) return;
            if (DrawSectionCard("Checks (" + issues.Count + ")", ref _foldChecks, UIEditorStyles.IconWarn))
            {
                foreach (ButtonFeedbackValidator.Issue issue in issues)
                {
                    DrawHelpCard(issue.Message, ToHelpType(issue.Severity));
                    if (issue.Fix == null) continue;
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(issue.FixLabel, GUILayout.Height(20f)))
                    {
                        issue.Fix();
                        serializedObject.Update();
                        GUIUtility.ExitGUI();
                    }
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.Space(2f);
                }
            }
            EndSectionCard();
        }

        private static HelpType ToHelpType(ButtonFeedbackValidator.Severity severity)
        {
            switch (severity)
            {
                case ButtonFeedbackValidator.Severity.Error: return HelpType.Danger;
                case ButtonFeedbackValidator.Severity.Warning: return HelpType.Warning;
                default: return HelpType.Info;
            }
        }

        private void DrawPreviewCard()
        {
            if (Application.isPlaying) return; // Play mode: bảng Play Mode Tools bên dưới.
            if (DrawSectionCard("Preview", ref _foldPreview, UIEditorStyles.IconPlay))
            {
                bool canPreview = ButtonFeedbackPreview.CanPreview(Feedback, out string reason);
                bool previewing = ButtonFeedbackPreview.IsPreviewing(Feedback);

                using (new EditorGUI.DisabledScope(!canPreview))
                {
                    EditorGUILayout.BeginHorizontal();
                    using (new EditorGUI.DisabledScope(previewing && ButtonFeedbackPreview.IsHolding))
                    {
                        if (GUILayout.Button(new GUIContent(" ▶ Press", "Squeeze and hold, to look at the pressed size"),
                                             GUILayout.Height(22f)))
                        {
                            ButtonFeedbackPreview.Press(Feedback);
                        }
                    }
                    using (new EditorGUI.DisabledScope(!(previewing && ButtonFeedbackPreview.IsHolding)))
                    {
                        if (GUILayout.Button(new GUIContent(" ⏏ Release", "Let go of the held press"), GUILayout.Height(22f)))
                        {
                            ButtonFeedbackPreview.Release();
                        }
                    }
                    if (GUILayout.Button(new GUIContent(" ▶ Tap", "Press, hold for the graph's Hold time, release"), GUILayout.Height(22f)))
                    {
                        float hold = _holdSeconds >= 0f ? _holdSeconds : ButtonFeedbackProfileEditor.DefaultHold(Feedback.Profile);
                        ButtonFeedbackPreview.Tap(Feedback, hold);
                    }
                    using (new EditorGUI.DisabledScope(!previewing))
                    {
                        if (GUILayout.Button(new GUIContent(" ↺ Reset", "Stop and put everything back"), GUILayout.Height(22f)))
                        {
                            ButtonFeedbackPreview.Stop();
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }

                if (previewing)
                {
                    EditorGUILayout.BeginHorizontal();
                    DrawPill(Feedback.PhaseLabel, UIEditorStyles.AnimationModuleColor);
                    DrawPill("×" + Feedback.CurrentFactor.ToString("0.00"), UIEditorStyles.Accent);
                    EditorGUILayout.EndHorizontal();
                }
                else if (!canPreview)
                {
                    EditorGUILayout.LabelField(reason, EditorStyles.wordWrappedMiniLabel);
                }
                else
                {
                    EditorGUILayout.LabelField("Plays the real squeeze on this object and puts it back afterwards — nothing is saved.",
                                               EditorStyles.wordWrappedMiniLabel);
                }
            }
            EndSectionCard();
        }

        private void DrawProfileCard()
        {
            if (DrawSectionCard("Profile", ref _foldProfile, UIEditorStyles.IconSettings))
            {
                EditorGUILayout.BeginHorizontal();
                DrawProperty("_profile", "Profile");
                if (GUILayout.Button(new GUIContent("New…", "Create a profile asset with the numbers this button uses now"),
                                     GUILayout.Width(52f)))
                {
                    CreateProfileForTargets();
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();

                if (SingleTarget) EditorGUILayout.LabelField(ProfileSource(), EditorStyles.wordWrappedMiniLabel);
            }
            EndSectionCard();
        }

        private string ProfileSource()
        {
            if (serializedObject.FindProperty("_profile").objectReferenceValue != null)
                return "Shared: every button using this profile squeezes the same way.";
            return ButtonFeedback.DefaultProfile != null
                ? "Using the game's default profile '" + ButtonFeedback.DefaultProfile.name + "' (ButtonFeedback.DefaultProfile)."
                : "Using the package defaults. Assign a profile, or press New… to create one you can tune.";
        }

        private void CreateProfileForTargets()
        {
            ButtonFeedbackProfile created = ButtonFeedbackProfileAssets.CreateCopyOf(Feedback.Profile);
            if (created == null) return;
            serializedObject.FindProperty("_profile").objectReferenceValue = created;
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawProfileSettings()
        {
            var assigned = serializedObject.FindProperty("_profile").objectReferenceValue as ButtonFeedbackProfile;
            if (assigned == null) return;

            if (DrawSectionCard("Profile settings — shared by every button using '" + assigned.name + "'", ref _foldProfileSettings))
            {
                CreateCachedEditor(assigned, typeof(ButtonFeedbackProfileEditor), ref _profileEditor);
                ((ButtonFeedbackProfileEditor)_profileEditor).DrawEmbedded();
            }
            EndSectionCard();
        }

        // ── Target tab ─────────────────────────────────────────────────────────────────────────────────────────

        private void DrawTargetTab()
        {
            if (DrawSectionCard("What squeezes", ref _foldScale))
            {
                DrawProperty("_scaleMode", "Scale Mode");
                switch (Feedback.Mode)
                {
                    case ButtonFeedback.ScaleMode.Self:
                        EditorGUILayout.LabelField("This object squeezes. Its touch area is kept at full size while pressed.",
                                                   EditorStyles.wordWrappedMiniLabel);
                        break;
                    case ButtonFeedback.ScaleMode.Target:
                        DrawProperty("_target", "Target");
                        EditorGUILayout.LabelField("Only the target squeezes — e.g. the art under a transparent touch area.",
                                                   EditorStyles.wordWrappedMiniLabel);
                        break;
                    case ButtonFeedback.ScaleMode.Children:
                        DrawProperty("_excludeFromScale", "Exclude From Scale");
                        EditorGUILayout.LabelField("Each direct child squeezes; this object (and its touch area) stays put. Children in " +
                                                   "the list, or named with the profile's exclude suffix, stay too.",
                                                   EditorStyles.wordWrappedMiniLabel);
                        break;
                }
            }
            EndSectionCard();

            if (DrawSectionCard("Touch", ref _foldTouch))
            {
                DrawProperty("_hitGraphic", "Hit Graphic");
                DrawProperty("_selectable", "Selectable");
                EditorGUILayout.LabelField("Empty = found automatically: the Button / Toggle on this object and its Target Graphic.",
                                           EditorStyles.wordWrappedMiniLabel);
            }
            EndSectionCard();
        }

        // ── Sound tab ──────────────────────────────────────────────────────────────────────────────────────────

        private void DrawSoundTab()
        {
            if (DrawSectionCard("Click cue", ref _foldCue))
            {
                DrawProperty("_cueKeyOverride", "Cue Key Override");
                DrawProperty("_muteCue", "Mute");
                if (SingleTarget)
                {
                    string key = Feedback.CueKey;
                    EditorGUILayout.LabelField(string.IsNullOrEmpty(key)
                                                   ? "Silent: nothing is sent when this button is clicked."
                                                   : "On click, sends \"" + key + "\" to ButtonFeedbackCues.Handler.",
                                               EditorStyles.wordWrappedMiniLabel);
                }
                DrawHelpCard("Hook sound / haptics once at startup:\nButtonFeedbackCues.Handler = (button, key) => …", HelpType.Info);
            }
            EndSectionCard();
        }

        // ── Play mode ──────────────────────────────────────────────────────────────────────────────────────────

        protected override void DrawPlayModeContent()
        {
            if (!SingleTarget)
            {
                EditorGUILayout.LabelField("Select a single button to test it.", EditorStyles.miniLabel);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Phase:", GUILayout.Width(44f));
            DrawPill(Feedback.PhaseLabel, UIEditorStyles.AnimationModuleColor);
            EditorGUILayout.Space(8f);
            DrawPill("×" + Feedback.CurrentFactor.ToString("0.00"), UIEditorStyles.Accent);
            if (Application.isPlaying)
            {
                EditorGUILayout.Space(8f);
                DrawPill(ButtonFeedbackCues.Handler != null ? "Cue handler set" : "No cue handler",
                         ButtonFeedbackCues.Handler != null ? UIEditorStyles.SuccessColor : UIEditorStyles.WarningColor);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Press")) Feedback.OnPointerDown(null);
            if (GUILayout.Button("Release")) Feedback.OnPointerUp(null);
            if (GUILayout.Button("Tap"))
            {
                float hold = _holdSeconds >= 0f ? _holdSeconds : ButtonFeedbackProfileEditor.DefaultHold(Feedback.Profile);
                PlayModeTap(Feedback, hold);
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>Play mode: nhấn ngay, nhả sau <paramref name="holdSeconds"/> giây thật (Update của nút lo phần chuyển động).</summary>
        private static void PlayModeTap(ButtonFeedback feedback, float holdSeconds)
        {
            feedback.OnPointerDown(null);
            double releaseAt = EditorApplication.timeSinceStartup + holdSeconds;
            void WaitThenRelease()
            {
                if (feedback == null || !Application.isPlaying)
                {
                    EditorApplication.update -= WaitThenRelease;
                    return;
                }
                if (EditorApplication.timeSinceStartup < releaseAt) return;
                EditorApplication.update -= WaitThenRelease;
                feedback.OnPointerUp(null);
            }
            EditorApplication.update += WaitThenRelease;
        }
    }
}

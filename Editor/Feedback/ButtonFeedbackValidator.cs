using System;
using System.Collections.Generic;
using DreamTech.UICore.Animations.Modules;
using DreamTech.UICore.Base;
using DreamTech.UICore.Feedback;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DreamTech.UICore.Editor.Feedback
{
    /// <summary>
    /// Kiểm cấu hình của một <see cref="ButtonFeedback"/> — những chỗ làm nút "không nhún" hoặc nhún sai mà nhìn Inspector mặc định
    /// không thấy: không có gì nhận chạm, transition / Animator / AnimatedButton cùng co một nút, chế độ co thiếu đích, nút trong
    /// danh sách cuộn mà không chờ. Mỗi lỗi sửa được thì kèm cách sửa (có Undo).
    /// </summary>
    internal static class ButtonFeedbackValidator
    {
        internal enum Severity
        {
            Error,
            Warning,
            Info,
        }

        internal sealed class Issue
        {
            internal Issue(Severity severity, string message, string fixLabel = null, Action fix = null)
            {
                Severity = severity;
                Message = message;
                FixLabel = fixLabel;
                Fix = fix;
            }

            internal Severity Severity { get; }
            internal string Message { get; }
            internal string FixLabel { get; }
            internal Action Fix { get; }
        }

        internal static List<Issue> Validate(ButtonFeedback feedback)
        {
            var issues = new List<Issue>();
            if (feedback == null) return issues;

            var serialized = new SerializedObject(feedback);
            ButtonFeedbackProfile profile = feedback.Profile;
            Selectable selectable = ResolveSelectable(feedback, serialized);

            CheckTouchArea(feedback, issues);
            CheckSelectable(feedback, selectable, profile, issues);
            CheckScaleMode(feedback, serialized, issues);
            CheckCompetingScale(feedback, issues);
            CheckScrollList(feedback, profile, issues);

            issues.Sort((left, right) => left.Severity.CompareTo(right.Severity));
            return issues;
        }

        private static Selectable ResolveSelectable(ButtonFeedback feedback, SerializedObject serialized)
        {
            var assigned = serialized.FindProperty("_selectable").objectReferenceValue as Selectable;
            if (assigned != null) return assigned;
            Selectable own = feedback.GetComponent<Selectable>();
            return own != null ? own : null;
        }

        private static void CheckTouchArea(ButtonFeedback feedback, List<Issue> issues)
        {
            foreach (Graphic graphic in feedback.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic.raycastTarget) return;
            }

            var own = feedback.GetComponent<Graphic>();
            if (own != null)
            {
                issues.Add(new Issue(Severity.Error,
                    "'" + own.name + "' has Raycast Target off and nothing under it has it on — touches pass straight through.",
                    "Turn on Raycast Target",
                    () =>
                    {
                        Undo.RecordObject(own, "Turn on Raycast Target");
                        own.raycastTarget = true;
                    }));
                return;
            }

            GameObject host = feedback.gameObject;
            issues.Add(new Issue(Severity.Error,
                "Nothing here receives touches. Add a Graphic with Raycast Target on — an Image can be fully transparent.",
                "Add transparent Image",
                () =>
                {
                    var image = Undo.AddComponent<Image>(host);
                    image.color = new Color(1f, 1f, 1f, 0f);
                }));
        }

        private static void CheckSelectable(ButtonFeedback feedback, Selectable selectable, ButtonFeedbackProfile profile,
                                            List<Issue> issues)
        {
            if (selectable == null)
            {
                issues.Add(new Issue(Severity.Info,
                    "No Button or Toggle here — it squeezes on any touch and does not check 'interactable'. Fine for a custom touch area."));
            }
            else if (selectable.transition != Selectable.Transition.None)
            {
                string transition = selectable.transition.ToString();
                string owner = selectable.GetType().Name;
                Action fix = () =>
                {
                    Undo.RecordObject(selectable, "Set Transition to None");
                    selectable.transition = Selectable.Transition.None;
                };
                if (profile.TakeOverTransition)
                {
                    issues.Add(new Issue(Severity.Info,
                        "The " + owner + "'s " + transition + " transition is switched off when the game runs (profile: Take Over Transition).",
                        "Switch it off now", fix));
                }
                else
                {
                    issues.Add(new Issue(Severity.Warning,
                        "The " + owner + "'s " + transition + " transition runs together with the squeeze.", "Set Transition to None", fix));
                }
            }

            var animator = feedback.GetComponent<Animator>();
            if (animator != null && animator.enabled)
            {
                issues.Add(profile.TakeOverTransition
                    ? new Issue(Severity.Info, "The Animator on this object is disabled when the game runs (profile: Take Over Transition).")
                    : new Issue(Severity.Warning, "An Animator on this object can fight the squeeze.", "Disable Animator", () =>
                    {
                        Undo.RecordObject(animator, "Disable Animator");
                        animator.enabled = false;
                    }));
            }
        }

        private static void CheckScaleMode(ButtonFeedback feedback, SerializedObject serialized, List<Issue> issues)
        {
            SerializedProperty scaleMode = serialized.FindProperty("_scaleMode");
            if (feedback.Mode == ButtonFeedback.ScaleMode.Target && serialized.FindProperty("_target").objectReferenceValue == null)
            {
                issues.Add(new Issue(Severity.Error, "Scale Mode is Target but no Target is set — it will scale this object instead.",
                    "Use Self", () =>
                    {
                        scaleMode.enumValueIndex = (int)ButtonFeedback.ScaleMode.Self;
                        serialized.ApplyModifiedProperties();
                    }));
            }
            else if (feedback.Mode == ButtonFeedback.ScaleMode.Children && feedback.transform.childCount == 0)
            {
                issues.Add(new Issue(Severity.Warning, "Scale Mode is Children but this object has no children — nothing will move."));
            }
        }

        private static void CheckCompetingScale(ButtonFeedback feedback, List<Issue> issues)
        {
            var animated = feedback.GetComponent<UIAnimatedComponent>();
            if (animated == null) return;
            bool scales = IsOn(animated.GetModule<ScaleModule>()) || IsOn(animated.GetModule<PressScaleModule>());
            if (!scales) return;
            issues.Add(new Issue(Severity.Warning,
                animated.GetType().Name + " on this object also animates scale — the two will fight. Keep one of them."));
        }

        private static bool IsOn(IAnimationModule module) => module != null && module.Enabled;

        private static void CheckScrollList(ButtonFeedback feedback, ButtonFeedbackProfile profile, List<Issue> issues)
        {
            if (feedback.GetComponentInParent<ScrollRect>(true) == null) return;
            issues.Add(profile.ScrollPressDelay > 0f
                ? new Issue(Severity.Info, "Inside a scroll list: the squeeze waits " + profile.ScrollPressDelay.ToString("0.00") +
                                           " s, so a drag during the wait scrolls instead of squeezing.")
                : new Issue(Severity.Warning, "Inside a scroll list with no press delay: starting a scroll on this button makes it " +
                                              "squeeze and bounce. Set Scroll Press Delay in the profile."));
        }
    }
}

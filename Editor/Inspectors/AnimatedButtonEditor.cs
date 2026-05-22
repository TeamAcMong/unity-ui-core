using DreamTech.UICore.Animations.Modules;
using DreamTech.UICore.Behaviors;
using DreamTech.UICore.Buttons;
using DreamTech.UICore.Editor.Base;
using DreamTech.UICore.Editor.Drawers;
using DreamTech.UICore.Editor.Styles;
using UnityEditor;
using UnityEngine;

namespace DreamTech.UICore.Editor.Inspectors
{
    [CustomEditor(typeof(AnimatedButton))]
    public class AnimatedButtonEditor : UIComponentEditorBase
    {
        protected override string[] TabNames => new[] { "Settings", "Animation", "Behaviors", "Events" };

        protected override string HeaderTitle    => "Animated Button";
        protected override string HeaderSubtitle => "Interactive button with modular animations & behaviors";
        protected override GUIContent HeaderIcon => UIEditorStyles.IconPlay;

        // ── Foldout states ─────────────────────────────────────────────────────
        private bool _foldInteraction = true;
        private bool _foldAudio       = true;
        private bool _foldAnimEvents  = true;
        private bool _foldEvents      = true;

        protected override void DrawTabContent(int tabIndex)
        {
            switch (tabIndex)
            {
                case 0: DrawSettingsTab();    break;
                case 1: DrawAnimationTab();   break;
                case 2: DrawBehaviorsTab();   break;
                case 3: DrawEventsTab();      break;
            }
        }

        // ── Settings tab: Interaction + Audio ──────────────────────────────────

        private void DrawSettingsTab()
        {
            // Interaction Settings card
            if (DrawSectionCard("Interaction Settings", ref _foldInteraction, UIEditorStyles.IconSettings))
            {
                DrawProperty("interactable");
                DrawProperty("clickCooldown");
            }
            EndSectionCard();

            // Audio Settings card
            if (DrawSectionCard("Audio Settings", ref _foldAudio))
            {
                DrawProperty("audioSource");
                DrawProperty("hoverSound");
                DrawProperty("clickSound");
            }
            EndSectionCard();
        }

        // ── Animation tab: ModuleListDrawer + AnimationEventHooks ──────────────

        private void DrawAnimationTab()
        {
            SerializedProperty animModulesProp = serializedObject.FindProperty("animationModules");
            if (animModulesProp != null)
            {
                ModuleListDrawer.Draw(
                    animModulesProp,
                    typeof(IAnimationModule),
                    UIEditorStyles.AnimationModuleColor,
                    UIEditorStyles.IconAnimation,
                    "Add Animation Module");
            }

            EditorGUILayout.Space(4f);
            DrawHelpCard(
                "Add modules to customize button animations per state.\n" +
                "Any [Serializable] class implementing IAnimationModule auto-appears — no registration needed.",
                HelpType.Info);

            EditorGUILayout.Space(8f);

            if (DrawSectionCard("Animation Event Hooks", ref _foldAnimEvents))
            {
                DrawProperty("animationEvents");
            }
            EndSectionCard();
        }

        // ── Behaviors tab: ModuleListDrawer ────────────────────────────────────

        private void DrawBehaviorsTab()
        {
            SerializedProperty behaviorsProp = serializedObject.FindProperty("behaviorModules");
            if (behaviorsProp != null)
            {
                ModuleListDrawer.Draw(
                    behaviorsProp,
                    typeof(IBehaviorModule),
                    UIEditorStyles.BehaviorModuleColor,
                    UIEditorStyles.IconBehavior,
                    "Add Behavior");
            }

            EditorGUILayout.Space(4f);
            DrawHelpCard(
                "Add behaviors (Cooldown, LongPress, MultiClick, HoldRepeat) via the + button.\n" +
                "Custom behaviors: implement IBehaviorModule + [Serializable] → auto-detected.",
                HelpType.Info);
        }

        // ── Events tab ─────────────────────────────────────────────────────────

        private void DrawEventsTab()
        {
            if (DrawSectionCard("Unity Events", ref _foldEvents))
            {
                DrawProperty("onClick");
            }
            EndSectionCard();
        }

        // ── Play mode ─────────────────────────────────────────────────────────

        protected override void DrawPlayModeContent()
        {
            var btn = (AnimatedButton)target;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("State:", GUILayout.Width(40f));
            DrawPill(btn.CurrentState.ToString(), UIEditorStyles.AnimationModuleColor);
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Interactable:", GUILayout.Width(80f));
            DrawPill(btn.IsInteractable ? "Yes" : "No",
                btn.IsInteractable ? UIEditorStyles.SuccessColor : UIEditorStyles.DangerColor);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4f);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Force Trigger Anim")) btn.ForceTriggerAnimation();
            if (GUILayout.Button("Reset"))               btn.ResetButton();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Set Interactable: true"))  btn.SetInteractable(true);
            if (GUILayout.Button("Set Interactable: false")) btn.SetInteractable(false);
            EditorGUILayout.EndHorizontal();
        }
    }
}

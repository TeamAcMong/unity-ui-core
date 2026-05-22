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
    [CustomEditor(typeof(AnimatedToggle))]
    public class AnimatedToggleEditor : UIComponentEditorBase
    {
        protected override string[] TabNames => new[] { "Settings", "Animation", "Behaviors", "Events" };

        protected override string HeaderTitle    => "Animated Toggle";
        protected override string HeaderSubtitle => "Toggle with On/Off state animations and modular behaviors";
        protected override GUIContent HeaderIcon => UIEditorStyles.IconAnimation;

        // ── Foldout states ─────────────────────────────────────────────────────
        private bool _foldGeneral    = true;
        private bool _foldStateMap   = true;
        private bool _foldAnimEvents = true;
        private bool _foldEvents     = true;

        protected override void DrawTabContent(int tabIndex)
        {
            switch (tabIndex)
            {
                case 0: DrawSettingsTab();   break;
                case 1: DrawAnimationTab();  break;
                case 2: DrawBehaviorsTab();  break;
                case 3: DrawEventsTab();     break;
            }
        }

        // ── Settings tab: General + State Mapping ──────────────────────────────

        private void DrawSettingsTab()
        {
            if (DrawSectionCard("General Settings", ref _foldGeneral, UIEditorStyles.IconSettings))
            {
                DrawProperty("interactable");
                DrawProperty("initialState");
                DrawProperty("linkedToggle");
            }
            EndSectionCard();

            if (DrawSectionCard("State Mapping", ref _foldStateMap))
            {
                DrawHelpCard("Maps On/Off toggle state to UIState for animation playback.", HelpType.Info);
                EditorGUILayout.Space(4f);
                DrawProperty("offState", "Off → UIState");
                DrawProperty("onState",  "On  → UIState");
            }
            EndSectionCard();
        }

        // ── Animation tab ─────────────────────────────────────────────────────

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
                "Each module animates the toggle when state changes (Off→On or On→Off via State Mapping).",
                HelpType.Info);

            EditorGUILayout.Space(8f);

            if (DrawSectionCard("Animation Event Hooks", ref _foldAnimEvents))
            {
                DrawProperty("animationEvents");
            }
            EndSectionCard();
        }

        // ── Behaviors tab ─────────────────────────────────────────────────────

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

        // ── Events tab ────────────────────────────────────────────────────────

        private void DrawEventsTab()
        {
            if (DrawSectionCard("Unity Events", ref _foldEvents))
            {
                DrawProperty("onValueChanged");
            }
            EndSectionCard();
        }

        // ── Play mode ─────────────────────────────────────────────────────────

        protected override void DrawPlayModeContent()
        {
            if (target == null) return;
            var tg = (AnimatedToggle)target;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("State:", GUILayout.Width(40f));
            DrawPill(tg.CurrentToggleState.ToString(), UIEditorStyles.AnimationModuleColor);
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Is On:", GUILayout.Width(40f));
            DrawPill(tg.IsOn ? "On" : "Off",
                tg.IsOn ? UIEditorStyles.SuccessColor : UIEditorStyles.MutedText);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4f);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Toggle"))              tg.Toggle();
            if (GUILayout.Button("Force Trigger Anim"))  tg.ForceTriggerAnimation();
            EditorGUILayout.EndHorizontal();
        }
    }
}

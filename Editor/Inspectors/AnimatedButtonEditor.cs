using DreamTech.UICore.Buttons;
using DreamTech.UICore.Editor.Base;
using UnityEditor;
using UnityEngine;

namespace DreamTech.UICore.Editor.Inspectors
{
    [CustomEditor(typeof(AnimatedButton))]
    public class AnimatedButtonEditor : UIComponentEditorBase
    {
        protected override string[] TabNames => new[] { "Settings", "Animation", "Behaviors", "Audio", "Events" };

        protected override void DrawTabContent(int tabIndex)
        {
            switch (tabIndex)
            {
                case 0: DrawSettingsTab();    break;
                case 1: DrawAnimationTab();   break;
                case 2: DrawBehaviorsTab();   break;
                case 3: DrawAudioTab();       break;
                case 4: DrawEventsTab();      break;
            }
        }

        private void DrawSettingsTab()
        {
            DrawProperty("interactable");
            DrawProperty("clickCooldown");
        }

        private void DrawAnimationTab()
        {
            DrawProperty("animationModules", "Animation Modules");
            EditorGUILayout.HelpBox(
                "Add animation modules via the dropdown to customize button behavior per state.\n" +
                "Any [Serializable] class implementing IAnimationModule with a public no-arg " +
                "constructor will appear here automatically — no registration required.",
                MessageType.Info);
            EditorGUILayout.Space(4f);
            DrawProperty("animationEvents", "Animation Event Hooks");
        }

        private void DrawBehaviorsTab()
        {
            DrawProperty("behaviorModules", "Behavior Modules");
            EditorGUILayout.HelpBox(
                "Add behaviors (Cooldown, LongPress, MultiClick, HoldRepeat) qua dropdown.\n" +
                "Custom behaviors: implement IBehaviorModule + [Serializable] → auto xuất hiện.",
                MessageType.Info);
        }

        private void DrawAudioTab()
        {
            DrawProperty("audioSource");
            DrawProperty("hoverSound");
            DrawProperty("clickSound");
        }

        private void DrawEventsTab()
        {
            DrawProperty("onClick");
        }

        protected override void DrawPlayModeContent()
        {
            var btn = (AnimatedButton)target;
            EditorGUILayout.LabelField($"Current State: {btn.CurrentState}",   EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Interactable:  {btn.IsInteractable}", EditorStyles.miniLabel);
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

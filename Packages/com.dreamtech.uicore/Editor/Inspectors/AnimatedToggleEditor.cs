using DreamTech.UICore.Buttons;
using DreamTech.UICore.Editor.Base;
using UnityEditor;
using UnityEngine;

namespace DreamTech.UICore.Editor.Inspectors
{
    [CustomEditor(typeof(AnimatedToggle))]
    public class AnimatedToggleEditor : UIComponentEditorBase
    {
        protected override string[] TabNames => new[] { "Settings", "Animation", "Behaviors", "Events" };

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

        private void DrawSettingsTab()
        {
            DrawProperty("interactable");
            DrawProperty("initialState");
            DrawProperty("linkedToggle");

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("State Mapping", EditorStyles.boldLabel);
            DrawProperty("offState", "Off → UIState");
            DrawProperty("onState",  "On  → UIState");
        }

        private void DrawAnimationTab()
        {
            DrawProperty("animationModules", "Animation Modules");
            EditorGUILayout.HelpBox(
                "Each module animates the toggle when state changes " +
                "(Off→On or On→Off via the State Mapping above).",
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

        private void DrawEventsTab()
        {
            DrawProperty("onValueChanged");
        }

        protected override void DrawPlayModeContent()
        {
            var tg = (AnimatedToggle)target;
            EditorGUILayout.LabelField($"Toggle State: {tg.CurrentToggleState}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Is On:        {tg.IsOn}",               EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Toggle"))              tg.Toggle();
            if (GUILayout.Button("Force Trigger Anim"))  tg.ForceTriggerAnimation();
            EditorGUILayout.EndHorizontal();
        }
    }
}

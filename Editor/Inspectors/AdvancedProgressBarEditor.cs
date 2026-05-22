using DreamTech.UICore.Editor.Base;
using DreamTech.UICore.ProgressBars;
using UnityEditor;
using UnityEngine;

namespace DreamTech.UICore.Editor.Inspectors
{
    [CustomEditor(typeof(AdvancedProgressBar))]
    public class AdvancedProgressBarEditor : UIComponentEditorBase
    {
        protected override string[] TabNames =>
            new[] { "Value", "Fill", "Color", "Text", "Effects", "Events" };

        protected override void DrawTabContent(int tabIndex)
        {
            switch (tabIndex)
            {
                case 0: DrawValueTab();   break;
                case 1: DrawFillTab();    break;
                case 2: DrawColorTab();   break;
                case 3: DrawTextTab();    break;
                case 4: DrawEffectsTab(); break;
                case 5: DrawEventsTab();  break;
            }
        }

        private void DrawValueTab()
        {
            DrawProperty("currentValue");
            DrawProperty("minValue");
            DrawProperty("maxValue");

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Value Animation", EditorStyles.boldLabel);
            DrawProperty("valueAnimationMode");
            DrawProperty("animationDuration");

            // Show spring parameters only when Spring mode is selected
            // ValueAnimationMode enum: Instant=0, Smooth=1, EaseInOut=2, Spring=3
            SerializedProperty modeProp = serializedObject.FindProperty("valueAnimationMode");
            if (modeProp != null && modeProp.enumValueIndex == 3)
            {
                EditorGUI.indentLevel++;
                DrawProperty("springDamping");
                DrawProperty("springFrequency");
                EditorGUI.indentLevel--;
            }
        }

        private void DrawFillTab()
        {
            EditorGUILayout.LabelField("Fill Mode", EditorStyles.boldLabel);
            DrawProperty("fillMode");
            DrawProperty("fillDirection");

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("References (auto-found if null)", EditorStyles.boldLabel);
            DrawProperty("fillRect");
            DrawProperty("fillImage");
            DrawProperty("backgroundImage");
            DrawProperty("overlayImage");
        }

        private void DrawColorTab()
        {
            DrawProperty("colorMode");

            EditorGUILayout.Space(4f);

            // ColorMode enum: Solid=0, Threshold=1, Gradient=2
            SerializedProperty cmProp = serializedObject.FindProperty("colorMode");
            if (cmProp != null)
            {
                switch (cmProp.enumValueIndex)
                {
                    case 0: // Solid
                        DrawProperty("solidColor");
                        break;
                    case 1: // Threshold
                        EditorGUI.indentLevel++;
                        DrawProperty("lowThreshold",  "Low Threshold");
                        DrawProperty("midThreshold",  "Mid Threshold");
                        EditorGUILayout.Space(2f);
                        DrawProperty("criticalColor", "Critical (< Low)");
                        DrawProperty("warningColor",  "Warning  (< Mid)");
                        DrawProperty("healthyColor",  "Healthy  (>= Mid)");
                        EditorGUI.indentLevel--;
                        break;
                    case 2: // Gradient
                        DrawProperty("gradient");
                        break;
                }
            }
        }

        private void DrawTextTab()
        {
            DrawProperty("valueText");
            DrawProperty("textFormat");

            // TextFormat enum: None=0, Integer=1, Decimal1=2, Percent=3, Custom=4
            SerializedProperty tfProp = serializedObject.FindProperty("textFormat");
            if (tfProp != null && tfProp.enumValueIndex == 4)
            {
                EditorGUI.indentLevel++;
                DrawProperty("customFormat");
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4f);
            DrawProperty("textPrefix");
            DrawProperty("textSuffix");
        }

        private void DrawEffectsTab()
        {
            EditorGUILayout.LabelField("Flash Effect", EditorStyles.boldLabel);
            DrawProperty("flashTrigger");
            DrawProperty("flashColor");
            DrawProperty("flashDuration");

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Pulse Effect", EditorStyles.boldLabel);
            DrawProperty("pulseOnMax");
            DrawProperty("pulseScale");
            DrawProperty("pulseDuration");
        }

        private void DrawEventsTab()
        {
            DrawProperty("onValueChanged");
            DrawProperty("onReachMax");
            DrawProperty("onReachMin");
        }

        protected override void DrawPlayModeContent()
        {
            var pb = (AdvancedProgressBar)target;
            EditorGUILayout.LabelField(
                $"Display: {pb.DisplayValue:F2}  (Normalized: {pb.NormalizedValue:P0})",
                EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("0%"))   pb.SetValueNormalized(0f);
            if (GUILayout.Button("50%"))  pb.SetValueNormalized(0.5f);
            if (GUILayout.Button("100%")) pb.SetValueNormalized(1f);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Flash"))       pb.Flash();
            if (GUILayout.Button("Start Pulse")) pb.StartPulse();
            if (GUILayout.Button("Stop Pulse"))  pb.StopPulse();
            EditorGUILayout.EndHorizontal();
        }
    }
}

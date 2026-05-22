using DreamTech.UICore.Editor.Base;
using DreamTech.UICore.Editor.Styles;
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

        protected override string HeaderTitle    => "Advanced Progress Bar";
        protected override string HeaderSubtitle => "Animated, color-coded progress bar with rich formatting";
        protected override GUIContent HeaderIcon => UIEditorStyles.IconInfo;

        // ── Foldout states ─────────────────────────────────────────────────────
        private bool _foldValue     = true;
        private bool _foldValueAnim = true;
        private bool _foldFillMode  = true;
        private bool _foldFillRefs  = true;
        private bool _foldColor     = true;
        private bool _foldText      = true;
        private bool _foldFlash     = true;
        private bool _foldPulse     = true;
        private bool _foldEvents    = true;

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

        // ── Value tab ─────────────────────────────────────────────────────────

        private void DrawValueTab()
        {
            if (DrawSectionCard("Value Range", ref _foldValue))
            {
                DrawProperty("currentValue");
                DrawProperty("minValue");
                DrawProperty("maxValue");
            }
            EndSectionCard();

            if (DrawSectionCard("Value Animation", ref _foldValueAnim))
            {
                DrawProperty("valueAnimationMode");
                DrawProperty("animationDuration");

                // Show spring parameters only when Spring mode (index 3) is selected
                SerializedProperty modeProp = serializedObject.FindProperty("valueAnimationMode");
                if (modeProp != null && modeProp.enumValueIndex == 3)
                {
                    EditorGUI.indentLevel++;
                    DrawHelpCard("Spring requires damping and frequency tuning for stable oscillation.", HelpType.Warning);
                    DrawProperty("springDamping");
                    DrawProperty("springFrequency");
                    EditorGUI.indentLevel--;
                }
            }
            EndSectionCard();
        }

        // ── Fill tab ──────────────────────────────────────────────────────────

        private void DrawFillTab()
        {
            if (DrawSectionCard("Fill Mode", ref _foldFillMode))
            {
                DrawProperty("fillMode");
                DrawProperty("fillDirection");
            }
            EndSectionCard();

            if (DrawSectionCard("References  (auto-found if null)", ref _foldFillRefs))
            {
                DrawHelpCard("Leave null to auto-detect references by child name convention.", HelpType.Info);
                EditorGUILayout.Space(4f);
                DrawProperty("fillRect");
                DrawProperty("fillImage");
                DrawProperty("backgroundImage");
                DrawProperty("overlayImage");
            }
            EndSectionCard();
        }

        // ── Color tab ─────────────────────────────────────────────────────────

        private void DrawColorTab()
        {
            if (DrawSectionCard("Color Mode", ref _foldColor))
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
                            DrawHelpCard("Colors are applied based on value thresholds (normalized 0–1).", HelpType.Info);
                            EditorGUILayout.Space(4f);
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
            EndSectionCard();
        }

        // ── Text tab ──────────────────────────────────────────────────────────

        private void DrawTextTab()
        {
            if (DrawSectionCard("Text Display", ref _foldText))
            {
                DrawProperty("valueText");
                DrawProperty("textFormat");

                // TextFormat enum: None=0, Integer=1, Decimal1=2, Percent=3, Custom=4
                SerializedProperty tfProp = serializedObject.FindProperty("textFormat");
                if (tfProp != null && tfProp.enumValueIndex == 4)
                {
                    EditorGUI.indentLevel++;
                    DrawHelpCard("Use {0} as value placeholder, e.g. \"{0} pts\" or \"{0:F2}\".", HelpType.Info);
                    DrawProperty("customFormat");
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.Space(4f);
                DrawProperty("textPrefix");
                DrawProperty("textSuffix");
            }
            EndSectionCard();
        }

        // ── Effects tab ───────────────────────────────────────────────────────

        private void DrawEffectsTab()
        {
            if (DrawSectionCard("Flash Effect", ref _foldFlash))
            {
                DrawProperty("flashTrigger");
                DrawProperty("flashColor");
                DrawProperty("flashDuration");
            }
            EndSectionCard();

            if (DrawSectionCard("Pulse Effect", ref _foldPulse))
            {
                DrawProperty("pulseOnMax");
                DrawProperty("pulseScale");
                DrawProperty("pulseDuration");
            }
            EndSectionCard();
        }

        // ── Events tab ────────────────────────────────────────────────────────

        private void DrawEventsTab()
        {
            if (DrawSectionCard("Unity Events", ref _foldEvents))
            {
                DrawProperty("onValueChanged");
                DrawProperty("onReachMax");
                DrawProperty("onReachMin");
            }
            EndSectionCard();
        }

        // ── Play mode ─────────────────────────────────────────────────────────

        protected override void DrawPlayModeContent()
        {
            var pb = (AdvancedProgressBar)target;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Value:", GUILayout.Width(40f));
            DrawPill($"{pb.DisplayValue:F2}", UIEditorStyles.Accent);
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Normalized:", GUILayout.Width(72f));
            DrawPill($"{pb.NormalizedValue:P0}", UIEditorStyles.AnimationModuleColor);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4f);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("0%"))   pb.SetValueNormalized(0f);
            if (GUILayout.Button("25%"))  pb.SetValueNormalized(0.25f);
            if (GUILayout.Button("50%"))  pb.SetValueNormalized(0.5f);
            if (GUILayout.Button("75%"))  pb.SetValueNormalized(0.75f);
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

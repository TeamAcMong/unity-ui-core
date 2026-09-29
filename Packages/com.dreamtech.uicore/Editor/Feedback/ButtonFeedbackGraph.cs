using System.Collections.Generic;
using DreamTech.UICore.Editor.Styles;
using DreamTech.UICore.Feedback;
using UnityEditor;
using UnityEngine;

namespace DreamTech.UICore.Editor.Feedback
{
    /// <summary>
    /// Biểu đồ cỡ nút theo thời gian cho một cú chạm: nhấn, giữ, nhả. Đường vẽ lấy từ <see cref="ButtonFeedbackProfile.SampleTap"/> —
    /// đúng phép tính <see cref="ButtonFeedback"/> chạy — nên thấy gì là nút làm nấy: vạch cỡ gốc, vạch cỡ nhấn, lúc nhả, đỉnh vọt.
    /// </summary>
    internal static class ButtonFeedbackGraph
    {
        private const float LeftGutter = 44f;
        private const float RightGutter = 10f;
        private const float TopGutter = 18f;
        private const float BottomGutter = 16f;
        private const float SampleStep = 1f / 240f;

        private static readonly List<Vector2> Samples = new List<Vector2>(1024);
        private static Vector3[] _points = new Vector3[1024];
        private static GUIStyle _label;
        private static GUIStyle _rightLabel;

        private static GUIStyle Label => _label ??= new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = UIEditorStyles.MutedText },
        };

        private static GUIStyle RightLabel => _rightLabel ??= new GUIStyle(Label) { alignment = TextAnchor.MiddleRight };

        /// <summary>Vẽ biểu đồ vào <paramref name="area"/> (chỉ vẽ lúc Repaint) và trả về hình dạng cú chạm để ghi nhãn.</summary>
        internal static ButtonFeedbackProfile.TapShape Draw(Rect area, ButtonFeedbackProfile profile, float holdSeconds)
        {
            ButtonFeedbackProfile.TapShape shape = profile.SampleTap(holdSeconds, SampleStep, Samples);
            if (Event.current.type != EventType.Repaint) return shape;

            EditorGUI.DrawRect(area, EditorGUIUtility.isProSkin ? new Color(0f, 0f, 0f, 0.25f) : new Color(0f, 0f, 0f, 0.06f));
            var plot = new Rect(area.x + LeftGutter, area.y + TopGutter, area.width - LeftGutter - RightGutter,
                                area.height - TopGutter - BottomGutter);
            if (plot.width < 20f || plot.height < 20f) return shape;

            float lowest = Mathf.Min(shape.Lowest, profile.PressedScale);
            float highest = Mathf.Max(shape.Peak, 1f);
            float padding = Mathf.Max(0.02f, (highest - lowest) * 0.12f);
            float yMin = lowest - padding;
            float yMax = highest + padding;
            float xMax = Mathf.Max(0.05f, shape.EndTime);

            float X(float seconds) => plot.x + seconds / xMax * plot.width;
            float Y(float scale) => plot.yMax - (scale - yMin) / (yMax - yMin) * plot.height;

            // Vạch cỡ gốc (×1) và cỡ nhấn, vạch lúc nhả.
            DashedHorizontal(plot, Y(1f), new Color(UIEditorStyles.MutedText.r, UIEditorStyles.MutedText.g, UIEditorStyles.MutedText.b, 0.6f));
            DashedHorizontal(plot, Y(profile.PressedScale), new Color(UIEditorStyles.WarningColor.r, UIEditorStyles.WarningColor.g,
                                                                     UIEditorStyles.WarningColor.b, 0.7f));
            float releaseX = X(shape.ReleaseTime);
            EditorGUI.DrawRect(new Rect(releaseX, plot.y, 1f, plot.height), new Color(UIEditorStyles.Accent.r, UIEditorStyles.Accent.g,
                                                                                     UIEditorStyles.Accent.b, 0.35f));

            GUI.Label(new Rect(area.x, Y(1f) - 8f, LeftGutter - 4f, 16f), "×1.00", RightLabel);
            GUI.Label(new Rect(area.x, Y(profile.PressedScale) - 8f, LeftGutter - 4f, 16f), "×" + profile.PressedScale.ToString("0.00"),
                      RightLabel);
            GUI.Label(new Rect(plot.x - 2f, plot.yMax, 40f, BottomGutter), "0 s", Label);
            GUI.Label(new Rect(plot.xMax - 60f, plot.yMax, 60f, BottomGutter), shape.EndTime.ToString("0.00") + " s", RightLabel);
            GUI.Label(new Rect(releaseX + 3f, plot.yMax, 80f, BottomGutter), "release", Label);

            // Đường cỡ theo thời gian.
            int count = Samples.Count;
            if (_points.Length < count) _points = new Vector3[count * 2];
            for (int index = 0; index < count; index++) _points[index] = new Vector3(X(Samples[index].x), Y(Samples[index].y), 0f);
            Color previousColor = Handles.color;
            Handles.color = UIEditorStyles.Accent;
            Handles.DrawAAPolyLine(2.5f, count, _points);
            Handles.color = previousColor;

            // Đỉnh vọt sau khi nhả.
            if (shape.Overshoots)
            {
                float peakX = X(shape.ReleaseTime + shape.PeakAfterRelease);
                float peakY = Y(shape.Peak);
                EditorGUI.DrawRect(new Rect(peakX - 2.5f, peakY - 2.5f, 5f, 5f), UIEditorStyles.SuccessColor);
                string peakText = "×" + shape.Peak.ToString("0.00") + " at +" + shape.PeakAfterRelease.ToString("0.00") + " s";
                float labelX = Mathf.Min(peakX + 5f, plot.xMax - 110f);
                GUI.Label(new Rect(labelX, peakY - 16f, 110f, 14f), peakText, Label);
            }

            // Chạm nhanh: nút chưa co tới cỡ nhấn đã được nhả.
            if (shape.Lowest > profile.PressedScale + 0.005f)
            {
                GUI.Label(new Rect(plot.x + 4f, plot.y - TopGutter + 1f, plot.width, 14f),
                          "Released at ×" + shape.Lowest.ToString("0.00") + " — before the press finished", Label);
            }
            return shape;
        }

        private static void DashedHorizontal(Rect plot, float y, Color color)
        {
            for (float x = plot.x; x < plot.xMax; x += 7f)
            {
                EditorGUI.DrawRect(new Rect(x, y, Mathf.Min(4f, plot.xMax - x), 1f), color);
            }
        }
    }
}

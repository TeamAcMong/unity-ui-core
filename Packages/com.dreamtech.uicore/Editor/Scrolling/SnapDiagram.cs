using DreamTech.UICore.Editor.Styles;
using UnityEditor;
using UnityEngine;

namespace DreamTech.UICore.Editor.Scrolling
{
    /// <summary>
    /// Hình minh hoạ điểm neo của snap: khung nhìn, đường neo, ô được hút (điểm neo của ô nằm đúng trên đường), hai ô bên cạnh
    /// mờ đi. Kéo thanh trượt là thấy ô dời theo — không phải đoán "0,5 với 0,5 nghĩa là gì".
    /// </summary>
    internal static class SnapDiagram
    {
        private static readonly Color ViewportFill = new Color(0.26f, 0.59f, 0.98f, 0.10f);
        private static readonly Color ItemFill = new Color(0.40f, 0.85f, 0.50f, 0.55f);
        private static readonly Color NeighbourFill = new Color(0.60f, 0.60f, 0.60f, 0.25f);
        private static readonly Color PivotLine = new Color(1f, 0.35f, 0.75f, 1f);

        public static void Draw(Rect area, bool vertical, bool reversed, float viewportPivot, float itemPivot)
        {
            if (Event.current.type != EventType.Repaint) return;
            EditorGUI.DrawRect(area, EditorGUIUtility.isProSkin ? new Color(0f, 0f, 0f, 0.25f) : new Color(0f, 0f, 0f, 0.06f));

            // Khung nhìn chiếm giữa vùng vẽ; trục cuộn theo hướng thật (dọc/ngang), mép đầu theo chiều thật (đảo khi reversed).
            float mainLength = (vertical ? area.height : area.width) - 24f;
            float crossLength = (vertical ? area.width : area.height) * 0.42f;
            float viewportLength = mainLength * 0.62f;
            float itemLength = viewportLength * 0.32f;
            float spacing = itemLength * 0.12f;

            float mainOrigin = (mainLength - viewportLength) * 0.5f + 12f;
            float crossOrigin = ((vertical ? area.width : area.height) - crossLength) * 0.5f;

            DrawBox(area, vertical, reversed, mainOrigin, viewportLength, crossOrigin, crossLength, ViewportFill, UIEditorStyles.Accent, mainLength + 24f);

            float anchor = mainOrigin + viewportPivot * viewportLength;
            float itemStart = anchor - itemPivot * itemLength;
            float itemCrossOrigin = crossOrigin + crossLength * 0.12f;
            float itemCrossLength = crossLength * 0.76f;

            for (int neighbour = -2; neighbour <= 2; neighbour++)
            {
                if (neighbour == 0) continue;
                float start = itemStart + neighbour * (itemLength + spacing);
                DrawBox(area, vertical, reversed, start, itemLength, itemCrossOrigin, itemCrossLength, NeighbourFill, Color.clear,
                        mainLength + 24f);
            }
            DrawBox(area, vertical, reversed, itemStart, itemLength, itemCrossOrigin, itemCrossLength, ItemFill, UIEditorStyles.SuccessColor,
                    mainLength + 24f);

            // Đường neo cắt ngang khung nhìn + chấm tại điểm neo của ô.
            DrawBox(area, vertical, reversed, anchor - 1f, 2f, crossOrigin - 6f, crossLength + 12f, PivotLine, Color.clear, mainLength + 24f);
            DrawBox(area, vertical, reversed, anchor - 3f, 6f, itemCrossOrigin + itemCrossLength * 0.5f - 3f, 6f, Color.white, Color.clear,
                    mainLength + 24f);
        }

        /// <summary>Vẽ hộp theo toạ độ (trục cuộn, trục phụ) trong vùng; reversed = trục cuộn chạy từ mép đối diện.</summary>
        private static void DrawBox(Rect area, bool vertical, bool reversed, float mainStart, float mainSize, float crossStart,
                                    float crossSize, Color fill, Color outline, float mainTotal)
        {
            if (reversed) mainStart = mainTotal - mainStart - mainSize;
            Rect rect = vertical
                ? new Rect(area.x + crossStart, area.y + mainStart, crossSize, mainSize)
                : new Rect(area.x + mainStart, area.y + crossStart, mainSize, crossSize);
            rect = Clip(rect, area);
            if (rect.width <= 0f || rect.height <= 0f) return;
            EditorGUI.DrawRect(rect, fill);
            if (outline.a <= 0f) return;
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), outline);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), outline);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), outline);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), outline);
        }

        private static Rect Clip(Rect rect, Rect area)
        {
            float xMin = Mathf.Max(rect.xMin, area.xMin);
            float yMin = Mathf.Max(rect.yMin, area.yMin);
            float xMax = Mathf.Min(rect.xMax, area.xMax);
            float yMax = Mathf.Min(rect.yMax, area.yMax);
            return Rect.MinMaxRect(xMin, yMin, Mathf.Max(xMin, xMax), Mathf.Max(yMin, yMax));
        }
    }
}

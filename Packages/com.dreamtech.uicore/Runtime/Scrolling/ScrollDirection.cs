using UnityEngine;

namespace DreamTech.UICore.Scrolling
{
    /// <summary>
    /// Hướng các ô nối tiếp nhau. Ô 0 nằm ở mép đầu (TopToBottom: mép trên; BottomToTop: mép dưới — kiểu khung chat;
    /// LeftToRight: mép trái; RightToLeft: mép phải).
    /// </summary>
    public enum ScrollDirection
    {
        TopToBottom = 0,
        BottomToTop = 1,
        LeftToRight = 2,
        RightToLeft = 3,
    }

    /// <summary>Căn một khối theo trục ngang của hướng cuộn (trục phụ).</summary>
    public enum CrossAlignment
    {
        Start = 0,
        Center = 1,
        End = 2,
    }

    /// <summary>
    /// Quy đổi giữa <c>anchoredPosition</c> của Content và "độ cuộn" (offset): khoảng nội dung đã cuộn qua tính từ mép đầu,
    /// 0 = đang thấy ô đầu, dương = đã cuộn về phía các ô sau. Mọi phép tính bố cục làm trên offset nên không phụ thuộc hướng.
    /// </summary>
    public static class ScrollDirectionExtensions
    {
        public static bool IsVertical(this ScrollDirection direction)
        {
            return direction == ScrollDirection.TopToBottom || direction == ScrollDirection.BottomToTop;
        }

        public static bool IsReversed(this ScrollDirection direction)
        {
            return direction == ScrollDirection.BottomToTop || direction == ScrollDirection.RightToLeft;
        }

        /// <summary>Chỉ số trục cuộn trong Vector2: 1 (y) khi dọc, 0 (x) khi ngang.</summary>
        public static int MainAxis(this ScrollDirection direction) => direction.IsVertical() ? 1 : 0;

        /// <summary>Chỉ số trục phụ trong Vector2.</summary>
        public static int CrossAxis(this ScrollDirection direction) => direction.IsVertical() ? 0 : 1;

        public static float ToOffset(this ScrollDirection direction, Vector2 anchoredPosition)
        {
            switch (direction)
            {
                case ScrollDirection.BottomToTop: return -anchoredPosition.y;
                case ScrollDirection.LeftToRight: return -anchoredPosition.x;
                case ScrollDirection.RightToLeft: return anchoredPosition.x;
                default: return anchoredPosition.y;
            }
        }

        public static Vector2 WithOffset(this ScrollDirection direction, Vector2 anchoredPosition, float offset)
        {
            switch (direction)
            {
                case ScrollDirection.BottomToTop: anchoredPosition.y = -offset; break;
                case ScrollDirection.LeftToRight: anchoredPosition.x = -offset; break;
                case ScrollDirection.RightToLeft: anchoredPosition.x = offset; break;
                default: anchoredPosition.y = offset; break;
            }
            return anchoredPosition;
        }

        /// <summary>Vận tốc của Content (px/giây, như <c>ScrollRect.velocity</c>) quy về vận tốc của offset.</summary>
        public static float ToOffsetVelocity(this ScrollDirection direction, Vector2 velocity) => direction.ToOffset(velocity);

        /// <summary>
        /// Neo Content vào mép đầu của khung nhìn và kéo giãn theo trục phụ. Kích thước theo trục cuộn do list tự đặt.
        /// </summary>
        public static void ConfigureContent(this ScrollDirection direction, RectTransform content)
        {
            if (content == null) return;
            direction.GetContentAnchors(out Vector2 anchorMin, out Vector2 anchorMax, out Vector2 pivot);

            if (content.anchorMin != anchorMin) content.anchorMin = anchorMin;
            if (content.anchorMax != anchorMax) content.anchorMax = anchorMax;
            if (content.pivot != pivot) content.pivot = pivot;

            int cross = direction.CrossAxis();
            Vector2 sizeDelta = content.sizeDelta;
            Vector2 position = content.anchoredPosition;
            sizeDelta[cross] = 0f;
            position[cross] = 0f;
            if (content.sizeDelta != sizeDelta) content.sizeDelta = sizeDelta;
            if (content.anchoredPosition != position) content.anchoredPosition = position;
        }

        /// <summary>Neo và pivot mà Content cần có với hướng này (xem <see cref="ConfigureContent"/>).</summary>
        public static void GetContentAnchors(this ScrollDirection direction, out Vector2 anchorMin, out Vector2 anchorMax,
                                             out Vector2 pivot)
        {
            switch (direction)
            {
                case ScrollDirection.BottomToTop:
                    anchorMin = new Vector2(0f, 0f);
                    anchorMax = new Vector2(1f, 0f);
                    pivot = new Vector2(0.5f, 0f);
                    break;
                case ScrollDirection.LeftToRight:
                    anchorMin = new Vector2(0f, 0f);
                    anchorMax = new Vector2(0f, 1f);
                    pivot = new Vector2(0f, 0.5f);
                    break;
                case ScrollDirection.RightToLeft:
                    anchorMin = new Vector2(1f, 0f);
                    anchorMax = new Vector2(1f, 1f);
                    pivot = new Vector2(1f, 0.5f);
                    break;
                default:
                    anchorMin = new Vector2(0f, 1f);
                    anchorMax = new Vector2(1f, 1f);
                    pivot = new Vector2(0.5f, 1f);
                    break;
            }
        }

        /// <summary>
        /// Đặt một ô vào Content theo "toạ độ dòng chảy": <paramref name="mainStart"/> tính từ mép đầu của Content dọc trục cuộn,
        /// <paramref name="crossStart"/> tính từ mép trái (list dọc) hoặc mép trên (list ngang). Neo ô vào góc đầu của Content nên
        /// kết quả không phụ thuộc pivot của prefab.
        /// </summary>
        public static void PlaceItem(this ScrollDirection direction, RectTransform item, float mainStart, float mainSize,
                                     float crossStart, float crossSize)
        {
            if (item == null) return;
            bool vertical = direction.IsVertical();
            float width = vertical ? crossSize : mainSize;
            float height = vertical ? mainSize : crossSize;
            Vector2 pivot = item.pivot;

            Vector2 anchor;
            Vector2 position;
            switch (direction)
            {
                case ScrollDirection.BottomToTop:
                    anchor = new Vector2(0f, 0f);
                    position = new Vector2(crossStart + pivot.x * width, mainStart + pivot.y * height);
                    break;
                case ScrollDirection.LeftToRight:
                    anchor = new Vector2(0f, 1f);
                    position = new Vector2(mainStart + pivot.x * width, -(crossStart + (1f - pivot.y) * height));
                    break;
                case ScrollDirection.RightToLeft:
                    anchor = new Vector2(1f, 1f);
                    position = new Vector2(-(mainStart + (1f - pivot.x) * width), -(crossStart + (1f - pivot.y) * height));
                    break;
                default:
                    anchor = new Vector2(0f, 1f);
                    position = new Vector2(crossStart + pivot.x * width, -(mainStart + (1f - pivot.y) * height));
                    break;
            }

            if (item.anchorMin != anchor) item.anchorMin = anchor;
            if (item.anchorMax != anchor) item.anchorMax = anchor;
            var size = new Vector2(width, height);
            if (item.sizeDelta != size) item.sizeDelta = size;
            if (item.anchoredPosition != position) item.anchoredPosition = position;
        }
    }
}

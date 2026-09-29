using System.Collections.Generic;
using DreamTech.UICore.Scrolling;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DreamTech.UICore.Editor.Scrolling
{
    /// <summary>
    /// Kiểm tra cấu hình của một list ảo hoá và sửa (có Undo): ScrollRect, Content, Viewport, template. Inspector hiện từng
    /// vấn đề kèm nút sửa ngay chỗ đó.
    /// </summary>
    internal static class VirtualScrollSetup
    {
        internal enum Severity
        {
            Warning,
            Danger,
        }

        internal readonly struct Issue
        {
            public Issue(Severity severity, string message, string fixLabel = null, System.Action fix = null)
            {
                Level = severity;
                Message = message;
                FixLabel = fixLabel;
                Fix = fix;
            }

            public Severity Level { get; }
            public string Message { get; }
            public string FixLabel { get; }
            public System.Action Fix { get; }
        }

        public static List<Issue> Validate(VirtualScrollViewBase view)
        {
            var issues = new List<Issue>();
            var scrollRect = view.GetComponent<ScrollRect>();
            if (scrollRect == null)
            {
                issues.Add(new Issue(Severity.Danger, "Thiếu ScrollRect."));
                return issues;
            }

            RectTransform content = scrollRect.content;
            if (content == null)
            {
                issues.Add(new Issue(Severity.Danger, "ScrollRect chưa có Content — list không có chỗ đặt ô.", "Tạo Content",
                                     () => CreateContent(scrollRect)));
                return issues;
            }

            RectTransform viewport = scrollRect.viewport != null ? scrollRect.viewport : (RectTransform)scrollRect.transform;
            if (viewport.GetComponent<RectMask2D>() == null && viewport.GetComponent<Mask>() == null)
            {
                issues.Add(new Issue(view.RecycleMode == RecycleMode.Park ? Severity.Danger : Severity.Warning,
                                     view.RecycleMode == RecycleMode.Park
                                         ? "Recycle = Park cần khung nhìn có mask — view cất trong pool sẽ lộ ra."
                                         : "Khung nhìn chưa có RectMask2D — ô ở vùng đệm ngoài khung vẫn được vẽ.",
                                     "Thêm RectMask2D", () => Undo.AddComponent<RectMask2D>(viewport.gameObject)));
            }

            var layoutGroup = content.GetComponent<LayoutGroup>();
            var fitter = content.GetComponent<ContentSizeFitter>();
            if (layoutGroup != null || fitter != null)
            {
                issues.Add(new Issue(Severity.Danger,
                                     "Content có LayoutGroup / ContentSizeFitter — chúng đánh nhau với list (list tự xếp ô và tự đặt cỡ Content).",
                                     "Gỡ", () => RemoveLayoutDrivers(content)));
            }

            if (!IsSetUp(view, scrollRect, content))
            {
                issues.Add(new Issue(Severity.Warning, "ScrollRect / Content chưa khớp hướng cuộn " + DirectionLabel(view.Direction) + ".",
                                     "Áp cấu hình", () => ApplySetup(view)));
            }

            bool anyTemplate = false;
            for (int index = 0; index < view.Templates.Count; index++)
            {
                RectTransform template = view.Templates[index]?.Template;
                if (template == null) continue;
                anyTemplate = true;
                bool isSceneObject = template.gameObject.scene.IsValid();
                if (isSceneObject && template.IsChildOf(content) && template.gameObject.activeSelf)
                {
                    RectTransform captured = template;
                    issues.Add(new Issue(Severity.Warning,
                                         $"Template {index} \"{template.name}\" nằm trong Content và đang bật — lúc chạy nó sẽ bị tắt, nhưng khi soạn thì chồng lên ô.",
                                         "Tắt template", () =>
                                         {
                                             Undo.RecordObject(captured.gameObject, "Tắt template");
                                             captured.gameObject.SetActive(false);
                                         }));
                }
            }
            if (!anyTemplate)
            {
                issues.Add(new Issue(Severity.Danger, "Chưa có template nào — thêm ở tab Ô."));
            }

            return issues;
        }

        /// <summary>ScrollRect cuộn đúng trục và Content neo đúng mép đầu của hướng cuộn.</summary>
        public static bool IsSetUp(VirtualScrollViewBase view, ScrollRect scrollRect, RectTransform content)
        {
            bool vertical = view.Direction.IsVertical();
            if (scrollRect.vertical != vertical || scrollRect.horizontal == vertical) return false;

            view.Direction.GetContentAnchors(out Vector2 anchorMin, out Vector2 anchorMax, out Vector2 pivot);
            return anchorMin == content.anchorMin && anchorMax == content.anchorMax && pivot == content.pivot;
        }

        public static void ApplySetup(VirtualScrollViewBase view)
        {
            var scrollRect = view.GetComponent<ScrollRect>();
            if (scrollRect == null || scrollRect.content == null) return;
            Undo.RecordObject(scrollRect, "Áp cấu hình list ảo hoá");
            Undo.RecordObject(scrollRect.content, "Áp cấu hình list ảo hoá");
            bool vertical = view.Direction.IsVertical();
            scrollRect.vertical = vertical;
            scrollRect.horizontal = !vertical;
            view.Direction.ConfigureContent(scrollRect.content);
            Vector2 position = scrollRect.content.anchoredPosition;
            position[view.Direction.MainAxis()] = 0f;
            scrollRect.content.anchoredPosition = position;
            EditorUtility.SetDirty(scrollRect);
            EditorUtility.SetDirty(scrollRect.content);
        }

        public static void CreateContent(ScrollRect scrollRect)
        {
            RectTransform parent = scrollRect.viewport != null ? scrollRect.viewport : (RectTransform)scrollRect.transform;
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            Undo.RegisterCreatedObjectUndo(content.gameObject, "Tạo Content");
            content.SetParent(parent, false);
            Undo.RecordObject(scrollRect, "Tạo Content");
            scrollRect.content = content;
            var view = scrollRect.GetComponent<VirtualScrollViewBase>();
            if (view != null) view.Direction.ConfigureContent(content);
        }

        private static void RemoveLayoutDrivers(RectTransform content)
        {
            var fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter != null) Undo.DestroyObjectImmediate(fitter);
            var group = content.GetComponent<LayoutGroup>();
            if (group != null) Undo.DestroyObjectImmediate(group);
        }

        public static string DirectionLabel(ScrollDirection direction)
        {
            switch (direction)
            {
                case ScrollDirection.BottomToTop: return "Dưới → Trên";
                case ScrollDirection.LeftToRight: return "Trái → Phải";
                case ScrollDirection.RightToLeft: return "Phải → Trái";
                default: return "Trên → Dưới";
            }
        }

        /// <summary>Tên 4 cạnh đệm theo hướng: (đầu, cuối, phụ đầu, phụ cuối).</summary>
        public static (string start, string end, string crossStart, string crossEnd) PaddingLabels(ScrollDirection direction)
        {
            switch (direction)
            {
                case ScrollDirection.BottomToTop: return ("Dưới", "Trên", "Trái", "Phải");
                case ScrollDirection.LeftToRight: return ("Trái", "Phải", "Trên", "Dưới");
                case ScrollDirection.RightToLeft: return ("Phải", "Trái", "Trên", "Dưới");
                default: return ("Trên", "Dưới", "Trái", "Phải");
            }
        }
    }
}

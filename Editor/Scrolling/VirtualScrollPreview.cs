using System.Collections.Generic;
using DreamTech.UICore.Scrolling;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DreamTech.UICore.Editor.Scrolling
{
    /// <summary>
    /// Xem trước list ảo hoá ngay trong Edit mode, bằng CHÍNH đường code chạy lúc Play (pool, bố cục, đo cỡ): chỉnh số trong
    /// inspector là thấy ô dời chỗ trong Scene.
    ///
    /// <para><b>Không để lại dấu vết.</b> View xem trước mang cờ không lưu; Content và ScrollRect được chụp lại trước khi xem và
    /// trả về như cũ khi tắt — kể cả trước lúc lưu scene / prefab, trước khi reload code, trước khi vào Play.</para>
    /// </summary>
    [InitializeOnLoad]
    internal static class VirtualScrollPreview
    {
        private sealed class State
        {
            public ScrollRect ScrollRect;
            public RectTransform Content;
            public Vector2 AnchorMin;
            public Vector2 AnchorMax;
            public Vector2 Pivot;
            public Vector2 SizeDelta;
            public Vector2 AnchoredPosition;
            public bool Vertical;
            public bool Horizontal;
            public readonly PreviewAdapter Adapter = new PreviewAdapter();
            public float Offset;
        }

        private static readonly Dictionary<VirtualScrollViewBase, State> States = new Dictionary<VirtualScrollViewBase, State>();
        private static readonly List<VirtualScrollViewBase> Scratch = new List<VirtualScrollViewBase>();

        static VirtualScrollPreview()
        {
            AssemblyReloadEvents.beforeAssemblyReload += StopAll;
            EditorApplication.playModeStateChanged += change =>
            {
                if (change == PlayModeStateChange.ExitingEditMode) StopAll();
            };
            EditorSceneManager.sceneSaving += (scene, path) => StopAll();
            PrefabStage.prefabSaving += _ => StopAll();
            EditorApplication.quitting += StopAll;
        }

        public static bool IsActive(VirtualScrollViewBase view) => view != null && States.ContainsKey(view);

        public static float GetOffset(VirtualScrollViewBase view) => States.TryGetValue(view, out State state) ? state.Offset : 0f;

        public static void Show(VirtualScrollViewBase view, int count, bool mixTypes, float offset)
        {
            if (Application.isPlaying || view == null) return;
            var scrollRect = view.GetComponent<ScrollRect>();
            if (scrollRect == null || scrollRect.content == null) return;

            if (!States.TryGetValue(view, out State state))
            {
                RectTransform content = scrollRect.content;
                state = new State
                {
                    ScrollRect = scrollRect,
                    Content = content,
                    AnchorMin = content.anchorMin,
                    AnchorMax = content.anchorMax,
                    Pivot = content.pivot,
                    SizeDelta = content.sizeDelta,
                    AnchoredPosition = content.anchoredPosition,
                    Vertical = scrollRect.vertical,
                    Horizontal = scrollRect.horizontal,
                };
                States[view] = state;
            }

            state.Adapter.Configure(count, mixTypes, view.Templates.Count);
            state.Offset = Mathf.Max(0f, offset);
            view.EditorPreview(state.Adapter, state.Offset);
            SceneView.RepaintAll();
        }

        /// <summary>Dựng lại theo cài đặt mới (giữ số ô và độ cuộn đang xem).</summary>
        public static void Refresh(VirtualScrollViewBase view)
        {
            if (view == null || !States.TryGetValue(view, out State state)) return;
            view.EditorPreview(state.Adapter, state.Offset);
            SceneView.RepaintAll();
        }

        public static void Stop(VirtualScrollViewBase view)
        {
            if (ReferenceEquals(view, null) || !States.TryGetValue(view, out State state)) return;
            States.Remove(view);
            if (view != null) view.EditorClearPreview();
            Restore(state);
            SceneView.RepaintAll();
        }

        public static void StopAll()
        {
            Scratch.Clear();
            Scratch.AddRange(States.Keys);
            foreach (VirtualScrollViewBase view in Scratch) Stop(view);
            Scratch.Clear();
        }

        private static void Restore(State state)
        {
            if (state.Content != null)
            {
                state.Content.anchorMin = state.AnchorMin;
                state.Content.anchorMax = state.AnchorMax;
                state.Content.pivot = state.Pivot;
                state.Content.sizeDelta = state.SizeDelta;
                state.Content.anchoredPosition = state.AnchoredPosition;
            }
            if (state.ScrollRect != null)
            {
                state.ScrollRect.vertical = state.Vertical;
                state.ScrollRect.horizontal = state.Horizontal;
            }
        }

        /// <summary>Dữ liệu giả cho xem trước: ô ghi "#chỉ số", tuỳ chọn xoay vòng qua mọi template.</summary>
        internal sealed class PreviewAdapter : IVirtualScrollAdapter
        {
            private bool _mixTypes;
            private int _templateCount;

            public int ItemCount { get; private set; }

            public void Configure(int count, bool mixTypes, int templateCount)
            {
                ItemCount = Mathf.Max(0, count);
                _mixTypes = mixTypes;
                _templateCount = Mathf.Max(1, templateCount);
            }

            public int GetItemType(int index) => _mixTypes ? index % _templateCount : 0;

            public void BindItem(VirtualItem item, int index)
            {
                item.name = "[Preview] #" + index;
                var label = item.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = "#" + index;
            }
        }
    }
}

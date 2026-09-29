using System.Collections;
using System.Collections.Generic;
using DreamTech.UICore.Scrolling;
using DreamTech.UICore.Scrolling.Layout;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DreamTech.UICore.Tests.Scrolling
{
    /// <summary>List / grid ảo hoá chạy thật trên ScrollRect: tái dùng view, cuộn tới ô, giữ chỗ khi đo, snap, sự kiện.</summary>
    public sealed class VirtualScrollViewPlayTests
    {
        private const float Tolerance = 0.5f;

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
            {
                if (created != null) Object.Destroy(created);
            }
            _created.Clear();
        }

        [UnityTest]
        public IEnumerator Recycling_KeepsOnlyTheVisibleViews_WhileScrollingThroughAThousandItems()
        {
            VirtualListView list = CreateList(ScrollDirection.TopToBottom, new Vector2(400f, 600f), 100f);
            list.SetItems(1000, (item, index) => { });
            yield return null;

            // Khung [0, 600) + đệm 100 hai phía → ô 0…6 (ô 7 bắt đầu đúng ở 700).
            Assert.AreEqual(7, list.ActiveItemCount);

            int mostCreated = 0;
            for (float offset = 0f; offset <= list.MaxScrollOffset; offset += 350f)
            {
                list.ScrollOffset = offset;
                yield return null;
                mostCreated = Mathf.Max(mostCreated, list.CreatedItemCount);
            }
            Assert.LessOrEqual(mostCreated, 12, "cuộn hết 1000 ô mà chỉ tạo vài view");
            Assert.AreEqual(100000f, list.ContentLength, Tolerance);
        }

        [UnityTest]
        public IEnumerator JumpToIndex_PutsTheItemAtTheViewportStart()
        {
            VirtualListView list = CreateList(ScrollDirection.TopToBottom, new Vector2(400f, 600f), 100f);
            list.SetItems(1000, (item, index) => { });
            yield return null;

            Assert.IsTrue(list.JumpToIndex(500));
            yield return null;

            Assert.AreEqual(50000f, list.ScrollOffset, Tolerance);
            Assert.AreEqual(500, list.FirstVisibleIndex);
            Assert.IsTrue(list.TryGetItem(500, out VirtualItem item));
            Assert.AreEqual(WorldTop(list.Viewport), WorldTop(item.RectTransform), Tolerance);
            Assert.IsFalse(list.JumpToIndex(5000), "không có ô đó");
        }

        [UnityTest]
        public IEnumerator ScrollToIndex_Animates_ThenLandsOnTheTarget()
        {
            VirtualListView list = CreateList(ScrollDirection.TopToBottom, new Vector2(400f, 600f), 100f);
            list.SetItems(1000, (item, index) => { });
            yield return null;

            Assert.IsTrue(list.ScrollToIndex(200, viewportPivot: 0.5f, itemPivot: 0.5f, duration: 1f));
            yield return null;
            Assert.IsTrue(list.IsAnimating);
            Assert.Greater(list.ScrollOffset, 0f);
            Assert.Less(list.ScrollOffset, 19750f);

            yield return new WaitForSecondsRealtime(1.2f);
            // Tâm ô 200 (20 000 + 50) về giữa khung 600 → 19 750.
            Assert.IsFalse(list.IsAnimating);
            Assert.AreEqual(19750f, list.ScrollOffset, Tolerance);
        }

        [UnityTest]
        public IEnumerator MeasureMode_ItemsMeasuredAboveTheView_DoNotMoveWhatIsOnScreen()
        {
            VirtualListView list = CreateList(ScrollDirection.TopToBottom, new Vector2(400f, 600f), 100f, withLayoutElement: true);
            list.SizeMode = ListItemSizeMode.Measure;
            list.EstimatedSize = 100f;
            list.SetItems(300, (item, index) => item.GetCachedComponent<LayoutElement>().preferredHeight = 50f + index % 5 * 40f);
            yield return null;

            list.JumpToIndex(100);
            yield return null;
            Assert.IsTrue(list.TryGetItem(100, out VirtualItem anchor));
            float before = WorldTop(anchor.RectTransform) - WorldTop(list.Viewport);

            list.ScrollOffset -= 300f; // cuộn ngược lên: các ô phía trên lần đầu hiện ra và được đo
            yield return null;
            yield return null;

            Assert.IsTrue(list.TryGetItem(100, out anchor));
            float after = WorldTop(anchor.RectTransform) - WorldTop(list.Viewport);
            Assert.AreEqual(before - 300f, after, 1f, "ô 100 chỉ được dời đúng bằng quãng cuộn");
            Assert.IsTrue(list.Layout.IsMeasured(99), "ô ngay trên ô 100 đã lộ ra và được đo");
            Assert.AreEqual(50f + 99 % 5 * 40f, list.Layout.GetSize(99), Tolerance);
        }

        [UnityTest]
        public IEnumerator Grid_LaysOutRows_AndOnlyBindsVisibleRows()
        {
            VirtualGridView grid = CreateGrid(new Vector2(300f, 600f), new Vector2(100f, 100f), columns: 3);
            grid.SetItems(100, (item, index) => { });
            yield return null;

            Assert.AreEqual(3, grid.ItemsPerLine);
            Assert.AreEqual(34, grid.LineTotal);
            Assert.AreEqual(3400f, grid.ContentLength, Tolerance);
            // Dòng 0…6 (khung 600 + đệm 100) × 3 ô.
            Assert.AreEqual(21, grid.ActiveItemCount);

            Assert.IsTrue(grid.TryGetItem(4, out VirtualItem item));
            Vector2 local = LocalMin(item.RectTransform, grid.Content);
            Assert.AreEqual(100f, local.x - LocalMin(grid.Content, grid.Content).x, Tolerance, "ô 4 = hàng 1, cột 1");
        }

        [UnityTest]
        public IEnumerator NearestSnap_SettlesOnTheClosestItem_AndReportsIt()
        {
            VirtualListView list = CreateList(ScrollDirection.TopToBottom, new Vector2(400f, 600f), 100f);
            list.Snap.Configure(ScrollSnapMode.Nearest, viewportPivot: 0f, itemPivot: 0f, settleSpeed: 50f, duration: 0.1f);
            list.SetItems(100, (item, index) => { });
            yield return null;

            int snapped = -1;
            list.OnSnapped.AddListener(index => snapped = index);
            list.ScrollOffset = 1030f;
            list.OnScroll(new PointerEventData(EventSystem.current)); // như vừa lăn chuột xong
            yield return new WaitForSecondsRealtime(0.3f);

            Assert.AreEqual(1000f, list.ScrollOffset, Tolerance);
            Assert.AreEqual(10, snapped);
        }

        [UnityTest]
        public IEnumerator Paged_NextAndPrevious_MoveOnePageAtATime()
        {
            VirtualListView list = CreateList(ScrollDirection.LeftToRight, new Vector2(500f, 300f), 500f);
            list.Snap.Configure(ScrollSnapMode.Paged, viewportPivot: 0.5f, itemPivot: 0.5f);
            list.SetItems(5, (item, index) => { });
            yield return null;

            var focused = new List<int>();
            list.OnFocusedIndexChanged.AddListener(focused.Add);
            Assert.IsTrue(list.ScrollToNext(0.1f));
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.AreEqual(500f, list.ScrollOffset, Tolerance);
            Assert.AreEqual(1, list.FocusedIndex);

            Assert.IsTrue(list.ScrollToPrevious(0f));
            yield return null;
            Assert.AreEqual(0f, list.ScrollOffset, Tolerance);
            Assert.IsFalse(list.ScrollToPrevious(0f), "đang ở trang đầu");
            CollectionAssert.Contains(focused, 1);
        }

        [UnityTest]
        public IEnumerator ReachedEnd_FiresForShortContent_AndWhenScrolledToTheEnd()
        {
            VirtualListView shortList = CreateList(ScrollDirection.TopToBottom, new Vector2(400f, 600f), 100f);
            int shortEnds = 0;
            shortList.OnReachedEnd.AddListener(() => shortEnds++);
            shortList.SetItems(3, (item, index) => { });
            yield return null;
            yield return null;
            Assert.AreEqual(1, shortEnds, "nội dung ngắn hơn khung: bắn một lần");

            VirtualListView longList = CreateList(ScrollDirection.TopToBottom, new Vector2(400f, 600f), 100f);
            int ends = 0;
            int starts = 0;
            longList.OnReachedEnd.AddListener(() => ends++);
            longList.OnReachedStart.AddListener(() => starts++);
            longList.SetItems(50, (item, index) => { });
            yield return null;
            Assert.AreEqual(0, ends);

            longList.ScrollOffset = longList.MaxScrollOffset;
            yield return null;
            Assert.AreEqual(1, ends);
            longList.ScrollOffset = 0f;
            yield return null;
            Assert.AreEqual(1, starts, "chạm mép đầu sau khi đã rời nó");
        }

        [UnityTest]
        public IEnumerator BottomToTop_PutsItemZeroAtTheBottom()
        {
            VirtualListView list = CreateList(ScrollDirection.BottomToTop, new Vector2(400f, 600f), 100f);
            list.SetItems(20, (item, index) => { });
            yield return null;

            Assert.IsTrue(list.TryGetItem(0, out VirtualItem first));
            Assert.AreEqual(WorldBottom(list.Viewport), WorldBottom(first.RectTransform), Tolerance);
            Assert.IsTrue(list.TryGetItem(1, out VirtualItem second));
            Assert.AreEqual(WorldTop(first.RectTransform), WorldBottom(second.RectTransform), Tolerance);
        }

        [UnityTest]
        public IEnumerator ShrinkingTheData_ReleasesViewsOfRemovedItems()
        {
            VirtualListView list = CreateList(ScrollDirection.TopToBottom, new Vector2(400f, 600f), 100f);
            DelegateScrollAdapter adapter = list.SetItems(100, (item, index) => { });
            list.ScrollOffset = list.MaxScrollOffset;
            yield return null;

            adapter.ItemCount = 10;
            list.NotifyDataSetChanged();
            yield return null;

            // Dữ liệu co lại: kẹp ngay vào giới hạn mới (không bật đàn hồi từ tận xa về), ô cũ về pool.
            Assert.AreEqual(list.MaxScrollOffset, list.ScrollOffset, Tolerance);
            foreach (VirtualItem item in list.ActiveItems) Assert.Less(item.Index, 10);
            Assert.AreEqual(7, list.ActiveItemCount, "ô 3…9 trong khung 400…1000 + đệm");
        }

        [UnityTest]
        public IEnumerator ItemLifecycleListeners_HearBindAndRecycle()
        {
            VirtualListView list = CreateList(ScrollDirection.TopToBottom, new Vector2(400f, 600f), 100f, withLifecycleProbe: true);
            list.SetItems(100, (item, index) => { });
            yield return null;

            Assert.IsTrue(list.TryGetItem(0, out VirtualItem item));
            var probe = item.GetComponent<LifecycleProbe>();
            Assert.AreEqual(1, probe.Bound);

            list.ScrollOffset = 5000f;
            yield return null;
            Assert.GreaterOrEqual(probe.Recycled, 1, "ô 0 trôi khỏi khung → về pool");
        }

        // ── Dựng ───────────────────────────────────────────────────────────────────────────────────────────────

        private VirtualListView CreateList(ScrollDirection direction, Vector2 viewportSize, float itemMain,
                                           bool withLayoutElement = false, bool withLifecycleProbe = false)
        {
            ScrollRect scrollRect = CreateScroll(direction, viewportSize);
            RectTransform template = CreateTemplate(scrollRect.transform, direction.IsVertical()
                                                        ? new Vector2(viewportSize.x, itemMain)
                                                        : new Vector2(itemMain, viewportSize.y));
            if (withLayoutElement) template.gameObject.AddComponent<LayoutElement>();
            if (withLifecycleProbe) template.gameObject.AddComponent<LifecycleProbe>();

            var list = scrollRect.gameObject.AddComponent<VirtualListView>();
            list.Direction = direction;
            list.Spacing = 0f;
            list.Overscan = 100f;
            list.SetTemplates(template);
            return list;
        }

        private VirtualGridView CreateGrid(Vector2 viewportSize, Vector2 cell, int columns)
        {
            ScrollRect scrollRect = CreateScroll(ScrollDirection.TopToBottom, viewportSize);
            RectTransform template = CreateTemplate(scrollRect.transform, cell);
            var grid = scrollRect.gameObject.AddComponent<VirtualGridView>();
            grid.CellSize = cell;
            grid.Spacing = Vector2.zero;
            grid.Constraint = GridConstraint.FixedCount;
            grid.ConstraintCount = columns;
            grid.Overscan = 100f;
            grid.SetTemplates(template);
            return grid;
        }

        private ScrollRect CreateScroll(ScrollDirection direction, Vector2 viewportSize)
        {
            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            _created.Add(canvasObject);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            var root = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect)).GetComponent<RectTransform>();
            root.SetParent(canvasObject.transform, false);
            root.sizeDelta = viewportSize;

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            viewport.SetParent(root, false);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;

            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);

            var scrollRect = root.GetComponent<ScrollRect>();
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            return scrollRect;
        }

        private static RectTransform CreateTemplate(Transform parent, Vector2 size)
        {
            var template = new GameObject("Template", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            template.SetParent(parent, false);
            template.sizeDelta = size;
            template.gameObject.SetActive(false);
            return template;
        }

        private static float WorldTop(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return corners[1].y;
        }

        private static float WorldBottom(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return corners[0].y;
        }

        private static Vector2 LocalMin(RectTransform rect, RectTransform space)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return space.InverseTransformPoint(corners[0]);
        }
    }
}

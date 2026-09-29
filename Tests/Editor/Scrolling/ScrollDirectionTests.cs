using DreamTech.UICore.Scrolling;
using NUnit.Framework;
using UnityEngine;

namespace DreamTech.UICore.Tests.Scrolling
{
    /// <summary>Quy đổi offset ↔ vị trí Content và cách đặt ô, cho cả 4 hướng.</summary>
    public sealed class ScrollDirectionTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        [TestCase(ScrollDirection.TopToBottom)]
        [TestCase(ScrollDirection.BottomToTop)]
        [TestCase(ScrollDirection.LeftToRight)]
        [TestCase(ScrollDirection.RightToLeft)]
        public void Offset_RoundTrips_AndLeavesTheCrossAxisAlone(ScrollDirection direction)
        {
            var start = new Vector2(3f, 7f);
            Vector2 moved = direction.WithOffset(start, 123f);
            Assert.AreEqual(123f, direction.ToOffset(moved), 0.0001f);
            Assert.AreEqual(start[direction.CrossAxis()], moved[direction.CrossAxis()], 0.0001f);
        }

        [Test]
        public void Offset_GrowsTowardsLaterItems()
        {
            // Nội dung đi lên khi cuộn dọc xuống các ô sau; đi sang trái khi cuộn ngang sang phải.
            Assert.Greater(ScrollDirection.TopToBottom.ToOffset(new Vector2(0f, 50f)), 0f);
            Assert.Greater(ScrollDirection.BottomToTop.ToOffset(new Vector2(0f, -50f)), 0f);
            Assert.Greater(ScrollDirection.LeftToRight.ToOffset(new Vector2(-50f, 0f)), 0f);
            Assert.Greater(ScrollDirection.RightToLeft.ToOffset(new Vector2(50f, 0f)), 0f);
        }

        [TestCase(ScrollDirection.TopToBottom)]
        [TestCase(ScrollDirection.BottomToTop)]
        [TestCase(ScrollDirection.LeftToRight)]
        [TestCase(ScrollDirection.RightToLeft)]
        public void PlaceItem_PutsTheItemAtItsFlowPosition_WhateverItsPivot(ScrollDirection direction)
        {
            RectTransform content = CreateContent(direction, mainLength: 1000f);
            var item = new GameObject("Item", typeof(RectTransform)).GetComponent<RectTransform>();
            item.SetParent(content, false);
            item.pivot = new Vector2(0.3f, 0.8f); // pivot lạ: vị trí không được phụ thuộc nó

            direction.PlaceItem(item, mainStart: 100f, mainSize: 50f, crossStart: 20f, crossSize: 60f);

            Rect contentRect = LocalRect(content, content);
            Rect itemRect = LocalRect(item, content);
            bool vertical = direction.IsVertical();
            Assert.AreEqual(vertical ? 60f : 50f, itemRect.width, 0.01f);
            Assert.AreEqual(vertical ? 50f : 60f, itemRect.height, 0.01f);

            switch (direction)
            {
                case ScrollDirection.TopToBottom:
                    Assert.AreEqual(contentRect.yMax - 100f, itemRect.yMax, 0.01f);
                    Assert.AreEqual(contentRect.xMin + 20f, itemRect.xMin, 0.01f);
                    break;
                case ScrollDirection.BottomToTop:
                    Assert.AreEqual(contentRect.yMin + 100f, itemRect.yMin, 0.01f);
                    Assert.AreEqual(contentRect.xMin + 20f, itemRect.xMin, 0.01f);
                    break;
                case ScrollDirection.LeftToRight:
                    Assert.AreEqual(contentRect.xMin + 100f, itemRect.xMin, 0.01f);
                    Assert.AreEqual(contentRect.yMax - 20f, itemRect.yMax, 0.01f);
                    break;
                case ScrollDirection.RightToLeft:
                    Assert.AreEqual(contentRect.xMax - 100f, itemRect.xMax, 0.01f);
                    Assert.AreEqual(contentRect.yMax - 20f, itemRect.yMax, 0.01f);
                    break;
            }
        }

        [TestCase(ScrollDirection.TopToBottom)]
        [TestCase(ScrollDirection.BottomToTop)]
        [TestCase(ScrollDirection.LeftToRight)]
        [TestCase(ScrollDirection.RightToLeft)]
        public void ConfigureContent_AnchorsTheContentToTheStartEdge(ScrollDirection direction)
        {
            RectTransform content = CreateContent(direction, mainLength: 1000f);
            RectTransform viewport = (RectTransform)content.parent;
            Rect viewportRect = LocalRect(viewport, viewport);
            Rect contentRect = LocalRect(content, viewport);

            switch (direction)
            {
                case ScrollDirection.TopToBottom:
                    Assert.AreEqual(viewportRect.yMax, contentRect.yMax, 0.01f);
                    break;
                case ScrollDirection.BottomToTop:
                    Assert.AreEqual(viewportRect.yMin, contentRect.yMin, 0.01f);
                    break;
                case ScrollDirection.LeftToRight:
                    Assert.AreEqual(viewportRect.xMin, contentRect.xMin, 0.01f);
                    break;
                case ScrollDirection.RightToLeft:
                    Assert.AreEqual(viewportRect.xMax, contentRect.xMax, 0.01f);
                    break;
            }
            int cross = direction.CrossAxis();
            Assert.AreEqual(viewportRect.size[cross], contentRect.size[cross], 0.01f, "Content kéo giãn theo trục phụ");
        }

        private RectTransform CreateContent(ScrollDirection direction, float mainLength)
        {
            _root = new GameObject("Viewport", typeof(RectTransform));
            var viewport = _root.GetComponent<RectTransform>();
            viewport.sizeDelta = new Vector2(400f, 600f);
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            direction.ConfigureContent(content);
            content.SetSizeWithCurrentAnchors(direction.IsVertical() ? RectTransform.Axis.Vertical : RectTransform.Axis.Horizontal,
                                              mainLength);
            return content;
        }

        /// <summary>Hình chữ nhật của <paramref name="target"/> trong hệ local của <paramref name="space"/>.</summary>
        private static Rect LocalRect(RectTransform target, RectTransform space)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector3 min = space.InverseTransformPoint(corners[0]);
            Vector3 max = space.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}

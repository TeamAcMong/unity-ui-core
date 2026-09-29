using System;
using DreamTech.UICore.Scrolling;
using DreamTech.UICore.Scrolling.Layout;
using NUnit.Framework;

namespace DreamTech.UICore.Tests.Scrolling
{
    /// <summary>Toán bố cục của list / grid ảo hoá, so với cách tính tay.</summary>
    public sealed class LayoutModelTests
    {
        private const float Tolerance = 0.001f;

        // ── Fenwick ────────────────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void Fenwick_MatchesNaivePrefixSums_UnderRandomUpdates()
        {
            var random = new Random(1234);
            const int count = 257;
            var values = new float[count];
            for (int index = 0; index < count; index++) values[index] = (float)(random.NextDouble() * 100d);
            var tree = new FenwickTree();
            tree.Build(values, count);

            for (int round = 0; round < 500; round++)
            {
                int index = random.Next(count);
                float next = (float)(random.NextDouble() * 100d);
                tree.Add(index, next - values[index]);
                values[index] = next;

                int length = random.Next(count + 1);
                double naive = 0d;
                for (int position = 0; position < length; position++) naive += values[position];
                Assert.AreEqual(naive, tree.Prefix(length), 0.01d, "prefix " + length);
            }
        }

        [Test]
        public void Fenwick_CountWithin_FindsTheLongestPrefixNotAboveTarget()
        {
            var tree = new FenwickTree();
            tree.Build(new[] { 10f, 20f, 30f, 40f }, 4);

            Assert.AreEqual(0, tree.CountWithin(-1d));
            Assert.AreEqual(0, tree.CountWithin(9.99d));
            Assert.AreEqual(1, tree.CountWithin(10d));
            Assert.AreEqual(1, tree.CountWithin(29.99d));
            Assert.AreEqual(2, tree.CountWithin(30d));
            Assert.AreEqual(3, tree.CountWithin(60d));
            Assert.AreEqual(4, tree.CountWithin(1000d));
        }

        // ── List ───────────────────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void List_PositionsFollowPaddingSizesAndSpacing()
        {
            ListLayoutModel layout = List(10f, 20f, 5f, 100f, 3, index => (index + 1) * 50f);

            Assert.AreEqual(10f, layout.GetStart(0), Tolerance);
            Assert.AreEqual(60f, layout.GetEnd(0), Tolerance);
            Assert.AreEqual(65f, layout.GetStart(1), Tolerance);
            Assert.AreEqual(165f, layout.GetEnd(1), Tolerance);
            Assert.AreEqual(170f, layout.GetStart(2), Tolerance);
            // 10 + 50 + 5 + 100 + 5 + 150 + 20
            Assert.AreEqual(340f, layout.ContentLength, Tolerance);
        }

        [Test]
        public void List_Empty_HasOnlyPadding_AndNoRange()
        {
            ListLayoutModel layout = List(10f, 20f, 5f, 100f, 0);
            Assert.AreEqual(30f, layout.ContentLength, Tolerance);
            Assert.AreEqual(-1, layout.IndexAt(50f));
            layout.GetRange(0f, 500f, out int first, out int last);
            Assert.Less(last, first);
        }

        [Test]
        public void List_IndexAt_CoversTheGapsAndClamps()
        {
            ListLayoutModel layout = List(10f, 0f, 5f, 100f, 3, _ => 50f); // ô: [10,60) [65,115) [120,170)
            Assert.AreEqual(0, layout.IndexAt(-100f));
            Assert.AreEqual(0, layout.IndexAt(0f));
            Assert.AreEqual(0, layout.IndexAt(59f));
            Assert.AreEqual(0, layout.IndexAt(62f), "khoảng cách sau ô 0 thuộc khe của ô 0");
            Assert.AreEqual(1, layout.IndexAt(65f));
            Assert.AreEqual(2, layout.IndexAt(1000f));
        }

        [Test]
        public void List_Range_IsHalfOpen_AndSkipsItemsHiddenInTheGap()
        {
            ListLayoutModel layout = List(10f, 0f, 5f, 100f, 3, _ => 50f); // ô: [10,60) [65,115) [120,170)

            layout.GetRange(0f, 10f, out int first, out int last);
            Assert.Less(last, first, "khung nhìn kết thúc đúng mép đầu ô 0 → chưa thấy ô nào");

            layout.GetRange(61f, 64f, out first, out last);
            Assert.Less(last, first, "chỉ nhìn thấy khoảng cách");

            layout.GetRange(60f, 66f, out first, out last);
            Assert.AreEqual((1, 1), (first, last), "ô 0 kết thúc đúng mép đầu khung → đã khuất");

            layout.GetRange(20f, 130f, out first, out last);
            Assert.AreEqual((0, 2), (first, last));

            layout.GetRange(500f, 600f, out first, out last);
            Assert.Less(last, first);
        }

        [Test]
        public void List_SetSize_MovesEveryLaterItem()
        {
            ListLayoutModel layout = List(0f, 0f, 10f, 100f, 4, _ => 100f);
            Assert.AreEqual(330f, layout.GetStart(3), Tolerance);

            Assert.IsTrue(layout.SetSize(1, 250f));
            Assert.AreEqual(110f, layout.GetStart(1), Tolerance);
            Assert.AreEqual(370f, layout.GetStart(2), Tolerance);
            Assert.AreEqual(480f, layout.GetStart(3), Tolerance);
            Assert.IsFalse(layout.SetSize(1, 250f), "cỡ không đổi thì báo không đổi");
        }

        [Test]
        public void List_UnknownSizes_UseTheEstimate_UntilMeasured()
        {
            ListLayoutModel layout = List(0f, 0f, 0f, 80f, 3);
            Assert.IsFalse(layout.IsMeasured(1));
            Assert.AreEqual(80f, layout.GetSize(1), Tolerance);

            layout.SetSize(1, 30f);
            Assert.IsTrue(layout.IsMeasured(1));

            layout.Configure(0f, 0f, 0f, 60f); // ước lượng mới: chỉ ô chưa đo đổi theo
            Assert.AreEqual(60f, layout.GetSize(0), Tolerance);
            Assert.AreEqual(30f, layout.GetSize(1), Tolerance);
            Assert.AreEqual(150f, layout.ContentLength, Tolerance);
        }

        [Test]
        public void List_InsertAndRemove_KeepTheOtherSizes()
        {
            ListLayoutModel layout = List(0f, 0f, 0f, 10f, 3, index => (index + 1) * 100f); // 100 200 300
            layout.Insert(1, 2); // 100 [10 10] 200 300
            Assert.AreEqual(5, layout.Count);
            Assert.AreEqual(100f, layout.GetSize(0), Tolerance);
            Assert.AreEqual(10f, layout.GetSize(1), Tolerance);
            Assert.AreEqual(200f, layout.GetSize(3), Tolerance);
            Assert.AreEqual(300f, layout.GetSize(4), Tolerance);
            Assert.AreEqual(120f, layout.GetStart(3), Tolerance);

            layout.Remove(0, 3); // 200 300
            Assert.AreEqual(2, layout.Count);
            Assert.AreEqual(200f, layout.GetSize(0), Tolerance);
            Assert.AreEqual(500f, layout.ContentLength, Tolerance);
        }

        [Test]
        public void List_SetCount_KeepsSurvivingSizes()
        {
            ListLayoutModel layout = List(0f, 0f, 0f, 10f, 2, _ => 70f);
            layout.SetCount(4);
            Assert.AreEqual(70f, layout.GetSize(1), Tolerance);
            Assert.AreEqual(10f, layout.GetSize(3), Tolerance);
            layout.SetCount(1);
            Assert.AreEqual(70f, layout.ContentLength, Tolerance);
        }

        [Test]
        public void List_TenThousandVariedItems_MatchANaiveWalk()
        {
            var random = new Random(99);
            const int count = 10000;
            var sizes = new float[count];
            for (int index = 0; index < count; index++) sizes[index] = 40f + (float)random.NextDouble() * 200f;
            ListLayoutModel layout = List(12f, 12f, 6f, 100f, count, index => sizes[index]);

            double start = 12d;
            for (int index = 0; index < count; index++)
            {
                if (index % 997 == 0)
                {
                    Assert.AreEqual((float)start, layout.GetStart(index), 0.05f, "start " + index);
                    float probe = (float)(start + sizes[index] * 0.5d);
                    Assert.AreEqual(index, layout.IndexAt(probe), "index at " + probe);
                }
                start += sizes[index] + 6d;
            }
            Assert.AreEqual((float)(start - 6d + 12d), layout.ContentLength, 0.1f);
        }

        // ── Grid ───────────────────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void Grid_FlexibleFit_FillsTheCrossLength()
        {
            GridLayoutModel grid = Grid(GridConstraint.FlexibleFit, 1, cross: 100f, spacingCross: 10f, crossLength: 330f);
            Assert.AreEqual(3, grid.ItemsPerLine, "3×100 + 2×10 = 320 ≤ 330");

            grid.SetCrossLength(319f);
            Assert.AreEqual(2, grid.ItemsPerLine);

            grid.SetCrossLength(10f);
            Assert.AreEqual(1, grid.ItemsPerLine, "luôn ít nhất 1");
        }

        [Test]
        public void Grid_FixedCount_LinesAndContentLength()
        {
            GridLayoutModel grid = Grid(GridConstraint.FixedCount, 4, main: 50f, spacingMain: 10f, paddingStart: 5f, paddingEnd: 7f);
            grid.SetCount(10);
            Assert.AreEqual(4, grid.ItemsPerLine);
            Assert.AreEqual(3, grid.LineCount);
            Assert.AreEqual(5f + 3f * 50f + 2f * 10f + 7f, grid.ContentLength, Tolerance);
            Assert.AreEqual(2, grid.LineOfItem(9));
            grid.GetItemsOfLine(2, out int first, out int last);
            Assert.AreEqual((8, 9), (first, last));
        }

        [Test]
        public void Grid_CentersTheBlock_AndAlignsTheShortLastLine()
        {
            GridLayoutModel grid = Grid(GridConstraint.FixedCount, 3, cross: 100f, spacingCross: 10f, crossLength: 400f,
                                   alignment: CrossAlignment.Center, lastLine: CrossAlignment.Center);
            grid.SetCount(4);
            // Khối 3×100 + 2×10 = 320 giữa 400 → bắt đầu ở 40.
            Assert.AreEqual(40f, grid.GetItemCrossStart(0), Tolerance);
            Assert.AreEqual(150f, grid.GetItemCrossStart(1), Tolerance);
            Assert.AreEqual(260f, grid.GetItemCrossStart(2), Tolerance);
            // Dòng cuối 1 ô, căn giữa trong khối: 40 + (320 - 100) / 2 = 150.
            Assert.AreEqual(150f, grid.GetItemCrossStart(3), Tolerance);
        }

        [Test]
        public void Grid_LineRange_UsesTheCellNotTheGap()
        {
            GridLayoutModel grid = Grid(GridConstraint.FixedCount, 2, main: 100f, spacingMain: 20f);
            grid.SetCount(10); // dòng: [0,100) [120,220) [240,340) [360,460) [480,580)
            grid.GetLineRange(100f, 119f, out int first, out int last);
            Assert.Less(last, first, "chỉ nhìn thấy khoảng cách");
            grid.GetLineRange(50f, 250f, out first, out last);
            Assert.AreEqual((0, 2), (first, last));
        }

        // ── Snap ───────────────────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void SnapTarget_PutsTheItemPivotOnTheViewportPivot_Clamped()
        {
            // Tâm ô [500, 600) về giữa khung 400: 550 - 200 = 350.
            Assert.AreEqual(350f, ScrollSnapMath.TargetOffset(500f, 100f, 400f, 0.5f, 0.5f, 10000f), Tolerance);
            // Mép đầu ô về mép đầu khung.
            Assert.AreEqual(500f, ScrollSnapMath.TargetOffset(500f, 100f, 400f, 0f, 0f, 10000f), Tolerance);
            // Mép cuối ô về mép cuối khung.
            Assert.AreEqual(200f, ScrollSnapMath.TargetOffset(500f, 100f, 400f, 1f, 1f, 10000f), Tolerance);
            // Kẹp.
            Assert.AreEqual(0f, ScrollSnapMath.TargetOffset(10f, 100f, 400f, 0.5f, 0.5f, 10000f), Tolerance);
            Assert.AreEqual(300f, ScrollSnapMath.TargetOffset(5000f, 100f, 400f, 0f, 0f, 300f), Tolerance);
        }

        // ── Dựng ───────────────────────────────────────────────────────────────────────────────────────────────

        private static ListLayoutModel List(float paddingStart, float paddingEnd, float spacing, float estimate, int count,
                                            Func<int, float> sizeOf = null)
        {
            var layout = new ListLayoutModel();
            layout.Configure(paddingStart, paddingEnd, spacing, estimate);
            layout.SetCount(count, sizeOf);
            return layout;
        }

        private static GridLayoutModel Grid(GridConstraint constraint, int count, float main = 100f, float cross = 100f,
                                            float spacingMain = 0f, float spacingCross = 0f, float paddingStart = 0f,
                                            float paddingEnd = 0f, float crossLength = 1000f,
                                            CrossAlignment alignment = CrossAlignment.Start,
                                            CrossAlignment lastLine = CrossAlignment.Start)
        {
            var grid = new GridLayoutModel();
            grid.Configure(main, cross, spacingMain, spacingCross, paddingStart, paddingEnd, 0f, 0f, constraint, count, alignment,
                           lastLine);
            grid.SetCrossLength(crossLength);
            return grid;
        }
    }
}

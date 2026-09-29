using System;

namespace DreamTech.UICore.Scrolling.Layout
{
    /// <summary>Cách chọn số ô trên một dòng (theo trục phụ).</summary>
    public enum GridConstraint
    {
        /// <summary>Nhét được bao nhiêu ô theo bề rộng khung nhìn thì nhét (ít nhất 1).</summary>
        FlexibleFit = 0,

        /// <summary>Luôn đúng <see cref="GridLayoutModel.ConstraintCount"/> ô.</summary>
        FixedCount = 1,
    }

    /// <summary>
    /// Toán vị trí của lưới ô cùng cỡ. "Dòng" (line) là một hàng khi cuộn dọc, một cột khi cuộn ngang; ô xếp đầy từng dòng
    /// theo trục phụ rồi mới sang dòng sau. Cả khối dòng được căn theo <see cref="Alignment"/> trong phần bề rộng còn trống;
    /// dòng cuối thiếu ô căn riêng theo <see cref="LastLineAlignment"/>.
    /// </summary>
    public sealed class GridLayoutModel
    {
        public float CellMain { get; private set; } = 100f;
        public float CellCross { get; private set; } = 100f;
        public float SpacingMain { get; private set; }
        public float SpacingCross { get; private set; }
        public float PaddingStart { get; private set; }
        public float PaddingEnd { get; private set; }
        public float PaddingCrossStart { get; private set; }
        public float PaddingCrossEnd { get; private set; }
        public GridConstraint Constraint { get; private set; } = GridConstraint.FlexibleFit;
        public int ConstraintCount { get; private set; } = 3;
        public CrossAlignment Alignment { get; private set; } = CrossAlignment.Center;
        public CrossAlignment LastLineAlignment { get; private set; } = CrossAlignment.Start;

        /// <summary>Bề rộng trục phụ của nội dung (thường = khung nhìn).</summary>
        public float CrossLength { get; private set; }

        public int Count { get; private set; }

        public int ItemsPerLine { get; private set; } = 1;

        public int LineCount => Count == 0 ? 0 : (Count + ItemsPerLine - 1) / ItemsPerLine;

        public float LineStride => CellMain + SpacingMain;

        public void Configure(float cellMain, float cellCross, float spacingMain, float spacingCross, float paddingStart,
                              float paddingEnd, float paddingCrossStart, float paddingCrossEnd, GridConstraint constraint,
                              int constraintCount, CrossAlignment alignment, CrossAlignment lastLineAlignment)
        {
            CellMain = Math.Max(1f, cellMain);
            CellCross = Math.Max(1f, cellCross);
            SpacingMain = Math.Max(0f, spacingMain);
            SpacingCross = Math.Max(0f, spacingCross);
            PaddingStart = paddingStart;
            PaddingEnd = paddingEnd;
            PaddingCrossStart = paddingCrossStart;
            PaddingCrossEnd = paddingCrossEnd;
            Constraint = constraint;
            ConstraintCount = Math.Max(1, constraintCount);
            Alignment = alignment;
            LastLineAlignment = lastLineAlignment;
            RecomputeItemsPerLine();
        }

        public void SetCrossLength(float crossLength)
        {
            CrossLength = Math.Max(0f, crossLength);
            RecomputeItemsPerLine();
        }

        public void SetCount(int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            Count = count;
        }

        public float ContentLength
        {
            get
            {
                int lines = LineCount;
                if (lines == 0) return PaddingStart + PaddingEnd;
                return PaddingStart + lines * CellMain + (lines - 1) * SpacingMain + PaddingEnd;
            }
        }

        public float GetLineStart(int line) => PaddingStart + line * LineStride;

        public int LineOfItem(int index) => index / ItemsPerLine;

        public void GetItemsOfLine(int line, out int firstItem, out int lastItem)
        {
            firstItem = line * ItemsPerLine;
            lastItem = Math.Min(Count, firstItem + ItemsPerLine) - 1;
        }

        /// <summary>Dòng mà offset rơi vào khe của nó (dòng + khoảng cách sau nó), kẹp trong [0, LineCount-1]. -1 khi rỗng.</summary>
        public int LineAt(float offset)
        {
            int lines = LineCount;
            if (lines == 0) return -1;
            double local = offset - PaddingStart;
            if (local <= 0d) return 0;
            int line = (int)Math.Floor(local / LineStride);
            return Math.Min(line, lines - 1);
        }

        /// <summary>Các dòng chạm vào đoạn [<paramref name="from"/>, <paramref name="to"/>). Rỗng: first = 0, last = -1.</summary>
        public void GetLineRange(float from, float to, out int first, out int last)
        {
            first = 0;
            last = -1;
            int lines = LineCount;
            if (lines == 0 || to <= from) return;

            int start = LineAt(from);
            if (GetLineStart(start) + CellMain <= from) start++;
            int end = LineAt(to);
            if (GetLineStart(end) >= to) end--;
            if (start > end || start >= lines || end < 0) return;
            first = start;
            last = end;
        }

        /// <summary>Mép đầu theo trục phụ của ô <paramref name="index"/> (tính từ mép trái / mép trên của nội dung).</summary>
        public float GetItemCrossStart(int index)
        {
            int line = index / ItemsPerLine;
            int slot = index - line * ItemsPerLine;
            float stride = CellCross + SpacingCross;

            float blockLength = ItemsPerLine * CellCross + (ItemsPerLine - 1) * SpacingCross;
            float available = CrossLength - PaddingCrossStart - PaddingCrossEnd;
            float blockStart = PaddingCrossStart + (available - blockLength) * Factor(Alignment);

            GetItemsOfLine(line, out int firstItem, out int lastItem);
            int itemsInLine = lastItem - firstItem + 1;
            float lineStart = blockStart;
            if (itemsInLine < ItemsPerLine)
            {
                float lineLength = itemsInLine * CellCross + (itemsInLine - 1) * SpacingCross;
                lineStart = blockStart + (blockLength - lineLength) * Factor(LastLineAlignment);
            }
            return lineStart + slot * stride;
        }

        private void RecomputeItemsPerLine()
        {
            if (Constraint == GridConstraint.FixedCount)
            {
                ItemsPerLine = ConstraintCount;
                return;
            }
            float available = CrossLength - PaddingCrossStart - PaddingCrossEnd;
            int fit = (int)Math.Floor((available + SpacingCross) / (CellCross + SpacingCross) + 0.0001f);
            ItemsPerLine = Math.Max(1, fit);
        }

        internal static float Factor(CrossAlignment alignment)
        {
            switch (alignment)
            {
                case CrossAlignment.Center: return 0.5f;
                case CrossAlignment.End: return 1f;
                default: return 0f;
            }
        }
    }
}

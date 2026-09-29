using System;

namespace DreamTech.UICore.Scrolling.Layout
{
    /// <summary>
    /// Toán vị trí của list một cột (dọc hoặc ngang) có ô cao khác nhau, trên "offset" dọc trục cuộn: 0 = mép đầu của nội dung.
    ///
    /// <para>Mỗi ô chiếm một "khe" = cỡ ô + khoảng cách (ô cuối không có khoảng cách). Cỡ ô biết trước (cố định, theo template,
    /// theo adapter) hoặc đo khi ô xuất hiện: ô chưa đo mang cỡ ước lượng, đo xong gọi <see cref="SetSize"/>. Mọi truy vấn
    /// vị trí O(log n) nhờ <see cref="FenwickTree"/>.</para>
    /// </summary>
    public sealed class ListLayoutModel
    {
        private readonly FenwickTree _slots = new FenwickTree();
        private float[] _sizes = Array.Empty<float>();
        private bool[] _measured = Array.Empty<bool>();
        private float[] _slotScratch = Array.Empty<float>();
        private int _count;

        public float PaddingStart { get; private set; }
        public float PaddingEnd { get; private set; }
        public float Spacing { get; private set; }

        /// <summary>Cỡ gán cho ô chưa biết cỡ.</summary>
        public float EstimatedSize { get; private set; } = 100f;

        public int Count => _count;

        public void Configure(float paddingStart, float paddingEnd, float spacing, float estimatedSize)
        {
            spacing = Math.Max(0f, spacing);
            estimatedSize = Math.Max(0f, estimatedSize);
            bool spacingChanged = !Approximately(Spacing, spacing);
            bool estimateChanged = !Approximately(EstimatedSize, estimatedSize);
            PaddingStart = paddingStart;
            PaddingEnd = paddingEnd;
            Spacing = spacing;
            EstimatedSize = estimatedSize;

            if (estimateChanged)
            {
                // Ô chưa đo đang mang ước lượng cũ — đổi theo, ô đã biết cỡ giữ nguyên.
                for (int index = 0; index < _count; index++)
                {
                    if (!_measured[index]) _sizes[index] = estimatedSize;
                }
            }
            if (spacingChanged || estimateChanged) RebuildSlots();
        }

        /// <summary>
        /// Đổi số ô. Ô cũ còn lại giữ cỡ; ô mới mang cỡ <paramref name="sizeOf"/> (nếu trả &gt; 0, coi như đã biết) hoặc
        /// <see cref="EstimatedSize"/> (chưa đo).
        /// </summary>
        public void SetCount(int count, Func<int, float> sizeOf = null)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            EnsureCapacity(count);
            for (int index = _count; index < count; index++) AssignInitialSize(index, sizeOf);
            _count = count;
            RebuildSlots();
        }

        /// <summary>Gán lại cỡ ban đầu cho mọi ô (khi đổi cách lấy cỡ, hoặc dữ liệu đổi toàn bộ).</summary>
        public void ResetSizes(Func<int, float> sizeOf = null)
        {
            for (int index = 0; index < _count; index++) AssignInitialSize(index, sizeOf);
            RebuildSlots();
        }

        /// <summary>Chèn <paramref name="count"/> ô (chưa đo) trước ô <paramref name="index"/>.</summary>
        public void Insert(int index, int count, Func<int, float> sizeOf = null)
        {
            if ((uint)index > (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
            if (count <= 0) return;
            EnsureCapacity(_count + count);
            Array.Copy(_sizes, index, _sizes, index + count, _count - index);
            Array.Copy(_measured, index, _measured, index + count, _count - index);
            _count += count;
            for (int offset = 0; offset < count; offset++) AssignInitialSize(index + offset, sizeOf);
            RebuildSlots();
        }

        /// <summary>Bỏ <paramref name="count"/> ô từ ô <paramref name="index"/>.</summary>
        public void Remove(int index, int count)
        {
            if (index < 0 || count < 0 || index + count > _count) throw new ArgumentOutOfRangeException(nameof(index));
            if (count == 0) return;
            Array.Copy(_sizes, index + count, _sizes, index, _count - index - count);
            Array.Copy(_measured, index + count, _measured, index, _count - index - count);
            _count -= count;
            RebuildSlots();
        }

        public float GetSize(int index)
        {
            CheckIndex(index);
            return _sizes[index];
        }

        public bool IsMeasured(int index)
        {
            CheckIndex(index);
            return _measured[index];
        }

        /// <summary>Đặt cỡ một ô. Trả về true nếu cỡ thật sự đổi (vị trí các ô sau đó đổi theo).</summary>
        public bool SetSize(int index, float size, bool measured = true)
        {
            CheckIndex(index);
            size = Math.Max(0f, size);
            _measured[index] = measured;
            float delta = size - _sizes[index];
            if (Math.Abs(delta) < 0.0001f) return false;
            _sizes[index] = size;
            _slots.Add(index, delta);
            return true;
        }

        /// <summary>Mép đầu của ô <paramref name="index"/>.</summary>
        public float GetStart(int index)
        {
            if (index <= 0) return PaddingStart;
            if (index > _count) index = _count;
            return PaddingStart + (float)_slots.Prefix(index);
        }

        public float GetEnd(int index) => GetStart(index) + GetSize(index);

        /// <summary>Chiều dài nội dung: đệm đầu + các ô + khoảng cách giữa chúng + đệm cuối.</summary>
        public float ContentLength
        {
            get
            {
                if (_count == 0) return PaddingStart + PaddingEnd;
                return PaddingStart + (float)_slots.Prefix(_count) - Spacing + PaddingEnd;
            }
        }

        /// <summary>
        /// Ô mà offset rơi vào khe của nó (khe = ô + khoảng cách sau nó); trước ô đầu → 0, sau ô cuối → ô cuối. -1 khi rỗng.
        /// </summary>
        public int IndexAt(float offset)
        {
            if (_count == 0) return -1;
            double local = offset - PaddingStart;
            if (local <= 0d) return 0;
            int fullSlots = _slots.CountWithin(local);
            return Math.Min(fullSlots, _count - 1);
        }

        /// <summary>
        /// Các ô chạm vào đoạn [<paramref name="from"/>, <paramref name="to"/>). Rỗng: first = 0, last = -1.
        /// </summary>
        public void GetRange(float from, float to, out int first, out int last)
        {
            first = 0;
            last = -1;
            if (_count == 0 || to <= from) return;

            int start = IndexAt(from);
            if (GetEnd(start) <= from) start++;            // offset nằm trong khoảng cách sau ô → ô đó đã khuất
            int end = IndexAt(to);
            if (GetStart(end) >= to) end--;                // ô bắt đầu đúng mép cuối → chưa lộ
            if (start > end || start >= _count || end < 0) return;
            first = start;
            last = end;
        }

        private void AssignInitialSize(int index, Func<int, float> sizeOf)
        {
            float known = sizeOf != null ? sizeOf(index) : -1f;
            bool isKnown = known > 0f;
            _sizes[index] = isKnown ? known : EstimatedSize;
            _measured[index] = isKnown;
        }

        private void RebuildSlots()
        {
            if (_slotScratch.Length < _count) _slotScratch = new float[Math.Max(_count, _slotScratch.Length * 2)];
            for (int index = 0; index < _count; index++) _slotScratch[index] = _sizes[index] + Spacing;
            _slots.Build(_slotScratch, _count);
        }

        private void EnsureCapacity(int count)
        {
            if (_sizes.Length >= count) return;
            int capacity = Math.Max(count, Math.Max(16, _sizes.Length * 2));
            Array.Resize(ref _sizes, capacity);
            Array.Resize(ref _measured, capacity);
        }

        private void CheckIndex(int index)
        {
            if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
        }

        private static bool Approximately(float a, float b) => Math.Abs(a - b) < 0.0001f;
    }
}

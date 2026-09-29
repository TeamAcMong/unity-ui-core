using System;

namespace DreamTech.UICore.Scrolling.Layout
{
    /// <summary>
    /// Cây Fenwick (binary indexed tree) trên dãy số KHÔNG ÂM: đổi một phần tử, lấy tổng tiền tố và tìm "bao nhiêu phần tử đầu
    /// có tổng ≤ x" đều O(log n). Đây là thứ cho list 10 000 ô cao khác nhau tra được "offset này đang ở ô nào" mà không duyệt cả
    /// dãy. Cộng dồn bằng <c>double</c>: tổng tới vài triệu px vẫn không lệch vị trí ô.
    /// </summary>
    internal sealed class FenwickTree
    {
        private double[] _tree = new double[1];
        private int _count;

        public int Count => _count;

        /// <summary>Dựng lại từ <paramref name="values"/>[0..count) trong O(n).</summary>
        public void Build(float[] values, int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            _count = count;
            if (_tree.Length < count + 1) _tree = new double[Math.Max(count + 1, _tree.Length * 2)];
            Array.Clear(_tree, 0, count + 1);
            for (int index = 1; index <= count; index++)
            {
                _tree[index] += values[index - 1];
                int parent = index + (index & -index);
                if (parent <= count) _tree[parent] += _tree[index];
            }
        }

        /// <summary>Cộng <paramref name="delta"/> vào phần tử <paramref name="index"/> (0-based).</summary>
        public void Add(int index, double delta)
        {
            if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
            for (int node = index + 1; node <= _count; node += node & -node) _tree[node] += delta;
        }

        /// <summary>Tổng của <paramref name="length"/> phần tử đầu (0 ≤ length ≤ Count).</summary>
        public double Prefix(int length)
        {
            if (length > _count) length = _count;
            double sum = 0d;
            for (int node = length; node > 0; node -= node & -node) sum += _tree[node];
            return sum;
        }

        /// <summary>
        /// Số phần tử đầu dài nhất có tổng ≤ <paramref name="target"/> (0 nếu phần tử đầu đã vượt). Dãy phải không âm.
        /// </summary>
        public int CountWithin(double target)
        {
            if (_count == 0 || target < 0d) return 0;
            int position = 0;
            int mask = HighestPowerOfTwoAtMost(_count);
            for (; mask > 0; mask >>= 1)
            {
                int next = position + mask;
                if (next <= _count && _tree[next] <= target)
                {
                    position = next;
                    target -= _tree[next];
                }
            }
            return position;
        }

        private static int HighestPowerOfTwoAtMost(int value)
        {
            int power = 1;
            while (power <= value >> 1) power <<= 1;
            return power;
        }
    }
}

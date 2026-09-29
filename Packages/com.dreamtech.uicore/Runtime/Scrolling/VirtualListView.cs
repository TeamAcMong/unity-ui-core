using System;
using DreamTech.UICore.Scrolling.Layout;
using UnityEngine;

namespace DreamTech.UICore.Scrolling
{
    /// <summary>Cách list biết cỡ theo trục cuộn của từng ô.</summary>
    public enum ListItemSizeMode
    {
        /// <summary>Mọi ô cùng một cỡ (<see cref="VirtualListView.FixedSize"/>). Nhanh nhất.</summary>
        Fixed = 0,

        /// <summary>Cỡ của template ứng với loại view của ô — list nhiều loại ô (tiêu đề nhóm + dòng) mà mỗi loại cỡ cố định.</summary>
        Template = 1,

        /// <summary>Adapter báo cỡ từng ô (<see cref="IVirtualItemSizeSource"/>); ô nào báo ≤ 0 dùng cỡ template.</summary>
        Adapter = 2,

        /// <summary>
        /// Đo khi ô hiện ra: dựng layout của view rồi đọc cỡ ưu tiên (LayoutGroup / LayoutElement / chữ trên gốc view). Ô chưa
        /// hiện mang cỡ ước lượng; đo xong list dời các ô sau mà vẫn giữ chỗ người chơi đang xem. Hợp với ô chữ dài ngắn khác nhau.
        /// </summary>
        Measure = 3,
    }

    /// <summary>Cỡ của ô theo trục phụ.</summary>
    public enum ListCrossSizeMode
    {
        /// <summary>Kéo giãn hết bề rộng khung nhìn (trừ đệm).</summary>
        Stretch = 0,

        /// <summary>Giữ cỡ template, căn theo <see cref="VirtualListView.CrossAlignment"/>.</summary>
        Template = 1,
    }

    /// <summary>
    /// List ảo hoá một cột (dọc) hoặc một hàng (ngang), ô cao khác nhau được. Xem <see cref="VirtualScrollViewBase"/> cho phần
    /// cuộn, hút, sự kiện.
    /// </summary>
    [AddComponentMenu("DreamTech/UI Core/Virtual List View")]
    public sealed class VirtualListView : VirtualScrollViewBase
    {
        [Tooltip("Khoảng cách giữa hai ô theo trục cuộn.")]
        [SerializeField, Min(0f)] private float _spacing = 8f;

        [SerializeField] private ListItemSizeMode _sizeMode = ListItemSizeMode.Template;

        [Tooltip("Fixed: cỡ mọi ô.")]
        [SerializeField, Min(1f)] private float _fixedSize = 100f;

        [Tooltip("Measure: cỡ tạm của ô chưa đo. 0 = cỡ template 0. Càng sát cỡ thật thì thanh cuộn càng ít nhảy.")]
        [SerializeField, Min(0f)] private float _estimatedSize;

        [SerializeField] private ListCrossSizeMode _crossSizeMode = ListCrossSizeMode.Stretch;
        [SerializeField] private CrossAlignment _crossAlignment = CrossAlignment.Center;

        private readonly ListLayoutModel _layout = new ListLayoutModel();
        private bool _sizesStale = true;
        private float _crossLength;

        /// <summary>Mô hình bố cục (chỉ đọc) — vị trí và cỡ từng ô.</summary>
        public ListLayoutModel Layout => _layout;

        public float Spacing
        {
            get => _spacing;
            set
            {
                _spacing = Mathf.Max(0f, value);
                MarkLayoutDirty();
            }
        }

        public ListItemSizeMode SizeMode
        {
            get => _sizeMode;
            set
            {
                if (_sizeMode == value) return;
                _sizeMode = value;
                _sizesStale = true;
                MarkLayoutDirty();
            }
        }

        public float FixedSize
        {
            get => _fixedSize;
            set
            {
                _fixedSize = Mathf.Max(1f, value);
                if (_sizeMode == ListItemSizeMode.Fixed) _sizesStale = true;
                MarkLayoutDirty();
            }
        }

        public float EstimatedSize
        {
            get => _estimatedSize;
            set
            {
                _estimatedSize = Mathf.Max(0f, value);
                MarkLayoutDirty();
            }
        }

        public ListCrossSizeMode CrossSizeMode
        {
            get => _crossSizeMode;
            set
            {
                _crossSizeMode = value;
                MarkLayoutDirty();
            }
        }

        public CrossAlignment CrossAlignment
        {
            get => _crossAlignment;
            set
            {
                _crossAlignment = value;
                MarkLayoutDirty();
            }
        }

        protected override void OnItemsReset(int itemCount)
        {
            _sizesStale = true;
        }

        protected override void ConfigureLayout(int itemCount, float crossLength)
        {
            _crossLength = crossLength;
            ScrollPadding padding = Padding;
            _layout.Configure(padding.Start, padding.End, _spacing, EstimateSize());

            if (_sizesStale)
            {
                _layout.SetCount(itemCount);
                _layout.ResetSizes(SizeFunction());
                _sizesStale = false;
            }
            else if (_layout.Count != itemCount)
            {
                _layout.SetCount(itemCount, SizeFunction());
            }
        }

        protected override int LineCount => _layout.Count;

        protected override float LayoutContentLength => _layout.ContentLength;

        protected override float GetLineStart(int line) => _layout.Count == 0 ? _layout.PaddingStart : _layout.GetStart(line);

        protected override float GetLineLength(int line) => _layout.Count == 0 ? 0f : _layout.GetSize(line);

        protected override int GetLineAt(float offset) => _layout.IndexAt(offset);

        protected override void GetLineRange(float from, float to, out int firstLine, out int lastLine)
        {
            _layout.GetRange(from, to, out firstLine, out lastLine);
        }

        protected override int GetLineOfItem(int index) => index;

        protected override void GetItemsOfLine(int line, out int firstItem, out int lastItem)
        {
            firstItem = line;
            lastItem = line;
        }

        protected override void GetItemPlacement(int index, out float mainStart, out float mainSize, out float crossStart,
                                                 out float crossSize)
        {
            mainStart = _layout.GetStart(index);
            mainSize = _layout.GetSize(index);

            ScrollPadding padding = Padding;
            float available = Mathf.Max(0f, _crossLength - padding.CrossStart - padding.CrossEnd);
            if (_crossSizeMode == ListCrossSizeMode.Stretch)
            {
                crossStart = padding.CrossStart;
                crossSize = available;
                return;
            }

            crossSize = GetTemplateSize(GetItemType(index), Direction.CrossAxis());
            crossStart = padding.CrossStart + (available - crossSize) * GridLayoutModel.Factor(_crossAlignment);
        }

        protected override bool NeedsMeasure(int index)
        {
            return _sizeMode == ListItemSizeMode.Measure && !_layout.IsMeasured(index);
        }

        protected override bool ApplyMeasuredSize(int index, float size)
        {
            return _layout.SetSize(index, size, measured: true);
        }

        protected override void InvalidateItemSize(int index)
        {
            if (_sizeMode != ListItemSizeMode.Measure || index >= _layout.Count) return;
            _layout.SetSize(index, _layout.GetSize(index), measured: false);
        }

        private float EstimateSize()
        {
            switch (_sizeMode)
            {
                case ListItemSizeMode.Fixed:
                    return _fixedSize;
                case ListItemSizeMode.Measure:
                    return _estimatedSize > 0f ? _estimatedSize : Mathf.Max(1f, GetTemplateSize(0, Direction.MainAxis()));
                default:
                    return Mathf.Max(1f, GetTemplateSize(0, Direction.MainAxis()));
            }
        }

        /// <summary>Cỡ biết trước của ô (&gt; 0) theo chế độ; -1 = chưa biết (dùng ước lượng, chờ đo).</summary>
        private Func<int, float> SizeFunction()
        {
            int main = Direction.MainAxis();
            switch (_sizeMode)
            {
                case ListItemSizeMode.Fixed:
                    return _ => _fixedSize;
                case ListItemSizeMode.Template:
                    return index => Mathf.Max(1f, GetTemplateSize(GetItemType(index), main));
                case ListItemSizeMode.Adapter:
                    return index =>
                    {
                        float size = Adapter is IVirtualItemSizeSource source ? source.GetItemSize(index) : -1f;
                        return size > 0f ? size : Mathf.Max(1f, GetTemplateSize(GetItemType(index), main));
                    };
                default:
                    return null;
            }
        }
    }
}

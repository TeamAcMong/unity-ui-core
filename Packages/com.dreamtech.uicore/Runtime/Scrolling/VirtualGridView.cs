using DreamTech.UICore.Scrolling.Layout;
using UnityEngine;

namespace DreamTech.UICore.Scrolling
{
    /// <summary>
    /// Lưới ảo hoá ô cùng cỡ: cuộn dọc thì xếp theo hàng, cuộn ngang thì xếp theo cột. Số ô mỗi dòng cố định hoặc tự vừa bề
    /// rộng khung nhìn. Xem <see cref="VirtualScrollViewBase"/> cho phần cuộn, hút (theo dòng), sự kiện.
    /// </summary>
    [AddComponentMenu("DreamTech/UI Core/Virtual Grid View")]
    public sealed class VirtualGridView : VirtualScrollViewBase
    {
        [Tooltip("Cỡ ô (rộng × cao).")]
        [SerializeField] private Vector2 _cellSize = new Vector2(200f, 200f);

        [Tooltip("Lấy cỡ ô từ template 0 thay cho Cell Size.")]
        [SerializeField] private bool _cellSizeFromTemplate;

        [Tooltip("Khoảng cách giữa các ô (ngang × dọc).")]
        [SerializeField] private Vector2 _spacing = new Vector2(12f, 12f);

        [SerializeField] private GridConstraint _constraint = GridConstraint.FlexibleFit;
        [SerializeField, Min(1)] private int _constraintCount = 3;

        [Tooltip("Căn cả khối ô trong phần bề rộng còn trống.")]
        [SerializeField] private CrossAlignment _alignment = CrossAlignment.Center;

        [Tooltip("Căn riêng dòng cuối khi thiếu ô.")]
        [SerializeField] private CrossAlignment _lastLineAlignment = CrossAlignment.Start;

        private readonly GridLayoutModel _layout = new GridLayoutModel();

        /// <summary>Mô hình bố cục (chỉ đọc).</summary>
        public GridLayoutModel Layout => _layout;

        /// <summary>Số ô mỗi dòng hiện tại.</summary>
        public int ItemsPerLine => _layout.ItemsPerLine;

        public Vector2 CellSize
        {
            get => _cellSize;
            set
            {
                _cellSize = Vector2.Max(Vector2.one, value);
                MarkLayoutDirty();
            }
        }

        public bool CellSizeFromTemplate
        {
            get => _cellSizeFromTemplate;
            set
            {
                _cellSizeFromTemplate = value;
                MarkLayoutDirty();
            }
        }

        public Vector2 Spacing
        {
            get => _spacing;
            set
            {
                _spacing = Vector2.Max(Vector2.zero, value);
                MarkLayoutDirty();
            }
        }

        public GridConstraint Constraint
        {
            get => _constraint;
            set
            {
                _constraint = value;
                MarkLayoutDirty();
            }
        }

        public int ConstraintCount
        {
            get => _constraintCount;
            set
            {
                _constraintCount = Mathf.Max(1, value);
                MarkLayoutDirty();
            }
        }

        public CrossAlignment Alignment
        {
            get => _alignment;
            set
            {
                _alignment = value;
                MarkLayoutDirty();
            }
        }

        public CrossAlignment LastLineAlignment
        {
            get => _lastLineAlignment;
            set
            {
                _lastLineAlignment = value;
                MarkLayoutDirty();
            }
        }

        /// <summary>Cỡ ô đang dùng thật (Cell Size hoặc cỡ template 0).</summary>
        public Vector2 EffectiveCellSize
        {
            get
            {
                if (!_cellSizeFromTemplate) return _cellSize;
                var fromTemplate = new Vector2(GetTemplateSize(0, 0), GetTemplateSize(0, 1));
                return fromTemplate.x > 0f && fromTemplate.y > 0f ? fromTemplate : _cellSize;
            }
        }

        protected override void ConfigureLayout(int itemCount, float crossLength)
        {
            Vector2 cell = Vector2.Max(Vector2.one, EffectiveCellSize);
            bool vertical = Direction.IsVertical();
            ScrollPadding padding = Padding;
            _layout.Configure(vertical ? cell.y : cell.x, vertical ? cell.x : cell.y, vertical ? _spacing.y : _spacing.x,
                              vertical ? _spacing.x : _spacing.y, padding.Start, padding.End, padding.CrossStart, padding.CrossEnd,
                              _constraint, _constraintCount, _alignment, _lastLineAlignment);
            _layout.SetCrossLength(crossLength);
            _layout.SetCount(itemCount);
        }

        protected override int LineCount => _layout.LineCount;

        protected override float LayoutContentLength => _layout.ContentLength;

        protected override float GetLineStart(int line) => _layout.GetLineStart(line);

        protected override float GetLineLength(int line) => _layout.CellMain;

        protected override int GetLineAt(float offset) => _layout.LineAt(offset);

        protected override void GetLineRange(float from, float to, out int firstLine, out int lastLine)
        {
            _layout.GetLineRange(from, to, out firstLine, out lastLine);
        }

        protected override int GetLineOfItem(int index) => _layout.LineOfItem(index);

        protected override void GetItemsOfLine(int line, out int firstItem, out int lastItem)
        {
            _layout.GetItemsOfLine(line, out firstItem, out lastItem);
        }

        protected override void GetItemPlacement(int index, out float mainStart, out float mainSize, out float crossStart,
                                                 out float crossSize)
        {
            mainStart = _layout.GetLineStart(_layout.LineOfItem(index));
            mainSize = _layout.CellMain;
            crossStart = _layout.GetItemCrossStart(index);
            crossSize = _layout.CellCross;
        }
    }
}

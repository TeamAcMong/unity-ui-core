using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DreamTech.UICore.Scrolling
{
    /// <summary>
    /// Nền của list / grid ảo hoá chạy trên <see cref="ScrollRect"/>: chỉ những ô nằm trong khung nhìn (cộng vùng đệm
    /// <see cref="Overscan"/>) có view; view trôi ra ngoài được trả về pool rồi bind lại cho ô vừa lộ ra. 10 000 ô vẫn chỉ tốn
    /// vài chục view.
    ///
    /// <para><b>Trục và offset.</b> Mọi phép tính làm trên "offset" = khoảng nội dung đã cuộn qua tính từ ô 0 (xem
    /// <see cref="ScrollDirectionExtensions"/>), nên 4 hướng cuộn dùng chung một đường code. Content được neo vào mép đầu của
    /// khung nhìn, kéo giãn theo trục phụ; kích thước theo trục cuộn do list đặt.</para>
    ///
    /// <para><b>Dòng (line).</b> Lớp con mô tả nội dung thành các dòng dọc trục cuộn: list = mỗi ô một dòng, grid = mỗi hàng
    /// (hoặc cột) một dòng. Hút (snap), cuộn tới ô, ô đang focus đều tính theo dòng.</para>
    ///
    /// <para><b>Chạy sau ScrollRect</b> (<c>DefaultExecutionOrder(100)</c>): đọc vị trí cuộn cuối frame rồi mới dựng ô, nên ô
    /// không bao giờ trễ một frame so với nội dung.</para>
    /// </summary>
    [RequireComponent(typeof(ScrollRect))]
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public abstract class VirtualScrollViewBase : UIBehaviour, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler,
                                                  IEndDragHandler, IScrollHandler
    {
        /// <summary>Số vòng tối đa đo → dời → đo lại trong một lần dựng (ô đổi cỡ làm lộ thêm ô).</summary>
        private const int MaxReconcilePasses = 4;

        [SerializeField] private ScrollDirection _direction = ScrollDirection.TopToBottom;
        [SerializeField] private ScrollPadding _padding = new ScrollPadding(0f, 0f, 0f, 0f);

        [Tooltip("Vùng đệm (px) trước và sau khung nhìn vẫn giữ view sẵn — cuộn nhanh không thấy ô trống ở mép.")]
        [SerializeField, Min(0f)] private float _overscan = 120f;

        [SerializeField] private List<VirtualItemTemplate> _templates = new List<VirtualItemTemplate>();
        [SerializeField] private RecycleMode _recycleMode = RecycleMode.Deactivate;
        [SerializeField] private ScrollAnimationSettings _scrollAnimation = new ScrollAnimationSettings();
        [SerializeField] private ScrollSnapSettings _snap = new ScrollSnapSettings();

        [Tooltip("Cách mép đầu/cuối bao nhiêu px thì coi như đã chạm (bắn OnReachedStart / OnReachedEnd — ví dụ để tải thêm).")]
        [SerializeField, Min(0f)] private float _edgeThreshold;

        [SerializeField] private UnityEvent<int> _onFocusedIndexChanged = new UnityEvent<int>();
        [SerializeField] private UnityEvent<int> _onSnapped = new UnityEvent<int>();
        [SerializeField] private UnityEvent _onReachedStart = new UnityEvent();
        [SerializeField] private UnityEvent _onReachedEnd = new UnityEvent();

        private readonly Dictionary<int, VirtualItem> _active = new Dictionary<int, VirtualItem>();
        private readonly List<int> _scratchIndices = new List<int>();

        private ScrollRect _scrollRect;
        private RectTransform _viewport;
        private RectTransform _content;
        private VirtualItemPool _pool;
        private IVirtualScrollAdapter _adapter;
        private int _itemCount;
        private bool _initialized;
        private bool _layoutDirty = true;
        private bool _rebindAll;
        private readonly HashSet<int> _pendingRebind = new HashSet<int>();
        private float _lastOffset = float.NaN;
        private Vector2 _lastViewportSize;
        private int _firstBoundLine;
        private int _lastBoundLine = -1;
        private bool _warnedBadType;
        private bool _reportedMissingContent;
        private bool _prewarmed;
        private bool _clampAfterLayout;

        // Chuyển động do list tự chạy (cuộn tới ô / hút).
        private bool _motionActive;
        private bool _motionIsSnap;
        private float _motionFrom;
        private float _motionElapsed;
        private float _motionDuration;
        private AnimationCurve _motionCurve;
        private int _motionLine = -1;
        private float _motionOffset;
        private float _motionViewportPivot;
        private float _motionItemPivot;

        // Kéo tay.
        private bool _dragging;
        private bool _awaitingSnap;
        private int _lineAtDragStart = -1;
        private PointerEventData _dragEvent;

        private int _focusedIndex = -1;
        private bool _startArmed;
        private bool _endArmed = true;

        // ─────────────────────────────────────────────────────────────────────
        // Sự kiện
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Một view vừa được bind cho ô (sau <see cref="IVirtualScrollAdapter.BindItem"/>).</summary>
        public event Action<VirtualItem, int> ItemBound;

        /// <summary>View của ô vừa trôi khỏi vùng hiển thị, trước khi về pool.</summary>
        public event Action<VirtualItem, int> ItemRecycled;

        /// <summary>Ô gần điểm neo của khung nhìn nhất đổi (dùng điểm neo của snap). Hợp với chấm trang, tiêu đề đang xem.</summary>
        public UnityEvent<int> OnFocusedIndexChanged => _onFocusedIndexChanged;

        /// <summary>Hút xong vào ô (chỉ số ô đầu của dòng).</summary>
        public UnityEvent<int> OnSnapped => _onSnapped;

        /// <summary>Cuộn về tới mép đầu (sau khi đã rời khỏi nó). Hợp với "tải tin cũ hơn" của khung chat.</summary>
        public UnityEvent OnReachedStart => _onReachedStart;

        /// <summary>Cuộn tới mép cuối — bắn cả khi nội dung ngắn hơn khung nhìn. Hợp với tải trang tiếp theo.</summary>
        public UnityEvent OnReachedEnd => _onReachedEnd;

        // ─────────────────────────────────────────────────────────────────────
        // Trạng thái
        // ─────────────────────────────────────────────────────────────────────

        public ScrollRect ScrollRect
        {
            get
            {
                EnsureInitialized();
                return _scrollRect;
            }
        }

        public RectTransform Content
        {
            get
            {
                EnsureInitialized();
                return _content;
            }
        }

        public RectTransform Viewport
        {
            get
            {
                EnsureInitialized();
                return _viewport;
            }
        }

        public ScrollDirection Direction
        {
            get => _direction;
            set
            {
                if (_direction == value) return;
                _direction = value;
                if (_initialized) ApplyDirection();
                MarkLayoutDirty();
            }
        }

        public ScrollPadding Padding
        {
            get => _padding;
            set
            {
                _padding = value;
                MarkLayoutDirty();
            }
        }

        public float Overscan
        {
            get => _overscan;
            set
            {
                _overscan = Mathf.Max(0f, value);
                MarkLayoutDirty();
            }
        }

        public RecycleMode RecycleMode
        {
            get => _recycleMode;
            set
            {
                _recycleMode = value;
                if (_pool != null) _pool.Mode = value;
            }
        }

        public IReadOnlyList<VirtualItemTemplate> Templates => _templates;
        public ScrollSnapSettings Snap => _snap;
        public ScrollAnimationSettings ScrollAnimation => _scrollAnimation;
        public IVirtualScrollAdapter Adapter => _adapter;
        public int ItemCount => _itemCount;
        public int LineTotal => _initialized ? LineCount : 0;

        /// <summary>Số view đang hiển thị ô.</summary>
        public int ActiveItemCount => _active.Count;

        /// <summary>Số view đang nằm chờ trong pool.</summary>
        public int PooledItemCount => _pool != null ? _pool.TotalFreeCount : 0;

        /// <summary>Số view list từng tạo.</summary>
        public int CreatedItemCount => _pool != null ? _pool.CreatedCount : 0;

        public int GetPooledCount(int type) => _pool != null ? _pool.FreeCount(type) : 0;

        public float ViewportLength
        {
            get
            {
                EnsureInitialized();
                return _viewport != null ? _viewport.rect.size[_direction.MainAxis()] : 0f;
            }
        }

        public float CrossLength
        {
            get
            {
                EnsureInitialized();
                return _viewport != null ? _viewport.rect.size[_direction.CrossAxis()] : 0f;
            }
        }

        public float ContentLength
        {
            get
            {
                EnsureLayout();
                return LayoutContentLength;
            }
        }

        public float MaxScrollOffset => Mathf.Max(0f, ContentLength - ViewportLength);

        /// <summary>Độ cuộn hiện tại (px từ ô 0). Gán = nhảy ngay (dừng mọi chuyển động đang chạy).</summary>
        public float ScrollOffset
        {
            get
            {
                EnsureInitialized();
                return _content != null ? _direction.ToOffset(_content.anchoredPosition) : 0f;
            }
            set
            {
                StopScrolling();
                SetOffsetInternal(value);
                RequestReconcile();
            }
        }

        /// <summary>0 = đầu, 1 = cuối.</summary>
        public float NormalizedPosition
        {
            get
            {
                float max = MaxScrollOffset;
                return max > 0f ? Mathf.Clamp01(ScrollOffset / max) : 0f;
            }
            set => ScrollOffset = Mathf.Clamp01(value) * MaxScrollOffset;
        }

        /// <summary>Ô đầu tiên lộ ra trong khung nhìn (không tính vùng đệm); -1 khi rỗng.</summary>
        public int FirstVisibleIndex
        {
            get
            {
                GetVisibleItems(0f, out int first, out _);
                return first;
            }
        }

        /// <summary>Ô cuối cùng lộ ra trong khung nhìn (không tính vùng đệm); -1 khi rỗng.</summary>
        public int LastVisibleIndex
        {
            get
            {
                GetVisibleItems(0f, out _, out int last);
                return last;
            }
        }

        /// <summary>Ô gần điểm neo nhất (theo điểm neo của snap); -1 khi rỗng.</summary>
        public int FocusedIndex => _focusedIndex;

        /// <summary>List đang tự chạy (cuộn tới ô hoặc hút).</summary>
        public bool IsAnimating => _motionActive;

        public bool IsDragging => _dragging;

        public IEnumerable<VirtualItem> ActiveItems => _active.Values;

        // ─────────────────────────────────────────────────────────────────────
        // Dữ liệu
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Gắn nguồn dữ liệu. <paramref name="resetPosition"/>: về ô 0; không thì giữ độ cuộn (kẹp trong giới hạn mới).</summary>
        public void SetAdapter(IVirtualScrollAdapter adapter, bool resetPosition = true)
        {
            EnsureInitialized();
            if (Application.isPlaying) Prewarm();
            _adapter = adapter;
            ReleaseAll();
            _itemCount = adapter != null ? Mathf.Max(0, adapter.ItemCount) : 0;
            OnItemsReset(_itemCount);
            _layoutDirty = true;
            _endArmed = true;
            _startArmed = false;
            _focusedIndex = -1;
            StopScrolling();
            if (resetPosition) SetOffsetInternal(0f);
            else _clampAfterLayout = true;
            ReconcileNow();
        }

        /// <summary>
        /// Cách nhanh không cần viết adapter: <paramref name="bind"/> đổ dữ liệu vào view, <paramref name="typeOf"/> chọn template
        /// (bỏ trống = template 0), <paramref name="sizeOf"/> cỡ ô biết trước (list ở chế độ Adapter).
        /// </summary>
        public DelegateScrollAdapter SetItems(int count, Action<VirtualItem, int> bind, Func<int, int> typeOf = null,
                                             Func<int, float> sizeOf = null, bool resetPosition = true)
        {
            var adapter = new DelegateScrollAdapter(count, bind, typeOf, sizeOf);
            SetAdapter(adapter, resetPosition);
            return adapter;
        }

        /// <summary>
        /// Dữ liệu đổi (số ô, nội dung). Mọi ô đang hiện được bind lại; <paramref name="keepPosition"/> giữ độ cuộn hiện tại (kẹp
        /// trong giới hạn mới), không thì về ô 0.
        /// </summary>
        public void NotifyDataSetChanged(bool keepPosition = true)
        {
            EnsureInitialized();
            int count = _adapter != null ? Mathf.Max(0, _adapter.ItemCount) : 0;
            _itemCount = count;
            OnItemsReset(count);
            _layoutDirty = true;
            _rebindAll = true;
            _endArmed = true;
            if (!keepPosition)
            {
                StopScrolling();
                SetOffsetInternal(0f);
            }
            else
            {
                _clampAfterLayout = true;
            }
            RequestReconcile();
        }

        /// <summary>Bind lại một ô nếu nó đang hiện (dữ liệu của riêng ô đó đổi).</summary>
        public void RefreshItem(int index)
        {
            if (index < 0 || index >= _itemCount) return;
            _pendingRebind.Add(index);
            RequestReconcile();
        }

        /// <summary>Bind lại mọi ô đang hiện, không đổi số ô.</summary>
        public void RefreshVisibleItems()
        {
            _rebindAll = true;
            RequestReconcile();
        }

        /// <summary>Ô <paramref name="index"/> đổi cỡ (list đo cỡ ô): đo lại, dời các ô sau, giữ chỗ người chơi đang xem.</summary>
        public void NotifyItemSizeChanged(int index)
        {
            if (index < 0 || index >= _itemCount) return;
            EnsureLayout();
            InvalidateItemSize(index);
            RequestReconcile();
        }

        public bool TryGetItem(int index, out VirtualItem item) => _active.TryGetValue(index, out item);

        /// <summary>
        /// Đặt danh sách template bằng code (dựng list hoàn toàn từ code, hoặc đổi bộ view). Mọi view cũ bị huỷ; list dựng lại ở
        /// lần đối chiếu kế tiếp.
        /// </summary>
        public void SetTemplates(params RectTransform[] templates)
        {
            EnsureInitialized();
            ReleaseAll();
            _pool?.DestroyAll();
            _templates.Clear();
            if (templates != null)
            {
                foreach (RectTransform template in templates) _templates.Add(new VirtualItemTemplate(template));
            }
            _prewarmed = false;
            _warnedBadType = false;
            if (_initialized && Application.isPlaying) HideSceneTemplates();
            OnItemsReset(_itemCount);
            _rebindAll = true;
            MarkLayoutDirty();
        }

        /// <summary>Offset của mép đầu ô <paramref name="index"/> (px từ ô 0 theo trục cuộn).</summary>
        public float GetItemOffset(int index)
        {
            EnsureLayout();
            if (_itemCount == 0) return 0f;
            return GetLineStart(GetLineOfItem(Mathf.Clamp(index, 0, _itemCount - 1)));
        }

        /// <summary>Dựng lại toàn bộ bố cục ngay (sau khi đổi cài đặt bằng code).</summary>
        public void Rebuild()
        {
            EnsureInitialized();
            if (_initialized) ApplyDirection();
            MarkLayoutDirty();
            ReconcileNow();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Cuộn bằng code
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Cuộn tới ô <paramref name="index"/>: điểm <paramref name="itemPivot"/> của ô (0 = mép đầu, 1 = mép cuối) về điểm
        /// <paramref name="viewportPivot"/> của khung nhìn, kẹp trong giới hạn cuộn. <paramref name="duration"/> &lt; 0 = thời lượng
        /// trong cài đặt; 0 = nhảy ngay. Trả false khi không có ô đó.
        /// </summary>
        public bool ScrollToIndex(int index, float viewportPivot = 0f, float itemPivot = 0f, float duration = -1f)
        {
            EnsureLayout();
            if (index < 0 || index >= _itemCount) return false;
            StartMotion(GetLineOfItem(index), float.NaN, Mathf.Clamp01(viewportPivot), Mathf.Clamp01(itemPivot),
                        duration < 0f ? _scrollAnimation.Duration : duration, _scrollAnimation.Curve, isSnap: false);
            return true;
        }

        public bool JumpToIndex(int index, float viewportPivot = 0f, float itemPivot = 0f)
        {
            return ScrollToIndex(index, viewportPivot, itemPivot, 0f);
        }

        /// <summary>Cuộn tới độ cuộn <paramref name="offset"/> (kẹp trong giới hạn).</summary>
        public void ScrollToOffset(float offset, float duration = -1f)
        {
            EnsureLayout();
            StartMotion(-1, offset, 0f, 0f, duration < 0f ? _scrollAnimation.Duration : duration, _scrollAnimation.Curve, isSnap: false);
        }

        /// <summary>Sang dòng kế tiếp theo điểm neo của snap (trang sau của carousel). Trả false nếu đã ở cuối.</summary>
        public bool ScrollToNext(float duration = -1f) => ScrollByLines(1, duration);

        /// <summary>Về dòng trước theo điểm neo của snap. Trả false nếu đã ở đầu.</summary>
        public bool ScrollToPrevious(float duration = -1f) => ScrollByLines(-1, duration);

        /// <summary>Dừng mọi chuyển động (tự chạy lẫn quán tính).</summary>
        public void StopScrolling()
        {
            _motionActive = false;
            _awaitingSnap = false;
            if (_scrollRect != null) _scrollRect.StopMovement();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Lớp con mô tả bố cục
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Đẩy cài đặt, số ô và bề rộng trục phụ (<paramref name="crossLength"/>) vào mô hình bố cục.</summary>
        protected abstract void ConfigureLayout(int itemCount, float crossLength);

        /// <summary>Dữ liệu đổi toàn bộ (adapter mới / NotifyDataSetChanged): quên cỡ đã đo.</summary>
        protected virtual void OnItemsReset(int itemCount)
        {
        }

        protected abstract int LineCount { get; }

        protected abstract float LayoutContentLength { get; }

        protected abstract float GetLineStart(int line);

        protected abstract float GetLineLength(int line);

        /// <summary>Dòng mà offset rơi vào (kẹp trong giới hạn), -1 khi rỗng.</summary>
        protected abstract int GetLineAt(float offset);

        protected abstract void GetLineRange(float from, float to, out int firstLine, out int lastLine);

        protected abstract int GetLineOfItem(int index);

        protected abstract void GetItemsOfLine(int line, out int firstItem, out int lastItem);

        /// <summary>Vị trí + cỡ của ô theo toạ độ dòng chảy (xem <see cref="ScrollDirectionExtensions.PlaceItem"/>).</summary>
        protected abstract void GetItemPlacement(int index, out float mainStart, out float mainSize, out float crossStart,
                                                 out float crossSize);

        /// <summary>Ô có cần đo cỡ khi vừa bind không (list chế độ đo).</summary>
        protected virtual bool NeedsMeasure(int index) => false;

        /// <summary>Ghi cỡ vừa đo. Trả true nếu cỡ đổi.</summary>
        protected virtual bool ApplyMeasuredSize(int index, float size) => false;

        /// <summary>Quên cỡ đã đo của ô (nội dung đổi) — đo lại khi nó hiện.</summary>
        protected virtual void InvalidateItemSize(int index)
        {
        }

        /// <summary>Cỡ template theo trục (0 = x, 1 = y). Template thiếu → 0.</summary>
        protected float GetTemplateSize(int type, int axis)
        {
            if (type < 0 || type >= _templates.Count || _templates[type] == null || _templates[type].Template == null) return 0f;
            return _templates[type].Template.rect.size[axis];
        }

        /// <summary>Loại view của ô, kẹp về template hợp lệ.</summary>
        protected int GetItemType(int index)
        {
            if (_adapter == null) return 0;
            int type = _adapter.GetItemType(index);
            if (type >= 0 && type < _templates.Count) return type;
            if (!_warnedBadType)
            {
                _warnedBadType = true;
                Debug.LogWarning($"[{GetType().Name}] Adapter trả loại view {type} cho ô {index} nhưng list chỉ có " +
                                 $"{_templates.Count} template — dùng template 0.", this);
            }
            return 0;
        }

        protected void MarkLayoutDirty()
        {
            _layoutDirty = true;
            RequestReconcile();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Vòng đời
        // ─────────────────────────────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();
            EnsureInitialized(reportMissingContent: false);
            if (Application.isPlaying) Prewarm();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _layoutDirty = true;
            _lastOffset = float.NaN;
        }

        protected override void OnDisable()
        {
            _motionActive = false;
            _awaitingSnap = false;
            _dragging = false;
            base.OnDisable();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            _layoutDirty = true;
        }

        protected virtual void LateUpdate()
        {
            if (!_initialized) return;
            float deltaTime = _scrollAnimation.UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

            AdvanceMotion(deltaTime);
            TryStartPendingSnap();

            Vector2 viewportSize = _viewport != null ? _viewport.rect.size : Vector2.zero;
            if (viewportSize != _lastViewportSize)
            {
                _lastViewportSize = viewportSize;
                _layoutDirty = true;
            }

            float offset = ScrollOffset;
            if (_layoutDirty || _rebindAll || _pendingRebind.Count > 0 || !Mathf.Approximately(offset, _lastOffset))
            {
                ReconcileNow();
            }

            UpdateFocus();
            UpdateEdges();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Kéo tay
        // ─────────────────────────────────────────────────────────────────────

        public virtual void OnInitializePotentialDrag(PointerEventData eventData)
        {
            // Chạm vào list đang tự chạy = "bắt" nó lại, như ScrollRect dừng quán tính.
            _motionActive = false;
            _awaitingSnap = false;
        }

        public virtual void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (IsRoutedToParent(eventData)) return;
            _dragging = true;
            _dragEvent = eventData;
            _motionActive = false;
            _awaitingSnap = false;
            _lineAtDragStart = NearestLine(_snap.ViewportPivot, _snap.ItemPivot);
        }

        public virtual void OnDrag(PointerEventData eventData)
        {
            if (_dragging && IsRoutedToParent(eventData)) _dragging = false;
            if (_dragging) _dragEvent = eventData;
        }

        public virtual void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !_dragging) return;
            _dragging = false;
            _dragEvent = null;
            if (!_snap.Enabled || _itemCount == 0) return;

            if (_snap.Mode == ScrollSnapMode.Paged)
            {
                float velocity = _direction.ToOffsetVelocity(_scrollRect.velocity);
                int target = NearestLine(_snap.ViewportPivot, _snap.ItemPivot);
                if (Mathf.Abs(velocity) >= _snap.FlickSpeed && _lineAtDragStart >= 0)
                {
                    target = Mathf.Clamp(_lineAtDragStart + (velocity > 0f ? 1 : -1), 0, LineCount - 1);
                }
                _scrollRect.StopMovement();
                StartSnap(target);
                return;
            }

            _awaitingSnap = true;
        }

        public virtual void OnScroll(PointerEventData eventData)
        {
            // Lăn chuột: người dùng tự cuộn — bỏ chuyển động đang chạy, hút lại sau khi dừng.
            _motionActive = false;
            if (_snap.Enabled) _awaitingSnap = true;
        }

        private bool IsRoutedToParent(PointerEventData eventData)
        {
            return _scrollRect is NestedScrollRect nested && nested.IsRoutingToParent(eventData);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Dựng ô
        // ─────────────────────────────────────────────────────────────────────

        private void RequestReconcile()
        {
            _lastOffset = float.NaN;
        }

        private void EnsureInitialized(bool reportMissingContent = true)
        {
            if (_initialized) return;
            _scrollRect = GetComponent<ScrollRect>();
            if (_scrollRect == null) return;
            _content = _scrollRect.content;
            _viewport = _scrollRect.viewport != null ? _scrollRect.viewport : (RectTransform)_scrollRect.transform;
            if (_content == null)
            {
                // Tạo bằng code: RequireComponent thêm ScrollRect trống trước, Content gán sau — Awake chưa phải lúc báo lỗi.
                if (reportMissingContent && !_reportedMissingContent)
                {
                    _reportedMissingContent = true;
                    Debug.LogError($"[{GetType().Name}] ScrollRect chưa có Content — list không dựng được.", this);
                }
                return;
            }

            _pool = new VirtualItemPool(_content, this) { Mode = _recycleMode };
            ApplyDirection();
            if (Application.isPlaying) HideSceneTemplates();
            _initialized = true;
        }

        private void EnsureLayout()
        {
            EnsureInitialized();
            if (!_initialized || !_layoutDirty) return;
            ConfigureLayout(_itemCount, CrossLength);
            _layoutDirty = false;
            ApplyContentSize();
        }

        private void ApplyDirection()
        {
            bool vertical = _direction.IsVertical();
            _scrollRect.vertical = vertical;
            _scrollRect.horizontal = !vertical;
            _direction.ConfigureContent(_content);
            _layoutDirty = true;
        }

        private void HideSceneTemplates()
        {
            foreach (VirtualItemTemplate entry in _templates)
            {
                RectTransform template = entry?.Template;
                if (template == null || !template.gameObject.scene.IsValid()) continue;   // prefab asset: không đụng
                if (template.gameObject.activeSelf) template.gameObject.SetActive(false);
            }
        }

        private void Prewarm()
        {
            if (!_initialized || _prewarmed) return;
            _prewarmed = true;
            for (int type = 0; type < _templates.Count; type++)
            {
                VirtualItemTemplate entry = _templates[type];
                if (entry?.Template != null && entry.PrewarmCount > 0) _pool.Prewarm(type, entry.Template, entry.PrewarmCount);
            }
        }

        private void ApplyContentSize()
        {
            if (_content == null) return;
            int main = _direction.MainAxis();
            float length = LayoutContentLength;
            if (!Mathf.Approximately(_content.rect.size[main], length))
            {
                _content.SetSizeWithCurrentAnchors(main == 1 ? RectTransform.Axis.Vertical : RectTransform.Axis.Horizontal, length);
            }
        }

        /// <summary>Đối chiếu view với vùng đang nhìn thấy: trả view ra ngoài về pool, bind view cho ô vừa lộ, đo cỡ, xếp chỗ.</summary>
        internal void ReconcileNow()
        {
            EnsureInitialized();
            if (!_initialized) return;
            EnsureLayout();

            if (_clampAfterLayout)
            {
                // Dữ liệu vừa đổi mà giữ vị trí: nội dung ngắn đi thì về ngay giới hạn mới, không bật đàn hồi từ tận xa về.
                _clampAfterLayout = false;
                float clamped = Mathf.Clamp(ScrollOffset, 0f, MaxScrollOffset);
                if (!Mathf.Approximately(clamped, ScrollOffset))
                {
                    _scrollRect.StopMovement();
                    SetOffsetInternal(clamped);
                }
            }

            if (_rebindAll)
            {
                // Dữ liệu đổi: ô cũ có thể không còn — trả hết về pool rồi bind lại những ô đang cần.
                ReleaseAll();
                _rebindAll = false;
                _pendingRebind.Clear();
            }

            for (int pass = 0; pass < MaxReconcilePasses; pass++)
            {
                float offset = ScrollOffset;
                float viewport = ViewportLength;
                GetLineRange(offset - _overscan, offset + viewport + _overscan, out int firstLine, out int lastLine);

                int firstItem = 0;
                int lastItem = -1;
                if (lastLine >= firstLine && _itemCount > 0)
                {
                    GetItemsOfLine(firstLine, out firstItem, out _);
                    GetItemsOfLine(lastLine, out _, out lastItem);
                }

                ReleaseOutside(firstItem, lastItem);
                _firstBoundLine = firstLine;
                _lastBoundLine = lastLine;

                // Mốc giữ chỗ: một ô ĐÃ hiện từ trước (đã đo, cỡ không đổi) đang nằm trong khung — ô mới đo phía trên nó co giãn
                // thế nào thì nó vẫn đứng yên trên màn.
                int anchorLine = FindStableAnchorLine(offset, viewport);
                float anchorDelta = anchorLine >= 0 ? offset - GetLineStart(anchorLine) : 0f;
                bool sizesChanged = false;

                for (int index = firstItem; index <= lastItem; index++)
                {
                    bool isNew = !_active.TryGetValue(index, out VirtualItem item);
                    if (isNew)
                    {
                        item = Acquire(index);
                        if (item == null) continue;
                    }
                    if (isNew || _pendingRebind.Contains(index)) Bind(item, index);

                    if (NeedsMeasure(index) && ApplyMeasuredSize(index, Measure(item, index))) sizesChanged = true;
                }
                _pendingRebind.Clear();

                if (!sizesChanged) break;

                ApplyContentSize();
                KeepAnchor(anchorLine, anchorDelta, offset);
            }

            PositionActiveItems();
            ApplyContentSize();
            _lastOffset = ScrollOffset;
        }

        /// <summary>
        /// Dòng làm mốc giữ chỗ: dòng nhỏ nhất trong số các ô đang có view (bind từ trước lượt này) còn chạm khung nhìn. Chưa có
        /// view nào thì lấy dòng tại mép đầu khung.
        /// </summary>
        private int FindStableAnchorLine(float offset, float viewport)
        {
            if (_itemCount == 0) return -1;
            int best = -1;
            foreach (int index in _active.Keys)
            {
                if (index >= _itemCount) continue;
                int line = GetLineOfItem(index);
                if (best >= 0 && line >= best) continue;
                float start = GetLineStart(line);
                if (start + GetLineLength(line) <= offset || start >= offset + viewport) continue;
                best = line;
            }
            return best >= 0 ? best : GetLineAt(offset);
        }

        private VirtualItem Acquire(int index)
        {
            int type = GetItemType(index);
            if (type >= _templates.Count || _templates[type] == null || _templates[type].Template == null) return null;
            VirtualItem item = _pool.Get(type, _templates[type].Template);
            item.Owner = this;
            item.Index = index;
            _active[index] = item;
            return item;
        }

        private void Bind(VirtualItem item, int index)
        {
            item.Index = index;
            // Đặt cỡ trục phụ TRƯỚC khi bind: nội dung tự xuống dòng (TMP, layout) cần biết bề rộng thật.
            GetItemPlacement(index, out float mainStart, out float mainSize, out float crossStart, out float crossSize);
            _direction.PlaceItem(item.RectTransform, mainStart, mainSize, crossStart, crossSize);
            _adapter?.BindItem(item, index);
            item.NotifyBound();
            ItemBound?.Invoke(item, index);
        }

        private float Measure(VirtualItem item, int index)
        {
            RectTransform rect = item.RectTransform;
            int main = _direction.MainAxis();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            float preferred = PreferredSize(rect, main);
            return preferred > 0f ? preferred : GetTemplateSize(item.TypeIndex, main);
        }

        private static readonly List<Component> LayoutElementBuffer = new List<Component>();

        /// <summary>
        /// Cỡ ưu tiên của gốc view theo trục — như <see cref="LayoutUtility.GetPreferredSize"/> (ưu tiên layoutPriority cao nhất,
        /// cùng mức thì lấy lớn nhất) nhưng BỎ QUA Image / RawImage: ảnh nền của ô báo cỡ gốc của sprite chứ không phải cỡ nội
        /// dung. Không có thành phần layout nào → -1 (list dùng cỡ template).
        /// </summary>
        internal static float PreferredSize(RectTransform rect, int axis)
        {
            float best = -1f;
            int bestPriority = int.MinValue;
            rect.GetComponents(typeof(ILayoutElement), LayoutElementBuffer);
            for (int position = 0; position < LayoutElementBuffer.Count; position++)
            {
                Component component = LayoutElementBuffer[position];
                if (component is Image || component is RawImage) continue;
                if (component is Behaviour behaviour && !behaviour.isActiveAndEnabled) continue;
                var element = (ILayoutElement)component;
                float value = axis == 0 ? element.preferredWidth : element.preferredHeight;
                if (value < 0f) continue;
                int priority = element.layoutPriority;
                if (priority > bestPriority)
                {
                    bestPriority = priority;
                    best = value;
                }
                else if (priority == bestPriority && value > best)
                {
                    best = value;
                }
            }
            LayoutElementBuffer.Clear();
            return best;
        }

        /// <summary>
        /// Ô phía trên đổi cỡ thì mọi thứ phía dưới dời theo — dời độ cuộn một khoảng đúng bằng thế để dòng đang nhìn đứng yên.
        /// Đang kéo tay thì báo lại mốc kéo cho ScrollRect, không thì cú kéo kế tiếp giật về vị trí cũ.
        /// </summary>
        private void KeepAnchor(int anchorLine, float anchorDelta, float previousOffset)
        {
            if (anchorLine < 0 || previousOffset <= 0f) return;      // đang ở mép đầu: giữ mép đầu
            float target = GetLineStart(anchorLine) + anchorDelta;
            if (Mathf.Approximately(target, previousOffset)) return;
            SetOffsetInternal(target);
            if (_dragging && _dragEvent != null) _scrollRect.OnBeginDrag(_dragEvent);
        }

        private void PositionActiveItems()
        {
            foreach (KeyValuePair<int, VirtualItem> pair in _active)
            {
                GetItemPlacement(pair.Key, out float mainStart, out float mainSize, out float crossStart, out float crossSize);
                _direction.PlaceItem(pair.Value.RectTransform, mainStart, mainSize, crossStart, crossSize);
            }
        }

        private void ReleaseOutside(int firstItem, int lastItem)
        {
            _scratchIndices.Clear();
            foreach (int index in _active.Keys)
            {
                if (index < firstItem || index > lastItem || index >= _itemCount) _scratchIndices.Add(index);
            }
            for (int position = 0; position < _scratchIndices.Count; position++) Release(_scratchIndices[position]);
        }

        private void ReleaseAll()
        {
            _scratchIndices.Clear();
            _scratchIndices.AddRange(_active.Keys);
            for (int position = 0; position < _scratchIndices.Count; position++) Release(_scratchIndices[position]);
        }

        private void Release(int index)
        {
            if (!_active.TryGetValue(index, out VirtualItem item)) return;
            _active.Remove(index);
            if (item == null) return;
            item.NotifyRecycled();
            ItemRecycled?.Invoke(item, index);
            if (_adapter is IVirtualItemRecycleListener listener) listener.OnItemRecycled(item, index);
            _pool.Release(item);
        }

        private void SetOffsetInternal(float offset)
        {
            if (_content == null) return;
            Vector2 position = _direction.WithOffset(_content.anchoredPosition, offset);
            if (_content.anchoredPosition != position) _content.anchoredPosition = position;
        }

        private void GetVisibleItems(float margin, out int firstItem, out int lastItem)
        {
            firstItem = -1;
            lastItem = -1;
            EnsureLayout();
            if (!_initialized || _itemCount == 0) return;
            float offset = ScrollOffset;
            GetLineRange(offset - margin, offset + ViewportLength + margin, out int firstLine, out int lastLine);
            if (lastLine < firstLine) return;
            GetItemsOfLine(firstLine, out firstItem, out _);
            GetItemsOfLine(lastLine, out _, out lastItem);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Chuyển động
        // ─────────────────────────────────────────────────────────────────────

        private bool ScrollByLines(int step, float duration)
        {
            EnsureLayout();
            if (_itemCount == 0) return false;
            int current = _motionActive && _motionLine >= 0 ? _motionLine : NearestLine(_snap.ViewportPivot, _snap.ItemPivot);
            int target = Mathf.Clamp(current + step, 0, LineCount - 1);
            if (target == current) return false;
            StartMotion(target, float.NaN, _snap.ViewportPivot, _snap.ItemPivot,
                        duration < 0f ? _scrollAnimation.Duration : duration, _scrollAnimation.Curve, isSnap: _snap.Enabled);
            return true;
        }

        private void StartSnap(int line)
        {
            StartMotion(line, float.NaN, _snap.ViewportPivot, _snap.ItemPivot, _snap.Duration, _snap.Curve, isSnap: true);
        }

        private void StartMotion(int line, float offset, float viewportPivot, float itemPivot, float duration, AnimationCurve curve,
                                 bool isSnap)
        {
            if (_scrollRect != null) _scrollRect.StopMovement();
            _awaitingSnap = false;
            _motionLine = line;
            _motionOffset = offset;
            _motionViewportPivot = viewportPivot;
            _motionItemPivot = itemPivot;
            _motionFrom = ScrollOffset;
            _motionElapsed = 0f;
            _motionDuration = duration;
            _motionCurve = curve;
            _motionIsSnap = isSnap;
            _motionActive = true;

            if (duration <= 0f) AdvanceMotion(0f);
        }

        /// <summary>Đích của chuyển động, tính lại mỗi frame: ô vừa đo cỡ xong làm đích dời theo.</summary>
        private float MotionTarget()
        {
            float max = MaxScrollOffset;
            if (_motionLine < 0) return Mathf.Clamp(_motionOffset, 0f, max);
            int line = Mathf.Clamp(_motionLine, 0, Mathf.Max(0, LineCount - 1));
            return ScrollSnapMath.TargetOffset(GetLineStart(line), GetLineLength(line), ViewportLength, _motionViewportPivot,
                                               _motionItemPivot, max);
        }

        private void AdvanceMotion(float deltaTime)
        {
            if (!_motionActive) return;
            EnsureLayout();
            _motionElapsed += Mathf.Max(0f, deltaTime);
            float progress = _motionDuration <= 0f ? 1f : Mathf.Clamp01(_motionElapsed / _motionDuration);
            float eased = _motionCurve != null && _motionCurve.length > 0 ? _motionCurve.Evaluate(progress) : progress;
            float target = MotionTarget();
            SetOffsetInternal(Mathf.LerpUnclamped(_motionFrom, target, eased));
            if (_scrollRect != null) _scrollRect.velocity = Vector2.zero;
            RequestReconcile();

            if (progress < 1f) return;
            SetOffsetInternal(MotionTarget());
            _motionActive = false;
            if (_motionIsSnap && _motionLine >= 0 && _itemCount > 0)
            {
                GetItemsOfLine(Mathf.Clamp(_motionLine, 0, LineCount - 1), out int firstItem, out _);
                _onSnapped.Invoke(firstItem);
            }
        }

        private void TryStartPendingSnap()
        {
            if (!_awaitingSnap || _dragging || _motionActive) return;
            if (!_snap.Enabled || _itemCount == 0)
            {
                _awaitingSnap = false;
                return;
            }
            float offset = ScrollOffset;
            float max = MaxScrollOffset;
            if (offset < -0.5f || offset > max + 0.5f) return;            // đang bật đàn hồi ở mép: chờ nó về
            float speed = Mathf.Abs(_direction.ToOffsetVelocity(_scrollRect.velocity));
            if (speed > _snap.SettleSpeed) return;
            _awaitingSnap = false;
            StartSnap(NearestLine(_snap.ViewportPivot, _snap.ItemPivot));
        }

        /// <summary>Dòng có điểm <paramref name="itemPivot"/> gần điểm <paramref name="viewportPivot"/> của khung nhìn nhất.</summary>
        internal int NearestLine(float viewportPivot, float itemPivot)
        {
            EnsureLayout();
            int lines = LineCount;
            if (lines == 0) return -1;
            float anchor = ScrollOffset + viewportPivot * ViewportLength;
            int center = Mathf.Clamp(GetLineAt(anchor), 0, lines - 1);
            int best = center;
            float bestDistance = float.MaxValue;
            for (int line = Mathf.Max(0, center - 1); line <= Mathf.Min(lines - 1, center + 1); line++)
            {
                float point = GetLineStart(line) + itemPivot * GetLineLength(line);
                float distance = Mathf.Abs(point - anchor);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = line;
                }
            }
            return best;
        }

        private void UpdateFocus()
        {
            int focused = -1;
            if (_itemCount > 0)
            {
                int line = NearestLine(_snap.ViewportPivot, _snap.ItemPivot);
                if (line >= 0) GetItemsOfLine(line, out focused, out _);
            }
            if (focused == _focusedIndex) return;
            _focusedIndex = focused;
            if (focused >= 0) _onFocusedIndexChanged.Invoke(focused);
        }

        private void UpdateEdges()
        {
            if (_itemCount == 0) return;
            float offset = ScrollOffset;
            bool atStart = offset <= _edgeThreshold + 0.5f;
            bool atEnd = offset >= MaxScrollOffset - _edgeThreshold - 0.5f;

            if (atStart && _startArmed) _onReachedStart.Invoke();
            _startArmed = !atStart;

            if (atEnd && _endArmed) _onReachedEnd.Invoke();
            _endArmed = !atEnd;
        }

#if UNITY_EDITOR
        // ─────────────────────────────────────────────────────────────────────
        // Xem trước trong Edit mode (editor gọi)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Dựng lại view xem trước theo cài đặt hiện tại (Edit mode, không Awake/LateUpdate).</summary>
        internal void EditorPreview(IVirtualScrollAdapter adapter, float offset)
        {
            if (Application.isPlaying) return;
            if (_initialized && _pool != null) _pool.Mode = _recycleMode;
            EnsureInitialized();
            if (!_initialized) return;
            if (!ReferenceEquals(_adapter, adapter))
            {
                _adapter = adapter;
                ReleaseAll();
            }
            _itemCount = adapter != null ? adapter.ItemCount : 0;
            OnItemsReset(_itemCount);
            _layoutDirty = true;
            _rebindAll = true;
            ApplyDirection();
            EnsureLayout();
            SetOffsetInternal(Mathf.Clamp(offset, 0f, MaxScrollOffset));
            ReconcileNow();
        }

        /// <summary>Xoá hết view xem trước, trả list về trạng thái chưa khởi tạo.</summary>
        internal void EditorClearPreview()
        {
            if (Application.isPlaying) return;
            _active.Clear();
            _pool?.DestroyAll();
            _adapter = null;
            _itemCount = 0;
            _initialized = false;
            _layoutDirty = true;
        }

        /// <summary>Dòng đang có view (kể cả vùng đệm) — cho gizmo.</summary>
        internal void EditorGetBoundLines(out int first, out int last)
        {
            first = _firstBoundLine;
            last = _lastBoundLine;
        }

        internal float EditorLineStart(int line) => GetLineStart(line);

        internal float EditorLineLength(int line) => GetLineLength(line);
#endif
    }

    /// <summary>Toán đích cuộn/hút — tách riêng để test.</summary>
    public static class ScrollSnapMath
    {
        /// <summary>
        /// Độ cuộn đưa điểm <paramref name="itemPivot"/> của dòng (mép đầu <paramref name="lineStart"/>, dài
        /// <paramref name="lineLength"/>) tới điểm <paramref name="viewportPivot"/> của khung nhìn, kẹp trong [0, max].
        /// </summary>
        public static float TargetOffset(float lineStart, float lineLength, float viewportLength, float viewportPivot,
                                         float itemPivot, float maxOffset)
        {
            float target = lineStart + itemPivot * lineLength - viewportPivot * viewportLength;
            return Mathf.Clamp(target, 0f, Mathf.Max(0f, maxOffset));
        }
    }
}

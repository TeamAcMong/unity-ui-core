using System;

namespace DreamTech.UICore.Scrolling
{
    /// <summary>
    /// Nguồn dữ liệu của list ảo hoá: có bao nhiêu ô, ô nào dùng loại view nào, và đổ dữ liệu vào view. List chỉ gọi
    /// <see cref="BindItem"/> cho ô sắp lộ ra, trên view lấy từ pool — cùng một view có thể được bind cho nhiều ô khác nhau.
    /// </summary>
    public interface IVirtualScrollAdapter
    {
        int ItemCount { get; }

        /// <summary>Chỉ số template (trong danh sách template của list) cho ô <paramref name="index"/>. List một loại: trả 0.</summary>
        int GetItemType(int index);

        void BindItem(VirtualItem item, int index);
    }

    /// <summary>
    /// Tuỳ chọn: cỡ theo trục cuộn của từng ô, biết trước (list ở chế độ Adapter). Trả ≤ 0 = không biết, list dùng cỡ template.
    /// </summary>
    public interface IVirtualItemSizeSource
    {
        float GetItemSize(int index);
    }

    /// <summary>Tuỳ chọn: được gọi khi view của một ô trôi khỏi vùng hiển thị và về pool.</summary>
    public interface IVirtualItemRecycleListener
    {
        void OnItemRecycled(VirtualItem item, int index);
    }

    /// <summary>Adapter dựng từ hàm — đủ cho phần lớn trường hợp mà không phải viết class riêng.</summary>
    public sealed class DelegateScrollAdapter : IVirtualScrollAdapter, IVirtualItemSizeSource, IVirtualItemRecycleListener
    {
        private readonly Action<VirtualItem, int> _bind;
        private readonly Func<int, int> _typeOf;
        private readonly Func<int, float> _sizeOf;
        private readonly Action<VirtualItem, int> _recycle;

        public DelegateScrollAdapter(int itemCount, Action<VirtualItem, int> bind, Func<int, int> typeOf = null,
                                     Func<int, float> sizeOf = null, Action<VirtualItem, int> recycle = null)
        {
            if (itemCount < 0) throw new ArgumentOutOfRangeException(nameof(itemCount));
            ItemCount = itemCount;
            _bind = bind;
            _typeOf = typeOf;
            _sizeOf = sizeOf;
            _recycle = recycle;
        }

        /// <summary>Đổi được — nhớ gọi <see cref="VirtualScrollViewBase.NotifyDataSetChanged"/> sau đó.</summary>
        public int ItemCount { get; set; }

        public bool HasSizes => _sizeOf != null;

        public int GetItemType(int index) => _typeOf != null ? _typeOf(index) : 0;

        public void BindItem(VirtualItem item, int index) => _bind?.Invoke(item, index);

        public float GetItemSize(int index) => _sizeOf != null ? _sizeOf(index) : -1f;

        public void OnItemRecycled(VirtualItem item, int index) => _recycle?.Invoke(item, index);
    }
}

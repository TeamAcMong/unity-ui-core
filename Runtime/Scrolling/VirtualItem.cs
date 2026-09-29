using System;
using System.Collections.Generic;
using UnityEngine;

namespace DreamTech.UICore.Scrolling
{
    /// <summary>
    /// Component trên view của một ô (list tự gắn nếu template chưa có). Cho biết ô đang hiển thị dữ liệu nào và thuộc list nào.
    /// View được TÁI SỬ DỤNG: cùng một object lần lượt hiển thị nhiều ô khi cuộn — mọi thứ phụ thuộc dữ liệu phải đặt lại mỗi
    /// lần bind.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("DreamTech/UI Core/Virtual Item")]
    public sealed class VirtualItem : MonoBehaviour
    {
        private readonly Dictionary<Type, Component> _componentCache = new Dictionary<Type, Component>();
        private IVirtualItemLifecycle[] _lifecycleListeners;

        /// <summary>Chỉ số dữ liệu đang hiển thị; -1 khi đang nằm trong pool.</summary>
        public int Index { get; internal set; } = -1;

        /// <summary>Loại view (chỉ số template trong list).</summary>
        public int TypeIndex { get; internal set; }

        public VirtualScrollViewBase Owner { get; internal set; }

        public RectTransform RectTransform => (RectTransform)transform;

        public bool IsBound => Index >= 0;

        /// <summary>
        /// Báo nội dung của ô vừa đổi cỡ (list đo cỡ ô — ví dụ mở rộng một dòng). List đo lại và dời các ô sau ở frame kế,
        /// giữ nguyên chỗ người chơi đang xem.
        /// </summary>
        public void NotifySizeChanged()
        {
            if (Owner != null && Index >= 0) Owner.NotifyItemSizeChanged(Index);
        }

        /// <summary>GetComponent có nhớ đệm — gọi trong bind mỗi lần cuộn mà không tốn tìm kiếm.</summary>
        public T GetCachedComponent<T>() where T : Component
        {
            if (_componentCache.TryGetValue(typeof(T), out Component cached) && cached != null) return (T)cached;
            var component = GetComponentInChildren<T>(true);
            _componentCache[typeof(T)] = component;
            return component;
        }

        internal void NotifyBound()
        {
            IVirtualItemLifecycle[] listeners = LifecycleListeners();
            for (int index = 0; index < listeners.Length; index++) listeners[index].OnItemBound(this);
        }

        internal void NotifyRecycled()
        {
            IVirtualItemLifecycle[] listeners = LifecycleListeners();
            for (int index = 0; index < listeners.Length; index++) listeners[index].OnItemRecycled(this);
        }

        private IVirtualItemLifecycle[] LifecycleListeners()
        {
            return _lifecycleListeners ??= GetComponentsInChildren<IVirtualItemLifecycle>(true);
        }
    }

    /// <summary>
    /// Component trên view của ô (hoặc con của nó) muốn biết lúc được bind / trả về pool — ví dụ dừng hoạt ảnh, huỷ tải ảnh.
    /// </summary>
    public interface IVirtualItemLifecycle
    {
        void OnItemBound(VirtualItem item);

        void OnItemRecycled(VirtualItem item);
    }

    /// <summary>Một loại view của ô: template (prefab hoặc object trong scene) và số view tạo sẵn.</summary>
    [Serializable]
    public sealed class VirtualItemTemplate
    {
        [Tooltip("Prefab hoặc object trong scene (để tắt, nằm ngoài Content) làm mẫu cho loại ô này.")]
        [SerializeField] private RectTransform _template;

        [Tooltip("Số view tạo sẵn khi list khởi động, tránh giật khung hình lần cuộn đầu.")]
        [SerializeField, Min(0)] private int _prewarmCount;

        public VirtualItemTemplate()
        {
        }

        public VirtualItemTemplate(RectTransform template, int prewarmCount = 0)
        {
            _template = template;
            _prewarmCount = Mathf.Max(0, prewarmCount);
        }

        public RectTransform Template => _template;
        public int PrewarmCount => _prewarmCount;
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace DreamTech.UICore.Scrolling
{
    /// <summary>Cách cất view đã trôi khỏi vùng hiển thị.</summary>
    public enum RecycleMode
    {
        /// <summary>Tắt object (SetActive false). An toàn, nhưng bật lại tốn OnEnable (Text, Animator, layout…).</summary>
        Deactivate = 0,

        /// <summary>
        /// Giữ object bật, dời ra rất xa ngoài khung nhìn (mask cắt bỏ, không vẽ). Cuộn nhanh mượt hơn với ô nặng, nhưng khung
        /// nhìn PHẢI có RectMask2D/Mask.
        /// </summary>
        Park = 1,
    }

    /// <summary>Pool view theo loại template. Object tạo trong Edit mode (xem trước) mang cờ không lưu.</summary>
    internal sealed class VirtualItemPool
    {
        private const float ParkDistance = 100000f;

        private readonly Dictionary<int, Stack<VirtualItem>> _free = new Dictionary<int, Stack<VirtualItem>>();
        private readonly List<VirtualItem> _all = new List<VirtualItem>();
        private readonly RectTransform _parent;
        private readonly VirtualScrollViewBase _owner;

        public VirtualItemPool(RectTransform parent, VirtualScrollViewBase owner)
        {
            _parent = parent;
            _owner = owner;
        }

        public RecycleMode Mode { get; set; }

        /// <summary>Mọi view list từng tạo (đang dùng + đang nằm trong pool).</summary>
        public int CreatedCount => _all.Count;

        public int FreeCount(int type) => _free.TryGetValue(type, out Stack<VirtualItem> stack) ? stack.Count : 0;

        public int TotalFreeCount
        {
            get
            {
                int total = 0;
                foreach (Stack<VirtualItem> stack in _free.Values) total += stack.Count;
                return total;
            }
        }

        public VirtualItem Get(int type, RectTransform template)
        {
            if (_free.TryGetValue(type, out Stack<VirtualItem> stack))
            {
                while (stack.Count > 0)
                {
                    VirtualItem pooled = stack.Pop();
                    if (pooled == null) continue;          // bị ai đó huỷ bên ngoài
                    if (!pooled.gameObject.activeSelf) pooled.gameObject.SetActive(true);
                    return pooled;
                }
            }
            return Create(type, template, active: true);
        }

        public void Release(VirtualItem item)
        {
            if (item == null) return;
            item.Index = -1;
            if (Mode == RecycleMode.Park)
            {
                item.RectTransform.anchoredPosition = new Vector2(-ParkDistance, ParkDistance);
            }
            else if (item.gameObject.activeSelf)
            {
                item.gameObject.SetActive(false);
            }
            if (!_free.TryGetValue(item.TypeIndex, out Stack<VirtualItem> stack))
            {
                stack = new Stack<VirtualItem>();
                _free[item.TypeIndex] = stack;
            }
            stack.Push(item);
        }

        public void Prewarm(int type, RectTransform template, int count)
        {
            int missing = count - FreeCount(type);
            for (int index = 0; index < missing; index++) Release(Create(type, template, active: Mode == RecycleMode.Park));
        }

        /// <summary>Huỷ mọi view list đã tạo (đổi template, xoá xem trước).</summary>
        public void DestroyAll()
        {
            foreach (VirtualItem item in _all)
            {
                if (item == null) continue;
                if (Application.isPlaying) Object.Destroy(item.gameObject);
                else Object.DestroyImmediate(item.gameObject);
            }
            _all.Clear();
            _free.Clear();
        }

        private VirtualItem Create(int type, RectTransform template, bool active)
        {
            RectTransform instance = Object.Instantiate(template, _parent, false);
            if (!Application.isPlaying)
            {
                // Xem trước trong Edit mode: không bao giờ được lưu vào scene / prefab.
                foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
                {
                    child.gameObject.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
                }
            }
            instance.name = template.name;
            if (instance.gameObject.activeSelf != active) instance.gameObject.SetActive(active);

            var item = instance.GetComponent<VirtualItem>();
            if (item == null) item = instance.gameObject.AddComponent<VirtualItem>();
            if (!Application.isPlaying) item.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
            item.TypeIndex = type;
            item.Owner = _owner;
            item.Index = -1;
            _all.Add(item);
            return item;
        }
    }
}

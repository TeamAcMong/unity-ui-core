using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DreamTech.UICore.Scrolling
{
    /// <summary>
    /// Đổ dữ liệu mẫu vào list/grid lúc chạy — để bấm Play là thấy list hoạt động ngay sau khi tạo từ menu. Ghi "#chỉ số" vào
    /// chữ đầu tiên của ô và tô màu nền xen kẽ. Xoá component này khi đã nối dữ liệu thật.
    /// </summary>
    [AddComponentMenu("DreamTech/UI Core/Virtual Scroll Demo Filler")]
    public sealed class VirtualScrollDemoFiller : MonoBehaviour
    {
        [SerializeField, Min(0)] private int _itemCount = 200;

        [Tooltip("Dùng lần lượt mọi template (ô 0 → template 0, ô 1 → template 1…).")]
        [SerializeField] private bool _cycleTemplates;

        [SerializeField] private Color _evenColor = new Color(1f, 1f, 1f, 0.9f);
        [SerializeField] private Color _oddColor = new Color(0.86f, 0.9f, 1f, 0.9f);

        private void Start()
        {
            var view = GetComponent<VirtualScrollViewBase>();
            if (view == null)
            {
                Debug.LogWarning("[VirtualScrollDemoFiller] Cần VirtualListView / VirtualGridView trên cùng object.", this);
                return;
            }
            int templateCount = Mathf.Max(1, view.Templates.Count);
            view.SetItems(_itemCount, Bind, _cycleTemplates ? index => index % templateCount : null);
        }

        private void Bind(VirtualItem item, int index)
        {
            var label = item.GetCachedComponent<TMP_Text>();
            if (label != null) label.SetText("#{0}", index);
            var background = item.GetComponent<Image>();
            if (background != null) background.color = index % 2 == 0 ? _evenColor : _oddColor;
        }
    }
}

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DreamTech.UICore.Scrolling
{
    /// <summary>
    /// ScrollRect nằm TRONG một ScrollRect khác cuộn theo trục kia (ví dụ carousel ngang trong trang cuộn dọc). Cú kéo đầu tiên
    /// lệch hẳn về trục mà ScrollRect này không cuộn thì cả cú kéo được chuyển cho ScrollRect cha — người chơi vuốt dọc trên
    /// carousel vẫn cuộn được trang. Dùng thay cho <see cref="ScrollRect"/> (list ảo hoá chạy được trên nó).
    /// </summary>
    [AddComponentMenu("DreamTech/UI Core/Nested Scroll Rect")]
    public class NestedScrollRect : ScrollRect
    {
        [Tooltip("Cú kéo lệch về trục của cha nhiều hơn tỉ lệ này (so với trục của mình) thì chuyển cho cha. 1 = trục nào trội hơn thì " +
                 "thắng; 1,5 = phải lệch rõ về phía cha.")]
        [SerializeField, Range(0.5f, 3f)] private float _parentBias = 1f;

        private ScrollRect _parent;
        private bool _dragDecided;
        private bool _routing;

        /// <summary>ScrollRect cha nhận các cú kéo không thuộc trục của mình (tự tìm khi trống).</summary>
        public ScrollRect Parent
        {
            get
            {
                if (_parent == null) _parent = FindParent();
                return _parent;
            }
            set => _parent = value;
        }

        /// <summary>
        /// Cú kéo hiện tại có đang được chuyển cho cha không. Component khác trên cùng object (list ảo hoá) gọi được ngay trong
        /// OnBeginDrag — quyết định được đưa ra một lần cho mỗi cú kéo, ai hỏi trước cũng như nhau.
        /// </summary>
        public bool IsRoutingToParent(PointerEventData eventData)
        {
            if (!_dragDecided) Decide(eventData);
            return _routing;
        }

        public override void OnInitializePotentialDrag(PointerEventData eventData)
        {
            _dragDecided = false;
            _routing = false;
            base.OnInitializePotentialDrag(eventData);
            if (Parent != null) ExecuteEvents.Execute(Parent.gameObject, eventData, ExecuteEvents.initializePotentialDrag);
        }

        public override void OnBeginDrag(PointerEventData eventData)
        {
            if (!_dragDecided) Decide(eventData);
            if (_routing)
            {
                ExecuteEvents.Execute(Parent.gameObject, eventData, ExecuteEvents.beginDragHandler);
                return;
            }
            base.OnBeginDrag(eventData);
        }

        public override void OnDrag(PointerEventData eventData)
        {
            if (_routing)
            {
                ExecuteEvents.Execute(Parent.gameObject, eventData, ExecuteEvents.dragHandler);
                return;
            }
            base.OnDrag(eventData);
        }

        public override void OnEndDrag(PointerEventData eventData)
        {
            if (_routing)
            {
                ExecuteEvents.Execute(Parent.gameObject, eventData, ExecuteEvents.endDragHandler);
            }
            else
            {
                base.OnEndDrag(eventData);
            }
            _dragDecided = false;
            _routing = false;
        }

        protected override void OnDisable()
        {
            _dragDecided = false;
            _routing = false;
            base.OnDisable();
        }

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            _parent = null;
        }

        private void Decide(PointerEventData eventData)
        {
            _dragDecided = true;
            _routing = false;
            if (Parent == null || eventData == null || (horizontal && vertical)) return;

            Vector2 delta = eventData.delta;
            if (delta == Vector2.zero) delta = eventData.position - eventData.pressPosition;
            float along = horizontal ? Mathf.Abs(delta.x) : Mathf.Abs(delta.y);
            float across = horizontal ? Mathf.Abs(delta.y) : Mathf.Abs(delta.x);
            _routing = across > along * _parentBias;
        }

        private ScrollRect FindParent()
        {
            Transform parent = transform.parent;
            return parent != null ? parent.GetComponentInParent<ScrollRect>() : null;
        }
    }
}

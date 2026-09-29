using DreamTech.UICore.Animations;
using DreamTech.UICore.Buttons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DreamTech.UICore.Tests
{
    /// <summary>Chạm bằng ngón tay sinh PointerEnter ngay trước PointerDown — không được loé state Hover.</summary>
    public sealed class InteractiveUIComponentTouchTests
    {
        private GameObject _host;

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
        }

        [Test]
        public void TouchEnter_DoesNotHover_ButMouseEnterDoes()
        {
            _host = new GameObject("Button", typeof(RectTransform));
            var button = _host.AddComponent<AnimatedButton>();

            button.OnPointerEnter(new PointerEventData(null) { pointerId = 0 });
            Assert.AreEqual(UIState.Normal, button.CurrentState);

            button.OnPointerExit(new PointerEventData(null) { pointerId = 0 });
            button.OnPointerEnter(new PointerEventData(null) { pointerId = -1 });
            Assert.AreEqual(UIState.Hover, button.CurrentState);
        }

        [Test]
        public void TouchRelease_ReturnsToNormal_NotHover()
        {
            _host = new GameObject("Button", typeof(RectTransform));
            var button = _host.AddComponent<AnimatedButton>();
            var touch = new PointerEventData(null) { pointerId = 0, button = PointerEventData.InputButton.Left };

            button.OnPointerEnter(touch);
            button.OnPointerDown(touch);
            Assert.AreEqual(UIState.Pressed, button.CurrentState);

            button.OnPointerUp(touch);
            Assert.AreEqual(UIState.Normal, button.CurrentState);
        }
    }
}

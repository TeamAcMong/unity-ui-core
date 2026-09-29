using System.Collections;
using DreamTech.UICore.Animations.Backends;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DreamTech.UICore.Tests
{
    /// <summary>
    /// Hai cách dừng tween của <see cref="UniTaskAnimationBackend"/>: <see cref="IAnimationHandle.Stop"/> trả target về "from" (hành vi
    /// cũ), <see cref="IInterruptibleAnimationHandle.Interrupt"/> giữ nguyên chỗ — để animation kế tiếp đi tiếp từ đó.
    /// </summary>
    public sealed class UniTaskAnimationBackendTests
    {
        private const int MaxFrames = 120;

        private GameObject _host;

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
        }

        [UnityTest]
        public IEnumerator Interrupt_KeepsTheCurrentValue()
        {
            MonoBehaviour host = CreateHost();
            var backend = new UniTaskAnimationBackend { TimeMode = AnimationTimeMode.Unscaled };
            float value = 0f;
            IAnimationHandle handle = backend.TweenFloat(host, 0f, 100f, 10f, v => value = v);

            for (int frame = 0; frame < MaxFrames && value <= 0f; frame++) yield return null;
            Assume.That(value, Is.GreaterThan(0f), "Tween chưa chạy trong thời gian chờ — không đo được.");

            float atInterrupt = value;
            Assert.IsInstanceOf<IInterruptibleAnimationHandle>(handle);
            ((IInterruptibleAnimationHandle)handle).Interrupt();
            for (int frame = 0; frame < 5; frame++) yield return null;

            Assert.IsFalse(handle.IsPlaying);
            Assert.AreEqual(atInterrupt, value, "Ngắt không được trả về giá trị đầu.");
        }

        [UnityTest]
        public IEnumerator Stop_RestoresTheStartValue()
        {
            MonoBehaviour host = CreateHost();
            var backend = new UniTaskAnimationBackend { TimeMode = AnimationTimeMode.Unscaled };
            float value = 0f;
            IAnimationHandle handle = backend.TweenFloat(host, 0f, 100f, 10f, v => value = v);

            for (int frame = 0; frame < MaxFrames && value <= 0f; frame++) yield return null;
            Assume.That(value, Is.GreaterThan(0f), "Tween chưa chạy trong thời gian chờ — không đo được.");

            handle.Stop();
            for (int frame = 0; frame < 5; frame++) yield return null;

            Assert.IsFalse(handle.IsPlaying);
            Assert.AreEqual(0f, value);
        }

        private MonoBehaviour CreateHost()
        {
            _host = new GameObject("Tween Host", typeof(RectTransform), typeof(Image));
            return _host.GetComponent<Image>();
        }
    }
}

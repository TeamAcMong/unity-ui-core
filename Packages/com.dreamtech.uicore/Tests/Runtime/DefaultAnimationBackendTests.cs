using System.Collections;
using DreamTech.UICore.Animations.Backends;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DreamTech.UICore.Tests
{
    /// <summary>
    /// <see cref="DefaultAnimationBackend"/> chạy thật trong Play mode (không cần UniTask): tween tới đúng giá trị đích và báo xong,
    /// <see cref="IAnimationHandle.Stop"/> trả về "from", <see cref="IInterruptibleAnimationHandle.Interrupt"/> giữ nguyên chỗ, host bị
    /// destroy thì tween thôi chạy.
    /// </summary>
    public sealed class DefaultAnimationBackendTests
    {
        private const int MaxFrames = 120;

        private GameObject _host;

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
        }

        [UnityTest]
        public IEnumerator Tween_ReachesTheExactEndValue_AndCompletes()
        {
            MonoBehaviour host = CreateHost();
            var backend = new DefaultAnimationBackend { TimeMode = AnimationTimeMode.Unscaled };
            float value = -1f;
            bool completed = false;
            IAnimationHandle handle = backend.TweenFloat(host, 0f, 10f, 0.1f, v => value = v, onComplete: () => completed = true);
            Assert.AreEqual(0f, value, "Khung đầu phải ghi giá trị ở t = 0 ngay lúc gọi.");

            yield return WaitRealTime(() => completed);

            Assert.IsTrue(completed);
            Assert.IsTrue(handle.IsCompleted);
            Assert.IsFalse(handle.IsPlaying);
            Assert.AreEqual(10f, value);
        }

        [UnityTest]
        public IEnumerator Interrupt_KeepsTheCurrentValue()
        {
            MonoBehaviour host = CreateHost();
            var backend = new DefaultAnimationBackend { TimeMode = AnimationTimeMode.Unscaled };
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
        public IEnumerator Stop_RestoresTheStartValue_Immediately()
        {
            MonoBehaviour host = CreateHost();
            var backend = new DefaultAnimationBackend { TimeMode = AnimationTimeMode.Unscaled };
            float value = 0f;
            IAnimationHandle handle = backend.TweenFloat(host, 0f, 100f, 10f, v => value = v);

            for (int frame = 0; frame < MaxFrames && value <= 0f; frame++) yield return null;
            Assume.That(value, Is.GreaterThan(0f), "Tween chưa chạy trong thời gian chờ — không đo được.");

            handle.Stop();
            Assert.AreEqual(0f, value, "Stop phải trả về giá trị đầu ngay, không đợi khung sau.");
            yield return null;
            Assert.IsFalse(handle.IsPlaying);
            Assert.AreEqual(0f, value);
        }

        [UnityTest]
        public IEnumerator DestroyedHost_StopsTheTween()
        {
            MonoBehaviour host = CreateHost();
            var backend = new DefaultAnimationBackend { TimeMode = AnimationTimeMode.Unscaled };
            int updates = 0;
            IAnimationHandle handle = backend.TweenFloat(host, 0f, 1f, 10f, _ => updates++);
            yield return null;

            Object.Destroy(_host);
            yield return null;
            yield return null;
            int afterDestroy = updates;
            yield return null;
            yield return null;

            Assert.IsFalse(handle.IsPlaying);
            Assert.IsFalse(handle.IsCompleted);
            Assert.AreEqual(afterDestroy, updates, "Host đã bị destroy thì tween không được chạy tiếp.");
        }

        [UnityTest]
        public IEnumerator Punch_ReturnsToTheOriginalScale()
        {
            MonoBehaviour host = CreateHost();
            var backend = new DefaultAnimationBackend { TimeMode = AnimationTimeMode.Unscaled };
            Transform target = host.transform;
            bool completed = false;
            backend.Punch(host, target, new Vector3(0.2f, 0.2f, 0f), 0.15f, onComplete: () => completed = true);

            yield return WaitRealTime(() => completed);

            Assert.IsTrue(completed);
            Assert.AreEqual(Vector3.one, target.localScale);
        }

        [Test]
        public void WithTimeMode_ReturnsItselfForTheSameClock_AndAStableSiblingOtherwise()
        {
            var backend = new DefaultAnimationBackend { TimeMode = AnimationTimeMode.Scaled };

            Assert.AreSame(backend, backend.WithTimeMode(AnimationTimeMode.Scaled));
            var unscaled = (DefaultAnimationBackend)backend.WithTimeMode(AnimationTimeMode.Unscaled);
            Assert.AreEqual(AnimationTimeMode.Unscaled, unscaled.TimeMode);
            Assert.AreSame(unscaled, backend.WithTimeMode(AnimationTimeMode.Unscaled));
        }

        [Test]
        public void TheRegistryDefault_NeedsNoExtraPackage()
        {
            IAnimationBackend previous = AnimationBackendRegistry.Current;
            try
            {
                AnimationBackendRegistry.Current = null;
                Assert.IsInstanceOf<DefaultAnimationBackend>(AnimationBackendRegistry.Current);
            }
            finally
            {
                AnimationBackendRegistry.Current = previous;
            }
        }

        /// <summary>
        /// Chờ theo giờ thật (tối đa 3 giây), không theo số khung: chạy batch mode một khung chỉ vài phần nghìn giây, 120 khung chưa
        /// tới 0,1 giây.
        /// </summary>
        private static IEnumerator WaitRealTime(System.Func<bool> done)
        {
            float deadline = Time.realtimeSinceStartup + 3f;
            while (!done() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        private MonoBehaviour CreateHost()
        {
            _host = new GameObject("Tween Host", typeof(RectTransform), typeof(Image));
            return _host.GetComponent<Image>();
        }
    }
}

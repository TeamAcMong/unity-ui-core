using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using DreamTech.UICore.Animations;
using DreamTech.UICore.Animations.Backends;
using DreamTech.UICore.Animations.Sequence;
using DreamTech.UICore.Base;
using DreamTech.UICore.Behaviors;
using DreamTech.UICore.Buttons;
using DreamTech.UICore.ProgressBars;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DreamTech.UICore.Tests
{
    /// <summary>
    /// <see cref="FrameLoop"/> — vòng Update thay UniTask — và mọi chỗ trước đây chạy bằng UniTask (behavior, sequence, overlay, thanh
    /// tiến độ), chạy từng khung bằng <see cref="FrameLoop.TickAll"/> để kết quả không phụ thuộc tốc độ máy.
    /// </summary>
    public sealed class FrameLoopTests
    {
        private readonly List<Object> _created = new List<Object>();

        [SetUp]
        public void SetUp() => FrameLoop.CancelAll();

        [TearDown]
        public void TearDown()
        {
            FrameLoop.CancelAll();
            foreach (Object created in _created)
            {
                if (created != null) Object.DestroyImmediate(created);
            }
            _created.Clear();
        }

        // ── FrameLoop ─────────────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void Loop_TicksUntilItReturnsFalse_ThenStops()
        {
            int ticks = 0;
            FrameLoopHandle loop = FrameLoop.Run(null, (deltaTime, unscaledDeltaTime) => ++ticks < 3);

            Assert.AreEqual(0, ticks, "Đăng ký không được chạy ngay trong lúc gọi.");
            for (int frame = 0; frame < 5; frame++) FrameLoop.TickAll(0.1f, 0.1f);

            Assert.AreEqual(3, ticks);
            Assert.IsFalse(loop.IsRunning);
        }

        [Test]
        public void LoopStartedDuringATick_BeginsOnTheNextTick()
        {
            int innerTicks = 0;
            FrameLoop.Run(null, (deltaTime, unscaledDeltaTime) =>
            {
                FrameLoop.Run(null, (innerDelta, innerUnscaled) => { innerTicks++; return true; });
                return false;
            });

            FrameLoop.TickAll(0.1f, 0.1f);
            Assert.AreEqual(0, innerTicks);
            FrameLoop.TickAll(0.1f, 0.1f);
            Assert.AreEqual(1, innerTicks);
        }

        [Test]
        public void Cancel_RunsTheCancelCallbackOnce_ButNaturalEndDoesNot()
        {
            int cancelled = 0;
            FrameLoopHandle loop = FrameLoop.Run(null, (deltaTime, unscaledDeltaTime) => true, () => cancelled++);
            loop.Cancel();
            loop.Cancel();
            Assert.AreEqual(1, cancelled);

            int finishedCancelled = 0;
            FrameLoop.Run(null, (deltaTime, unscaledDeltaTime) => false, () => finishedCancelled++);
            FrameLoop.TickAll(0.1f, 0.1f);
            Assert.AreEqual(0, finishedCancelled);
        }

        [Test]
        public void DestroyedOwner_CancelsTheLoop()
        {
            var owner = new GameObject("Owner");
            int ticks = 0;
            int cancelled = 0;
            FrameLoopHandle loop = FrameLoop.Run(owner, (deltaTime, unscaledDeltaTime) => { ticks++; return true; }, () => cancelled++);
            FrameLoop.TickAll(0.1f, 0.1f);

            Object.DestroyImmediate(owner);
            FrameLoop.TickAll(0.1f, 0.1f);

            Assert.AreEqual(1, ticks);
            Assert.AreEqual(1, cancelled);
            Assert.IsFalse(loop.IsRunning);
        }

        [Test]
        public void ThrowingTick_IsLogged_AndStopsOnlyThatLoop()
        {
            int healthyTicks = 0;
            FrameLoopHandle broken = FrameLoop.Run(null, (deltaTime, unscaledDeltaTime) => throw new InvalidOperationException("boom"));
            FrameLoop.Run(null, (deltaTime, unscaledDeltaTime) => { healthyTicks++; return true; });

            LogAssert.Expect(LogType.Exception, new Regex("boom"));
            FrameLoop.TickAll(0.1f, 0.1f);
            FrameLoop.TickAll(0.1f, 0.1f);

            Assert.IsFalse(broken.IsRunning);
            Assert.AreEqual(2, healthyTicks);
        }

        // ── Behavior ──────────────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void LongPress_FiresAfterTheThreshold_AndResetsProgressWhenReleasedEarly()
        {
            var longPress = new LongPressBehavior();
            longPress.Initialize(CreateButton());
            int fired = 0;
            float progress = -1f;
            longPress.onLongPress.AddListener(() => fired++);
            longPress.onProgress.AddListener(value => progress = value);

            longPress.OnPointerStateChanged(UIState.Pressed);
            Tick(0.5f);
            Assert.AreEqual(0, fired);
            longPress.OnPointerStateChanged(UIState.Normal);
            Assert.AreEqual(0f, progress, "Nhả sớm thì tiến độ về 0.");

            longPress.OnPointerStateChanged(UIState.Pressed);
            Tick(0.5f);
            Tick(0.3f);
            Assert.AreEqual(1, fired);
            Assert.AreEqual(1f, progress);
            Assert.IsFalse(longPress.OnBeforeClick(), "Long-press đã bắn thì click thường bị nuốt.");
        }

        [Test]
        public void HoldRepeat_WaitsTheInitialDelay_ThenRepeatsUntilReleased()
        {
            var holdRepeat = new HoldRepeatBehavior();
            holdRepeat.Initialize(CreateButton());
            int repeats = 0;
            holdRepeat.onRepeat.AddListener(() => repeats++);

            holdRepeat.OnPointerStateChanged(UIState.Pressed);
            Tick(0.3f);
            Assert.AreEqual(0, repeats);
            Tick(0.15f);
            Assert.AreEqual(1, repeats);
            Tick(0.11f);
            Assert.AreEqual(2, repeats);

            holdRepeat.OnPointerStateChanged(UIState.Normal);
            Tick(1f);
            Assert.AreEqual(2, repeats);
        }

        [Test]
        public void TimeCooldown_LocksTheButton_ThenReleasesItOnTime()
        {
            var cooldown = new CooldownBehavior();
            var button = CreateButton();
            cooldown.Initialize(button);
            int ended = 0;
            cooldown.onCooldownEnd.AddListener(() => ended++);

            cooldown.OnAfterClick();
            Assert.IsFalse(button.IsInteractable);
            Tick(2f);
            Assert.IsFalse(cooldown.IsReady);
            Tick(1.1f);

            Assert.IsTrue(cooldown.IsReady);
            Assert.IsTrue(button.IsInteractable);
            Assert.AreEqual(1, ended);
        }

        [Test]
        public void ChargeCooldown_RecoversOneChargePerInterval()
        {
            var cooldown = new CooldownBehavior();
            SetField(cooldown, "cooldownType", CooldownBehaviorType.ChargeBased);
            var button = CreateButton();
            cooldown.Initialize(button);

            for (int click = 0; click < 3; click++) cooldown.OnAfterClick();
            Assert.AreEqual(0, cooldown.CurrentCharges);
            Assert.IsFalse(button.IsInteractable);

            Tick(1.05f);
            Assert.AreEqual(1, cooldown.CurrentCharges);
            Assert.IsTrue(button.IsInteractable);
            for (int frame = 0; frame < 3; frame++) Tick(1.05f);
            Assert.AreEqual(3, cooldown.CurrentCharges);
        }

        // ── Sequence ──────────────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void Sequential_StartsEachStepAfterThePreviousOne_ThenCompletes()
        {
            var first = new FakeHandle();
            var second = new FakeHandle();
            int secondStarted = 0;
            bool completed = false;
            var sequence = new AnimationSequence(AnimationSequenceMode.Sequential)
                .Append(() => first)
                .Append(() => { secondStarted++; return second; });
            sequence.OnComplete(() => completed = true);
            sequence.Play(CreateButton());

            Tick(0.1f);
            Assert.AreEqual(0, secondStarted);
            first.Finish();
            Tick(0.1f);
            Assert.AreEqual(1, secondStarted);
            Assert.IsFalse(completed);
            second.Finish();
            Tick(0.1f);

            Assert.IsTrue(completed);
            Assert.IsTrue(sequence.IsCompleted);
        }

        [Test]
        public void Parallel_CompletesWhenEveryStepIsDone_AndStopDoesNotComplete()
        {
            var first = new FakeHandle();
            var second = new FakeHandle();
            bool completed = false;
            var sequence = new AnimationSequence(AnimationSequenceMode.Parallel).Append(() => first).Append(() => second);
            sequence.OnComplete(() => completed = true);
            sequence.Play(CreateButton());

            first.Finish();
            Tick(0.1f);
            Assert.IsFalse(completed);
            second.Finish();
            Tick(0.1f);
            Assert.IsTrue(completed);

            var stopped = new FakeHandle();
            bool stoppedCompleted = false;
            var other = new AnimationSequence(AnimationSequenceMode.Parallel).Append(() => stopped);
            other.OnComplete(() => stoppedCompleted = true);
            other.Play(CreateButton());
            other.Stop();
            Tick(0.1f);
            Assert.IsTrue(stopped.WasStopped);
            Assert.IsFalse(stoppedCompleted);
        }

        // ── Overlay + thanh tiến độ ───────────────────────────────────────────────────────────────────────────

        [Test]
        public void CooldownOverlay_EasesTowardsTheTarget_AndSnaps()
        {
            var host = new GameObject("Overlay", typeof(RectTransform), typeof(Image));
            _created.Add(host);
            var image = host.GetComponent<Image>();
            image.type = Image.Type.Filled;
            var overlay = host.AddComponent<CooldownOverlay>();
            SetField(overlay, "fillImage", image);
            image.fillAmount = 1f;

            overlay.SetProgress(0f);
            Tick(0.05f);
            Assert.AreEqual(0.5f, image.fillAmount, 0.0001f, "smoothSpeed 10/giây: 0,05 s đi được 0,5.");
            Tick(0.05f);
            Assert.AreEqual(0f, image.fillAmount, 0.0001f);
        }

        [Test]
        public void ProgressBar_AnimatesTheValue_AndStopPulseReallyStops()
        {
            var bar = CreateProgressBar(out Image overlay);
            bar.SetValue(1f);
            for (int frame = 0; frame < 10; frame++) Tick(0.05f);
            Assert.AreEqual(1f, bar.DisplayValue, 0.0001f);
            Assert.IsTrue(bar.IsFull);

            bar.StartPulse();
            Tick(0.1f);
            Assert.Greater(overlay.transform.localScale.x, 1f, "Đầy thì overlay đập.");

            bar.StopPulse();
            for (int frame = 0; frame < 20; frame++) Tick(0.1f);
            Assert.AreEqual(Vector3.one, overlay.transform.localScale, "StopPulse phải dừng hẳn, không đập tiếp.");
        }

        // ── Trợ giúp ──────────────────────────────────────────────────────────────────────────────────────────

        private static void Tick(float seconds) => FrameLoop.TickAll(seconds, seconds);

        private AnimatedButton CreateButton()
        {
            var host = new GameObject("Button", typeof(RectTransform), typeof(Image));
            _created.Add(host);
            return host.AddComponent<AnimatedButton>();
        }

        private AdvancedProgressBar CreateProgressBar(out Image overlay)
        {
            var host = new GameObject("Bar", typeof(RectTransform));
            _created.Add(host);
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(host.transform, false);
            fill.GetComponent<Image>().type = Image.Type.Filled;
            var overlayObject = new GameObject("Overlay", typeof(RectTransform), typeof(Image));
            overlayObject.transform.SetParent(host.transform, false);
            overlay = overlayObject.GetComponent<Image>();

            var bar = host.AddComponent<AdvancedProgressBar>();
            // Edit mode không gọi Awake — gọi tay để thanh tự tìm Fill / Overlay và chụp cỡ gốc như lúc chạy thật.
            typeof(AdvancedProgressBar).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(bar, null);
            return bar;
        }

        private static void SetField(object target, string name, object value)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (field == null) continue;
                field.SetValue(target, value);
                return;
            }
            Assert.Fail("Không có field " + name + " trên " + target.GetType().Name);
        }

        /// <summary>Handle giả: đang chạy cho tới khi test gọi <see cref="Finish"/>.</summary>
        private sealed class FakeHandle : IAnimationHandle
        {
            public bool WasStopped { get; private set; }
            public bool IsPlaying { get; private set; } = true;
            public bool IsCompleted { get; private set; }

            public void Finish()
            {
                IsPlaying = false;
                IsCompleted = true;
            }

            public void Stop()
            {
                WasStopped = true;
                IsPlaying = false;
            }

            public IAnimationHandle OnComplete(Action callback) => this;
        }
    }
}

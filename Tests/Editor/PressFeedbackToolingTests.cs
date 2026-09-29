using System;
using System.Collections.Generic;
using DreamTech.UICore.Animations;
using DreamTech.UICore.Animations.Backends;
using DreamTech.UICore.Animations.Modules;
using DreamTech.UICore.Base;
using DreamTech.UICore.Buttons;
using DreamTech.UICore.Editor.Feedback;
using DreamTech.UICore.Editor.Preview;
using DreamTech.UICore.Feedback;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DreamTech.UICore.Tests
{
    /// <summary>
    /// Phần "chuẩn package" của phản hồi nhấn: phép tính nhịp dùng chung (biểu đồ = nút thật), module Press Scale cho AnimatedButton
    /// (cùng profile → cùng dáng nhún), bảng xem trước đoán đúng thời lượng, bộ kiểm cấu hình và nhún thử trong Edit mode.
    /// </summary>
    public sealed class PressFeedbackToolingTests
    {
        private const float Step = 1f / 120f;

        private readonly List<Object> _created = new List<Object>();
        private ButtonFeedbackProfile _previousDefault;

        [SetUp]
        public void SetUp()
        {
            _previousDefault = ButtonFeedback.DefaultProfile;
            ButtonFeedback.DefaultProfile = null;
            FrameLoop.CancelAll();
        }

        [TearDown]
        public void TearDown()
        {
            ButtonFeedbackPreview.Stop();
            FrameLoop.CancelAll();
            ButtonFeedback.DefaultProfile = _previousDefault;
            foreach (Object created in _created)
            {
                if (created != null) Object.DestroyImmediate(created);
            }
            _created.Clear();
        }

        // ── Phép tính nhịp ────────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void SampleTap_MatchesTheRealComponentFrameByFrame()
        {
            ButtonFeedbackProfile profile = Punchy();
            var samples = new List<Vector2>();
            profile.SampleTap(0.2f, Step, samples);

            ButtonFeedback feedback = CreateFeedback(profile);
            feedback.OnPointerDown(null);
            int releaseStep = Mathf.RoundToInt(0.2f / Step);
            for (int index = 1; index < samples.Count; index++)
            {
                if (index - 1 == releaseStep) feedback.OnPointerUp(null);
                feedback.Advance(Step);
                Assert.AreEqual(samples[index].y, feedback.CurrentFactor, 0.0001f, "Khác nhau ở bước " + index);
            }
        }

        [Test]
        public void PunchyShape_PeaksAt1_12_Around70msAfterRelease()
        {
            ButtonFeedbackProfile.TapShape shape = Punchy().SampleTap(0.3f, Step);

            Assert.AreEqual(0.8f, shape.Lowest, 0.0001f);
            Assert.IsTrue(shape.Overshoots);
            Assert.AreEqual(1.12f, shape.Peak, 0.002f);
            Assert.AreEqual(0.07f, shape.PeakAfterRelease, 0.01f);
            Assert.AreEqual(0.2f, shape.EndTime - shape.ReleaseTime, 0.01f);
        }

        [Test]
        public void QuickTap_IsReleasedBeforeReachingThePressedScale()
        {
            ButtonFeedbackProfile.TapShape shape = Punchy().SampleTap(0.03f, Step);

            Assert.Greater(shape.Lowest, 0.8f + 0.01f);
            Assert.Less(shape.Lowest, 1f);
        }

        [Test]
        public void FollowShape_NeverOvershoots()
        {
            var profile = ScriptableObject.CreateInstance<ButtonFeedbackProfile>().Configure(PressMotion.Follow, 0.92f, followSpeed: 18f);
            _created.Add(profile);

            ButtonFeedbackProfile.TapShape shape = profile.SampleTap(0.4f, Step);
            Assert.IsFalse(shape.Overshoots);
            Assert.AreEqual(0.92f, shape.Lowest, 0.001f);
        }

        // ── Module Press Scale ────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void PressScaleModule_UsesThePressAndReleaseTimingOfTheProfile()
        {
            ButtonFeedbackProfile profile = Punchy();
            var module = new PressScaleModule();
            module.SetProfile(profile);
            AnimatedButton host = CreateAnimatedButton();
            host.transform.localScale = new Vector3(2f, 2f, 1f);
            module.CaptureInitialValue(host);
            var backend = new RecordingBackend();

            module.Play(host, UIState.Pressed, backend);
            Assert.AreEqual(new Vector3(1.6f, 1.6f, 0.8f), backend.LastTo, "Co theo cỡ gốc (nhân), không về cỡ tuyệt đối.");
            Assert.AreEqual(0.12f, backend.LastDuration, 0.0001f);
            Assert.AreSame(profile.PressCurve, backend.LastCurve);

            module.Play(host, UIState.Normal, backend);
            Assert.AreEqual(new Vector3(2f, 2f, 1f), backend.LastTo);
            Assert.AreEqual(0.2f, backend.LastDuration, 0.0001f);
            Assert.AreSame(profile.ReleaseCurve, backend.LastCurve);

            Assert.AreEqual(0.12f, module.GetDuration(UIState.Pressed), 0.0001f);
            Assert.AreEqual(0.2f, module.GetDuration(UIState.Hover), 0.0001f);
        }

        [Test]
        public void PressScaleModule_RunsOnRealTime_WhenTheProfileSaysSo()
        {
            ButtonFeedbackProfile profile = Punchy();
            var module = new PressScaleModule();
            module.SetProfile(profile);
            AnimatedButton host = CreateAnimatedButton();
            module.CaptureInitialValue(host);

            module.Play(host, UIState.Pressed, new DefaultAnimationBackend { TimeMode = AnimationTimeMode.Scaled });
            // Game đang dừng (timeScale 0): chỉ đồng hồ thật chạy.
            for (int frame = 0; frame < 20; frame++) FrameLoop.TickAll(0f, 0.01f);

            Assert.AreEqual(0.8f, host.transform.localScale.x, 0.0001f);
        }

        [Test]
        public void AnimatedButtonWithPressScale_SqueezesLikeButtonFeedback()
        {
            ButtonFeedbackProfile profile = Punchy();

            var module = new PressScaleModule();
            module.SetProfile(profile);
            AnimatedButton host = CreateAnimatedButton();
            module.CaptureInitialValue(host);
            var backend = new DefaultAnimationBackend { TimeMode = AnimationTimeMode.Unscaled };
            module.Play(host, UIState.Pressed, backend);
            for (int frame = 0; frame < 30; frame++) FrameLoop.TickAll(Step, Step);
            float modulePressed = host.transform.localScale.x;
            module.Play(host, UIState.Normal, backend);
            float modulePeak = 0f;
            for (int frame = 0; frame < 40; frame++)
            {
                FrameLoop.TickAll(Step, Step);
                modulePeak = Mathf.Max(modulePeak, host.transform.localScale.x);
            }

            ButtonFeedbackProfile.TapShape feedbackShape = profile.SampleTap(30 * Step, Step);

            Assert.AreEqual(feedbackShape.Lowest, modulePressed, 0.0001f);
            Assert.AreEqual(feedbackShape.Peak, modulePeak, 0.01f);
            Assert.AreEqual(1f, host.transform.localScale.x, 0.0001f);
        }

        // ── Bảng xem trước ────────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void PreviewDurationEstimate_AsksTheModuleFirst_AndSurvivesMixedModuleTypes()
        {
            var press = new PressScaleModule();
            press.SetProfile(Punchy());

            float scale = PreviewSession.EstimateModuleDuration(new ScaleModule(), UIState.Pressed);
            float pressDuration = PreviewSession.EstimateModuleDuration(press, UIState.Pressed);
            float releaseDuration = PreviewSession.EstimateModuleDuration(press, UIState.Normal);
            float custom = PreviewSession.EstimateModuleDuration(new CustomDurationModule(), UIState.Pressed);
            float scaleAgain = PreviewSession.EstimateModuleDuration(new ScaleModule(), UIState.Normal);

            Assert.AreEqual(0.2f, scale, 0.0001f);
            Assert.AreEqual(0.12f, pressDuration, 0.0001f);
            Assert.AreEqual(0.2f, releaseDuration, 0.0001f);
            Assert.AreEqual(0.75f, custom, 0.0001f);
            Assert.AreEqual(0.2f, scaleAgain, 0.0001f);
        }

        // ── Kiểm cấu hình ─────────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void Validator_FlagsANonTouchableButton_AndItsFixWorks()
        {
            ButtonFeedback feedback = CreateFeedback(Punchy());
            var image = feedback.GetComponent<Image>();
            image.raycastTarget = false;

            ButtonFeedbackValidator.Issue issue = Find(ButtonFeedbackValidator.Validate(feedback), ButtonFeedbackValidator.Severity.Error);
            Assert.IsNotNull(issue);
            Assert.IsNotNull(issue.Fix);
            issue.Fix();

            Assert.IsTrue(image.raycastTarget);
            Assert.IsNull(Find(ButtonFeedbackValidator.Validate(feedback), ButtonFeedbackValidator.Severity.Error));
        }

        [Test]
        public void Validator_WarnsAboutACompetingTransition_OnlyWhenTheProfileDoesNotTakeOver()
        {
            ButtonFeedbackProfile keeps = Punchy().Configure(takeOverTransition: false);
            ButtonFeedback feedback = CreateFeedback(keeps);
            feedback.GetComponent<Button>().transition = Selectable.Transition.ColorTint;

            Assert.IsNotNull(Find(ButtonFeedbackValidator.Validate(feedback), ButtonFeedbackValidator.Severity.Warning));

            keeps.Configure(takeOverTransition: true);
            Assert.IsNull(Find(ButtonFeedbackValidator.Validate(feedback), ButtonFeedbackValidator.Severity.Warning));
        }

        [Test]
        public void Validator_FlagsTargetModeWithoutATarget()
        {
            ButtonFeedback feedback = CreateFeedback(Punchy());
            feedback.SetScaleMode(ButtonFeedback.ScaleMode.Target);

            Assert.IsNotNull(Find(ButtonFeedbackValidator.Validate(feedback), ButtonFeedbackValidator.Severity.Error));
        }

        [Test]
        public void Validator_WarnsWhenAnAnimatedButtonAlsoScalesTheSameObject()
        {
            ButtonFeedback feedback = CreateFeedback(Punchy());
            var animated = feedback.gameObject.AddComponent<AnimatedButton>();
            animated.AddModule(new PressScaleModule());

            Assert.IsNotNull(Find(ButtonFeedbackValidator.Validate(feedback), ButtonFeedbackValidator.Severity.Warning));
        }

        // ── Nhún thử trong Edit mode ──────────────────────────────────────────────────────────────────────────

        [Test]
        public void EditModePreview_PutsEverythingBack()
        {
            ButtonFeedback feedback = CreateFeedback(Punchy());
            feedback.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
            var image = feedback.GetComponent<Image>();
            Vector4 padding = image.raycastPadding;

            Assume.That(ButtonFeedbackPreview.CanPreview(feedback, out string reason), Is.True, reason);
            ButtonFeedbackPreview.Press(feedback);
            feedback.Advance(0.2f);
            Assert.AreEqual(1.5f * 0.8f, feedback.transform.localScale.x, 0.0001f, "Đang nhấn thì phải co thật.");
            Assert.AreNotEqual(padding, image.raycastPadding, "Đang nhấn thì vùng chạm được nới.");

            ButtonFeedbackPreview.Stop();

            Assert.AreEqual(new Vector3(1.5f, 1.5f, 1f), feedback.transform.localScale);
            Assert.AreEqual(padding, image.raycastPadding);
            var serialized = new UnityEditor.SerializedObject(feedback);
            Assert.IsNull(serialized.FindProperty("_selectable").objectReferenceValue, "Ô tham chiếu tự điền lúc xem phải được trả lại.");
            Assert.IsNull(serialized.FindProperty("_hitGraphic").objectReferenceValue);
            Assert.IsFalse(feedback.IsEditorPreviewing);
        }

        // ── Trợ giúp ──────────────────────────────────────────────────────────────────────────────────────────

        private ButtonFeedbackProfile Punchy()
        {
            var profile = ScriptableObject.CreateInstance<ButtonFeedbackProfile>().Configure(
                PressMotion.Timed, 0.8f, 0.12f, 0.2f, AnimationCurve.Linear(0f, 0f, 1f, 1f),
                new AnimationCurve(new Keyframe(0f, 0.005f, 0f, 0f), new Keyframe(0.35f, 1.6f, 0f, 0f), new Keyframe(1f, 1f, 0f, 0f)),
                useUnscaledTime: true, clickCueKey: string.Empty);
            _created.Add(profile);
            return profile;
        }

        private ButtonFeedback CreateFeedback(ButtonFeedbackProfile profile)
        {
            var host = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            _created.Add(host);
            host.GetComponent<Button>().targetGraphic = host.GetComponent<Image>();
            var feedback = host.AddComponent<ButtonFeedback>();
            var serialized = new UnityEditor.SerializedObject(feedback);
            serialized.FindProperty("_profile").objectReferenceValue = profile;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return feedback;
        }

        private AnimatedButton CreateAnimatedButton()
        {
            var host = new GameObject("Animated Button", typeof(RectTransform), typeof(Image));
            _created.Add(host);
            return host.AddComponent<AnimatedButton>();
        }

        private static ButtonFeedbackValidator.Issue Find(List<ButtonFeedbackValidator.Issue> issues, ButtonFeedbackValidator.Severity severity)
        {
            foreach (ButtonFeedbackValidator.Issue issue in issues)
            {
                if (issue.Severity == severity) return issue;
            }
            return null;
        }

        /// <summary>Module lạ có field <c>duration</c> riêng — trước đây làm bảng xem trước ném ArgumentException.</summary>
        [Serializable]
        private sealed class CustomDurationModule : IAnimationModule
        {
            [SerializeField] private float duration = 0.75f;

            public string DisplayName => "Custom";
            public bool Enabled => duration > 0f;
            public void CaptureInitialValue(MonoBehaviour target) { }
            public IAnimationHandle Play(MonoBehaviour target, UIState newState, IAnimationBackend backend) => null;
        }

        /// <summary>Backend ghi lại lần tween cuối để kiểm số truyền vào.</summary>
        private sealed class RecordingBackend : IAnimationBackend
        {
            public Vector3 LastTo;
            public float LastDuration;
            public AnimationCurve LastCurve;

            public IAnimationHandle TweenVector3(MonoBehaviour host, Vector3 from, Vector3 to, float duration, Action<Vector3> onUpdate,
                                                 AnimationCurve curve = null, Action onStart = null, Action<float> onStep = null,
                                                 Action onComplete = null)
            {
                LastTo = to;
                LastDuration = duration;
                LastCurve = curve;
                return null;
            }

            public IAnimationHandle TweenFloat(MonoBehaviour host, float from, float to, float duration, Action<float> onUpdate,
                                               AnimationCurve curve = null, Action onStart = null, Action<float> onStep = null,
                                               Action onComplete = null) => null;

            public IAnimationHandle TweenColor(MonoBehaviour host, Color from, Color to, float duration, Action<Color> onUpdate,
                                               AnimationCurve curve = null, Action onStart = null, Action<float> onStep = null,
                                               Action onComplete = null) => null;

            public IAnimationHandle Punch(MonoBehaviour host, Transform target, Vector3 punchAmount, float duration, int vibrato = 10,
                                          float elasticity = 1f, Action onComplete = null) => null;

            public IAnimationHandle Shake(MonoBehaviour host, Transform target, float strength, float duration, int vibrato = 10,
                                          float randomness = 90f, Action onComplete = null) => null;
        }
    }
}

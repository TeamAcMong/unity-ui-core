using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using DreamTech.UICore.Feedback;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DreamTech.UICore.Tests
{
    /// <summary>
    /// Hành vi của <see cref="ButtonFeedback"/>, chạy bằng <see cref="ButtonFeedback.Advance"/> (không cần Play mode): nhịp nhấn/nhả,
    /// chạm nhanh, vùng chạm, nút trong ScrollRect, nút bị khoá, điểm nối âm/rung, gắn hàng loạt.
    /// </summary>
    public sealed class ButtonFeedbackTests
    {
        private const float Tolerance = 0.0001f;

        private readonly List<Object> _created = new List<Object>();
        private Action<ButtonFeedback, string> _previousHandler;
        private ButtonFeedbackProfile _previousDefault;

        [SetUp]
        public void SetUp()
        {
            _previousHandler = ButtonFeedbackCues.Handler;
            _previousDefault = ButtonFeedback.DefaultProfile;
            ButtonFeedbackCues.Handler = null;
            ButtonFeedback.DefaultProfile = null;
        }

        [TearDown]
        public void TearDown()
        {
            ButtonFeedbackCues.Handler = _previousHandler;
            ButtonFeedback.DefaultProfile = _previousDefault;
            foreach (Object created in _created)
            {
                if (created != null) Object.DestroyImmediate(created);
            }
            _created.Clear();
        }

        // ── Nhịp ───────────────────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void Press_ReachesPressedScale_ThenReleaseOvershootsAndSettles()
        {
            ButtonFeedback feedback = CreateButton(Profile(pressedScale: 0.8f, pressDuration: 0.12f, releaseDuration: 0.2f), out _);

            feedback.OnPointerDown(Pointer());
            feedback.Advance(0.12f);
            Assert.AreEqual(0.8f, feedback.CurrentFactor, Tolerance);
            Assert.AreEqual(0.8f, feedback.transform.localScale.x, Tolerance);

            feedback.OnPointerUp(Pointer());
            float peak = 0f;
            for (int step = 0; step < 20; step++)
            {
                feedback.Advance(0.01f);
                peak = Mathf.Max(peak, feedback.CurrentFactor);
            }

            Assert.Greater(peak, 1f, "Đường nhả mặc định phải vọt quá cỡ gốc.");
            Assert.AreEqual(1f, feedback.CurrentFactor, Tolerance);
            Assert.AreEqual(Vector3.one, feedback.transform.localScale);
            Assert.IsFalse(feedback.IsPressed);
        }

        [Test]
        public void QuickTap_ReleasesFromThePartialScale_WithoutSnappingBack()
        {
            ButtonFeedback feedback = CreateButton(Profile(pressedScale: 0.8f, pressDuration: 0.12f, releaseDuration: 0.2f), out _);

            feedback.OnPointerDown(Pointer());
            feedback.Advance(0.03f);
            float partial = feedback.CurrentFactor;
            Assert.Less(partial, 1f);

            feedback.OnPointerUp(Pointer());
            feedback.Advance(0.0001f);
            Assert.AreEqual(partial, feedback.CurrentFactor, 0.01f, "Nhả phải đi tiếp từ cỡ đang co dở, không giật về 1.");
        }

        [Test]
        public void FollowMotion_ConvergesWithoutOvershoot_AndReturns()
        {
            ButtonFeedback feedback = CreateButton(Profile(motion: PressMotion.Follow, pressedScale: 0.92f, followSpeed: 20f), out _);

            feedback.OnPointerDown(Pointer());
            float lowest = 1f;
            for (int step = 0; step < 120; step++)
            {
                feedback.Advance(1f / 60f);
                lowest = Mathf.Min(lowest, feedback.CurrentFactor);
            }
            Assert.AreEqual(0.92f, feedback.CurrentFactor, Tolerance);
            Assert.GreaterOrEqual(lowest, 0.92f - Tolerance, "Follow không được vượt quá cỡ đích.");

            feedback.OnPointerUp(Pointer());
            for (int step = 0; step < 120; step++) feedback.Advance(1f / 60f);
            Assert.AreEqual(1f, feedback.CurrentFactor, Tolerance);
        }

        [Test]
        public void PointerExit_WhilePressed_Releases()
        {
            ButtonFeedback feedback = CreateButton(Profile(pressedScale: 0.8f), out _);
            feedback.OnPointerDown(Pointer());
            feedback.Advance(0.5f);

            feedback.OnPointerExit(Pointer());
            for (int step = 0; step < 60; step++) feedback.Advance(0.01f);

            Assert.AreEqual(1f, feedback.CurrentFactor, Tolerance);
        }

        [Test]
        public void KeepsABakedRestScale()
        {
            ButtonFeedback feedback = CreateButton(Profile(pressedScale: 0.5f, pressDuration: 0.1f), out _);
            feedback.transform.localScale = new Vector3(2f, 3f, 1f);

            feedback.OnPointerDown(Pointer());
            feedback.Advance(0.1f);
            Assert.AreEqual(new Vector3(1f, 1.5f, 0.5f), feedback.transform.localScale);

            feedback.OnPointerUp(Pointer());
            for (int step = 0; step < 60; step++) feedback.Advance(0.01f);
            Assert.AreEqual(new Vector3(2f, 3f, 1f), feedback.transform.localScale);
        }

        [Test]
        public void ExternalScale_MultipliesThePressFactor()
        {
            ButtonFeedback feedback = CreateButton(Profile(pressedScale: 0.8f, pressDuration: 0.1f), out _);
            feedback.SetExternalScale(1.5f);
            Assert.AreEqual(1.5f, feedback.transform.localScale.x, Tolerance);

            feedback.OnPointerDown(Pointer());
            feedback.Advance(0.1f);
            Assert.AreEqual(1.2f, feedback.transform.localScale.x, Tolerance);

            feedback.SetExternalScale(1f);
            Assert.AreEqual(0.8f, feedback.transform.localScale.x, Tolerance);
        }

        [Test]
        public void Disable_MidPress_ResetsScaleAndHitArea()
        {
            ButtonFeedback feedback = CreateButton(Profile(pressedScale: 0.8f, pressDuration: 0.1f), out Image image);
            feedback.OnPointerDown(Pointer());
            feedback.Advance(0.05f);

            typeof(ButtonFeedback).GetMethod("OnDisable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(feedback, null);

            Assert.AreEqual(Vector3.one, feedback.transform.localScale);
            Assert.AreEqual(Vector4.zero, image.raycastPadding);
            Assert.IsFalse(feedback.IsPressed);
        }

        // ── Vùng chạm, chế độ co ───────────────────────────────────────────────────────────────────────────────

        [Test]
        public void HitArea_KeepsItsUnpressedSize_WhilePressed()
        {
            ButtonFeedback feedback = CreateButton(Profile(pressedScale: 0.8f), out Image image);
            image.rectTransform.sizeDelta = new Vector2(200f, 100f);

            feedback.OnPointerDown(Pointer());
            // Co còn 0,8 → cần nới 1/0,8 − 1 = 0,25 cỡ, chia đều hai phía.
            AssertApproximately(new Vector4(-25f, -12.5f, -25f, -12.5f), image.raycastPadding);

            feedback.OnPointerUp(Pointer());
            Assert.AreEqual(Vector4.zero, image.raycastPadding);
        }

        [Test]
        public void ChildrenMode_ScalesChildrenOnly_AndSkipsExcludedOnes()
        {
            ButtonFeedback feedback = CreateButton(Profile(pressedScale: 0.5f, pressDuration: 0.1f, excludeSuffix: "_still"), out Image image);
            Transform face = CreateChild(feedback.transform, "Face");
            Transform frame = CreateChild(feedback.transform, "Frame_still");
            feedback.SetScaleMode(ButtonFeedback.ScaleMode.Children);

            feedback.OnPointerDown(Pointer());
            feedback.Advance(0.1f);

            Assert.AreEqual(Vector3.one, feedback.transform.localScale, "Gốc (vùng chạm) phải đứng yên.");
            Assert.AreEqual(new Vector3(0.5f, 0.5f, 0.5f), face.localScale);
            Assert.AreEqual(Vector3.one, frame.localScale);
            Assert.AreEqual(Vector4.zero, image.raycastPadding, "Không co gốc thì không cần nới vùng chạm.");
        }

        [Test]
        public void TargetMode_ScalesTheTarget()
        {
            ButtonFeedback feedback = CreateButton(Profile(pressedScale: 0.5f, pressDuration: 0.1f), out _);
            var target = (RectTransform)CreateChild(feedback.transform, "Art");
            feedback.SetScaleMode(ButtonFeedback.ScaleMode.Target, target);

            feedback.OnPointerDown(Pointer());
            feedback.Advance(0.1f);

            Assert.AreEqual(Vector3.one, feedback.transform.localScale);
            Assert.AreEqual(new Vector3(0.5f, 0.5f, 0.5f), target.localScale);
        }

        // ── Nút bị khoá ────────────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void NonInteractableButton_NeitherPressesNorCues()
        {
            ButtonFeedback feedback = CreateButton(Profile(), out _);
            feedback.GetComponent<Button>().interactable = false;
            int cues = 0;
            ButtonFeedbackCues.Handler = (source, key) => cues++;

            feedback.OnPointerDown(Pointer());
            feedback.Advance(0.5f);
            feedback.OnPointerClick(Pointer());

            Assert.AreEqual(1f, feedback.CurrentFactor, Tolerance);
            Assert.AreEqual(0, cues);
        }

        // ── ScrollRect ─────────────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void InsideScrollRect_PressWaitsForTheDelay()
        {
            ButtonFeedback feedback = CreateButtonInScroll(Profile(pressedScale: 0.8f, pressDuration: 0.1f, scrollPressDelay: 0.06f));

            feedback.OnPointerDown(Pointer());
            Assert.IsTrue(feedback.IsPressPending);
            feedback.Advance(0.05f);
            Assert.AreEqual(1f, feedback.CurrentFactor, Tolerance);
            Assert.IsFalse(feedback.IsPressed);

            feedback.Advance(0.02f);
            Assert.IsTrue(feedback.IsPressed);
        }

        [Test]
        public void InsideScrollRect_DraggingDuringTheDelay_CancelsThePress()
        {
            ButtonFeedback feedback = CreateButtonInScroll(Profile(scrollPressDelay: 0.06f));
            PointerEventData pointer = Pointer();

            feedback.OnPointerDown(pointer);
            pointer.position += new Vector2(0f, 40f); // ngón tay kéo để cuộn
            feedback.Advance(0.1f);

            Assert.IsFalse(feedback.IsPressPending);
            Assert.IsFalse(feedback.IsPressed);
            Assert.AreEqual(1f, feedback.CurrentFactor, Tolerance);
        }

        [Test]
        public void InsideScrollRect_DraggingAfterThePressStarted_Releases()
        {
            ButtonFeedback feedback = CreateButtonInScroll(Profile(pressedScale: 0.8f, pressDuration: 0.1f, scrollPressDelay: 0.06f));
            PointerEventData pointer = Pointer();

            feedback.OnPointerDown(pointer);
            feedback.Advance(0.2f);
            Assert.IsTrue(feedback.IsPressed);

            pointer.position += new Vector2(0f, 40f); // giữ nút một lúc rồi mới kéo để cuộn
            feedback.Advance(0.01f);
            Assert.IsFalse(feedback.IsPressed, "Thành cú cuộn thì nút phải nhả, không chờ tới lúc nhấc tay.");
            for (int step = 0; step < 60; step++) feedback.Advance(0.01f);
            Assert.AreEqual(1f, feedback.CurrentFactor, Tolerance);
        }

        [Test]
        public void InsideScrollRect_QuickTap_StillBounces()
        {
            ButtonFeedback feedback = CreateButtonInScroll(Profile(pressedScale: 0.8f, pressDuration: 0.1f, releaseDuration: 0.2f));

            feedback.OnPointerDown(Pointer());
            feedback.OnPointerUp(Pointer()); // nhả trước khi hết thời gian chờ, không kéo
            float lowest = 1f;
            for (int step = 0; step < 60; step++)
            {
                feedback.Advance(0.01f);
                lowest = Mathf.Min(lowest, feedback.CurrentFactor);
            }

            Assert.AreEqual(0.8f, lowest, 0.001f, "Chạm nhanh vẫn phải co hết một nhịp rồi mới nhả.");
            Assert.AreEqual(1f, feedback.CurrentFactor, Tolerance);
        }

        [Test]
        public void InsideAFastScrollingList_TouchStopsTheListWithoutPressing()
        {
            ButtonFeedback feedback = CreateButtonInScroll(Profile(scrollVelocityThreshold: 10f));
            feedback.GetComponentInParent<ScrollRect>().velocity = new Vector2(0f, 500f);

            feedback.OnPointerDown(Pointer());
            feedback.Advance(0.5f);

            Assert.IsFalse(feedback.IsPressPending);
            Assert.AreEqual(1f, feedback.CurrentFactor, Tolerance);
        }

        // ── Âm / rung ──────────────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void Click_SendsTheProfileCueKey_OrTheOverride_OrNothingWhenMuted()
        {
            ButtonFeedback feedback = CreateButton(Profile(clickCueKey: "tap"), out _);
            var received = new List<string>();
            ButtonFeedbackCues.Handler = (source, key) =>
            {
                Assert.AreSame(feedback, source);
                received.Add(key);
            };

            feedback.OnPointerClick(Pointer());
            feedback.SetCue("chest");
            feedback.OnPointerClick(Pointer());
            feedback.SetCue(null, mute: true);
            feedback.OnPointerClick(Pointer());

            CollectionAssert.AreEqual(new[] { "tap", "chest" }, received);
        }

        [Test]
        public void AThrowingCueHandler_IsLogged_NotPropagated()
        {
            ButtonFeedback feedback = CreateButton(Profile(), out _);
            ButtonFeedbackCues.Handler = (source, key) => throw new InvalidOperationException("boom");
            LogAssert.Expect(LogType.Exception, new Regex("boom"));

            Assert.DoesNotThrow(() => feedback.OnPointerClick(Pointer()));
        }

        [Test]
        public void DefaultProfile_IsUsed_WhenTheButtonHasNone()
        {
            ButtonFeedback.DefaultProfile = Profile(pressedScale: 0.6f, pressDuration: 0.1f);
            ButtonFeedback feedback = CreateButton(null, out _);

            feedback.OnPointerDown(Pointer());
            feedback.Advance(0.1f);

            Assert.AreEqual(0.6f, feedback.CurrentFactor, Tolerance);
        }

        // ── Gắn hàng loạt, tiếp quản nút cũ ─────────────────────────────────────────────────────────────────────

        [Test]
        public void RegisterAll_AddsToButtonsAndToggles_SkippingExcludedAndExisting()
        {
            var root = new GameObject("Root", typeof(RectTransform));
            _created.Add(root);
            AddSelectable<Button>(root.transform, "Play");
            AddSelectable<Toggle>(root.transform, "Sound");
            AddSelectable<Button>(root.transform, "Tab_noFeedback");
            Button existing = AddSelectable<Button>(root.transform, "Close");
            existing.gameObject.AddComponent<ButtonFeedback>();
            AddSelectable<Slider>(root.transform, "Volume");

            int added = ButtonFeedback.RegisterAll(root.transform);

            Assert.AreEqual(2, added);
            Assert.IsNotNull(root.transform.Find("Play").GetComponent<ButtonFeedback>());
            Assert.IsNotNull(root.transform.Find("Sound").GetComponent<ButtonFeedback>());
            Assert.IsNull(root.transform.Find("Tab_noFeedback").GetComponent<ButtonFeedback>());
            Assert.IsNull(root.transform.Find("Volume").GetComponent<ButtonFeedback>());
        }

        [Test]
        public void SetProfile_TakesOverTheButtonTransitionAndAnimator()
        {
            var host = new GameObject("Kit Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Animator));
            _created.Add(host);
            var button = host.GetComponent<Button>();
            button.transition = Selectable.Transition.Animation;

            host.AddComponent<ButtonFeedback>().SetProfile(Profile(takeOverTransition: true));

            Assert.AreEqual(Selectable.Transition.None, button.transition);
            Assert.IsFalse(host.GetComponent<Animator>().enabled);
        }

        // ── Dựng ───────────────────────────────────────────────────────────────────────────────────────────────

        private ButtonFeedbackProfile Profile(PressMotion motion = PressMotion.Timed, float pressedScale = 0.9f, float pressDuration = 0.1f,
                                              float releaseDuration = 0.2f, float followSpeed = 20f, float scrollPressDelay = 0.06f,
                                              float scrollVelocityThreshold = 10f, bool takeOverTransition = false,
                                              string clickCueKey = "click", string excludeSuffix = "_noFeedback")
        {
            var profile = ScriptableObject.CreateInstance<ButtonFeedbackProfile>();
            _created.Add(profile);
            return profile.Configure(motion, pressedScale, pressDuration, releaseDuration, followSpeed: followSpeed,
                                     scrollPressDelay: scrollPressDelay, scrollVelocityThreshold: scrollVelocityThreshold,
                                     takeOverTransition: takeOverTransition, clickCueKey: clickCueKey, excludeSuffix: excludeSuffix);
        }

        private ButtonFeedback CreateButton(ButtonFeedbackProfile profile, out Image image)
        {
            var host = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            _created.Add(host);
            image = host.GetComponent<Image>();
            host.GetComponent<Button>().targetGraphic = image;
            var feedback = host.AddComponent<ButtonFeedback>();
            if (profile != null) feedback.SetProfile(profile);
            return feedback;
        }

        private ButtonFeedback CreateButtonInScroll(ButtonFeedbackProfile profile)
        {
            var scrollHost = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
            _created.Add(scrollHost);
            ButtonFeedback feedback = CreateButton(profile, out _);
            feedback.transform.SetParent(scrollHost.transform, false);
            return feedback;
        }

        private Transform CreateChild(Transform parent, string name)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static T AddSelectable<T>(Transform parent, string name) where T : Selectable
        {
            var host = new GameObject(name, typeof(RectTransform));
            host.transform.SetParent(parent, false);
            return host.AddComponent<T>();
        }

        private static void AssertApproximately(Vector4 expected, Vector4 actual)
        {
            for (int axis = 0; axis < 4; axis++) Assert.AreEqual(expected[axis], actual[axis], Tolerance, "trục " + axis);
        }

        private static PointerEventData Pointer()
        {
            return new PointerEventData(null) { button = PointerEventData.InputButton.Left, position = new Vector2(100f, 100f) };
        }
    }
}

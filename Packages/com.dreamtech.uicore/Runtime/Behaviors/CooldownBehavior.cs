using System;
using DreamTech.UICore.Animations;
using DreamTech.UICore.Base;
using DreamTech.UICore.Buttons;  // CooldownOverlay
using UnityEngine;
using UnityEngine.Events;

namespace DreamTech.UICore.Behaviors
{
    public enum CooldownBehaviorType
    {
        TimeBased,
        ChargeBased,
    }

    /// <summary>
    /// Gate click bằng cooldown. Áp dụng cho mọi <see cref="InteractiveUIComponent"/> (Button, Toggle, ...).
    /// <para>
    /// Hai mode:
    /// <list type="bullet">
    /// <item><b>TimeBased</b>: Mỗi click trigger cooldown <paramref name="cooldownDuration"/> giây.</item>
    /// <item><b>ChargeBased</b>: Có <paramref name="maxCharges"/> charge, mỗi click tốn 1, recovery <paramref name="chargeRecoveryTime"/>s/charge.</item>
    /// </list>
    /// </para>
    /// Đếm giờ chạy trên vòng Update của package (<see cref="FrameLoop"/>), tự dừng khi host bị destroy.
    /// </summary>
    [Serializable]
    public class CooldownBehavior : BehaviorModuleBase
    {
        [SerializeField] private CooldownBehaviorType cooldownType = CooldownBehaviorType.TimeBased;
        [SerializeField, Min(0.1f)] private float cooldownDuration = 3f;
        [SerializeField, Min(1)] private int maxCharges = 3;
        [SerializeField, Min(0.1f)] private float chargeRecoveryTime = 1f;

        [Header("Visual Overlay (optional)")]
        [SerializeField] private CooldownOverlay overlay;

        [Header("Events")]
        public UnityEvent onCooldownStart = new();
        public UnityEvent onCooldownEnd = new();
        public UnityEvent<int> onChargesChanged = new();

        public override string DisplayName => $"Cooldown ({cooldownType})";

        private float _cooldownRemaining;
        private bool _isOnCooldown;
        private int _currentCharges;
        private float _chargeRecoveryRemaining;

        private FrameLoopHandle _timeCooldownLoop;
        private FrameLoopHandle _chargeRecoveryLoop;

        public bool IsReady => cooldownType == CooldownBehaviorType.TimeBased ? !_isOnCooldown : _currentCharges > 0;
        public int CurrentCharges => _currentCharges;
        public float Progress01 => cooldownDuration > 0 ? 1f - (_cooldownRemaining / cooldownDuration) : 1f;

        public override void Initialize(InteractiveUIComponent host)
        {
            base.Initialize(host);
            _currentCharges = maxCharges;
            if (overlay != null) overlay.SetProgress(1f);
        }

        public override void Dispose()
        {
            CancelTimeCooldown();
            CancelChargeRecovery();
        }

        public override bool OnBeforeClick()
        {
            // Khi disabled, không gate (cho phép click bình thường).
            if (!enabled) return true;
            return IsReady;
        }

        public override void OnAfterClick()
        {
            if (!enabled) return;
            if (cooldownType == CooldownBehaviorType.TimeBased)
                StartCooldown();
            else
                ConsumeCharge();
        }

        public override void OnPointerStateChanged(UIState newState)
        {
            // Animation module + host.SetInteractable đã handle visual.
            // Hook để mở rộng — hiện tại no-op.
        }

        /// <summary>Trigger cooldown ngay lập tức (programmatic, ngoài click flow).</summary>
        public void StartCooldown()
        {
            CancelTimeCooldown();
            _isOnCooldown = true;
            _cooldownRemaining = cooldownDuration;
            onCooldownStart?.Invoke();
            if (host != null) host.SetInteractable(false);

            _timeCooldownLoop = FrameLoop.Run(host, TickTimeCooldown);
        }

        /// <summary>Reset toàn bộ state: cancel cooldown/recovery, restore charges, re-enable host.</summary>
        public void ResetCooldown()
        {
            CancelTimeCooldown();
            CancelChargeRecovery();
            _isOnCooldown = false;
            _cooldownRemaining = 0f;
            _currentCharges = maxCharges;
            _chargeRecoveryRemaining = 0f;
            if (overlay != null) overlay.SetProgress(1f);
            onChargesChanged?.Invoke(_currentCharges);
            if (host != null) host.SetInteractable(true);
        }

        /// <summary>Giảm cooldown remaining (booster effect).</summary>
        public void ReduceCooldown(float seconds)
        {
            if (!_isOnCooldown) return;
            _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - seconds);
        }

        private void ConsumeCharge()
        {
            _currentCharges--;
            onChargesChanged?.Invoke(_currentCharges);

            // Start recovery loop nếu chưa chạy
            if (_chargeRecoveryLoop == null || !_chargeRecoveryLoop.IsRunning)
            {
                _chargeRecoveryRemaining = chargeRecoveryTime;
                CancelChargeRecovery();
                _chargeRecoveryLoop = FrameLoop.Run(host, TickChargeRecovery);
            }

            if (_currentCharges <= 0 && host != null)
                host.SetInteractable(false);
        }

        /// <summary>Một khung của cooldown theo thời gian: trừ giờ, cập nhật overlay, hết giờ thì mở lại nút.</summary>
        private bool TickTimeCooldown(float deltaTime, float unscaledDeltaTime)
        {
            _cooldownRemaining -= deltaTime;
            if (overlay != null) overlay.SetProgress(Progress01);
            if (_cooldownRemaining > 0) return true;

            _cooldownRemaining = 0f;
            _isOnCooldown = false;
            if (overlay != null) overlay.SetProgress(1f);
            onCooldownEnd?.Invoke();
            if (host != null) host.SetInteractable(true);
            return false;
        }

        /// <summary>Một khung của hồi charge: đủ giờ thì cộng một charge; đầy thì dừng.</summary>
        private bool TickChargeRecovery(float deltaTime, float unscaledDeltaTime)
        {
            if (_currentCharges >= maxCharges) return false;

            _chargeRecoveryRemaining -= deltaTime;
            if (overlay != null)
                overlay.SetProgress(1f - (_chargeRecoveryRemaining / chargeRecoveryTime));
            if (_chargeRecoveryRemaining > 0) return true;

            _currentCharges++;
            onChargesChanged?.Invoke(_currentCharges);
            if (_currentCharges < maxCharges)
            {
                _chargeRecoveryRemaining = chargeRecoveryTime;
            }
            else if (overlay != null)
            {
                overlay.SetProgress(1f);
            }
            // vừa từ 0 → 1: re-enable host
            if (_currentCharges == 1 && host != null)
                host.SetInteractable(true);
            return _currentCharges < maxCharges;
        }

        private void CancelTimeCooldown()
        {
            _timeCooldownLoop?.Cancel();
            _timeCooldownLoop = null;
        }

        private void CancelChargeRecovery()
        {
            _chargeRecoveryLoop?.Cancel();
            _chargeRecoveryLoop = null;
        }
    }
}

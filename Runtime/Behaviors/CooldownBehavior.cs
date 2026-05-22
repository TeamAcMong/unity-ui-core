using System;
using System.Threading;
using Cysharp.Threading.Tasks;
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
    /// CTS lifecycle: dispose trong <c>finally</c> của async method (KHÔNG dispose trong CancelXxx —
    /// vì async method còn đang awaiting, dispose sớm có thể throw <see cref="ObjectDisposedException"/>).
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

        private CancellationTokenSource _timeCooldownCts;
        private CancellationTokenSource _chargeRecoveryCts;

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

            _timeCooldownCts = CancellationTokenSource.CreateLinkedTokenSource(
                host != null ? host.GetCancellationTokenOnDestroy() : default);
            RunTimeCooldownAsync(_timeCooldownCts).Forget();
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
            bool needNewLoop = _chargeRecoveryCts == null;
            if (!needNewLoop)
            {
                try { needNewLoop = _chargeRecoveryCts.IsCancellationRequested; }
                catch (ObjectDisposedException) { needNewLoop = true; }
            }
            if (needNewLoop)
            {
                _chargeRecoveryRemaining = chargeRecoveryTime;
                CancelChargeRecovery();
                _chargeRecoveryCts = CancellationTokenSource.CreateLinkedTokenSource(
                    host != null ? host.GetCancellationTokenOnDestroy() : default);
                RunChargeRecoveryAsync(_chargeRecoveryCts).Forget();
            }

            if (_currentCharges <= 0 && host != null)
                host.SetInteractable(false);
        }

        private async UniTaskVoid RunTimeCooldownAsync(CancellationTokenSource cts)
        {
            var ct = cts.Token;
            try
            {
                while (_cooldownRemaining > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    _cooldownRemaining -= Time.deltaTime;
                    if (overlay != null) overlay.SetProgress(Progress01);
                }
                _cooldownRemaining = 0f;
                _isOnCooldown = false;
                if (overlay != null) overlay.SetProgress(1f);
                onCooldownEnd?.Invoke();
                if (host != null) host.SetInteractable(true);
            }
            catch (OperationCanceledException) { /* destroyed or reset */ }
            finally
            {
                cts.Dispose();
                if (ReferenceEquals(_timeCooldownCts, cts)) _timeCooldownCts = null;
            }
        }

        private async UniTaskVoid RunChargeRecoveryAsync(CancellationTokenSource cts)
        {
            var ct = cts.Token;
            try
            {
                while (_currentCharges < maxCharges)
                {
                    while (_chargeRecoveryRemaining > 0)
                    {
                        ct.ThrowIfCancellationRequested();
                        await UniTask.Yield(PlayerLoopTiming.Update, ct);
                        _chargeRecoveryRemaining -= Time.deltaTime;
                        if (overlay != null)
                            overlay.SetProgress(1f - (_chargeRecoveryRemaining / chargeRecoveryTime));
                    }
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
                }
            }
            catch (OperationCanceledException) { /* destroyed or reset */ }
            finally
            {
                cts.Dispose();
                if (ReferenceEquals(_chargeRecoveryCts, cts)) _chargeRecoveryCts = null;
            }
        }

        private void CancelTimeCooldown()
        {
            if (_timeCooldownCts != null)
            {
                try { if (!_timeCooldownCts.IsCancellationRequested) _timeCooldownCts.Cancel(); }
                catch (ObjectDisposedException) { }
                _timeCooldownCts = null;
            }
        }

        private void CancelChargeRecovery()
        {
            if (_chargeRecoveryCts != null)
            {
                try { if (!_chargeRecoveryCts.IsCancellationRequested) _chargeRecoveryCts.Cancel(); }
                catch (ObjectDisposedException) { }
                _chargeRecoveryCts = null;
            }
        }
    }
}

# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.7.1] - 2026-05-22

### Fixed
- **Toggling `interactable` in Inspector during Play mode did not trigger animation** — `[SerializeField] interactable` is edited directly by Inspector, bypassing the public `SetInteractable()` entry point. Fix: `OnValidate()` (editor-only) on `InteractiveUIComponent` detects value changes and defers `ApplyState()` via `EditorApplication.delayCall`.
- Edit mode: still no auto-animation (UniTask backend doesn't tick in edit mode). Use the Preview Panel to test states without entering Play.

### Notes
- `clickCooldown` is anti-spam timing — by design no visual effect.
- Module config edits don't auto-replay animation by design — use Preview Panel.

## [0.7.0] - 2026-05-22

### Fixed

**BLOCKER — ObjectDisposedException in 3 Behaviors:**

- `CooldownBehavior` — `RunTimeCooldownAsync` and `RunChargeRecoveryAsync` disposed their CTS in `finally` but did not self-clear the field. Any subsequent caller accessing `IsCancellationRequested` on the disposed CTS threw `ObjectDisposedException`. Fix: added `ReferenceEquals` guard in each `finally` block to null the field only when the CTS still matches; `CancelTimeCooldown()` and `CancelChargeRecovery()` now wrap `IsCancellationRequested` + `Cancel()` in `try/catch ObjectDisposedException`. `ConsumeCharge()` similarly guards the `_chargeRecoveryCts.IsCancellationRequested` read with a defensive try/catch.

- `LongPressBehavior` — same root cause in `RunDetectionAsync`. Fix: `ReferenceEquals` guard in `finally` self-clears `_detectCts`; `StopDetection()` wraps cancel in `try/catch ObjectDisposedException`.

- `HoldRepeatBehavior` — same root cause in `RunRepeatAsync`. Fix: `ReferenceEquals` guard in `finally` self-clears `_repeatCts`; `StopRepeating()` wraps cancel in `try/catch ObjectDisposedException`.

**HIGH — AdvancedProgressBar Flash phase race:**

- `AdvancedProgressBar.Flash()` — rapid successive calls would orphan the Phase 1 handle: when Phase 1 completed, its `OnComplete` callback would unconditionally overwrite `_flashHandle` with Phase 2, even though a newer `Flash()` call had already replaced it. Fix: capture `phase1` in a local variable before registering `OnComplete`; the callback checks `ReferenceEquals(_flashHandle, phase1)` and returns early if the handle has been superseded. Also added `_flashHandle = null` after the initial `Stop()` to ensure a clean slate.

**HIGH — AnimationSequence stale handle references:**

- `AnimationSequence.MarkComplete()` — completed handles were never removed from `_activeHandles`, holding references to finished animation objects and risking `Stop()` being called on already-completed handles if the sequence is replayed. Fix: added `_activeHandles?.Clear()` at the top of `MarkComplete()`.

**MEDIUM — PreviewAnimationBackend double-restore on StopAll mid-Tick:**

- `PreviewAnimationBackend.StopAll()` called `RestoreTarget` for every active tween, then when `_isTicking` was true the deferred Tick loop would hit the `IsCancelled` branch and call `RestoreTarget` a second time on the same tweens. Fix: when `_isTicking`, all active tweens are added to `_pendingRemoval` so the Tick loop's `IsCancelled` branch skips re-restoring tweens already handled by `StopAll`.

**MEDIUM — AnimationSequence foreach mutation risk:**

- `AnimationSequence.RunSequential()` and `RunParallel()` iterated `_steps` directly. A completion callback calling `Append()` mid-iteration would cause `InvalidOperationException`. Fix: both methods take a `List<Func<IAnimationHandle>>` snapshot of `_steps` before iterating.

**LOW — PreviewSession MonitorTick duplicate subscribe:**

- `PreviewSession.ScheduleAutoRestore()` called `EditorApplication.update += MonitorTick` unconditionally — repeated calls (e.g. preview chaining) stacked multiple subscriptions, causing `MonitorTick` to fire multiple times per editor frame. Fix: added `_monitorSubscribed` static flag; subscribe only when not already subscribed, and clear the flag in both unsubscribe paths inside `MonitorTick`.

### Notes

Audit-driven release. All fixes are backward-compatible — no public API surface changed. Verified clean (no changes needed): `UniTaskAnimationBackend` (already guarded), `AnimationBackendRegistry`, `InteractiveUIComponent`, all 7 animation modules, `AnimatedButton`, `AnimatedToggle`, all Editor inspectors, `UIPreviewPanel`, `UIComponentEditorBase`, `CooldownOverlay`.

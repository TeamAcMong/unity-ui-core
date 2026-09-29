# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.9.0] - 2026-09-29

### Changed
- **UniTask is now optional.** The package needs only uGUI and TextMeshPro.
  - New default backend **`DefaultAnimationBackend`**: same timing as before (the first frame writes t = 0, the last writes the exact end value), `TimeMode`, `Stop` vs `Interrupt`, and it stops when the host is destroyed. `AnimationBackendRegistry.Current` defaults to it.
  - `UniTaskAnimationBackend` moved to the optional assembly **`DreamTech.UICore.UniTask`**, compiled only when the `com.cysharp.unitask` package is installed (or when the scripting define `DREAMTECH_UICORE_UNITASK` is set, e.g. for a `.unitypackage` install).
  - `CooldownBehavior`, `LongPressBehavior`, `HoldRepeatBehavior`, `AnimationSequence`, `CooldownOverlay` and `AdvancedProgressBar` run on the package's own per-frame loop instead of UniTask. Timing is unchanged: scaled vs unscaled time as before, and loops end when their host is destroyed.
  - `package.json` no longer lists UniTask.
  - **Migration:** code in an asmdef that uses `UniTaskAnimationBackend` must add a reference to `DreamTech.UICore.UniTask`, or switch to `DefaultAnimationBackend`.
- `Stop()` on a `DefaultAnimationBackend` tween puts the value back at once. The UniTask backend did it one frame later, so a value set right after `Stop()` could be overwritten.

### Added
- **`ButtonFeedback` inspector**, in the same style as the Animated Button inspector (header, tabs, cards):
  - **Checks with one-click fixes**: nothing receives touches; a Transition, Animator or AnimatedButton also scales the button; Scale Mode Target without a target; a button inside a scroll list with no press delay.
  - **Edit-mode preview** (Press, Release, Tap) in a scene or Prefab Mode. It runs the real component and then restores scale, touch area and auto-filled references, so nothing is saved.
  - **Scale-over-time graph** with the pressed scale, the release moment and the overshoot peak. A **Hold** slider shows quick taps too.
  - Edit the shared profile in place; **New…** creates a profile asset from the numbers in use.
  - Play Mode tools: phase, scale factor, Press / Release / Tap buttons, and whether a cue handler is set.
- **`ButtonFeedbackProfile` inspector**: presets (Punchy, Default, Subtle, Soft), the graph, and settings grouped by motion, timing, input, sound and filters.
- **`PressScaleModule`** for `AnimatedButton` and other module components. It reads a `ButtonFeedbackProfile`, so both kinds of button squeeze the same way: separate press and release duration and curve, release overshoot, and real time when the profile says so.
- **Tap** button in the Animation-tab preview panel (`PreviewSession.PreviewTap`): Pressed, a hold, then Normal in one session, so the release animation is visible.
- `ButtonFeedbackProfile.SampleTap(hold, step)` → `TapShape` (lowest scale, peak, peak time, release and end times). It uses the exact per-frame math of `ButtonFeedback`.
- Optional interfaces:
  - `ITimeModeAnimationBackend.WithTimeMode(mode)`: run one tween on a different clock.
  - `IAnimationDurationHint.GetDuration(state)`: a module reports its per-state duration to the preview.

### Fixed
- The preview panel could throw `ArgumentException` on a component whose modules were of different types: the reflected `duration` field was cached from the first module type. It is now cached per type, and `IAnimationDurationHint` is asked first.
- `AdvancedProgressBar.StopPulse()` did not stop the pulse while the bar was still full; the loop started the next beat.

### Added
- **`ButtonFeedback`** — press feedback that attaches to an existing `Button` / `Toggle` (does not replace it):
  - Separate press and release timing + curves (release curve may overshoot for a small bounce), or a `Follow` motion (exponential approach, no overshoot) for soft objects such as chests and cards.
  - Scale `Self`, a `Target`, or only the `Children` (root = touch area stays still).
  - Keeps the touch area at its un-pressed size while scaled (`raycastPadding` compensation), so a touch near the edge that is released still counts as a click.
  - Buttons inside a `ScrollRect`: the press waits a short delay; a finger that starts scrolling cancels it — or releases it, if the press had already started; touching a fast-moving list only stops it.
  - Non-interactable buttons neither press nor emit a cue. Runs on unscaled time by default. Optionally disables the button's own `Animator` / `Transition`.
  - `SetExternalScale` lets another effect (e.g. an attention pulse) multiply the press factor instead of fighting over `localScale`.
- **Virtual scrolling** (`DreamTech.UICore.Scrolling`) — lists and grids that only keep views for the visible items plus an overscan band, recycling them through per-type pools:
  - `VirtualListView`: one column/row, four directions (top→bottom, bottom→top for chat, left→right, right→left). Item size modes: Fixed, per Template, from the Adapter (`IVirtualItemSizeSource`), or Measured when the item appears (layout-driven; the root `Image` is ignored). Measured size changes keep the on-screen items still — anchored on an item that was already visible, even mid-drag.
  - `VirtualGridView`: equal cells, fixed count or fit-to-width per line, block and last-line alignment.
  - Snap: Nearest (after inertia slows below a speed) or Paged (one item per swipe), with viewport/item pivots. `ScrollToIndex` / `JumpToIndex` / `ScrollToOffset` / `ScrollToNext` / `ScrollToPrevious`, eased, optional unscaled time.
  - Events: `OnFocusedIndexChanged`, `OnSnapped`, `OnReachedStart` / `OnReachedEnd` (fires for short content too), C# `ItemBound` / `ItemRecycled`, and `IVirtualItemLifecycle` on item components.
  - Data: `IVirtualScrollAdapter` or `SetItems(count, bind, typeOf, sizeOf)`; `NotifyDataSetChanged` (clamps at once when the data shrinks), `RefreshItem`, `NotifyItemSizeChanged`, `SetTemplates`.
  - `RecycleMode.Park` keeps recycled views active off-screen (behind the mask) for heavy items.
  - `NestedScrollRect`: a ScrollRect inside a ScrollRect of the other axis hands off drags that lean towards the parent's axis.
  - Pure layout models `ListLayoutModel` (Fenwick tree — O(log n) lookups, double-precision offsets) and `GridLayoutModel`, usable on their own.
- **Editor for virtual scrolling:** tabbed inspector (Layout · Items · Motion · Events · Preview) with setup checks and one-click fixes, **edit-mode preview** that runs the real runtime path (preview views are never saved; Content is restored before save / play / reload), snap pivot diagram, scene gizmos (viewport, overscan, snap line, item indices), play-mode stats and scroll controls, drag-and-drop templates, and *GameObject ▸ DreamTech UI Core* presets (vertical / horizontal list, grid, page view, chat list) with `VirtualScrollDemoFiller` so Play works immediately.
- **`ButtonFeedbackProfile`** (ScriptableObject) — shared numbers for many buttons. `ButtonFeedback.DefaultProfile` sets the game-wide default at boot.
- **`ButtonFeedbackCues.Handler`** — single hook for click sound / haptics. The package plays nothing itself; the game maps cue keys to its own audio + haptic systems.
- **`ButtonFeedback.RegisterAll(root)`** and menu *GameObject ▸ DreamTech UI Core ▸ Add Button Feedback To Buttons Under Selection* (with Undo) — attach to every Button/Toggle under a root, skipping names that end with the profile's exclude suffix.
- **`AnimationTimeMode`** on `UniTaskAnimationBackend` (`Scaled` default, `Unscaled` for UI that must keep its pace during slow-motion / pause).
- **`IInterruptibleAnimationHandle.Interrupt()`** — stop a tween and keep the target where it is (as opposed to `Stop()`, which restores `from`).
- **`ignoreHoverOnTouch`** on `InteractiveUIComponent` (default on).
- First test suites: `DreamTech.UICore.Tests` (EditMode, 52 tests) and `DreamTech.UICore.Tests.Runtime` (PlayMode, 13 tests — backend Stop vs Interrupt, virtual list/grid recycling, scroll-to, measure anchoring, snapping, edges, direction, data shrink, lifecycle).

### Fixed
- **Quick taps showed almost no press animation** on `AnimatedButton` / `AnimatedToggle`: a state change stopped the running tween with `Stop()`, which snapped the target back to that tween's `from` value, so a release that arrived before the press finished animated from the original scale to the original scale. State changes now use `Interrupt()` — the next state's animation continues from the current value.
- **Package did not compile on Unity 6.5** — the edit-mode preview keyed its snapshots by `GetInstanceID()` and looked them up with `EditorUtility.InstanceIDToObject()`, both compile errors (CS0619) from Unity 6.5. Snapshots are now keyed by the object reference itself, which works on every supported version.
- **Touch showed a one-frame Hover** before Pressed (touch sends PointerEnter right before PointerDown). Hover is now skipped for touch pointers unless `ignoreHoverOnTouch` is turned off.

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

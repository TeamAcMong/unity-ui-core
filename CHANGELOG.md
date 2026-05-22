# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.6.0] - 2026-05-22

### Added
- **Edit-mode Animation Preview** — designers can preview animations without entering Play mode.
  - **Per-module ▶ button** on each animation module card — click to preview that module solo (default targetState = Pressed). Button is disabled (with tooltip) while another preview is already running.
  - **State-based Preview Panel** at the top of the Animation tab in AnimatedButton/AnimatedToggle inspectors: dropdown to choose UIState + ▶ Play / ↺ Reset buttons + real-time progress bar.
  - Backend `PreviewAnimationBackend` (IAnimationBackend impl) drives tweens via `EditorApplication.update` — no UniTask dependency in edit mode.
  - `PreviewSession` manages lifecycle: snapshot state → swap backend → run preview → restore on completion / cancellation.
  - Lifecycle guards: auto-cancel & restore on selection change, entering Play mode, or Domain Reload.
  - Snapshot covers all Transforms + Graphic colors + CanvasGroup alpha in the hierarchy.
  - No Undo history pollution — restore does not call `Undo.RecordObject`.
  - Preview only works on **scene instances** (not prefab assets in the Project view). A HelpBox informs the user when a prefab asset is selected.
  - AnimationEventHooks are NOT fired during edit-mode preview (no SFX in edit mode — by design).
- **`UIPreviewPanel`** (`Editor/Base/UIPreviewPanel.cs`) — static helper callable from any UIAnimatedComponent inspector to render the preview panel.

## [0.5.0] - 2026-05-22

### Added
- **`targetTransform` field cho ScaleModule, PositionModule, RotationModule** — optional override để animate transform khác thay vì root.
  - `null` (default): animate `target.transform` của component (behavior cũ, backward compat).
  - Assigned: animate transform được assign — hữu ích cho pattern "hit area lớn, visual feedback trên child" (button root xử lý click, child shrink khi pressed).
  - `CaptureInitialValue` đọc giá trị từ transform được resolve, không phải root.
  - Lambda capture local transform reference với null-check để safe khi GameObject destroy giữa animation.
- **Use case điển hình:** Migrate từ AnimatorController-based button (state machine → trigger SetTrigger("Pressed") → clip animates child) sang code-based — chỉ cần assign child vào `targetTransform`, set per-state scales, không cần `.controller` asset.

## [0.4.1] - 2026-05-22

### Fixed
- **Redundant foldout wrapper on `AnimationEventHooks` field** — the inspector showed an unnecessary "Animation Event Hooks" foldout (from Unity's default property rendering for `[Serializable]` classes) wrapped inside the already-grouped section card. New `AnimationEventHooksDrawer` (a `[CustomPropertyDrawer]`) renders the three `UnityEvent` fields inline with no parent foldout. Each `UnityEvent` keeps its own native foldout (per Unity convention). Net result: one section card + three event foldouts, no extra middle wrapper.

## [0.4.0] - 2026-05-22

### Fixed

- **[HIGH-1] SubclassSelectorDrawer popup callback null-guard** — All `ShowSearchablePopup` and `ShowTypeMenu` callbacks now guard `if (so == null) return` before accessing the captured `SerializedObject`. Previously, if the Inspector was destroyed or the selection changed before the GenericMenu callback fired, the lambda would dereference a disposed `SerializedObject` and throw `NullReferenceException`. The existing `prop == null` guard was retained; `so == null` is now checked first.

- **[HIGH-2] UIEditorStyles reinit on skin change (Dark ↔ Light)** — `Init()` now tracks `_lastKnownIsProSkin`. If the editor skin changes while the cached styles are still alive (user toggles Dark/Light at runtime), `Dispose()` is called first and all styles/textures are rebuilt. Previously switching skins left all colour values stale until the next domain reload.

- **[MEDIUM-1] DrawSectionCard whole-header clickable** — The section header row is now allocated via `GetControlRect(false, 20f)` as a single rect. A `MouseDown` on any part of the row toggles the foldout, not just the 16×16 arrow icon. The arrow is now a decorative label (not a Button), eliminating the invisible hit-area ambiguity. A subtle hover tint is drawn during `Repaint` to communicate clickability.

- **[MEDIUM-2] DrawHelpCard cached style** — `msgStyle` (previously `new GUIStyle(...)` on every `OnInspectorGUI` call) is now a static cached `_helpCardMessage` field in `UIEditorStyles`, initialised once in `Init()`. Accessible via `UIEditorStyles.HelpCardMessage`.

- **[MEDIUM-3] DrawPill texture leak eliminated** — `DrawPill` previously called `UIEditorStyles.MakeTex(2, 2, ...)` on every frame, allocating a `Texture2D` that was never freed. Replaced with `EditorGUI.DrawRect` on the pill's rect during `Repaint`; the `Pill` GUIStyle is used only for text layout/color, not background.

- **[MEDIUM-3 extension] ModuleListDrawer count badge + type pill texture leaks** — same `MakeTex` per-frame leak pattern as `DrawPill`, applied to two more render paths in `ModuleListDrawer` (count badge at the top of the list, and the per-card type pill). Both replaced with `EditorGUI.DrawRect` during `Repaint`.

- **[MEDIUM-4] Keyboard navigation off-by-one with headers** — `_keyboardIndex` previously tracked into `_visibleRows` (which includes header rows), causing Up/Down to land on headers and the highlight to skip visible rows. Introduced `_selectableIndices` (`List<int>`) that maps contiguous keyboard positions to non-header `_visibleRows` indices. `HandleKeyboard` and the row highlight check both use `_selectableIndices`. Rebuilt after every `RebuildRows` call with `_keyboardIndex` clamped to valid range.

- **[MEDIUM-5] Focus steal loop in popup** — `EditorGUI.FocusTextInControl("SubclassSearch")` was called unconditionally on every `Repaint` event, preventing the user from typing after the first character because focus was immediately re-stolen. Added `_focusDone` bool; focus is requested only once per popup open (reset in `OnOpen()`).

- **[MEDIUM-6 / LOW-3 / LOW-4] IMGUI layout-pass background flicker** — `DrawHeroHeader`, `DrawPlayModePanel`, and `DrawHelpCard` all captured the rect returned by `BeginVertical()` to draw a background colour. During the Layout pass IMGUI returns an empty rect (height = 0), causing a one-frame flicker where the background covered a zero-height strip. Fixed by moving all `EditorGUI.DrawRect` calls to `if (Event.current.type == EventType.Repaint)` blocks using `GUILayoutUtility.GetLastRect()` after `EndVertical()`, which always has the correct final height.

- **[LOW-1] AccentColor / ErrorColor legacy aliases now theme-aware** — Previously `readonly Color` fields with hardcoded values that did not respond to Dark/Light skin changes. Converted to `static Color` properties that delegate to `Accent` and `DangerColor` respectively.

- **[LOW-2] GetDisplayNameForType result cached** — Added `_displayNameCache (Dictionary<Type, string>)` to avoid instantiating a module instance via `Activator.CreateInstance` on every popup render frame for the same type.

- **[NIT-1] Removed dead _tabUnderlineTex** — Texture was allocated in `Init()` and freed in `Dispose()` but never assigned to any `GUIStyle`. Removed field declaration, creation, and disposal call.

- **[NIT-3] target null-check in DrawPlayModeContent** — `AnimatedButtonEditor`, `AnimatedToggleEditor`, and `AdvancedProgressBarEditor` all cast `target` to the concrete type at the top of `DrawPlayModeContent`. Added `if (target == null) return` guard to prevent `NullReferenceException` during rapid selection changes or domain reload while play mode is active.

## [0.3.4] - 2026-05-22

### Fixed
- **Remove module deleted multiple entries** — Context menu "Remove" called `DeleteArrayElementAtIndex` twice with a flaky `arraySize > index` guard. For `[SerializeReference]` lists where the element is non-null, the first call only nullifies the slot; the guard then triggered another deletion that removed an adjacent element. Fix: explicitly set `managedReferenceValue = null` first, then a single `DeleteArrayElementAtIndex` deterministically removes exactly one slot.
- **Foldout arrow overlapped `⋮` menu button (~8px)** — both rects shared 8 pixels on the right edge, making it ambiguous which control receives clicks. New layout: chevron is now a **decorative indicator** (not a button) placed left of the menu button with explicit 4px gap; menu button has its own clearly-bounded 24px rect at the rightmost position.

### Changed (UX)
- **Click anywhere on card header to toggle expand/collapse** — previously only the small arrow icon was clickable. Now the entire header row toggles expand state, excluding the enabled toggle, drag handle, and `⋮` menu button. Hover tint added on the header to communicate clickability.
- **Module icon now tinted with accent color** — visually reinforces the animation (blue) vs behavior (orange) distinction.
- **`⋮` menu button gets a proper bordered miniButton style** instead of plain miniLabel, making it visually distinct from surrounding decorative icons.

## [0.3.3] - 2026-05-22

### Fixed
- **Module card expanded content overflow** — expanded properties drew beyond card boundary, invading next card's area.
  - Root cause: `elementHeightCallback` returned `propHeight - oneLineHeight` (subtracting the foldout header) but `PropertyField(includeChildren: true)` still rendered the full property height including its own foldout, causing visual overflow.
  - Fix: added `GetManagedReferenceChildrenHeight` + `DrawManagedReferenceChildren` helpers that iterate child properties directly (skipping the managed reference's own foldout header). Height calculation and rendering now match exactly.
- **Duplicate foldout header** — every expanded module card showed two foldouts: the card's own header arrow + the property's auto-generated foldout. Now only the card header is shown; children render flat below.

## [0.3.2] - 2026-05-22

### Fixed
- **ModuleListDrawer NullReferenceException** — `ReorderableList` callbacks captured the original `SerializedProperty` parameter in lambda closures. Across Inspector redraws (selection change, scene reload, prefab open/close), the underlying `SerializedObject` gets disposed but the cached `ReorderableList` instance still held the stale property reference, throwing `NullReferenceException: SerializedObject... has been Disposed` on `GetArrayElementAtIndex`.

  Fix: callbacks now read from `rl.serializedProperty` (re-bound each `Draw()` call) instead of the captured `listProp` parameter. Added defensive null/range checks before `GetArrayElementAtIndex`.

## [0.3.1] - 2026-05-22

### Fixed
- Compile error in `UIComponentEditorBase.cs:125` — `EditorGUILayout.FlexibleSpace` doesn't exist (must be `GUILayout.FlexibleSpace`).

## [0.3.0] - 2026-05-22

### Added — Editor Polish

**Visual overhaul** — production-grade IMGUI inspector:

- **Hero header** — large title + subtitle + accent line, theme-aware (Dark/Light).
- **Card-based sections** — collapsible cards with subtle backgrounds, accent separators, replacing flat HelpBoxes.
- **Underline tab indicator** — Material-style 2px accent line under active tab (was: toolbar buttons).
- **Theme-aware color palette** — 8 semantic colors (Accent, Card BG, Animation Blue, Behavior Orange, Success/Warning/Danger, Muted Text). `EditorGUIUtility.isProSkin` aware.
- **Built-in icon helpers** — 13 icons via `EditorGUIUtility.IconContent` (Add/Remove/Duplicate/Settings/Play/Help/Animation/Behavior/etc).

**ModuleListDrawer** (new) — accordion card list for `[SerializeReference] List<IModule>`:
- 3px accent bar per card (blue = animation, orange = behavior).
- Drag-handle + enabled checkbox + DisplayName + type pill + foldout arrow + ⋮ context menu.
- Drag-to-reorder via `ReorderableList`.
- Context menu: Move Up/Down, Duplicate, Remove.
- Add button → grouped `GenericMenu` with `Built-in/` and `Custom/` submenus.
- Empty state with call-to-action button.
- Expanded state persisted per property path.

**SubclassSelectorDrawer** improvements:
- Replaced `GenericMenu` with `PopupWindowContent` (320×360 popup).
- Auto-focus search field, real-time filter on display name + type name.
- Grouped sections: Built-in (DreamTech.UICore.*) vs Custom (user types).
- Keyboard navigation: Up/Down/Enter/Esc.
- Checkmark on current type.

**UIComponentEditorBase** layout primitives (new methods):
- `DrawHeroHeader(title, subtitle, icon)` — top-of-inspector banner.
- `DrawSectionCard(title, ref foldout, icon, collapsible)` — wrap content in styled card.
- `DrawHelpCard(message, HelpType)` — modernized HelpBox with colored left border.
- `DrawEmptyState(message, actionLabel, onAction)` — empty list CTA.
- `DrawPlayModePanel(drawTests)` — distinctive purple-tinted test panel.
- `DrawPill(label, color)`, `DrawDivider()`, `IconButton(icon, tooltip)`.

**Per-component editors refactored:**
- AnimatedButtonEditor, AnimatedToggleEditor, AdvancedProgressBarEditor now use new helpers.
- AdvancedProgressBar gains 25%/50%/75%/100% play-mode test buttons.

### Performance
- All textures cached with `HideFlags.HideAndDontSave`.
- `AssemblyReloadEvents.beforeAssemblyReload` cleanup on domain reload.
- Module list expanded-state stored in static dictionary (no per-frame allocation).

## [0.2.0] - 2026-05-22

### Added
- **Behavior Module System** — orthogonal plugin pattern parallel với Animation Modules.
  - `IBehaviorModule` interface + `BehaviorModuleBase` abstract class.
  - `InteractiveUIComponent` base class with pointer events + behavior list.
  - 4 built-in behaviors: `CooldownBehavior` (Time/Charge), `LongPressBehavior`, `MultiClickBehavior`, `HoldRepeatBehavior`.
- Editor: "Behaviors" tab in AnimatedButton/AnimatedToggle inspectors.
- README updated with custom behavior module example.

### Changed
- `AnimatedButton` and `AnimatedToggle` now inherit `InteractiveUIComponent` instead of `UIAnimatedComponent` directly.
- Cooldown logic moved from monolithic class to module. Now applicable to ANY clickable (Button, Toggle, custom).

### Removed
- `CooldownButton` class — replaced by `CooldownBehavior` module on `AnimatedButton` (or any `InteractiveUIComponent`).
- `CooldownButtonEditor` — no longer needed.

### Migration from 0.1.0
- Replace `CooldownButton` component → add `AnimatedButton` + add `CooldownBehavior` to Behaviors list.
- API change: `CooldownButton.StartCooldown()` → `animatedButton.GetBehavior<CooldownBehavior>().StartCooldown()`.

## [0.1.0] - 2026-05-22

### Added
- Initial release.
- **Animation Module System** — `[SerializeReference, SubclassSelector]` plugin pattern.
  - 7 built-in modules: Scale, Color, Position, Rotation, Fade, Punch, Shake.
  - Custom modules: implement `IAnimationModule` + `[Serializable]` → auto-appear in Inspector dropdown.
- **Animation Backend** — `IAnimationBackend` abstraction. Default `UniTaskAnimationBackend` (UniTask, linked CTS cancel on destroy).
- **AnimationSequence** — fluent Sequential/Parallel chaining.
- **AnimationEventHooks** — UnityEvent OnStart/OnStep(t)/OnComplete.
- Components: `AnimatedButton`, `AnimatedToggle`, `CooldownButton`, `CooldownOverlay`, `AdvancedProgressBar`.
- Editor: `SubclassSelectorDrawer` (dropdown for managed reference fields), `UIComponentEditorBase` (tab system template).

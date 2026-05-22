# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

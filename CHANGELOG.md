# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

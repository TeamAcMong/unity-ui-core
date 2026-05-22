# DreamTech UI Core

Modular UI framework cho Unity với 2 plugin patterns đối xứng:
- **Animation Modules** — visual feedback (Scale, Color, Position, Rotation, Fade, Punch, Shake)
- **Behavior Modules** — interaction gating (Cooldown, LongPress, MultiClick, HoldRepeat)

Add module qua Inspector dropdown — không cần code cho common cases. Custom module: implement interface + `[Serializable]`, auto xuất hiện trong dropdown.

> UniTask declared as git dependency in `package.json`. If UPM blocks it, install manually: `https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask`.

## Components

| Component | Mô tả |
|---|---|
| `AnimatedButton` | Push button (1 stable state), click event |
| `AnimatedToggle` | Toggle (On/Off), optional sync với Unity Toggle |
| `AdvancedProgressBar` | Progress bar đầy đủ tính năng (4 fill modes, gradient, flash, pulse) |
| `CooldownOverlay` | Visual overlay hiển thị cooldown progress (link từ `CooldownBehavior`) |

## Quick Start

1. Add `AnimatedButton` component to a Button GameObject in your Canvas.
2. **Animation Modules** tab → click `+` → chọn `ScaleModule`.
3. **Behaviors** tab → click `+` → chọn `CooldownBehavior` (optional).
4. Hit Play — button scales on press, blocks click during cooldown.

## Custom Animation Module

```csharp
using System;
using UnityEngine;
using DreamTech.UICore.Animations;
using DreamTech.UICore.Animations.Modules;
using DreamTech.UICore.Animations.Backends;

[Serializable]
public class MyShearModule : AnimationModuleBase
{
    [SerializeField] private float shearAmount = 0.3f;

    public override string DisplayName => "Shear";

    public override void CaptureInitialValue(MonoBehaviour target) { }

    public override IAnimationHandle Play(MonoBehaviour target, UIState newState, IAnimationBackend backend)
    {
        if (!Enabled || newState != UIState.Pressed) return null;
        return backend.Punch(target, target.transform, new Vector3(shearAmount, 0, 0), duration);
    }
}
```

## Custom Behavior Module

```csharp
using System;
using DreamTech.UICore.Behaviors;
using UnityEngine;
using UnityEngine.Events;

/// <summary>Yêu cầu user xác nhận trước khi click execute.</summary>
[Serializable]
public class ConfirmBehavior : BehaviorModuleBase
{
    [SerializeField] private bool requireConfirm = true;
    public UnityEvent onConfirmRequested = new();

    public override string DisplayName => "Confirm Required";
    private bool _confirmed;

    public override bool OnBeforeClick()
    {
        if (!enabled || !requireConfirm || _confirmed) return true;
        onConfirmRequested?.Invoke();  // hiển thị popup
        return false;  // cancel click hiện tại
    }

    public void Confirm() { _confirmed = true; }
}
```

`[Serializable]` → auto xuất hiện trong Behaviors dropdown, no registration.

## Animation Sequences

Modules chạy **parallel** mặc định khi state change. Programmatic sequence:

```csharp
new AnimationSequence(AnimationSequenceMode.Sequential)
    .Append(() => backend.TweenVector3(host, ...))
    .Append(() => backend.TweenColor(host, ...))
    .Play(host)
    .OnComplete(() => Debug.Log("done"));
```

## Event Hooks

Tất cả animated components expose:

| Event | Khi nào fire |
|---|---|
| `OnAnimationStart` | Trước khi module đầu tiên start |
| `OnAnimationStep(float t)` | Mỗi frame, normalized 0..1 |
| `OnAnimationComplete` | Sau khi tất cả modules xong |

Behaviors có UnityEvent riêng (ví dụ `LongPressBehavior.onLongPress`, `CooldownBehavior.onCooldownEnd`).

## Swap Animation Backend

```csharp
// Bootstrap — swap to DOTween wrapper (implement IAnimationBackend)
AnimationBackendRegistry.Current = new DOTweenAnimationBackend();
```

Default backend: `UniTaskAnimationBackend` (zero DOTween dependency, linked CTS cancel khi GameObject destroy).

## Built-in Modules

**Animation modules:**
`ScaleModule`, `ColorModule`, `PositionModule`, `RotationModule`, `FadeModule`, `PunchModule`, `ShakeModule`

**Behavior modules:**
`CooldownBehavior` (Time/Charge based), `LongPressBehavior`, `MultiClickBehavior`, `HoldRepeatBehavior`

## Version

`0.2.0` — Hybrid architecture: control types separate (Button/Toggle), behaviors modular.

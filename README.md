# Unity UI Core

> Modular UI framework for Unity with UniTask backend. Plug-in animation & behavior modules via Inspector — no code required for common cases.

[![Version](https://img.shields.io/badge/version-0.8.0-blue.svg)](https://github.com/TeamAcMong/unity-ui-core/releases)
[![Unity](https://img.shields.io/badge/unity-2022.3%2B-black.svg)](https://unity.com/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

## ✨ Features

- **2 symmetric plugin systems:**
  - **Animation Modules** — visual feedback: Scale, Color, Position, Rotation, Fade, Punch, Shake
  - **Behavior Modules** — interaction gating: Cooldown, LongPress, MultiClick, HoldRepeat
- **Custom modules trivial** — implement interface + `[Serializable]`, auto-appear in Inspector dropdown
- **UniTask backend** — zero DOTween dependency, linked cancellation on GameObject destroy
- **Hybrid architecture** — control types separate (Button, Toggle), behaviors composable
- **Components included:** AnimatedButton, AnimatedToggle, AdvancedProgressBar, CooldownOverlay
- **ButtonFeedback** — press feedback for the uGUI `Button` / `Toggle` you already have: shared profiles, press/release curves with a
  small overshoot, touch area that does not shrink with the art, scroll-aware presses, and a single hook for click sound / haptics
- **Virtual List / Grid** — `VirtualListView` and `VirtualGridView` keep views only for visible items (10 000 items ≈ a few dozen views):
  four directions, mixed item types, fixed / per-template / adapter / measured sizes with scroll anchoring, nearest or paged snapping,
  scroll-to-index, focus and load-more events, nested scroll hand-off, and an editor with **edit-mode preview**, snap diagram, scene
  gizmos, play-mode tools and ready-made presets (list, grid, page view, chat)
- **Unscaled-time backend option** — UI keeps its pace during slow-motion or pause
- **Editor support** — tab system, custom drawer with auto-discovery dropdown

## 📦 Installation

### Via Package Manager (Recommended)

1. Open **Window → Package Manager**
2. Click **`+`** → **Add package from git URL**
3. Paste:
   ```
   https://github.com/TeamAcMong/unity-ui-core.git#0.8.0
   ```

### Via manifest.json

```json
{
  "dependencies": {
    "com.dreamtech.uicore": "https://github.com/TeamAcMong/unity-ui-core.git#0.8.0",
    "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask"
  }
}
```

### Latest version (no tag pin)

```
https://github.com/TeamAcMong/unity-ui-core.git
```

> **Requirements:** Unity 2022.3+ (tested on 2022.3.62f2 and 6000.5.7f1), UniTask (auto-installed as dependency).

## 🔘 Button Feedback (existing uGUI buttons)

```csharp
// Bootstrap — once
ButtonFeedback.DefaultProfile = myProfile;              // ButtonFeedbackProfile asset: shared press numbers
ButtonFeedbackCues.Handler = (source, key) =>           // the package plays no sound by itself
{
    if (key == "click") { MyAudio.Play("ui_click"); MyHaptics.Light(); }
};

ButtonFeedback.RegisterAll(popupRoot);                  // every Button/Toggle under the popup
```

Or select a popup and use *GameObject ▸ DreamTech UI Core ▸ Add Button Feedback To Buttons Under Selection*. Details in the
[package README](Packages/com.dreamtech.uicore/README.md).

## 📜 Virtual List / Grid

Fastest start: *GameObject ▸ DreamTech UI Core ▸ Virtual List (Vertical)* — or Horizontal, Grid, Page View (carousel), Chat List —
then press Play (a demo filler binds 200 items). In code:

```csharp
list.SetItems(rows.Count,
    bind:   (item, index) => item.GetCachedComponent<RowView>().Show(rows[index]),
    typeOf: index => rows[index].IsHeader ? 1 : 0);      // template 1 = group header

list.ScrollToIndex(42, viewportPivot: 0.5f, itemPivot: 0.5f);
list.NotifyDataSetChanged();                              // data changed, keep the reading position
```

See the [package README](Packages/com.dreamtech.uicore/README.md) for size modes, snapping, events, nested scrolling and recipes.

## 🚀 Quick Start

1. Add `AnimatedButton` component to a UI button GameObject.
2. **Animation Modules** tab → click `+` → choose `ScaleModule`.
3. **Behaviors** tab → click `+` → choose `CooldownBehavior` (optional).
4. Hit Play — button scales on press, blocks click during cooldown.

## 🧩 Custom Modules

### Custom Animation Module

```csharp
using System;
using UnityEngine;
using DreamTech.UICore.Animations;
using DreamTech.UICore.Animations.Modules;
using DreamTech.UICore.Animations.Backends;

[Serializable]
public class ShearModule : AnimationModuleBase
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

### Custom Behavior Module

```csharp
using System;
using DreamTech.UICore.Behaviors;
using UnityEngine;
using UnityEngine.Events;

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
        onConfirmRequested?.Invoke();
        return false; // cancel click until confirmed
    }

    public void Confirm() { _confirmed = true; }
}
```

Both auto-appear in respective dropdowns. **No registration required.**

## 🔧 Swap Animation Backend

```csharp
// Bootstrap — swap to DOTween wrapper (implement IAnimationBackend)
AnimationBackendRegistry.Current = new DOTweenAnimationBackend();
```

## 📚 Documentation

Full package docs: [`Packages/com.dreamtech.uicore/README.md`](Packages/com.dreamtech.uicore/README.md)

Deployment guide: [`DEPLOY_UPM_SUBTREE.md`](DEPLOY_UPM_SUBTREE.md)

## 🛠️ Development

This repo is a full Unity project. Clone and open with Unity 6000.0+ to develop the package.

```bash
git clone https://github.com/TeamAcMong/unity-ui-core.git
cd unity-ui-core
# Open in Unity Hub
```

## 📄 License

[MIT](LICENSE)

---

Built by [TeamAcMong](https://github.com/TeamAcMong)

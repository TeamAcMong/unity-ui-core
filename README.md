# Unity UI Core

> Modular UI framework for Unity 6 with UniTask backend. Plug-in animation & behavior modules via Inspector — no code required for common cases.

[![Version](https://img.shields.io/badge/version-0.2.0-blue.svg)](https://github.com/TeamAcMong/unity-ui-core/releases)
[![Unity](https://img.shields.io/badge/unity-6000.0%2B-black.svg)](https://unity.com/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

## ✨ Features

- **2 symmetric plugin systems:**
  - **Animation Modules** — visual feedback: Scale, Color, Position, Rotation, Fade, Punch, Shake
  - **Behavior Modules** — interaction gating: Cooldown, LongPress, MultiClick, HoldRepeat
- **Custom modules trivial** — implement interface + `[Serializable]`, auto-appear in Inspector dropdown
- **UniTask backend** — zero DOTween dependency, linked cancellation on GameObject destroy
- **Hybrid architecture** — control types separate (Button, Toggle), behaviors composable
- **Components included:** AnimatedButton, AnimatedToggle, AdvancedProgressBar, CooldownOverlay
- **Editor support** — tab system, custom drawer with auto-discovery dropdown

## 📦 Installation

### Via Package Manager (Recommended)

1. Open **Window → Package Manager**
2. Click **`+`** → **Add package from git URL**
3. Paste:
   ```
   https://github.com/TeamAcMong/unity-ui-core.git#0.2.0
   ```

### Via manifest.json

```json
{
  "dependencies": {
    "com.dreamtech.uicore": "https://github.com/TeamAcMong/unity-ui-core.git#0.2.0",
    "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask"
  }
}
```

### Latest version (no tag pin)

```
https://github.com/TeamAcMong/unity-ui-core.git
```

> **Requirements:** Unity 6000.0+, UniTask (auto-installed as dependency).

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

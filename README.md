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

## Button Feedback (nút uGUI có sẵn)

Không muốn đổi `Button` sang `AnimatedButton`? Gắn **`ButtonFeedback`** lên nút có sẵn: nhấn co, nhả về (có thể vọt nhẹ), bấm thật
thì gửi khoá âm/rung cho game.

```csharp
// Bootstrap — một lần
ButtonFeedback.DefaultProfile = myProfile;                 // ButtonFeedbackProfile asset: số nhún dùng chung
ButtonFeedbackCues.Handler = (source, key) =>              // package không tự phát âm
{
    if (key == "click") { MyAudio.Play("ui_click"); MyHaptics.Light(); }
};

// Gắn cho mọi Button/Toggle dưới một popup (bỏ qua tên kết thúc bằng hậu tố loại trừ của profile)
ButtonFeedback.RegisterAll(popupRoot);
```

| Tuỳ chọn | Ở đâu | Ghi chú |
|---|---|---|
| Nhún theo thời lượng (nhấn/nhả riêng, đường nhả có thể vọt) hoặc đuổi theo (lerp mũ, không vọt) | Profile | `Timed` cho nút, `Follow` cho rương/thẻ |
| Co chính nút / một target / chỉ các con | Component | `Children`: gốc (vùng chạm) đứng yên |
| Giữ vùng chạm không co theo hình | Tự động | nới `raycastPadding` của ảnh nhận chạm |
| Nút trong ScrollRect | Profile | chờ một chút mới nhún, kéo = cuộn thì huỷ |
| Giờ thật | Profile | mặc định bật |
| Tắt Animator/Transition cũ của nút | Profile | tránh hai thứ cùng co một nút |
| Khoá âm riêng / tắt âm cho một nút | Component | `SetCue(key)` / `SetCue(null, mute: true)` |

Menu: *GameObject ▸ DreamTech UI Core ▸ Add Button Feedback To Buttons Under Selection* (có Undo).

## Virtual List / Grid (list ảo hoá)

List 10 000 ô chỉ tốn vài chục view: chỉ ô trong khung nhìn (cộng vùng đệm) có view, view trôi ra ngoài về pool rồi được bind lại
cho ô vừa lộ ra. Chạy trên `ScrollRect` có sẵn, 4 hướng cuộn, nhiều loại ô.

Nhanh nhất: *GameObject ▸ DreamTech UI Core ▸ Virtual List (Vertical)* (hoặc Horizontal / Grid / Page View / Chat List) — tạo sẵn
ScrollRect + Viewport + Content + template + bộ đổ dữ liệu mẫu, bấm Play là cuộn được.

```csharp
[SerializeField] VirtualListView list;   // template 0 = dòng thường, template 1 = tiêu đề nhóm

void Show(IReadOnlyList<Row> rows)
{
    list.SetItems(rows.Count,
        bind:   (item, index) => item.GetCachedComponent<RowView>().Show(rows[index]),
        typeOf: index => rows[index].IsHeader ? 1 : 0);
}

list.ScrollToIndex(42, viewportPivot: 0.5f, itemPivot: 0.5f);   // cuộn có hoạt ảnh, tâm ô 42 về giữa khung
list.NotifyDataSetChanged();                                     // dữ liệu đổi — giữ chỗ đang xem
```

| Tính năng | Ghi chú |
|---|---|
| `VirtualListView` | Một cột / một hàng. Cỡ ô: **Cố định**, **Theo template** (mỗi loại một cỡ), **Adapter báo** (`IVirtualItemSizeSource`), **Tự đo** (LayoutGroup / LayoutElement / chữ trên gốc view — ảnh nền không tính) |
| `VirtualGridView` | Ô cùng cỡ; số ô mỗi dòng cố định hoặc tự vừa bề rộng; căn khối và căn riêng dòng cuối |
| 4 hướng | Trên→Dưới, Dưới→Trên (khung chat), Trái→Phải, Phải→Trái |
| Giữ chỗ khi đo | Ô phía trên đổi cỡ (đo lần đầu, `item.NotifySizeChanged()`) thì ô đang nhìn đứng yên — kể cả đang kéo tay |
| Snap | **Ô gần nhất** (khi quán tính chậm lại) hoặc **Lật trang** (mỗi cú vuốt đúng một ô); điểm neo trên khung + trên ô |
| Cuộn bằng code | `ScrollToIndex`, `JumpToIndex`, `ScrollToOffset`, `ScrollToNext/Previous`; đường cong, giờ thật |
| Sự kiện | `OnFocusedIndexChanged` (chấm trang), `OnSnapped`, `OnReachedStart/End` (tải thêm), C# `ItemBound/ItemRecycled`, `IVirtualItemLifecycle` trên view |
| Pool | Tạo sẵn theo loại; cất bằng tắt object hoặc **Park** (giữ bật, dời ra ngoài mask — mượt hơn với ô nặng) |
| `NestedScrollRect` | ScrollRect lồng (carousel ngang trong trang dọc): cú kéo lệch về trục kia được chuyển cho ScrollRect cha |

Adapter riêng (thay vì `SetItems`): implement `IVirtualScrollAdapter` (+ tuỳ chọn `IVirtualItemSizeSource`,
`IVirtualItemRecycleListener`) rồi `list.SetAdapter(adapter)`.

**Editor:** inspector 5 tab (Bố cục · Ô · Chuyển động · Sự kiện · Xem trước) với thẻ kiểm tra setup có nút sửa, **xem trước ngay
trong Edit mode** (chạy đúng đường code lúc Play; view xem trước không được lưu, Content trả về như cũ), hình minh hoạ điểm neo
snap, gizmo khung nhìn / vùng đệm / đường neo / số thứ tự ô trong Scene, và bảng số liệu + nút cuộn khi Play.

Công thức:
- **Khung chat:** hướng Dưới→Trên + cỡ Tự đo; `OnReachedStart` → tải tin cũ hơn.
- **Carousel có chấm trang:** Snap Lật trang, điểm neo 0,5/0,5; `OnFocusedIndexChanged` → chấm trang; `ScrollToNext()` cho nút mũi tên.
- **Kho đồ:** `VirtualGridView` + Tự vừa bề rộng + căn giữa; `OnReachedEnd` → tải trang kế.

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

UI cần giữ nhịp khi game slow-motion / pause: chạy backend theo giờ thật.

```csharp
AnimationBackendRegistry.Current = new UniTaskAnimationBackend { TimeMode = AnimationTimeMode.Unscaled };
```

Dừng tween: `Stop()` trả target về giá trị đầu; `IInterruptibleAnimationHandle.Interrupt()` giữ nguyên chỗ (component dùng cái này
khi đổi state, nên nhấn/nhả liên tiếp đi tiếp từ cỡ hiện tại).

## Built-in Modules

**Animation modules:**
`ScaleModule`, `ColorModule`, `PositionModule`, `RotationModule`, `FadeModule`, `PunchModule`, `ShakeModule`

**Behavior modules:**
`CooldownBehavior` (Time/Charge based), `LongPressBehavior`, `MultiClickBehavior`, `HoldRepeatBehavior`

## Version

`0.8.0` — `ButtonFeedback` cho nút uGUI có sẵn, list / grid ảo hoá (`VirtualListView`, `VirtualGridView`) có editor xem trước, backend giờ thật, ngắt tween giữ nguyên chỗ. Xem `CHANGELOG.md`.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DreamTech.UICore.Feedback
{
    /// <summary>
    /// Phản hồi NHẤN cho một nút có sẵn (<see cref="Button"/>, <see cref="Toggle"/>, hoặc bất kỳ vùng chạm nào) — gắn kèm, không
    /// thay nút. Nhấn thì nút co lại, nhả thì về (có thể vọt nhẹ), bấm thật thì gửi khoá âm/rung qua
    /// <see cref="ButtonFeedbackCues"/>. Số liệu nằm trong <see cref="ButtonFeedbackProfile"/> dùng chung.
    ///
    /// <para><b>Những chỗ dễ sai mà component này lo sẵn:</b></para>
    /// <list type="bullet">
    /// <item><b>Chạm nhanh vẫn thấy nhún.</b> Nhả giữa lúc đang co thì nhịp nhả bắt đầu từ cỡ đang co dở, không giật về cỡ gốc.</item>
    /// <item><b>Vùng chạm không co theo hình.</b> Co chính nút (<see cref="ScaleMode.Self"/>/<see cref="ScaleMode.Target"/>) thì ảnh
    /// nhận chạm được nới <c>raycastPadding</c> đúng bằng phần bị co — chạm sát mép rồi nhả vẫn tính là bấm. Hoặc dùng
    /// <see cref="ScaleMode.Children"/>: chỉ co các con, gốc (vùng chạm) đứng yên.</item>
    /// <item><b>Nút trong ScrollRect.</b> Chờ một chút mới nhún; ngón tay kéo trong lúc chờ là đang cuộn → không nhún. Chạm vào
    /// danh sách đang trôi nhanh là để dừng cuộn → không nhún.</item>
    /// <item><b>Nút không cho bấm thì không nhún, không kêu.</b></item>
    /// <item><b>Giờ thật</b> (mặc định): UI mở lúc game slow-motion / pause vẫn nhún đúng nhịp.</item>
    /// <item><b>Không đánh nhau với Animator / Transition sẵn có</b> của nút (tắt đi khi gắn, tuỳ profile).</item>
    /// </list>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("DreamTech/UI Core/Button Feedback")]
    public sealed class ButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerClickHandler
    {
        /// <summary>Thứ bị co khi nhấn.</summary>
        public enum ScaleMode
        {
            /// <summary>Chính object này.</summary>
            Self = 0,

            /// <summary><see cref="ButtonFeedback.Target"/> (ví dụ ảnh nền nằm dưới một vùng chạm trong suốt).</summary>
            Target = 1,

            /// <summary>Từng con trực tiếp của object này; gốc đứng yên nên vùng chạm không đổi.</summary>
            Children = 2,
        }

        private enum Phase
        {
            Idle,
            Pressing,
            Releasing,
        }

        /// <summary>Dưới ngưỡng này coi như đã tới cỡ đích (lerp mũ không bao giờ tới đúng).</summary>
        private const float SettleThreshold = 0.0005f;

        [Tooltip("Bộ số dùng chung. Trống = ButtonFeedback.DefaultProfile (game đặt) hoặc profile mặc định của package.")]
        [SerializeField] private ButtonFeedbackProfile _profile;

        [SerializeField] private ScaleMode _scaleMode = ScaleMode.Self;

        [Tooltip("Chỉ dùng với ScaleMode.Target.")]
        [SerializeField] private RectTransform _target;

        [Tooltip("Ảnh nhận chạm cần giữ nguyên cỡ vùng chạm khi co. Trống = targetGraphic của Selectable, hoặc Graphic trên object.")]
        [SerializeField] private Graphic _hitGraphic;

        [Tooltip("Nút quyết định có cho bấm không. Trống = Selectable trên object (nếu có).")]
        [SerializeField] private Selectable _selectable;

        [Tooltip("ScaleMode.Children: các con KHÔNG co (ví dụ viền, bóng). Con có tên kết thúc bằng hậu tố loại trừ của profile " +
                 "cũng bị bỏ qua.")]
        [SerializeField] private List<Transform> _excludeFromScale = new List<Transform>();

        [Tooltip("Khoá âm/rung riêng của nút này. Trống = khoá của profile.")]
        [SerializeField] private string _cueKeyOverride;

        [Tooltip("Không gửi khoá âm/rung khi bấm (nút tự phát âm theo cách khác).")]
        [SerializeField] private bool _muteCue;

        private readonly List<Transform> _scaledChildren = new List<Transform>();
        private readonly List<Vector3> _childRestScales = new List<Vector3>();

        private Phase _phase = Phase.Idle;
        private float _elapsed;
        private float _phaseStartFactor = 1f;
        private float _factor = 1f;
        private float _externalScale = 1f;
        private Vector3 _restScale = Vector3.one;
        private bool _restCaptured;

        private Vector4 _restRaycastPadding;
        private bool _isPaddingExpanded;

        // Nút trong ScrollRect: theo dõi ngón tay từ lúc chạm tới lúc nhả — kéo (cuộn) lúc nào cũng là nhả, kể cả khi đã nhún.
        private bool _trackingScroll;
        private bool _pressPending;
        private float _pendingElapsed;
        private Vector2 _pendingPressPosition;
        private PointerEventData _pendingPointer;
        private bool _releaseAfterPress;

        private bool _tookOverTransition;
        private bool _referencesResolved;

        /// <summary>
        /// Profile cho mọi nút không gán profile riêng. Game đặt một lần lúc khởi động để cả game dùng chung một bộ số mà không
        /// phải kéo asset vào từng nút.
        /// </summary>
        public static ButtonFeedbackProfile DefaultProfile { get; set; }

        public ButtonFeedbackProfile Profile
        {
            get
            {
                if (_profile != null) return _profile;
                if (DefaultProfile != null) return DefaultProfile;
                return ButtonFeedbackProfile.BuiltInDefault;
            }
        }

        public ScaleMode Mode => _scaleMode;

        public RectTransform Target => _scaleMode == ScaleMode.Target && _target != null ? _target : (RectTransform)transform;

        /// <summary>Hệ số cỡ hiện tại so với cỡ gốc (1 = nghỉ) — để đọc, không để điều khiển.</summary>
        public float CurrentFactor => _factor;

        public bool IsPressed => _phase == Phase.Pressing;

        public bool IsPressPending => _pressPending;

        /// <summary>Khoá âm/rung sẽ gửi khi bấm; null = không gửi.</summary>
        public string CueKey
        {
            get
            {
                if (_muteCue) return null;
                return string.IsNullOrEmpty(_cueKeyOverride) ? Profile.ClickCueKey : _cueKeyOverride;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Cấu hình bằng code
        // ─────────────────────────────────────────────────────────────────────

        public void SetProfile(ButtonFeedbackProfile profile)
        {
            _profile = profile;
            ApplyTakeOver();
        }

        public void SetScaleMode(ScaleMode mode, RectTransform target = null)
        {
            ResetImmediate();
            _scaleMode = mode;
            _target = target;
            _restCaptured = false;
        }

        public void SetHitGraphic(Graphic hitGraphic) => _hitGraphic = hitGraphic;

        public void SetCue(string keyOverride, bool mute = false)
        {
            _cueKeyOverride = keyOverride;
            _muteCue = mute;
        }

        /// <summary>
        /// Hệ số cỡ của một hiệu ứng khác trên cùng nút (ví dụ nhịp "đập" gọi chú ý), nhân chồng lên hệ số nhấn. Hai thứ cùng ghi
        /// <c>localScale</c> một Transform thì cái ghi sau thắng — gom về đây để chỉ một chỗ ghi.
        /// </summary>
        public void SetExternalScale(float scale)
        {
            if (scale <= 0f) scale = 1f;
            if (_phase == Phase.Idle) CaptureRest();
            _externalScale = scale;
            ApplyFactor();
        }

        /// <summary>
        /// Gắn <see cref="ButtonFeedback"/> cho mọi <see cref="Button"/> / <see cref="Toggle"/> dưới <paramref name="root"/> chưa có.
        /// Bỏ qua object có tên kết thúc bằng hậu tố loại trừ của profile. Trả về số nút vừa gắn.
        /// </summary>
        public static int RegisterAll(Transform root, ButtonFeedbackProfile profile = null, bool includeInactive = true)
        {
            if (root == null) return 0;
            string suffix = (profile != null ? profile : DefaultProfile != null ? DefaultProfile : ButtonFeedbackProfile.BuiltInDefault)
                .ExcludeSuffix;
            int added = 0;
            foreach (Selectable selectable in root.GetComponentsInChildren<Selectable>(includeInactive))
            {
                if (!(selectable is Button) && !(selectable is Toggle)) continue;
                if (HasSuffix(selectable.name, suffix)) continue;
                if (selectable.GetComponent<ButtonFeedback>() != null) continue;
                var feedback = selectable.gameObject.AddComponent<ButtonFeedback>();
                if (profile != null) feedback.SetProfile(profile);
                added++;
            }
            return added;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Vòng đời
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            ResolveReferences();
            ApplyTakeOver();
        }

        private void Update()
        {
            if (_trackingScroll || _phase != Phase.Idle)
            {
                Advance(Profile.UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);
            }
        }

        private void OnDisable()
        {
            // Đóng màn / trả về pool giữa cú nhấn: về nghỉ ngay, không để nút kẹt ở cỡ co cho lần mở sau.
            ResetImmediate();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Con trỏ
        // ─────────────────────────────────────────────────────────────────────

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left) return;
            if (!CanRespond()) return;
            _releaseAfterPress = false;

            ScrollRect scroll = ActiveScrollRect();
            ButtonFeedbackProfile profile = Profile;
            if (scroll != null && profile.ScrollPressDelay > 0f)
            {
                float threshold = profile.ScrollVelocityThreshold;
                if (scroll.velocity.sqrMagnitude >= threshold * threshold) return; // chạm để dừng danh sách đang trôi
                _trackingScroll = true;
                _pressPending = true;
                _pendingElapsed = 0f;
                _pendingPointer = eventData;
                _pendingPressPosition = eventData != null ? eventData.position : Vector2.zero;
                return;
            }

            BeginPress();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left) return;
            if (_pressPending)
            {
                // Chạm nhanh trong vùng cuộn (nhả trước khi hết thời gian chờ) mà không kéo: vẫn phải thấy nút nhún.
                bool dragged = IsDrag(eventData);
                StopTracking();
                if (!dragged && CanRespond())
                {
                    BeginPress();
                    _releaseAfterPress = true;
                }
                return;
            }
            StopTracking();
            Release();
        }

        /// <summary>Rời khỏi nút trong lúc nhấn = nhả. Vùng chạm đã giữ nguyên cỡ nên rời ra là thật sự rời.</summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            StopTracking();
            _releaseAfterPress = false;
            Release();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left) return;
            if (!CanRespond()) return;
            ButtonFeedbackCues.Raise(this, CueKey);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Nhịp
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Tiến nhịp theo <paramref name="deltaTime"/> giây. Update tự gọi; test hoặc hệ thời gian riêng gọi thẳng.</summary>
        public void Advance(float deltaTime)
        {
            deltaTime = Mathf.Max(0f, deltaTime);
            ButtonFeedbackProfile profile = Profile;

            if (_trackingScroll)
            {
                if (IsDrag(_pendingPointer))
                {
                    // Ngón tay đã thành cú cuộn: chưa nhún thì thôi, đang nhún thì nhả.
                    StopTracking();
                    _releaseAfterPress = false;
                    Release();
                }
                else if (_pressPending)
                {
                    _pendingElapsed += deltaTime;
                    if (_pendingElapsed >= profile.ScrollPressDelay)
                    {
                        _pressPending = false;
                        BeginPress();
                    }
                }
            }

            if (_phase == Phase.Idle) return;
            _elapsed += deltaTime;
            bool reachedPressed = false;

            if (profile.Motion == PressMotion.Follow)
            {
                float goal = _phase == Phase.Pressing ? profile.PressedScale : 1f;
                _factor = Mathf.Lerp(_factor, goal, Mathf.Clamp01(deltaTime * profile.FollowSpeed));
                if (Mathf.Abs(_factor - goal) < SettleThreshold)
                {
                    _factor = goal;
                    if (_phase == Phase.Releasing) _phase = Phase.Idle;
                    else reachedPressed = true;
                }
            }
            else if (_phase == Phase.Pressing)
            {
                float progress = profile.PressDuration <= 0f ? 1f : Mathf.Clamp01(_elapsed / profile.PressDuration);
                _factor = Mathf.LerpUnclamped(_phaseStartFactor, profile.PressedScale, Evaluate(profile.PressCurve, progress));
                reachedPressed = progress >= 1f;
            }
            else
            {
                float progress = profile.ReleaseDuration <= 0f ? 1f : Mathf.Clamp01(_elapsed / profile.ReleaseDuration);
                _factor = progress >= 1f ? 1f : Mathf.LerpUnclamped(_phaseStartFactor, 1f, Evaluate(profile.ReleaseCurve, progress));
                if (progress >= 1f) _phase = Phase.Idle;
            }

            ApplyFactor();

            if (reachedPressed && _releaseAfterPress)
            {
                _releaseAfterPress = false;
                Release();
            }
        }

        private static float Evaluate(AnimationCurve curve, float progress)
        {
            return curve != null && curve.length > 0 ? curve.Evaluate(progress) : progress;
        }

        private void BeginPress()
        {
            if (_phase == Phase.Idle) CaptureRest();
            ExpandHitArea();
            StartPhase(Phase.Pressing);
        }

        private void Release()
        {
            RestoreHitArea();
            if (_phase != Phase.Pressing) return;
            StartPhase(Phase.Releasing);
        }

        private void StartPhase(Phase phase)
        {
            _phase = phase;
            _elapsed = 0f;
            _phaseStartFactor = _factor;
        }

        private void ResetImmediate()
        {
            StopTracking();
            _releaseAfterPress = false;
            if (_phase != Phase.Idle || !Mathf.Approximately(_factor, 1f) || !Mathf.Approximately(_externalScale, 1f))
            {
                _phase = Phase.Idle;
                _factor = 1f;
                _externalScale = 1f;
                ApplyFactor();
            }
            RestoreHitArea();
        }

        private void StopTracking()
        {
            _trackingScroll = false;
            _pressPending = false;
            _pendingElapsed = 0f;
            _pendingPointer = null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Cỡ
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Đọc cỡ gốc lúc đang nghỉ (chia bỏ hệ số hiện có) — không cần biết trước cỡ đã bake.</summary>
        private void CaptureRest()
        {
            float divisor = Mathf.Max(0.0001f, _factor * _externalScale);
            if (_scaleMode == ScaleMode.Children)
            {
                _scaledChildren.Clear();
                _childRestScales.Clear();
                string suffix = Profile.ExcludeSuffix;
                for (int index = 0; index < transform.childCount; index++)
                {
                    Transform child = transform.GetChild(index);
                    if (_excludeFromScale.Contains(child) || HasSuffix(child.name, suffix)) continue;
                    _scaledChildren.Add(child);
                    _childRestScales.Add(child.localScale / divisor);
                }
            }
            else
            {
                _restScale = Target.localScale / divisor;
            }
            _restCaptured = true;
        }

        private void ApplyFactor()
        {
            if (!_restCaptured) return;
            float scale = _factor * _externalScale;
            if (_scaleMode == ScaleMode.Children)
            {
                for (int index = 0; index < _scaledChildren.Count; index++)
                {
                    if (_scaledChildren[index] != null) _scaledChildren[index].localScale = _childRestScales[index] * scale;
                }
                return;
            }
            Target.localScale = _restScale * scale;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Vùng chạm
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Nới vùng chạm của ảnh nhận chạm để nó giữ đúng cỡ lúc chưa nhấn khi bị co còn <c>PressedScale</c>.
        /// <c>raycastPadding</c> tính trong hệ LOCAL của Graphic (đã bị co), âm = nới ra. Chế độ Children không co gốc nên
        /// không cần; ảnh nằm ngoài thứ bị co cũng không cần.
        /// </summary>
        private void ExpandHitArea()
        {
            if (_scaleMode == ScaleMode.Children || _hitGraphic == null || _isPaddingExpanded) return;
            float pressedScale = Profile.PressedScale;
            if (pressedScale <= 0f || pressedScale >= 1f) return;
            if (!_hitGraphic.rectTransform.IsChildOf(Target)) return;

            _restRaycastPadding = _hitGraphic.raycastPadding;
            Vector2 size = _hitGraphic.rectTransform.rect.size;
            float grow = 1f / pressedScale - 1f;
            float horizontal = size.x * grow * 0.5f;
            float vertical = size.y * grow * 0.5f;
            _hitGraphic.raycastPadding = _restRaycastPadding - new Vector4(horizontal, vertical, horizontal, vertical);
            _isPaddingExpanded = true;
        }

        private void RestoreHitArea()
        {
            if (!_isPaddingExpanded || _hitGraphic == null) return;
            _hitGraphic.raycastPadding = _restRaycastPadding;
            _isPaddingExpanded = false;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Trợ giúp
        // ─────────────────────────────────────────────────────────────────────

        private void ResolveReferences()
        {
            if (_referencesResolved) return;
            _referencesResolved = true;
            if (_selectable == null) _selectable = GetComponent<Selectable>();
            if (_hitGraphic == null) _hitGraphic = _selectable != null && _selectable.targetGraphic != null
                ? _selectable.targetGraphic
                : GetComponent<Graphic>();
        }

        /// <summary>Tắt Animator + Transition sẵn có của nút một lần — nếu profile yêu cầu.</summary>
        private void ApplyTakeOver()
        {
            if (_tookOverTransition || !Profile.TakeOverTransition) return;
            ResolveReferences();
            var animator = GetComponent<Animator>();
            if (animator != null) animator.enabled = false;
            if (_selectable != null) _selectable.transition = Selectable.Transition.None;
            _tookOverTransition = true;
        }

        private bool CanRespond()
        {
            ResolveReferences();
            if (!isActiveAndEnabled) return false;
            return _selectable == null || _selectable.IsInteractable();
        }

        private ScrollRect ActiveScrollRect()
        {
            ScrollRect scroll = GetComponentInParent<ScrollRect>();
            return scroll != null && scroll.isActiveAndEnabled ? scroll : null;
        }

        private bool IsDrag(PointerEventData eventData)
        {
            if (eventData == null) return false;
            if (eventData.dragging) return true;
            float threshold = eventData.useDragThreshold && EventSystem.current != null ? EventSystem.current.pixelDragThreshold : 0f;
            return (eventData.position - _pendingPressPosition).sqrMagnitude > threshold * threshold;
        }

        private static bool HasSuffix(string name, string suffix)
        {
            return !string.IsNullOrEmpty(suffix) && !string.IsNullOrEmpty(name) &&
                   name.EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}

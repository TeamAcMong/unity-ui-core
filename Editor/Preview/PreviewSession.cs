using System;
using System.Collections.Generic;
using System.Reflection;
using DreamTech.UICore.Animations.Backends;
using DreamTech.UICore.Animations.Modules;
using DreamTech.UICore.Animations;
using DreamTech.UICore.Base;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DreamTech.UICore.Editor.Preview
{
    /// <summary>
    /// Manages a single edit-mode preview session for <see cref="UIAnimatedComponent"/> animations.
    ///
    /// Lifecycle per session:
    ///  1. <see cref="PreviewState"/> or <see cref="PreviewModule"/> is called.
    ///  2. Current component state (Transform/Graphic/CanvasGroup values) is snapshotted.
    ///  3. <see cref="AnimationBackendRegistry.Current"/> is swapped to a new <see cref="PreviewAnimationBackend"/>.
    ///  4. Animation is triggered via reflection (<c>PlayAnimationsForState</c>) or the module API.
    ///  5. On natural completion, timeout, selection change, play-mode enter, or assembly reload —
    ///     the backend is restored, snapshot values are written back, and <c>onComplete</c> is invoked.
    ///
    /// Thread safety: all methods must be called from the main thread (editor UI / EditorApplication.update).
    /// </summary>
    public static class PreviewSession
    {
        // ─────────────────────────────────────────────────────────────────────
        // Public state
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>True while a preview animation is running.</summary>
        public static bool IsActive => _activeSession != null;

        /// <summary>Normalized progress 0..1 of the current preview. 0 if none active.</summary>
        public static float Progress => _activeSession?.GetProgress() ?? 0f;

        /// <summary>Human-readable label for the current preview (e.g. "Scale → Pressed").</summary>
        public static string CurrentInfo => _activeSession?.GetInfo() ?? string.Empty;

        // ─────────────────────────────────────────────────────────────────────
        // Internal session state
        // ─────────────────────────────────────────────────────────────────────

        private sealed class SessionState
        {
            public UIAnimatedComponent Target;
            public PreviewAnimationBackend PreviewBackend;
            public IAnimationBackend OriginalBackend;
            public Dictionary<Transform, TransformSnapshot> TransformSnapshots;
            public Dictionary<Graphic, GraphicSnapshot> GraphicSnapshots;
            public Dictionary<CanvasGroup, CanvasGroupSnapshot> CanvasGroupSnapshots;
            public float MaxDuration;
            public double StartTime;
            public Action OnComplete;
            public string Label;

            /// <summary>State phát tiếp trong cùng phiên (xem trước Tap: Pressed rồi Normal); null = không có.</summary>
            public UIState? PendingState;

            /// <summary>Giây (tính từ đầu phiên) lúc phát <see cref="PendingState"/>.</summary>
            public float PendingAt;

            public float GetProgress()
            {
                if (MaxDuration <= 0f) return 1f;
                return Mathf.Clamp01((float)(EditorApplication.timeSinceStartup - StartTime) / MaxDuration);
            }

            public string GetInfo() => Label;
        }

        // Snapshot structs — use component instance ID as key to avoid Unity fake-null lookup issues
        private struct TransformSnapshot
        {
            public Vector3 LocalScale;
            public Vector3 LocalPosition;
            public Vector3 LocalEulerAngles;
        }

        private struct GraphicSnapshot
        {
            public Color GraphicColor;
        }

        private struct CanvasGroupSnapshot
        {
            public float Alpha;
        }

        private static SessionState _activeSession;
        private static bool _monitorSubscribed;

        // Cached reflection: PlayAnimationsForState(UIState)
        private static MethodInfo _playAnimationsForStateMethod;

        // Cached reflection: animationModules field on UIAnimatedComponent
        private static FieldInfo _animationModulesField;

        // Cached reflection: `duration` field per module type (null = type has none)
        private static readonly Dictionary<Type, FieldInfo> _moduleDurationFields = new Dictionary<Type, FieldInfo>();

        // ─────────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Preview all enabled animation modules for a UIState transition.
        /// Snapshots the component hierarchy, swaps the backend, and invokes the protected
        /// <c>PlayAnimationsForState(UIState)</c> method via reflection.
        /// </summary>
        /// <param name="component">Scene instance of the UIAnimatedComponent to preview.</param>
        /// <param name="targetState">The UIState to animate toward.</param>
        /// <param name="onComplete">Optional callback invoked when the session ends (natural or cancelled).</param>
        public static void PreviewState(UIAnimatedComponent component, UIState targetState, Action onComplete = null)
        {
            if (component == null) return;
            if (!IsSceneInstance(component))
            {
                Debug.LogWarning("[PreviewSession] Preview requires a scene instance, not a prefab asset.");
                return;
            }

            CancelActive();

            var session = StartSessionScaffolding(component, $"{component.GetType().Name} State → {targetState}", onComplete);

            // Trigger PlayAnimationsForState via reflection (method is protected)
            var method = GetPlayAnimationsForStateMethod();
            if (method != null)
            {
                method.Invoke(component, new object[] { targetState });
            }
            else
            {
                Debug.LogWarning("[PreviewSession] Could not find PlayAnimationsForState via reflection. Cancelling.");
                CancelActive();
                return;
            }

            session.MaxDuration = EstimateMaxModuleDuration(component, targetState);
            ScheduleAutoRestore(session);
        }

        /// <summary>
        /// Preview a tap: Pressed, hold for <paramref name="holdSeconds"/>, then Normal — in one session, so the release (e.g. an
        /// overshoot) plays from the pressed values. Previewing Normal on its own starts from the rest values and shows nothing.
        /// </summary>
        /// <param name="component">Scene instance of the UIAnimatedComponent to preview.</param>
        /// <param name="holdSeconds">Seconds between Pressed and Normal.</param>
        /// <param name="onComplete">Optional callback invoked when the session ends (natural or cancelled).</param>
        public static void PreviewTap(UIAnimatedComponent component, float holdSeconds, Action onComplete = null)
        {
            if (component == null) return;
            if (!IsSceneInstance(component))
            {
                Debug.LogWarning("[PreviewSession] Preview requires a scene instance, not a prefab asset.");
                return;
            }

            CancelActive();

            var method = GetPlayAnimationsForStateMethod();
            if (method == null)
            {
                Debug.LogWarning("[PreviewSession] Could not find PlayAnimationsForState via reflection.");
                return;
            }

            var session = StartSessionScaffolding(component, $"{component.GetType().Name} Tap (Pressed → Normal)", onComplete);
            method.Invoke(component, new object[] { UIState.Pressed });
            session.PendingState = UIState.Normal;
            session.PendingAt = Mathf.Max(0f, holdSeconds);
            session.MaxDuration = session.PendingAt + EstimateMaxModuleDuration(component, UIState.Normal);
            ScheduleAutoRestore(session);
        }

        /// <summary>Hold time that lets every enabled module finish its Pressed animation, plus a short visible pause.</summary>
        public static float SuggestedTapHold(UIAnimatedComponent component)
        {
            return component == null ? 0.2f : EstimateMaxModuleDuration(component, UIState.Pressed) + 0.1f;
        }

        /// <summary>
        /// Preview a single animation module transitioning to <paramref name="targetState"/>.
        /// </summary>
        /// <param name="component">Scene instance of the UIAnimatedComponent.</param>
        /// <param name="module">The specific module to play.</param>
        /// <param name="targetState">The UIState to animate toward.</param>
        /// <param name="onComplete">Optional callback invoked when the session ends.</param>
        public static void PreviewModule(UIAnimatedComponent component, IAnimationModule module, UIState targetState, Action onComplete = null)
        {
            if (component == null || module == null) return;
            if (!IsSceneInstance(component))
            {
                Debug.LogWarning("[PreviewSession] Preview requires a scene instance, not a prefab asset.");
                return;
            }

            CancelActive();

            var session = StartSessionScaffolding(
                component,
                $"{module.DisplayName} → {targetState}",
                onComplete);

            // CaptureInitialValue so the module has a fresh baseline before playing
            module.CaptureInitialValue(component);
            var handle = module.Play(component, targetState, session.PreviewBackend);

            session.MaxDuration = EstimateModuleDuration(module, targetState);

            // End session when the module handle completes naturally
            if (handle != null)
            {
                handle.OnComplete(RestoreAndEnd);
            }

            ScheduleAutoRestore(session);
        }

        /// <summary>
        /// Cancel and restore the active preview session, if any. Safe to call when no session is active.
        /// </summary>
        public static void CancelActive() => RestoreAndEnd();

        // ─────────────────────────────────────────────────────────────────────
        // Session scaffolding
        // ─────────────────────────────────────────────────────────────────────

        private static SessionState StartSessionScaffolding(
            UIAnimatedComponent component,
            string label,
            Action onComplete)
        {
            var session = new SessionState
            {
                Target = component,
                Label = label,
                StartTime = EditorApplication.timeSinceStartup,
                OnComplete = onComplete,
                PreviewBackend = new PreviewAnimationBackend(),
                OriginalBackend = AnimationBackendRegistry.Current,
                TransformSnapshots = CaptureTransformSnapshots(component),
                GraphicSnapshots = CaptureGraphicSnapshots(component),
                CanvasGroupSnapshots = CaptureCanvasGroupSnapshots(component),
            };

            // Swap backend to preview backend
            AnimationBackendRegistry.Current = session.PreviewBackend;
            _activeSession = session;

            // Register lifecycle guards — unregistered in RestoreAndEnd
            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += OnAssemblyReload;

            return session;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Restore and teardown
        // ─────────────────────────────────────────────────────────────────────

        private static void RestoreAndEnd()
        {
            if (_activeSession == null) return;
            var s = _activeSession;
            _activeSession = null;  // clear first to prevent re-entrant calls

            // Stop all preview tweens
            s.PreviewBackend?.StopAll();

            // Restore original backend
            AnimationBackendRegistry.Current = s.OriginalBackend;

            // Write snapshot values back to components (no Undo.RecordObject — avoids history pollution)
            if (s.Target != null)
            {
                RestoreTransformSnapshots(s.TransformSnapshots);
                RestoreGraphicSnapshots(s.GraphicSnapshots);
                RestoreCanvasGroupSnapshots(s.CanvasGroupSnapshots);
            }

            // Unregister lifecycle guards
            Selection.selectionChanged -= OnSelectionChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= OnAssemblyReload;

            // Notify caller AFTER restore so their UI repaint sees clean state
            s.OnComplete?.Invoke();

            SceneView.RepaintAll();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Lifecycle guard callbacks
        // ─────────────────────────────────────────────────────────────────────

        private static void OnSelectionChanged() => CancelActive();

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            // Cancel before play mode actually starts so no editor tween bleeds into runtime
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode)
                CancelActive();
        }

        private static void OnAssemblyReload()
        {
            // RestoreAndEnd directly (not CancelActive) so we definitely run even if _activeSession
            // was just set in the same frame as an assembly reload event
            RestoreAndEnd();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Auto-restore monitor (duration-based timeout)
        // ─────────────────────────────────────────────────────────────────────

        private static void ScheduleAutoRestore(SessionState session)
        {
            if (!_monitorSubscribed)
            {
                EditorApplication.update += MonitorTick;
                _monitorSubscribed = true;
            }
        }

        private static void MonitorTick()
        {
            if (_activeSession == null)
            {
                EditorApplication.update -= MonitorTick;
                _monitorSubscribed = false;
                return;
            }

            double elapsed = EditorApplication.timeSinceStartup - _activeSession.StartTime;

            if (_activeSession.PendingState.HasValue && elapsed >= _activeSession.PendingAt)
            {
                UIState pending = _activeSession.PendingState.Value;
                _activeSession.PendingState = null;
                if (_activeSession.Target != null)
                    GetPlayAnimationsForStateMethod()?.Invoke(_activeSession.Target, new object[] { pending });
            }
            // Small buffer (0.15s) accounts for the final-value writes the tween does on its last tick
            if (elapsed > _activeSession.MaxDuration + 0.15f)
            {
                EditorApplication.update -= MonitorTick;
                _monitorSubscribed = false;
                RestoreAndEnd();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Snapshot capture
        // ─────────────────────────────────────────────────────────────────────

        private static Dictionary<Transform, TransformSnapshot> CaptureTransformSnapshots(UIAnimatedComponent component)
        {
            var snapshots = new Dictionary<Transform, TransformSnapshot>();
            foreach (var t in component.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                snapshots[t] = new TransformSnapshot
                {
                    LocalScale = t.localScale,
                    LocalPosition = t.localPosition,
                    LocalEulerAngles = t.localEulerAngles,
                };
            }
            return snapshots;
        }

        private static Dictionary<Graphic, GraphicSnapshot> CaptureGraphicSnapshots(UIAnimatedComponent component)
        {
            var snapshots = new Dictionary<Graphic, GraphicSnapshot>();
            foreach (var g in component.GetComponentsInChildren<Graphic>(includeInactive: true))
            {
                snapshots[g] = new GraphicSnapshot { GraphicColor = g.color };
            }
            return snapshots;
        }

        private static Dictionary<CanvasGroup, CanvasGroupSnapshot> CaptureCanvasGroupSnapshots(UIAnimatedComponent component)
        {
            var snapshots = new Dictionary<CanvasGroup, CanvasGroupSnapshot>();
            foreach (var cg in component.GetComponentsInChildren<CanvasGroup>(includeInactive: true))
            {
                snapshots[cg] = new CanvasGroupSnapshot { Alpha = cg.alpha };
            }
            return snapshots;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Snapshot restore
        // ─────────────────────────────────────────────────────────────────────

        private static void RestoreTransformSnapshots(Dictionary<Transform, TransformSnapshot> snapshots)
        {
            // Keyed by the object itself (not its instance ID): Unity 6.5 turned GetInstanceID and
            // EditorUtility.InstanceIDToObject into compile errors, and a destroyed object simply reads as null here.
            foreach (var kv in snapshots)
            {
                var obj = kv.Key;
                if (obj == null) continue;
                obj.localScale = kv.Value.LocalScale;
                obj.localPosition = kv.Value.LocalPosition;
                obj.localEulerAngles = kv.Value.LocalEulerAngles;
            }
        }

        private static void RestoreGraphicSnapshots(Dictionary<Graphic, GraphicSnapshot> snapshots)
        {
            foreach (var kv in snapshots)
            {
                var obj = kv.Key;
                if (obj == null) continue;
                obj.color = kv.Value.GraphicColor;
            }
        }

        private static void RestoreCanvasGroupSnapshots(Dictionary<CanvasGroup, CanvasGroupSnapshot> snapshots)
        {
            foreach (var kv in snapshots)
            {
                var obj = kv.Key;
                if (obj == null) continue;
                obj.alpha = kv.Value.Alpha;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Duration estimation
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Reflects the <c>animationModules</c> list on <paramref name="component"/> and returns the longest duration across
        /// all enabled modules for <paramref name="state"/>. Falls back to 1.0 s.
        /// </summary>
        internal static float EstimateMaxModuleDuration(UIAnimatedComponent component, UIState state)
        {
            var modulesField = GetAnimationModulesField();
            if (modulesField == null) return 1f;

            var modules = modulesField.GetValue(component) as IList<IAnimationModule>;
            if (modules == null || modules.Count == 0) return 1f;

            float max = 0f;
            foreach (var m in modules)
            {
                if (m == null || !m.Enabled) continue;
                float d = EstimateModuleDuration(m, state);
                if (d > max) max = d;
            }
            return max > 0f ? max : 1f;
        }

        /// <summary>
        /// How long <paramref name="module"/> animates towards <paramref name="state"/>: asks <see cref="IAnimationDurationHint"/>
        /// first, then reflects a float <c>duration</c> field (e.g. <see cref="AnimationModuleBase"/>). Falls back to 0.5 s.
        /// </summary>
        internal static float EstimateModuleDuration(IAnimationModule module, UIState state)
        {
            if (module is IAnimationDurationHint hint) return Mathf.Max(0f, hint.GetDuration(state));
            var field = GetModuleDurationField(module);
            if (field != null && field.GetValue(module) is float d) return d;
            return 0.5f;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Scene instance check
        // ─────────────────────────────────────────────────────────────────────

        private static bool IsSceneInstance(Component c)
        {
            // A prefab asset resides in a scene that is NOT valid/loaded
            return c != null && c.gameObject.scene.IsValid() && c.gameObject.scene.isLoaded;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Reflection helpers (cached)
        // ─────────────────────────────────────────────────────────────────────

        private static MethodInfo GetPlayAnimationsForStateMethod()
        {
            if (_playAnimationsForStateMethod != null) return _playAnimationsForStateMethod;
            _playAnimationsForStateMethod = typeof(UIAnimatedComponent).GetMethod(
                "PlayAnimationsForState",
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[] { typeof(UIState) },
                null);
            return _playAnimationsForStateMethod;
        }

        private static FieldInfo GetAnimationModulesField()
        {
            if (_animationModulesField != null) return _animationModulesField;
            _animationModulesField = typeof(UIAnimatedComponent).GetField(
                "animationModules",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return _animationModulesField;
        }

        private static FieldInfo GetModuleDurationField(IAnimationModule module)
        {
            // Cached per concrete type: a field found on one module type cannot be read from another (ArgumentException).
            Type moduleType = module.GetType();
            if (_moduleDurationFields.TryGetValue(moduleType, out FieldInfo cached)) return cached;

            FieldInfo found = null;
            for (var type = moduleType; type != null && type != typeof(object); type = type.BaseType)
            {
                var field = type.GetField("duration", BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null && field.FieldType == typeof(float))
                {
                    found = field;
                    break;
                }
            }
            _moduleDurationFields[moduleType] = found;
            return found;
        }
    }
}

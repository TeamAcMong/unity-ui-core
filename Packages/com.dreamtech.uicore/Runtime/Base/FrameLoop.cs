using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DreamTech.UICore.Base
{
    /// <summary>
    /// Vòng lặp theo khung hình của package — thay cho <c>UniTask.Yield</c> / <c>UniTask.Delay</c>, để package chạy được mà không cần
    /// UniTask. Một vòng chạy hàm tick ở mỗi lần Update kế tiếp (đăng ký trong Update thì bắt đầu từ khung sau, như
    /// <c>await UniTask.Yield()</c>) cho tới khi hàm tick trả <c>false</c>, vòng bị <see cref="FrameLoopHandle.Cancel"/>, hoặc object chủ
    /// bị destroy (như <c>GetCancellationTokenOnDestroy</c>).
    /// <para>Play mode: một runner ẩn (DontDestroyOnLoad) chạy trong Update, trước script thường. Edit mode: <c>EditorApplication.update</c>.
    /// Test gọi thẳng <see cref="TickAll"/> để chạy từng bước.</para>
    /// </summary>
    internal static class FrameLoop
    {
        /// <summary>Một bước của vòng; trả <c>false</c> để dừng (xong tự nhiên — không gọi <c>onCancelled</c>).</summary>
        internal delegate bool Tick(float deltaTime, float unscaledDeltaTime);

        private static readonly List<FrameLoopHandle> Active = new List<FrameLoopHandle>();
        private static readonly List<FrameLoopHandle> AddedWhileTicking = new List<FrameLoopHandle>();
        private static readonly Predicate<FrameLoopHandle> IsStopped = loop => !loop.IsRunning;
        private static bool _ticking;
        private static FrameLoopRunner _runner;

        /// <summary>Số vòng đang chạy (để test / debug).</summary>
        internal static int ActiveCount => Active.Count + AddedWhileTicking.Count;

        /// <summary>
        /// Chạy <paramref name="tick"/> mỗi khung hình. <paramref name="owner"/>: object chủ — bị destroy thì vòng tự huỷ (null = không
        /// gắn vòng đời). <paramref name="onCancelled"/>: gọi ngay khi vòng bị huỷ (Cancel hoặc chủ bị destroy), không gọi khi xong tự nhiên.
        /// </summary>
        internal static FrameLoopHandle Run(UnityEngine.Object owner, Tick tick, Action onCancelled = null)
        {
            if (tick == null) throw new ArgumentNullException(nameof(tick));
            var loop = new FrameLoopHandle(owner, tick, onCancelled);
            if (_ticking) AddedWhileTicking.Add(loop);
            else Active.Add(loop);
            EnsureDriver();
            return loop;
        }

        /// <summary>Chạy một bước cho mọi vòng. Runner / editor gọi mỗi khung hình; test gọi thẳng.</summary>
        internal static void TickAll(float deltaTime, float unscaledDeltaTime)
        {
            if (_ticking) return;
            _ticking = true;
            try
            {
                for (int index = 0; index < Active.Count; index++)
                {
                    FrameLoopHandle loop = Active[index];
                    if (!loop.IsRunning) continue;
                    if (loop.OwnerDestroyed)
                    {
                        loop.Cancel();
                        continue;
                    }

                    bool keepRunning;
                    try
                    {
                        keepRunning = loop.Callback(deltaTime, unscaledDeltaTime);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                        keepRunning = false;
                    }
                    if (!keepRunning) loop.Finish();
                }
            }
            finally
            {
                _ticking = false;
                Active.RemoveAll(IsStopped);
                if (AddedWhileTicking.Count > 0)
                {
                    Active.AddRange(AddedWhileTicking);
                    AddedWhileTicking.Clear();
                }
            }
        }

        /// <summary>Huỷ mọi vòng (test dọn dẹp; thoát Play mode).</summary>
        internal static void CancelAll()
        {
            var loops = new List<FrameLoopHandle>(Active);
            loops.AddRange(AddedWhileTicking);
            foreach (FrameLoopHandle loop in loops) loop.Cancel();
            Active.Clear();
            AddedWhileTicking.Clear();
        }

        private static void EnsureDriver()
        {
            if (Application.isPlaying)
            {
                if (_runner != null) return;
                var host = new GameObject("[DreamTech UI Core] Frame Loop") { hideFlags = HideFlags.HideInHierarchy };
                UnityEngine.Object.DontDestroyOnLoad(host);
                _runner = host.AddComponent<FrameLoopRunner>();
                return;
            }
#if UNITY_EDITOR
            EditorDriver.Ensure();
#endif
        }

#if UNITY_EDITOR
        /// <summary>Edit mode không có Update: chạy theo <c>EditorApplication.update</c> (không chạy lúc đang Play — runner lo).</summary>
        private static class EditorDriver
        {
            private static bool _hooked;
            private static double _lastTime;

            internal static void Ensure()
            {
                if (_hooked) return;
                _hooked = true;
                _lastTime = EditorApplication.timeSinceStartup;
                EditorApplication.update += Update;
            }

            [InitializeOnLoadMethod]
            private static void HookPlayModeChanges()
            {
                EditorApplication.playModeStateChanged += OnPlayModeChanged;
            }

            private static void Update()
            {
                double now = EditorApplication.timeSinceStartup;
                float deltaTime = Mathf.Clamp((float)(now - _lastTime), 0f, 0.1f);
                _lastTime = now;
                if (Application.isPlaying || ActiveCount == 0) return;
                TickAll(deltaTime, deltaTime);
            }

            private static void OnPlayModeChanged(PlayModeStateChange change)
            {
                // Vòng của Play mode không được chạy tiếp trong Edit mode (và ngược lại).
                if (change == PlayModeStateChange.ExitingPlayMode || change == PlayModeStateChange.ExitingEditMode) CancelAll();
            }
        }
#endif
    }

    /// <summary>Một vòng của <see cref="FrameLoop"/>.</summary>
    internal sealed class FrameLoopHandle
    {
        private readonly UnityEngine.Object _owner;
        private readonly bool _hasOwner;
        private Action _onCancelled;

        internal FrameLoopHandle(UnityEngine.Object owner, FrameLoop.Tick callback, Action onCancelled)
        {
            _owner = owner;
            _hasOwner = !ReferenceEquals(owner, null);
            Callback = callback;
            _onCancelled = onCancelled;
        }

        internal FrameLoop.Tick Callback { get; }

        /// <summary>Còn chạy (chưa xong, chưa bị huỷ).</summary>
        public bool IsRunning { get; private set; } = true;

        /// <summary>Object chủ đã bị destroy.</summary>
        internal bool OwnerDestroyed => _hasOwner && _owner == null;

        /// <summary>Dừng vòng và gọi <c>onCancelled</c> (một lần). Gọi lại / gọi sau khi xong: không làm gì.</summary>
        public void Cancel()
        {
            if (!IsRunning) return;
            IsRunning = false;
            Action onCancelled = _onCancelled;
            _onCancelled = null;
            onCancelled?.Invoke();
        }

        /// <summary>Xong tự nhiên — không gọi <c>onCancelled</c>.</summary>
        internal void Finish()
        {
            IsRunning = false;
            _onCancelled = null;
        }
    }
}

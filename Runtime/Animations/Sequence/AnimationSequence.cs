using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DreamTech.UICore.Animations.Backends;
using UnityEngine;

namespace DreamTech.UICore.Animations.Sequence
{
    /// <summary>
    /// Composable animation sequence built from <see cref="IAnimationHandle"/> steps.
    /// Supports both <see cref="AnimationSequenceMode.Sequential"/> (run one-by-one)
    /// and <see cref="AnimationSequenceMode.Parallel"/> (run all at once) modes.
    /// <para>
    /// Fluent usage example:
    /// <code>
    /// new AnimationSequence()
    ///     .Append(() => backend.TweenFloat(...))
    ///     .Append(() => backend.TweenColor(...))
    ///     .OnComplete(() => Debug.Log("done"))
    ///     .Play(this);
    /// </code>
    /// </para>
    /// <para>
    /// Lifecycle is tied to the <c>host</c> MonoBehaviour passed to <see cref="Play"/>.
    /// When the host is destroyed, all active steps are cancelled automatically.
    /// </para>
    /// </summary>
    public class AnimationSequence : IAnimationHandle
    {
        private readonly List<Func<IAnimationHandle>> _steps = new List<Func<IAnimationHandle>>();
        private readonly AnimationSequenceMode _mode;
        private readonly List<Action> _onCompleteCallbacks = new List<Action>();

        private List<IAnimationHandle> _activeHandles;
        private bool _isPlaying;
        private bool _isCompleted;

        /// <inheritdoc/>
        public bool IsPlaying => _isPlaying;

        /// <inheritdoc/>
        public bool IsCompleted => _isCompleted;

        /// <summary>
        /// Creates a new <see cref="AnimationSequence"/>.
        /// </summary>
        /// <param name="mode">Whether steps run sequentially or in parallel.</param>
        public AnimationSequence(AnimationSequenceMode mode = AnimationSequenceMode.Sequential)
        {
            _mode = mode;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Fluent builder
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Appends a step factory to the sequence. The factory is called lazily when the
        /// step is about to execute, so captures are evaluated at play-time, not build-time.
        /// </summary>
        /// <param name="step">Factory that returns the animation handle for this step. Return null to skip.</param>
        /// <returns>This sequence for chaining.</returns>
        public AnimationSequence Append(Func<IAnimationHandle> step)
        {
            if (step != null)
                _steps.Add(step);
            return this;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Playback
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Starts the sequence asynchronously. Can be called once; call <see cref="Stop"/>
        /// to cancel before completion.
        /// </summary>
        /// <param name="host">Lifecycle host. Sequence is cancelled when host is destroyed.</param>
        /// <returns>This sequence for chaining callbacks.</returns>
        public AnimationSequence Play(MonoBehaviour host)
        {
            if (_isPlaying) return this;

            _isPlaying = true;
            _isCompleted = false;
            _activeHandles = new List<IAnimationHandle>();

            if (_mode == AnimationSequenceMode.Parallel)
                RunParallel(host).Forget();
            else
                RunSequential(host).Forget();

            return this;
        }

        private async UniTaskVoid RunSequential(MonoBehaviour host)
        {
            try
            {
                var ct = host.GetCancellationTokenOnDestroy();
                var stepsSnapshot = new List<Func<IAnimationHandle>>(_steps);
                foreach (var step in stepsSnapshot)
                {
                    if (!_isPlaying) break;

                    IAnimationHandle handle = step.Invoke();
                    if (handle == null) continue;

                    _activeHandles.Add(handle);

                    // Wait until handle stops playing (completed or stopped externally)
                    await UniTask.WaitUntil(
                        () => !handle.IsPlaying || handle.IsCompleted,
                        cancellationToken: ct);
                }

                // Only fire completion callbacks when sequence ran to natural end,
                // not when Stop() was called externally (_isPlaying set to false).
                if (_isPlaying)
                    MarkComplete();
                else
                    _isPlaying = false;
            }
            catch (OperationCanceledException)
            {
                // Host was destroyed; clean up silently
                _isPlaying = false;
            }
        }

        private async UniTaskVoid RunParallel(MonoBehaviour host)
        {
            try
            {
                var ct = host.GetCancellationTokenOnDestroy();
                var waitTasks = new List<UniTask>();
                var stepsSnapshot = new List<Func<IAnimationHandle>>(_steps);

                foreach (var step in stepsSnapshot)
                {
                    IAnimationHandle handle = step.Invoke();
                    if (handle == null) continue;

                    _activeHandles.Add(handle);

                    // Capture handle for closure
                    IAnimationHandle captured = handle;
                    waitTasks.Add(UniTask.WaitUntil(
                        () => !captured.IsPlaying || captured.IsCompleted,
                        cancellationToken: ct));
                }

                await UniTask.WhenAll(waitTasks);

                // Only fire completion callbacks when sequence ran to natural end,
                // not when Stop() was called externally (_isPlaying set to false).
                if (_isPlaying)
                    MarkComplete();
                else
                    _isPlaying = false;
            }
            catch (OperationCanceledException)
            {
                // Host was destroyed; clean up silently
                _isPlaying = false;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // IAnimationHandle
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Stops the sequence immediately and cancels all active step handles.
        /// </summary>
        public void Stop()
        {
            _isPlaying = false;

            if (_activeHandles != null)
            {
                foreach (IAnimationHandle h in _activeHandles)
                    h?.Stop();

                _activeHandles.Clear();
            }
        }

        /// <summary>
        /// Registers a callback invoked when the sequence completes naturally.
        /// If the sequence is already complete when this is called, the callback fires immediately.
        /// </summary>
        /// <param name="callback">Completion callback.</param>
        /// <returns>This sequence for chaining.</returns>
        public IAnimationHandle OnComplete(Action callback)
        {
            if (callback == null) return this;

            if (_isCompleted)
                callback.Invoke();
            else
                _onCompleteCallbacks.Add(callback);

            return this;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private void MarkComplete()
        {
            _isPlaying = false;
            _isCompleted = true;
            _activeHandles?.Clear();  // release completed handle references (prevent stale refs + memory leak)

            foreach (Action cb in _onCompleteCallbacks)
                cb?.Invoke();

            _onCompleteCallbacks.Clear();
        }
    }
}

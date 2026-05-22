using System;
using DreamTech.UICore.Animations;
using DreamTech.UICore.Base;
using UnityEngine;

namespace DreamTech.UICore.Behaviors
{
    /// <summary>
    /// Abstract base class cho tất cả built-in behavior modules.
    /// Cung cấp default no-op implementation cho mọi hook, subclass override khi cần.
    /// Chứa <c>enabled</c> flag và <c>host</c> reference cho mọi module.
    /// </summary>
    [Serializable]
    public abstract class BehaviorModuleBase : IBehaviorModule
    {
        [SerializeField] protected bool enabled = true;

        /// <summary>Host reference, cache trong <see cref="Initialize"/>. Subclass dùng để query state, set interactable, ...</summary>
        protected InteractiveUIComponent host;

        /// <inheritdoc/>
        public bool Enabled => enabled;

        /// <inheritdoc/>
        public abstract string DisplayName { get; }

        /// <inheritdoc/>
        public virtual void Initialize(InteractiveUIComponent host) { this.host = host; }

        /// <inheritdoc/>
        public virtual bool OnBeforeClick() => true;

        /// <inheritdoc/>
        public virtual void OnAfterClick() { }

        /// <inheritdoc/>
        public virtual void OnPointerStateChanged(UIState newState) { }

        /// <inheritdoc/>
        public virtual void Dispose() { }
    }
}

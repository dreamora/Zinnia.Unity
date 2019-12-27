namespace Zinnia.Action
{
    using UnityEngine;
    using UnityEngine.Events;
    using System;
    using System.Collections.Generic;
    using Malimbe.BehaviourStateRequirementMethod;
    using Malimbe.MemberChangeMethod;
    using Malimbe.PropertySerializationAttribute;
    using Malimbe.XmlDocumentationAttribute;
    using Zinnia.Data.Type;

    /// <summary>
    /// The basis for all action signal types.
    /// </summary>
    public abstract class ActionSignal : ScriptableObject, IAction
    {
        /// <summary>
        /// Emitted when <see cref="IsActivated"/> changes.
        /// </summary>
        [Serialized]
        [field: DocumentedByXml]
        public BooleanUnityEvent ActivationStateChanged { get; } = new BooleanUnityEvent();

        /// <summary>
        /// Whether the action is currently activated.
        /// </summary>
        public bool IsActivated
        {
            get => isActivated;
            protected set
            {
                if (isActivated == value)
                {
                    return;
                }

                isActivated = value;
                ActivationStateChanged?.Invoke(value);
            }
        }

        private bool isActivated;

        /// <summary>
        /// Adds a given action to the sources collection.
        /// </summary>
        /// <param name="action">The action to add.</param>
        public abstract void AddSource(IAction action);

        /// <summary>
        /// Removes the given action from the sources collection.
        /// </summary>
        /// <param name="action">The action to remove.</param>
        public abstract void RemoveSource(IAction action);

        /// <summary>
        /// Clears all sources.
        /// </summary>
        public abstract void ClearSources();

        /// <summary>
        /// Emits the appropriate event for when the activation state changes from Activated or Deactivated.
        /// </summary>
        public abstract void EmitActivationState();

        /// <summary>
        /// Whether the event should be emitted.
        /// </summary>
        /// <returns><see langword="true"/> if the event should be emitted.</returns>
        public virtual bool CanEmit()
        {
            return true;
        }
    }


    /// <summary>
    /// A generic type that forms as the basis for all action signal types.
    /// </summary>
    /// <typeparam name="TSelf">This type itself.</typeparam>
    /// <typeparam name="TValue">The variable type the action will be utilizing.</typeparam>
    /// <typeparam name="TEvent">The <see cref="UnityEvent"/> type the action will be utilizing.</typeparam>
    public abstract class ActionSignal<TSelf, TValue, TEvent> : ActionSignal where TSelf : ActionSignal<TSelf, TValue, TEvent>
        where TEvent : UnityEvent<TValue>, new()
    {
        /// <summary>
        /// The initial value of the action.
        /// </summary>
        [Serialized]
        [field: DocumentedByXml]
        public TValue DefaultValue { get; set; }

        /// <summary>
        /// Actions to subscribe to when this action is <see cref="Behaviour.enabled"/>. Allows chaining the source actions to this action.
        /// </summary>
        [Serialized]
        [field: DocumentedByXml]
        protected List<TSelf> Sources { get; set; } = new List<TSelf>();

        /// <summary>
        /// Emitted when the action becomes active.
        /// </summary>
        [DocumentedByXml]
        public TEvent Activated = new TEvent();

        /// <summary>
        /// Emitted when the <see cref="Value"/> of the action changes.
        /// </summary>
        [DocumentedByXml]
        public TEvent ValueChanged = new TEvent();

        /// <summary>
        /// Emitted when the action becomes deactivated.
        /// </summary>
        [DocumentedByXml]
        public TEvent Deactivated = new TEvent();

        /// <summary>
        /// The value of the action.
        /// </summary>
        public TValue Value { get; protected set; }

        /// <summary>
        /// Actions subscribed to when this action is <see cref="Behaviour.enabled"/>. Allows chaining the source actions to this action.
        /// </summary>
        public HeapAllocationFreeReadOnlyList<TSelf> ReadOnlySources => Sources;

        /// <inheritdoc />
        public override void AddSource(IAction action)
        {
            if (action == null)
            {
                return;
            }

            Sources.Add((TSelf) action);
            SubscribeToSource((TSelf) action);
        }

        /// <inheritdoc />
        public override void RemoveSource(IAction action)
        {
            if (action == null)
            {
                return;
            }

            UnsubscribeFromSource((TSelf) action);
            Sources.Remove((TSelf) action);
        }

        /// <inheritdoc />
        public override void ClearSources()
        {
            UnsubscribeFromSources();
            Sources.Clear();
        }

        /// <inheritdoc />
        public override void EmitActivationState()
        {
            if (IsActivated)
            {
                Activated?.Invoke(Value);
                ValueChanged?.Invoke(Value);
            }
            else
            {
                ValueChanged?.Invoke(Value);
                Deactivated?.Invoke(Value);
            }
        }

        /// <summary>
        /// Acts on the value.
        /// </summary>
        /// <param name="value">The value to act on.</param>
        public virtual void Receive(TValue value)
        {
            if (IsValueEqual(value))
            {
                return;
            }

            ProcessValue(value);
        }

        protected virtual void OnEnable()
        {
            Value = DefaultValue;
            SubscribeToSources();
        }

        protected virtual void OnDisable()
        {
            ProcessValue(DefaultValue);
            UnsubscribeFromSources();
        }

        /// <summary>
        /// Subscribes the current action as a listener to the given action.
        /// </summary>
        /// <param name="source">The source action to subscribe listeners on.</param>
        protected virtual void SubscribeToSource(TSelf source)
        {
            if (source == null)
            {
                return;
            }

            source.ValueChanged.AddListener(Receive);
        }

        /// <summary>
        /// Unsubscribes the current action from listening to the given action.
        /// </summary>
        /// <param name="source">The source action to unsubscribe listeners on.</param>
        protected virtual void UnsubscribeFromSource(TSelf source)
        {
            if (source == null)
            {
                return;
            }

            source.ValueChanged.RemoveListener(Receive);
        }

        /// <summary>
        /// Attempts to subscribe listeners to each of the source actions.
        /// </summary>
        protected virtual void SubscribeToSources()
        {
            if (Sources == null)
            {
                return;
            }

            foreach (TSelf source in Sources)
            {
                SubscribeToSource(source);
            }
        }

        /// <summary>
        /// Attempts to unsubscribe existing listeners from each of the source actions.
        /// </summary>
        protected virtual void UnsubscribeFromSources()
        {
            if (Sources == null)
            {
                return;
            }

            foreach (TSelf source in Sources)
            {
                UnsubscribeFromSource(source);
            }
        }

        /// <summary>
        /// Processes the given value and emits the appropriate events.
        /// </summary>
        /// <param name="value">The new value.</param>
        protected virtual void ProcessValue(TValue value)
        {
            Value = value;

            bool shouldActivate = ShouldActivate(value);
            if (IsActivated != shouldActivate)
            {
                IsActivated = shouldActivate;
                EmitActivationState();
            }
            else
            {
                ValueChanged?.Invoke(Value);
            }
        }

        /// <summary>
        /// Whether the given <see cref="TValue"/> is equal to the action's cached <see cref="Value"/>.
        /// </summary>
        /// <param name="value">The value to check equality for.</param>
        /// <returns><see langword="true"/> if the given <see cref="TValue"/> is equal to the action's cached <see cref="Value"/>.</returns>
        protected virtual bool IsValueEqual(TValue value)
        {
            return EqualityComparer<TValue>.Default.Equals(Value, value);
        }

        /// <summary>
        /// Whether the action should become active.
        /// </summary>
        /// <param name="value">The current value to check activation state on.</param>
        /// <returns><see langword="true"/> if the action should activate.</returns>
        protected virtual bool ShouldActivate(TValue value)
        {
            return !EqualityComparer<TValue>.Default.Equals(DefaultValue, value);
        }

        /// <summary>
        /// Called after <see cref="DefaultValue"/> has been changed.
        /// </summary>
        [CalledAfterChangeOf(nameof(DefaultValue))]
        protected virtual void OnAfterDefaultValueChange()
        {
            bool shouldActivate = ShouldActivate(Value);
            if (IsActivated == shouldActivate)
            {
                return;
            }

            IsActivated = shouldActivate;
            EmitActivationState();
        }

        /// <summary>
        /// Called before <see cref="Sources"/> has been changed.
        /// </summary>
        [CalledBeforeChangeOf(nameof(Sources))]
        protected virtual void OnBeforeSourcesChange()
        {
            UnsubscribeFromSources();
        }

        /// <summary>
        /// Called after <see cref="Sources"/> has been changed.
        /// </summary>
        [CalledAfterChangeOf(nameof(Sources))]
        protected virtual void OnAfterSourcesChange()
        {
            SubscribeToSources();
        }
    }
}
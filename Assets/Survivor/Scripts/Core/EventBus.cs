using System;
using System.Collections.Generic;

namespace Survivor.Core
{
    public sealed class EventBus : IEventBus
    {
        private readonly Dictionary<Type, Delegate> _handlers = new Dictionary<Type, Delegate>();

        public void Subscribe<T>(Action<T> handler)
        {
            Delegate existing;
            _handlers.TryGetValue(typeof(T), out existing);
            _handlers[typeof(T)] = Delegate.Combine(existing, handler);
        }

        public void Unsubscribe<T>(Action<T> handler)
        {
            Delegate existing;
            if (!_handlers.TryGetValue(typeof(T), out existing)) return;
            Delegate result = Delegate.Remove(existing, handler);
            if (result == null) _handlers.Remove(typeof(T));
            else _handlers[typeof(T)] = result;
        }

        public void Publish<T>(T evt)
        {
            Delegate existing;
            if (!_handlers.TryGetValue(typeof(T), out existing)) return;
            var action = existing as Action<T>;
            if (action != null) action(evt);
        }
    }
}

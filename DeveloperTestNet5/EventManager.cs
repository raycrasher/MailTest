using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeveloperTestNet5
{
    // dead simple event manager
    public class EventManager
    {
        Dictionary<Type, List<WeakReference<object>>> _events;

        public void Fire<TEvent>(object sender, TEvent eventData)
        {
            var eventType = typeof(TEvent);
            if (_events.TryGetValue(eventType, out var handlers))
            {
                for (int i = handlers.Count - 1; i >= 0; i--)
                {
                    if (handlers[i].TryGetTarget(out var h))
                    {
                        var handler = (IEventHandler<TEvent>)h;
                        handler.HandleEvent(sender, eventData);
                    }
                    else
                    {
                        handlers.RemoveAt(i);
                    }
                }
            }
        }

        public void Register<TEvent>(IEventHandler<TEvent> handler)
        {
            var eventType = typeof(TEvent);
            if (!_events.TryGetValue(eventType, out var handlers))
            {
                handlers = new List<WeakReference<object>>();
                _events[eventType] = handlers;
            }
            handlers.Add(new WeakReference<object>(handler));
        }

        public void Unregister<TEvent>(IEventHandler<TEvent> handler)
        {
            var eventType = typeof(TEvent);
            if (_events.TryGetValue(eventType, out var handlers))
            {
                for (int i = handlers.Count - 1; i >= 0; i--)
                {
                    if (handlers[i].TryGetTarget(out var h))
                    {
                        if ((IEventHandler<TEvent>)h == handler)
                            handlers.RemoveAt(i);
                    }
                    else
                    {
                        handlers.RemoveAt(i);
                    }
                }
            }
        }
    }

    public interface IEventHandler<TEvent>
    {
        void HandleEvent(object sender, TEvent e);
    }
}

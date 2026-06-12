using System;
using System.Collections.Generic;

public static class EventManager
{
    private static Dictionary<string, Action> _eventsNoParams = new();
    private static Dictionary<string, Delegate> _eventsWithParams = new();

    #region NO PARAMS
    public static void Subscribe(string eventName, Action action)
    {
        if (!_eventsNoParams.ContainsKey(eventName))
            _eventsNoParams[eventName] = action;
        else
            _eventsNoParams[eventName] += action;
    }

    public static void Unsubscribe(string eventName, Action action)
    {
        if (_eventsNoParams.ContainsKey(eventName))
            _eventsNoParams[eventName] -= action;
    }

    public static void Trigger(string eventName)
    {
        if (_eventsNoParams.ContainsKey(eventName))
            _eventsNoParams[eventName]?.Invoke();
    }
    #endregion

    #region WITH PARAMS
    public static void Subscribe<T>(string eventName, Action<T> action)
    {
        if (_eventsWithParams.TryGetValue(eventName, out var existing))
            _eventsWithParams[eventName] = (Action<T>)existing + action;
        else
            _eventsWithParams[eventName] = action;
    }

    public static void Unsubscribe<T>(string eventName, Action<T> action)
    {
        if (_eventsWithParams.TryGetValue(eventName, out var existing))
            _eventsWithParams[eventName] = (Action<T>)existing - action;
    }

    public static void Trigger<T>(string eventName, T data)
    {
        if (_eventsWithParams.TryGetValue(eventName, out var existing))
            (existing as Action<T>)?.Invoke(data);
    }
    #endregion
}
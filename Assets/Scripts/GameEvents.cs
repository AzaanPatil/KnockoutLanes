using UnityEngine.Events;

// Concrete UnityEvent subclasses -- Unity's Inspector only draws typed
// parameter fields for a UnityEvent<T> if it's wrapped in a named,
// [Serializable] subclass like these, rather than used generically inline.
[System.Serializable] public class IntEvent : UnityEvent<int> { }
[System.Serializable] public class FloatEvent : UnityEvent<float> { }
[System.Serializable] public class CheckpointEvent : UnityEvent<int, int> { }

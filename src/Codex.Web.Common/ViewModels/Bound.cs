using System.ComponentModel;

namespace Codex.View
{
    public interface IBound<out T>
    { 
        T Value { get; }

        ValueBinding OnUpdate(Action<T> update, bool skipCurrentValue = false);
    }

    public class Bound<T>(T value = default) : IBound<T>
    {
        // TODO: Change updates
        private T _value = value;
        public T Value
        {
            get => _value;
            set
            {
                _value = value;
                onUpdate?.Invoke(value);
            }
        }

        private Action<T> onUpdate;

        //public T Value { get; set; }

        //public List<Action<T>> onUpdate = new List<Action<T>>();

        public static implicit operator T(Bound<T> bound)
        {
            return bound.Value;
        }

        public static implicit operator Bound<T>(T value)
        {
            return new Bound<T>() { Value = value };
        }

        public Bound<TValue> Select<TValue>(Func<T, TValue> select)
        {
            var result = new Bound<TValue>();
            OnUpdate(value => result.Value = select(value));
            return result;
        }

        public ValueBinding OnUpdate(Action<T> update, bool skipCurrentValue = false)
        {
            onUpdate += update;
            //onUpdate.Add(update);
            if (!skipCurrentValue)
            {
                update(Value);
            }

            return default;
        }
    }

    public class ValueBinding
    {
    }
}

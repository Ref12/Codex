using System.Collections.Generic;

namespace Codex.Utilities
{
    public class ListSet<T>
    {
        public List<T> List { get; set; } = new List<T>();
        private readonly Dictionary<T, int> map;

        public ListSet(IEqualityComparer<T> equalityComparer = null)
        {
            map = new Dictionary<T, int>(equalityComparer ?? EqualityComparer<T>.Default);
        }

        public int Add(T value)
        {
            if (!map.TryGetValue(value, out var index))
            {
                index = List.Count;
                map.Add(value, index);
                List.Add(value);
            }

            return index;
        }
    }
}

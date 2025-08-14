using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Codex.ObjectModel
{
    public record FieldListVisitor(List<(string Name, object Value)> Fields = null) : ValueVisitorBase, IStandardReadOnlyDictionary<string, object>
    {
        public List<(string Name, object Value)> Fields { get; } = Fields ?? new();

        public override bool HandlesNoneBehavior => true;

        public int Count => throw new NotImplementedException();

        public void Reset()
        {
            Fields.Clear();
            _lastName = null;
        }

        private string _lastName;

        public void Add<T>(IMappingField mapping, T value)
        {
            if (mapping.BehaviorInfo.IsHashExcluded) return;

            var name = mapping.Name;
            if (name == _lastName)
            {
                name = null;
            }
            else
            {
                _lastName = name;
            }

            Fields.Add((name, value));
        }

        public override void Visit(IMappingField mapping, long value)
        {
            Add(mapping, value);
        }

        public override void Visit(IMappingField mapping, string value, SearchBehaviorInfo behaviorInfo)
        {
            if (value != null)
            {
                Add(mapping, value);
            }
        }

        public override void VisitBinaryItem<T>(IMappingField mapping, T value)
        {
            Add(mapping, value);
        }

        public override void Visit(IMappingField mapping, TextSourceBase value)
        {
            Add(mapping, value);
        }

        public bool TryGetValue(string key, [MaybeNullWhen(false)] out object value)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            foreach (var group in Fields.SortedGroupBy(f => f.Name))
            {
                yield return new(group.Key, group.Items.Count == 1 ? group.Items[0] : group.Items);
            }
        }
    }
}
using System.Diagnostics.ContractsLight;

namespace Codex.ObjectModel
{
    public abstract record ValueVisitorBase : IVisitor
    {
        private static SearchBehaviorInfo TermBehavior = new SearchBehaviorInfo(SearchBehavior.Term);

        public abstract bool HandlesNoneBehavior { get; }

        public abstract void Visit(IMappingField mapping, long value);

        public abstract void Visit(IMappingField mapping, string value, SearchBehaviorInfo behaviorInfo);

        public abstract void VisitBinaryItem<T>(IMappingField mapping, T value)
            where T : struct, IBinaryItem<T>;

        public virtual void Visit(IMappingField mapping, DateTime value)
        {
            Visit(mapping, value.Ticks);
        }

        public virtual void Visit(IMappingField mapping, bool value)
        {
            Visit(mapping, value ? bool.TrueString : bool.FalseString);
        }

        public virtual void Visit(IMappingField mapping, ReadOnlyMemory<byte> value)
        {
            VisitBinaryItem(mapping, BinaryItem.Create(value));
        }

        public virtual void Visit(IMappingField mapping, MurmurHash value)
        {
            Visit(mapping, value.ToShortHash());
        }

        public void Visit(IMappingField mapping, ShortHash value)
        {
            VisitBinaryItem(mapping, BinaryItem.Create(value));
        }

        public void Visit(IMappingField mapping, string value)
        {
            Visit(mapping, value, mapping.BehaviorInfo);
        }

        public void Visit(IMappingField mapping, SymbolId value)
        {
            Visit(mapping, value.Value);
        }

        public void Visit(IMappingField mapping, int value)
        {
            Visit(mapping, (long)value);
        }

        public void Visit(IMappingField mapping, ReferenceKind value)
        {
            Visit(mapping, value.ToString());
        }

        public void Visit(IMappingField mapping, StringEnum<SymbolKinds> value)
        {
            Visit(mapping, value.ToDisplayString());
        }

        public void Visit(IMappingField mapping, StringEnum<PropertyKey> value)
        {
            Visit(mapping, value.ToDisplayString());
        }

        public void Visit(IMappingField mapping, ReferenceKindSet value)
        {
            switch (mapping.Behavior)
            {
                case SearchBehavior.Sortword:
                    foreach (var kind in value.Enumerate())
                    {
                        Visit(mapping, kind.ToString(), TermBehavior);
                    }

                    Visit(mapping, value.Value.CastToSigned());
                    break;
            }

            Contract.AssertFailure(
                $"Field {mapping.Name} must has unexpected search behavior '{mapping.Behavior}'.");
        }

        public abstract void Visit(IMappingField mapping, TextSourceBase value);
    }
}
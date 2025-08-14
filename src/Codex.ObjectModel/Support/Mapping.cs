namespace Codex.ObjectModel
{
    public class MappingInfo
    {
        public string Name { get; }
        public string FullName { get; }
        public string NGramFullName { get; }
        public SearchBehavior? SearchBehavior { get; }
        public ObjectStage ObjectStage { get; }

        public MappingInfo(string name, MappingInfo parent, SearchBehavior? searchBehavior, ObjectStage objectStage = ObjectStage.All)
        {
            Name = name;
            FullName = parent?.Name == null
                ? Name
                : string.Join(".", parent.Name, Name);

            NGramFullName = $"{FullName}-ngram";
            SearchBehavior = searchBehavior;
            ObjectStage = objectStage;
        }
    }

    [GeneratorExclude]
    public interface IMappingField
    {
        int Index { get; }

        string Name { get; }

        SearchBehavior Behavior { get; }

        SearchBehaviorInfo BehaviorInfo { get; }
    }

    public interface IMappingField<TMappingType> : IMappingField
    {
        void Visit(TMappingType entity, IVisitor visitor);
    }

    public interface IMappingField<TMappingType, TFieldType> : IMappingField<TMappingType>
    { 
    }

    public interface ISortField<TMappingType> : IMappingField<TMappingType>
    {
    }

    public interface ISortField<TMappingType, TFieldType> : IMappingField<TMappingType, TFieldType>
    {
    }
}
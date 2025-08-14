using Codex.Sdk.Utilities;

namespace Codex.ObjectModel
{
    public enum ObjectStage
    {
        None = 0,
        Analysis = 1,
        Index = 1 << 1,
        All = Index | Analysis,
        StoreRaw = 1 << 3 | All,
        BlockIndex = 1 << 4 | Index,

        // Excluding stages
        Hash = 1 << 10 | Index,

        OptimizedStore = 1 << 11 | All,
    }

    [GeneratorExclude]
    public interface IObjectStage
    {
        static virtual ObjectStage GetValue() => ObjectStage.None;

        static virtual IBox Box => default;
    }

    namespace Internal
    {
        public abstract class ObjectStageBase<TStage>
            where TStage : IObjectStage
        {
            public static IBox<IObjectStage> Box { get; } = (IBox<IObjectStage>)new Box<TStage>();
        }
    }
}

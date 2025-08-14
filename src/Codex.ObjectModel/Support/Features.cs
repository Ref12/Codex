using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Codex
{
    /// <summary>
    /// Defines on/off state of experimental features
    /// </summary>
    public static class Features
    {
        public static readonly FeatureSwitch<bool> DebugBigSet = false;
        public static readonly FeatureSwitch<bool> IsTest = false;
        public static readonly FeatureSwitch<bool> AllowValidateBlocks = true;

        public static readonly FeatureSwitch<Dbg> DebugScenarios = Dbg.None;

        public static readonly IFeatureSwitch<bool> ValidateBlocks = new FuncFeatureSwitch<bool>(() => IsTest && AllowValidateBlocks);

        public static readonly FeatureSwitch<bool> ComputeStableIdExtents = IsTest;

        public static readonly FeatureSwitch<bool> AddDefinitionForInheritedInterfaceImplementations = false;

        public static readonly FeatureSwitch<bool> VerifyEntityRoundtrip = false;

        public static readonly FeatureSwitch<bool> UseJsonDictionaryPropertyMap = true;

        public static readonly FeatureSwitch<bool> HideDefaultBranding = false;

        public static readonly FeatureSwitch<bool> AddReferenceDefinitions = true;

        public static readonly FeatureSwitch<bool> UseGitForSourceFileContentStorage = true;

        public static readonly FeatureSwitch<bool> UseExternalStorage = false;

        public static readonly FeatureSwitch<bool> TrackRangesOnRead = false;

        public static readonly FeatureSwitch<bool> ColumnStoreReferenceInfo = false;

        public static readonly FeatureSwitch<bool> EnableSummaryIndex = false;

        public static readonly FeatureSwitch<bool> TrackOpenFiles = false;

        public static readonly FeatureSwitch<bool> RebuildDocIdSetsOnLoad = false;

        public static readonly FeatureSwitch<bool> EnableRangeDocIdSets = true;

        public static readonly FeatureSwitch<bool> RedirectWorkflowStandardOut = false;

        public static readonly FeatureSwitch<bool> EnableIndexChecksum = true;

        public static readonly FeatureSwitch<bool> TestBoolFeature = true;

        public static ImmutableDictionary<string, IFeatureSwitch<bool>> FeaturesByName { get; }

        public const string FeatureEnvPrefix = "CodexFeature_";

        public static IDisposable EnableWithGlobal<T>(this FeatureSwitch<T> feature, T value)
            where T : unmanaged, Enum
        {
            return feature.EnableGlobal(feature.Value.Or(value));
        }

        static Features()
        {
            FeaturesByName = GetFeaturesByName();
        }

        public static ImmutableDictionary<string, IFeatureSwitch<bool>> GetFeaturesByName()
        {
            var builder = ImmutableDictionary.CreateBuilder<string, IFeatureSwitch<bool>>();
            foreach (var field in typeof(Features).GetFields())
            {
                var fieldValue = field.GetValue(null);
                if (fieldValue is FeatureSwitchBase baseFeature)
                {
                    baseFeature.SetStringValue(Environment.GetEnvironmentVariable(FeatureEnvPrefix + field.Name));
                }

                if (field.GetValue(null) is IFeatureSwitch<bool> feature)
                {
                    builder[field.Name] = feature;
                }
            }

            return builder.ToImmutable();
        }

        private record FuncFeatureSwitch<T>(Func<T> GetValue) : IFeatureSwitch<T>
        {
            public T Value => GetValue();
        }
    }

    public interface IFeatureSwitch<T>
    {
        T Value { get; }
    }

    public class TypeParser
    {
        public static T ParseOrDefault<T>(string s)
            where T : IParsable<T>
        {
            if (T.TryParse(s, null, out var result))
            {
                return result;
            }
            else return default(T);
        }
    }

    public class FeatureSwitch<T> : FeatureSwitchBase, IFeatureSwitch<T>
    {
        private T globalValue;
        private AsyncLocal<Optional<T>> localValue = null;

        public T Value => localValue != null && localValue.Value.IsSet ? localValue.Value.Value : globalValue;

        public FeatureSwitch(T initialGlobalValue)
            : this()
        {
            globalValue = initialGlobalValue;
        }

        public FeatureSwitch()
        {
        }

        public interface IFeatureScope : IDisposable
        {
            T Value { get;  set; }
        }

        private class FeatureScope(FeatureSwitch<T> feature, bool local, Optional<T> priorValue) : IFeatureScope
        {
            public T Value
            {
                get => feature.Value;
                set
                {
                    if (local) feature.localValue.Value = new(value);
                    else feature.globalValue = value;
                }
            }

            public void Dispose()
            {
                if (local) feature.localValue.Value = priorValue;
                else feature.globalValue = priorValue.Value;
            }
        }

        public IFeatureScope EnableLocal(T enabled = default)
        {
            localValue = localValue ?? new AsyncLocal<Optional<T>>();
            var priorValue = localValue.Value;
            localValue.Value = new(enabled);
            return new FeatureScope(this, true, priorValue);
        }

        public IFeatureScope EnableGlobal(T enabled = default)
        {
            var priorValue = globalValue;
            globalValue = enabled;
            return new FeatureScope(this, false, new(priorValue));
        }

        public static implicit operator T(FeatureSwitch<T> f)
        {
            return f.Value;
        }

        public static implicit operator FeatureSwitch<T>(T enabled)
        {
            return new FeatureSwitch<T>(enabled);
        }

        public override void SetStringValue(string value)
        {
            if (value != null)
            {
                var parsedValue = TypeHelper<T>.ParseOrDefault(value);
                if (parsedValue.IsSet)
                {
                    globalValue = parsedValue.Value;
                }
            }
        }
    }

    public class FeatureSwitchBase
    {
        protected record struct Optional<T>(T Value, bool IsSet = true);

        public virtual void SetStringValue(string value)
        {
        }

        protected class TypeHelper<T>
        {
            public static Func<string, Optional<T>> ParseOrDefault { get; } = (Func<string, Optional<T>>)GetParseHelper();

            private static Delegate GetParseHelper()
            {
                if (typeof(T) == typeof(bool))
                {
                    return new Func<string, Optional<bool>>(s => bool.TryParse(s, out var result) ? new(result) : default);
                }
                else if (typeof(T) == typeof(string))
                {
                    return new Func<string, Optional<string>>(s => new(s));
                }
                else
                {
                    return new Func<string, Optional<T>>(s => default);
                }
            }
        }
    }
}

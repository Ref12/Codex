using System.Collections.Immutable;
using System.Numerics;
using Codex.Sdk;
using Codex.Utilities.Serialization;

namespace Codex.ObjectModel
{
    /// <summary>
    /// Defines standard set of reference kinds
    /// </summary>
    public enum ReferenceKind : byte
    {
        None = 0,
        Definition,

        /// <summary>
        /// This represents a constructor declaration for the given type. This is different than
        /// instantiation which actually represents a call to the constructor
        /// </summary>
        Constructor,

        /// <summary>
        /// A call to the constructor of the type referenced by the symbol. This is different than
        /// constructor which is the actual declaration for a constructor for the type symbol.
        /// </summary>
        Instantiation,

        DerivedType,
        TypeForwardedTo,
        InterfaceInheritance,
        InterfaceImplementation,
        Override,
        InterfaceMemberImplementation,

        Write,
        Read,
        GuidUsage,
        UsingDispose,

        /// <summary>
        /// The symbol is the return type of a method or property getter
        /// </summary>
        ReturnType,

        ExplicitCast,

        // ParameterType? - type of a parameter

        EmptyArrayAllocation,

        /// <summary>
        /// Usage of msbuild entity (item metadata, property, or item group).
        /// This is separate because unlike <see cref="Read"/> this should take precedence
        /// over writes which are more common in MSBuild.
        /// </summary>
        MSBuildDefinition,
        MSBuildUsage,

        Text, // full-text-search result

        ProjectLevelReference,

        /// <summary>
        /// Catch-all reference comes after more specific reference kinds
        /// </summary>
        Reference,

        Getter,
        Setter,
        Partial,

        /// <summary>
        /// Represents the copy performed by with expression in C#
        /// </summary>
        CopyWith,
    }

    public record struct ReferenceKindSet(ulong Value) : IJsonConvertible<ReferenceKindSet, ulong>
    {
        public static readonly ReferenceKindSet Empty = default;

        public static readonly ReferenceKindSet AllKinds = From(Enum.GetValues<ReferenceKind>());

        public static readonly ReferenceKindSet DefinitionKinds = From(new[] { ReferenceKind.Definition }); 

        public void Add(ReferenceKind kind)
        {
            Value |= (1ul << (int)kind);
        }

        public ulong ValidValue => Value & AllKinds.Value;

        public bool IsEmpty => ValidValue == 0;

        public int Count => BitOperations.PopCount(ValidValue);

        public ReferenceKind GetFirst()
        {
            var value = ValidValue;
            if (value == 0) return ReferenceKind.None;

            return (ReferenceKind)ulong.TrailingZeroCount(value);
        }

        private IEnumerable<ReferenceKind> AsArray => Enumerate().ToArray();

        public static ReferenceKindSet From(IEnumerable<ReferenceKind> kinds)
        {
            var result = new ReferenceKindSet();
            foreach (var value in kinds)
            {
                result.Add(value);
            }
            return result;
        }

        public EnumeratorState Enumerate()
        {
            return new EnumeratorState(IntHelpers.EnumerateSetBits(ValidValue));
        }

        public record struct EnumeratorState(ValueEnumerator<ulong, ulong> Iterator) : IValueEnumerable<EnumeratorState, ReferenceKind>
        {
            // Make this a field.
            private ValueEnumerator<ulong, ulong> Iterator = Iterator;

            public static bool TryMoveNext(ref EnumeratorState state, out ReferenceKind kind)
            {
                if (state.Iterator.TryMoveNext(out var current))
                {
                    kind = (ReferenceKind)current;
                    return true;
                }
                else
                {
                    kind = default;
                    return false;
                }
            }

            public ValueEnumerator<EnumeratorState, ReferenceKind> GetEnumerator()
            {
                return new(this, TryMoveNext);
            }
        }

        public bool Contains(ReferenceKind kind)
        {
            return (GetFlag(kind) & Value) != 0;
        }

        public bool IsSupersetOf(ReferenceKindSet other)
        {
            return (Value & other.Value) == other.Value;
        }

        public bool IsSubsetOf(ReferenceKindSet other)
        {
            return (Value & other.Value) == Value;
        }

        private static ulong GetFlag(ReferenceKind kind)
        {
            return 1ul << (int)kind;
        }

        public static ReferenceKindSet ConvertFromJson(ulong jsonFormat)
        {
            return new(jsonFormat);
        }

        public ulong ConvertToJson()
        {
            return Value;
        }

        public static implicit operator ReferenceKindSet(ReferenceKind kind)
        {
            return new ReferenceKindSet(GetFlag(kind));
        }

        public static ReferenceKindSet operator &(ReferenceKindSet left, ReferenceKind right)
        {
            return left with { Value = left.Value & GetFlag(right) };
        }

        public static ReferenceKindSet operator &(ReferenceKindSet left, ReferenceKindSet right)
        {
            return new(left.Value & right.Value);
        }

        public static ReferenceKindSet operator |(ReferenceKindSet left, ReferenceKind right)
        {
            return left with { Value = left.Value | GetFlag(right) };
        }
    }

    public static class ReferenceKindExtensions
    {
        public static ImmutableArray<ReferenceKind> ReferenceKindPreferenceList = new ReferenceKind[]
        {
            ReferenceKind.Definition,
            ReferenceKind.TypeForwardedTo,
            ReferenceKind.Constructor,
            ReferenceKind.DerivedType,
            ReferenceKind.InterfaceInheritance,
            ReferenceKind.Instantiation,
            ReferenceKind.CopyWith,
            ReferenceKind.Override,
            ReferenceKind.InterfaceImplementation,
            ReferenceKind.InterfaceMemberImplementation,

            ReferenceKind.MSBuildUsage,
            ReferenceKind.Write,
            ReferenceKind.Read,
            ReferenceKind.ExplicitCast,

        }.ToImmutableArray();

        public static ImmutableArray<ReferenceKindSet> ReferenceKindPreferenceSetMaskList = GetMaskList(ReferenceKindPreferenceList);

        private static ImmutableArray<ReferenceKindSet> GetMaskList(ImmutableArray<ReferenceKind> referenceKindPreferenceList)
        {
            var list = ImmutableArray.CreateBuilder<ReferenceKindSet>();
            var used = new ReferenceKindSet();

            while (used.Count != referenceKindPreferenceList.Length)
            {
                var current = new ReferenceKindSet();
                var lastKind = default(ReferenceKind);

                for (int i = 0; i < referenceKindPreferenceList.Length; i++)
                {
                    var kind = referenceKindPreferenceList[i];
                    if (used.Contains(kind)) continue;

                    if (lastKind <= kind)
                    {
                        current.Add(kind);
                        used.Add(kind);
                        lastKind = kind;
                    }
                    else
                    {
                        break;
                    }
                }

                list.Add(current);
            }

            return list.ToImmutableArray();
        }

        private const byte DefaultPreference = byte.MaxValue;

        public static ReferenceKindSet ReferenceKindPreferenceSet = ReferenceKindSet.From(ReferenceKindPreferenceList);

        public static ImmutableArray<byte> ReferenceKindPreferenceMap = ImmutableArray.CreateBuilder<byte>((int)EnumData<ReferenceKind>.Max + 1)
            .Apply(array => Enumerable.Range(0, array.Capacity).ForEachIndex(i => array.Add(DefaultPreference)))
            .Apply(array => ReferenceKindPreferenceList.ForEachIndex(e => array[(int)e.Item] = (byte)e.Index))
            .ToImmutable();

        private static ReferenceKindSet GetterFindAllReferenceKinds = 
            ReferenceKindSet.Empty | ReferenceKind.Read | ReferenceKind.Definition;

        private static ReferenceKindSet SetterFindAllReferenceKinds = 
            ReferenceKindSet.Empty | ReferenceKind.Write | ReferenceKind.Definition;

        private static ReferenceKindSet PartialFindAllReferenceKinds =
            ReferenceKindSet.Empty | ReferenceKind.Definition;

        public static int GetPreference(this ReferenceKindSet kinds)
        {
            var value = kinds.Value & ReferenceKindPreferenceSet.Value;
            if (value == 0)
            {
                return DefaultPreference;
            }
            else if (BitOperations.PopCount(value) == 1)
            {
                var kind = (ReferenceKind)BitOperations.TrailingZeroCount(value);
                return GetPreference(kind);
            }
            else
            {
                foreach (var maskSet in ReferenceKindPreferenceSetMaskList)
                {
                    var kind = (maskSet & kinds).GetFirst();
                    if (kind != ReferenceKind.None)
                    {
                        return GetPreference(kind);
                    }
                }
            }

            return DefaultPreference;
        }

        public static int GetPreference(this ReferenceKind kind)
        {
            return ReferenceKindPreferenceMap.GetOrDefault((int)kind, DefaultPreference);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="kind"></param>
        /// <remarks>
        /// Changes this should also change <see cref="ReferenceKindSet.DefinitionKinds"/>
        /// </remarks>
        /// <returns></returns>
        public static bool IsDefinition(this ReferenceKind kind) => kind == ReferenceKind.Definition;

        public static ReferenceKindSet? FindAllReferenceKinds(this ReferenceKind kind, bool fallbackToNull = false)
        {
            switch (kind)
            {
                case ReferenceKind.Getter:
                    return GetterFindAllReferenceKinds;
                case ReferenceKind.Setter:
                    return SetterFindAllReferenceKinds;
                case ReferenceKind.Partial:
                    return PartialFindAllReferenceKinds;
                default:
                    return fallbackToNull ? null : kind;
            }
        }

    }
}

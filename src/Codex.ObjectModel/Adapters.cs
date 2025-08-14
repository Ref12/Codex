using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using Codex.ObjectModel;

namespace Codex.ObjectModel
{
    [AdapterType]
    public interface ISymbolLineSpanListModel
    {
    }

    [AdapterType]
    public interface IIntArray : IReadOnlyList<int>
    {
    }

    [AdapterType]
    public interface IClassificationListModel
    {
    }

    [AdapterType]
    public interface IReferenceListModel
    {
        IReadOnlyList<IReferenceSymbol> SharedValues { get; }
    }

    [AdapterType]
    public interface ISharedReferenceInfoSpanModel
    {
        IReadOnlyList<ISharedReferenceInfo> SharedValues { get; }
    }

    public interface ISharedReferenceInfo
    {
        /// <summary>
        /// <see cref="IReferenceSpan.RelatedDefinition"/>
        /// </summary>
        SymbolId RelatedDefinition { get; }

        /// <summary>
        /// <see cref="IReferenceSymbol.ReferenceKind"/>
        /// </summary>
        ReferenceKind ReferenceKind { get; }

        /// <summary>
        /// <see cref="IReferenceSymbol.ExcludeFromSearch"/>
        /// </summary>
        bool ExcludeFromSearch { get; }
    }
}

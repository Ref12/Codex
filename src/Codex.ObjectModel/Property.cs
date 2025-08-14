using System;
using System.Collections.Generic;
using System.Text;

namespace Codex.ObjectModel
{
    public interface IPropertySearchModel : ISearchEntity<IPropertySearchModel>
    {
        /// <summary>
        /// The key of the property
        /// </summary>
        [SearchBehavior(SearchBehavior.NormalizedKeyword)]
        StringEnum<PropertyKey> Key { get; }

        /// <summary>
        /// The value of the property
        /// </summary>
        [SearchBehavior(SearchBehavior.NormalizedKeyword)]
        string Value { get; }

        /// <summary>
        /// The identifier of the object owning this property
        /// </summary>
        [SearchBehavior(SearchBehavior.Term)]
        int OwnerId { get; }
    }

    [AdapterType]
    public interface IPropertyMap : IDictionary<StringEnum<PropertyKey>, string>
    {

    }
}

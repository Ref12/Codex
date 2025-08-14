using System;
using System.Collections.Generic;
using System.Text;
using Codex.ObjectModel;

namespace Codex
{
    /// <summary>
    /// Defines documentation about a symbol
    /// </summary>
    [GeneratorExclude]
    public interface IDocumentationInfo
    {
        /// <summary>
        /// The declaration name for the symbol
        /// </summary>
        string DeclarationName { get; }

        /// <summary>
        /// The comment applied to the definition (this is the raw doc comment text)
        /// </summary>
        string Comment { get; }

        /// <summary>
        /// Identity of symbol which generated this documentation
        /// </summary>
        ICodeSymbol AssociatedSymbol { get; }

        /// <summary>
        /// The other symbols referenced by this symbol (i.e. base type, implemented interface
        /// or interface members, overridden members, return type of method or property type)
        /// </summary>
        IReadOnlyList<IDocumentationReferenceSymbol> References { get; }

        /// <summary>
        /// Describes the symbol
        /// </summary>
        string Summary { get; }

        /// <summary>
        /// Supplementation information about the symbol
        /// </summary>
        string Remarks { get; }

        /// <summary>
        /// Comments on arguments
        /// </summary>
        IReadOnlyList<ITypedParameterDocumentation> Arguments { get; }

        /// <summary>
        /// Comments on type parameters
        /// </summary>
        IReadOnlyList<IParameterDocumentation> TypeParameters { get; }

        /// <summary>
        /// Information about exceptions which can be thrown
        /// </summary>
        IReadOnlyList<ITypedParameterDocumentation> Exceptions { get; }
    }

    /// <summary>
    /// Documentation for a method parameter
    /// </summary>
    public interface IParameterDocumentation
    {
        /// <summary>
        /// The name of the parameter
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The comment associated with the symbol
        /// </summary>
        string Comment { get; }
    }

    /// <summary>
    /// Documentation for typed parameter
    /// </summary>
    [GeneratorExclude]
    public interface ITypedParameterDocumentation : IParameterDocumentation
    {
        /// <summary>
        /// The type of the parameter
        /// </summary>
        IDocumentationReferenceSymbol Type { get; }
    }

    /// <summary>
    /// Decribes a symbol related to a given documented symbol
    /// </summary>
    public interface IDocumentationReferenceSymbol : IReferenceSymbol
    {
        /// <summary>
        /// The display name of the symbol reference as it should appear in documentation
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// The comment associated with the reference
        /// (i.e. return type description https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/xmldoc/returns)
        /// </summary>
        string Comment { get; }
    }
}

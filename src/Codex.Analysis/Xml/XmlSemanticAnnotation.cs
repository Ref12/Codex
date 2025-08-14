namespace Codex.Analysis.Xml.Linq
{
    public class XmlSemanticAnnotation
    {
        public bool AppliesToName;
        public DefinitionSymbol Definition;
        public ReferenceSymbol[] References;

        public static implicit operator XmlSemanticAnnotation(ReferenceSymbol[] value)
        {
            return new XmlSemanticAnnotation() { References = value };
        }

        public static implicit operator XmlSemanticAnnotation(ReferenceSymbol value)
        {
            if (value == null)
            {
                return null;
            }

            return new XmlSemanticAnnotation() { References = new[] { value } };
        }

        public static implicit operator XmlSemanticAnnotation(DefinitionSymbol value)
        {
            if (value == null)
            {
                return null;
            }

            return new XmlSemanticAnnotation() { Definition = value };
        }
    }
}

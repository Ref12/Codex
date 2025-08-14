using System;

namespace Codex.ObjectModel
{
    /// <summary>
    /// Defines Codex glyphs
    /// </summary>
    public enum Glyph : ushort
    {
        Unknown = 0,

        // GlyphGroupClass
        ClassPublic = 1,
        ClassInternal = 2,
        ClassProtected = 3,
        ClassPrivate = 4,

        // GlyphGroupConstant
        ConstantPublic = 6,
        ConstantInternal = 8,
        ConstantProtected = 9,
        ConstantPrivate = 10,

        // GlyphGroupDelegate
        DelegatePublic = 12,
        DelegateInternal = 14,
        DelegateProtected = 15,
        DelegatePrivate = 16,

        // GlyphGroupEnum
        EnumPublic = 18,
        EnumInternal = 20,
        EnumProtected = 21,
        EnumPrivate = 22,

        // GlyphGroupEnumMember
        EnumMember = 24,

        // GlyphGroupEvent
        EventPublic = 30,
        EventInternal = 32,
        EventProtected = 33,
        EventPrivate = 34,

        // GlyphGroupField
        FieldPublic = 42,
        FieldInternal = 44,
        FieldProtected = 45,
        FieldPrivate = 46,

        // GlyphGroupInterface
        InterfacePublic = 48,
        InterfaceInternal = 50,
        InterfaceProtected = 51,
        InterfacePrivate = 52,

        // GlyphGroupMethod
        MethodPublic = 72,
        MethodInternal = 74,
        MethodProtected = 75,
        MethodPrivate = 76,

        // GlyphGroupModule
        ModulePublic = 84,
        ModuleInternal = 86,
        ModuleProtected = 87,
        ModulePrivate = 88,

        // GlyphGroupNamespace
        Namespace = 90,

        // GlyphGroupOperator
        Operator = 96,

        // GlyphGroupProperty
        PropertyPublic = 102,
        PropertyInternal = 104,
        PropertyProtected = 105,
        PropertyPrivate = 106,

        // GlyphGroupStruct
        StructurePublic = 108,
        StructureInternal = 110,
        StructureProtected = 111,
        StructurePrivate = 112,

        // GlyphGroupType
        TypeParameter = 126,

        // GlyphGroupVariable
        Local = 138,
        Parameter = 139,
        RangeVariable = 140,

        // GlyphGroupIntrinsic
        Intrinsic = 150,
        Label = 151,

        // GlyphGroupError
        Error = 186,

        // GlyphAssembly
        Assembly = 192,

        // GlyphVBProject
        BasicProject = 194,

        // GlyphCoolProject
        CSharpProject = 196,

        // GlyphOpenFolder
        OpenFolder = 201,

        // GlyphClosedFolder
        ClosedFolder = 202,

        // GlyphCSharpFile
        CSharpFile = 204,

        // GlyphKeyword
        Keyword = 206,

        GenericFile = 212,

        // GlyphExtensionMethod
        ExtensionMethodPublic = 220,
        ExtensionMethodInternal = 221,
        ExtensionMethodProtected = 223,
        ExtensionMethodPrivate = 224,

        // GlyphReference
        Reference = 236,
        ReferenceGroup = 237,
        ShowReferencedElements = 238,

        // GlyphArrow
        Up = 240,
        Down = 241,
        Left = 242,
        Right = 243,
        Dot = 244,

        // GlyphGroupUnknown
        BasicFile = 249,
        Snippet = 319,
        Metadata = 250,
    }

    public static class GlyphUtilities
    {
        public static string GetGlyph(this IDefinitionSymbol s, string filePath = null)
        {
            var glyph = s.Glyph;
            if (glyph != Glyph.Unknown)
            {
                return GetGlyphIcon(glyph);
            }

            if (s.Kind == SymbolKinds.File)
            {
                filePath = !string.IsNullOrEmpty(filePath) ? filePath : s.ShortName;
                if (!string.IsNullOrEmpty(filePath))
                {
                    return GetFileNameGlyph(filePath);
                }

                return "212";
            }

            return "0";
        }

        public static string GetGlyphIcon(this Glyph glyph)
        {
            if (glyphIconMap.TryGetValue(glyph, out var icon))
            {
                return icon;
            }

            return glyph.GetGlyphNumber().ToString();
        }

        private static readonly Dictionary<Glyph, string> glyphIconMap = new()
        {
            [Glyph.BasicFile] = "vb"
        };

        private static readonly Dictionary<string, StringEnum<Glyph>> extensionGlyphMap = new(StringComparer.OrdinalIgnoreCase)
        {
            [".cs"] = "csharp",
            [".vb"] = "vb",
            [".ts"] = "typescript",
            //[".xml"] = "xml",
            [".xaml"] = "xaml",
            [".csproj"] = Glyph.CSharpProject,
            [".vbproj"] = Glyph.BasicProject
        };

        public static string GetFileNameGlyph(string fileName)
        {
            var extension = PathUtilities.GetExtension(fileName);
            if (extensionGlyphMap.TryGetValue(extension, out var glyph))
            {
                if (glyph.Value != null)
                {
                    return glyph.Value.Value.GetGlyphIcon();
                }
                else
                {
                    return glyph.StringValue;
                }
            }

            return "212";
        }

        public static ushort GetGlyphNumber(this Glyph glyph)
        {
            return (ushort)glyph;
        }
    }
}

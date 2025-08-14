using Microsoft.Language.Xml;

namespace Codex.Analysis
{
    public static class XmlAnalyzer
    {
        private static string[] ClassificationTypeNamesLookup = new string[]
        {
            "text",
            ClassificationTypeNames.XmlAttributeName,
            ClassificationTypeNames.XmlAttributeQuotes,
            ClassificationTypeNames.XmlAttributeValue,
            ClassificationTypeNames.XmlCDataSection,
            ClassificationTypeNames.XmlComment,
            ClassificationTypeNames.XmlDelimiter,
            ClassificationTypeNames.XmlEntityReference,
            ClassificationTypeNames.XmlName,
            ClassificationTypeNames.XmlProcessingInstruction,
            ClassificationTypeNames.XmlText,
        };

        public static XmlDocumentSyntax Analyze(BoundSourceFileBuilder binder)
        {
            var text = binder.SourceFile.Content;
            XmlDocumentSyntax parsedXml = binder is XmlSourceFileBuilder xmlBinder ? xmlBinder.Document : Parser.ParseText(text);
            Classify(binder, parsedXml);
            return parsedXml;
        }

        public static bool IsXml(string text)
        {
            if (text.Length > 10)
            {
                if (text.StartsWith("<?xml"))
                {
                    return true;
                }
                else
                {
                    for (int i = 0; i < text.Length; i++)
                    {
                        var ch = text[i];
                        if (ch == '<')
                        {
                            for (int j = text.Length - 1; j >= 0; j--)
                            {
                                ch = text[j];

                                if (ch == '>')
                                {
                                    return true;
                                }
                                else if (!char.IsWhiteSpace(ch))
                                {
                                    return false;
                                }
                            }

                            return false;
                        }
                        else if (!char.IsWhiteSpace(ch))
                        {
                            return false;
                        }
                    }
                }
            }

            return false;
        }

        public static void Classify(BoundSourceFileBuilder binder, XmlDocumentSyntax parsedXml)
        {
            ClassifierVisitor.Visit(parsedXml, 0, parsedXml.FullWidth, (start, length, node, classification) =>
            {
                var leadingTriviaWidth = node.GetLeadingTriviaWidth();
                var trailingTriviaWidth = node.GetTrailingTriviaWidth();
                start += leadingTriviaWidth;
                length -= (leadingTriviaWidth + trailingTriviaWidth);

                binder.AnnotateClassification(start, length, ClassificationTypeNamesLookup[(int)classification]);
            });
        }
    }
}

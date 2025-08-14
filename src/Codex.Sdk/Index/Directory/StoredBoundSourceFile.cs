using Codex.ObjectModel;
using Codex.ObjectModel.Implementation;
using Codex.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using static Codex.Utilities.SerializationUtilities;

namespace Codex.ObjectModel.Implementation
{
    public partial class StoredBoundSourceFile : EntityBase
    {
        public void BeforeSerialize(bool optimize, bool optimizeLineInfo = true, Action<string> logOptimizationIssue = null)
        {
            PopulateSourceFileLines();

            ReferenceSpan lastReference = null;
            foreach (var reference in BoundSourceFile.References)
            {
                if (lastReference != null 
                    && reference.Start != lastReference.Start 
                    && reference.Start < lastReference.End())
                {
                    logOptimizationIssue?.Invoke($"Overlapping spans: LastReference=({lastReference}) Current=({reference})");
                }

                lastReference = reference;
            }

            if (optimize)
            {
                string projectId = this.BoundSourceFile?.ProjectId;
                string containerQualifiedName = null;
                StringEnum<SymbolKinds> kind = default;
                Glyph glyph = default(Glyph);

                foreach (var definitionSpan in this.BoundSourceFile.Definitions)
                {
                    // Clone definition so references which are actually definitions do not get
                    // modified
                    var definition = new DefinitionSymbol(definitionSpan.Definition);
                    definitionSpan.Definition = definition;

                    definition.ProjectId = RemoveDuplicate(definition.ProjectId, ref projectId);
                    definition.Kind = RemoveDuplicate(definition.Kind, ref kind);
                    definition.ContainerQualifiedName = RemoveDuplicate(definition.ContainerQualifiedName, ref containerQualifiedName);
                    definition.Glyph = RemoveDuplicate(definition.Glyph, ref glyph);
                }

                CompressedClassifications = ClassificationListModel.CreateFrom(BoundSourceFile.Classifications);
                CompressedReferences = ReferenceListModel.CreateFrom(BoundSourceFile.References, includeLineInfo: true, externalLineTextPersistence: optimizeLineInfo);
                //OutliningRegions = ReferenceListModel.CreateFrom(BoundSourceFile.OutliningRegions, includeLineInfo: false);

                //BoundSourceFile.References = Array.Empty<ReferenceSpan>();
                //BoundSourceFile.Classifications = Array.Empty<ClassificationSpan>();
            }
        }

        private void PopulateSourceFileLines()
        {
            var content = this.BoundSourceFile?.SourceFile?.Content;
            if (content == null)
            {
                return;
            }

            this.BoundSourceFile.SourceFile.Content = null;

            SourceFileContentLines = new List<string>(content.GetLines(includeLineBreak: true));

            Debug.Assert(SourceFileContentLines.Sum(l => l.Length) == content.Length);
        }


        public void AfterDeserialization()
        {
            if (CompressedClassifications != null)
            {
                BoundSourceFile.Classifications = CompressedClassifications.ToList();
            }

            if (CompressedReferences != null)
            {
                if (SourceFileContentLines != null && SourceFileContentLines.Count != 0)
                {
                    var lineSpans = new List<SymbolSpan>();
                    var lineSpanStart = 0;
                    for (int i = 0; i < SourceFileContentLines.Count; i++)
                    {
                        var lineSpanText = SourceFileContentLines[i];
                        var lineSpan = new SymbolSpan()
                        {
                            Length = lineSpanText.Length,
                            LineSpanText = lineSpanText,
                        };

                        int lineOffset = 0;
                        // Set line span start to first non-whitespace character
                        for (int j = 0; j < lineSpanText.Length; j++)
                        {
                            if (!char.IsWhiteSpace(lineSpanText[j]))
                            {
                                lineOffset = j;
                                break;
                            }
                        }

                        lineSpan.Trim();
                        lineSpan.Start = lineSpanStart + lineOffset;
                        lineSpans.Add(lineSpan);

                        lineSpanStart += lineSpanText.Length;
                    }

                    foreach (var span in CompressedReferences.LineSpanModel.SharedValues)
                    {
                        if (span.LineIndex >= 0 && span.LineIndex < lineSpans.Count)
                        {
                            var lineSpan = lineSpans[span.LineIndex];
                            span.LineSpanText = lineSpan.LineSpanText;
                            span.Start = lineSpan.Start;
                        }
                        else
                        {
                            throw new Exception("Incorrect line number in file: " + BoundSourceFile.RepoRelativePath);
                        }
                    }
                }

                BoundSourceFile.References = CompressedReferences.ToList();
                //BoundSourceFile.OutliningRegions = OutliningRegions.ToList();
            }

            string projectId = this.BoundSourceFile.ProjectId;
            string containerQualifiedName = null;
            StringEnum<SymbolKinds> kind = default;
            Glyph glyph = default(Glyph);
            foreach (var definitionSpan in this.BoundSourceFile.Definitions)
            {
                var definition = definitionSpan.Definition;
                definition.ProjectId = AssignDuplicate(definition.ProjectId, ref projectId);
                definition.Kind = AssignDuplicate(definition.Kind, ref kind);
                definition.ContainerQualifiedName = AssignDuplicate(definition.ContainerQualifiedName, ref containerQualifiedName);
                definition.Glyph = AssignDuplicate(definition.Glyph, ref glyph);
            }

            if (this.BoundSourceFile.SourceFile.Content == null && SourceFileContentLines != null)
            {
                this.BoundSourceFile.SourceFile.Content = string.Join(string.Empty, SourceFileContentLines);
            }
        }
    }
}

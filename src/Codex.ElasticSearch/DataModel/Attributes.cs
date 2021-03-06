﻿using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Codex.Storage.ElasticProviders;
using Newtonsoft.Json;
using System.Runtime.Serialization;

namespace Codex.Storage.DataModel
{
    public class DataObjectAttribute : ObjectAttribute
    {
        public DataObjectAttribute(DataInclusionOptions option = DataInclusionOptions.None)
        {
            // Store but don't index object fields at all
            Enabled = false;

            if (!DataInclusion.HasOption(option))
            {
                // Don't store the object at all
                Ignore = true;
            }
        }
    }

    public class DataBooleanAttribute : BooleanAttribute
    {
        public DataBooleanAttribute()
        {
            DocValues = false;
        }
    }

    public class DataIntegerAttribute : NumberAttribute
    {
        public DataIntegerAttribute()
            : base(NumberType.Integer)
        {
            DocValues = false;
        }
    }

    public interface IPrefixTextProperty : ITextProperty
    {
        /// <summary>
        /// Indicates whether to store property in prefix format
        /// </summary>
        [PropertyName("use_prefix")]
        bool UsePrefix { get; set; }
    }

    public class PrefixTermAttribute : SearchStringAttribute, IPrefixTextProperty
    {
        public bool UsePrefix { get; set; }

        bool? ITextProperty.Norms { get; set; }

        public PrefixTermAttribute()
        {
            UsePrefix = true;
            Analyzer = CustomAnalyzers.LowerCaseKeywordAnalyzerName;
        }
    }

    public class PrefixPartialNameAttribute : SearchStringAttribute, IPrefixTextProperty
    {
        public bool UsePrefix { get; set; }

        bool? ITextProperty.Norms { get; set; }

        public PrefixPartialNameAttribute()
        {
            UsePrefix = true;
            Analyzer = CustomAnalyzers.PrefixFilterPartialNameNGramAnalyzerName;
        }
    }

    public class PrefixFullTextTextAttribute : SearchStringAttribute
    {
        public IProperty SelfProperty => this;

        public PrefixFullTextTextAttribute()
        {
            Analyzer = CustomAnalyzers.PrefixFilterFullNameNGramAnalyzerName;
        }
    }

    public class SearchStringAttribute : TextAttribute
    {
        public SearchStringAttribute()
        {
            IndexOptions = IndexOptions.Docs;
            Norms = false;
        }
    }

    public class CodexKeywordAttribute : KeywordAttribute
    {
        public CodexKeywordAttribute()
        {
            DocValues = false;
            Norms = false;
        }
    }

    public class NormalizedKeywordAttribute : KeywordAttribute
    {
        public NormalizedKeywordAttribute()
        {
            Normalizer = CustomAnalyzers.LowerCaseKeywordNormalizerName;
            Index = true;
            Norms = false;
        }
    }

    public class SortwordAttribute : KeywordAttribute
    {
        public SortwordAttribute()
        {
            Norms = false;
        }
    }

    public class DataStringAttribute : CodexKeywordAttribute
    {
        public DataStringAttribute()
        {
            Index = false;
        }
    }

    /// <summary>
    /// TODO: Set options for search a path
    /// </summary>
    public class HierarchicalPathAttribute : CodexKeywordAttribute
    {
        public HierarchicalPathAttribute()
        {
        }
    }

    public class FullTextAttribute : TextAttribute
    {
        public FullTextAttribute(DataInclusionOptions option)
        {
            IndexOptions = IndexOptions.Offsets;
            TermVector = TermVectorOption.Yes;
            PositionIncrementGap = 0;
            Analyzer = CustomAnalyzers.EncodedFullTextAnalyzerName;
            PositionIncrementGap = 0;
            if (!DataInclusion.HasOption(option))
            {
                Ignore = true;
            }
        }
    }
}

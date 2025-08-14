using System.Collections.Generic;
using System.Text;
using Codex.ObjectModel;
using Codex.View;
using Extent = Codex.Utilities.Extent;

namespace Codex.Web.Mvc.Rendering
{
    using static ViewUtilities;

    public class HtmlElementInfo : BaseElementInfo
    {
        public IReferenceSymbol Symbol { get; set; }
        public SourceSpan SourceSpan { get; set; }
        public string DeclaredSymbolId { get; set; }

        public string this[string attibuteName]
        {
            get => Attributes.GetValueOrDefault(attibuteName);
            set => Attributes[attibuteName] = value;
        }

        private ViewModelAddress _link;
        public ViewModelAddress Link
        {
            get => _link;
            set
            {
                _link = value;
                if (Link != null)
                {
                    AddAttribute("href", value.ToUrl().ToString(), set: true);
                    Click = "CxNav(this);return false;";
                }
            }
        }

        public IReferenceSpan Span { get; set; }
        public Extent Range => (Span.Start, Span.End());

        public string Text => SourceSpan.Segment;

        public override string ToString()
        {
            return $"*{Text}* [{Span}] ({Link})"; ;
        }
    }

    public class BaseElementInfo
    {
        public string Name { get; set; } = "span";
        public Dictionary<string, string> Attributes { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

        public string CssClass { get => Attributes.GetValueOrDefault("class"); set => AddAttribute("class", value, set: true); }
        public string Click { set => SetClick(value); }

        public void SetClick(string value, bool set = true)
        {
            AddAttribute("onclick", value, set: set);
        }

        public bool RequiresWrappingSpan { get; set; }
        public string OuterSpanClass { get; set; }

        public void Reset()
        {
            Name = null;
            Attributes.Clear();
        }

        public void AppendClass(string cssClass)
        {
            CssClass = ($"{CssClass} {cssClass}").Trim();
        }

        public void AddAttribute(string name, string value, bool set = false)
        {
            if (value == null) return;

            if (set) Attributes[name] = value;
            else Attributes.TryAdd(name, value);

            OuterSpanClass = null;
        }

        public void Write(TextWriter tw, StringSpan innerText, bool encodeInnerText = true)
        {
            if (OuterSpanClass != null)
            {
                tw.Write("<span");
                AddAttribute(tw, "class", OuterSpanClass);
                tw.Write(">");
            }

            tw.Write("<" + Name);
            foreach (var att in Attributes)
            {
                AddAttribute(tw, att.Key, att.Value);
            }

            if (innerText.Length != 0)
            {
                tw.Write(">");
                if (encodeInnerText)
                {
                    Html(innerText, tw);
                }
                else
                {
                    tw.Write(innerText);
                }
                tw.Write("</" + Name + ">");
            }
            else
            {
                tw.Write("/>");
            }

            if (OuterSpanClass != null)
            {
                tw.Write("</span>");
            }
        }

        bool AddAttribute(TextWriter tw, string name, string value)
        {
            if (value != null)
            {
                tw.Write(" " + name + "=\"" + value + "\"");
                return true;
            }

            return false;
        }
    }
}
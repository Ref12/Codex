using Codex.ObjectModel;
using Codex.ObjectModel.Implementation;
using Codex.Storage;
using Codex.Utilities;
using Codex.View;
using Codex.Web.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Diagnostics.ContractsLight;
using System.Net;
using System.Reflection;
using System.Text;

namespace Codex.Web.Common
{
    using static ViewUtilities;

    internal struct Stringable(StringWriter? writer, string? value)
    {
        public static implicit operator Stringable(StringWriter writer) => new(writer, null);

        public static implicit operator Stringable(string value) => new(null, value);

        public override string ToString()
        {
            return value ?? writer?.ToString();
        }
    }

    internal class HtmlViewFactory : ViewFactory<Stringable>
    {
        public StringWriter Html { get; } = new StringWriter() { NewLine = "\n" };
        public StringWriter Temp { get; } = new StringWriter() { NewLine = "\n" };

        public override Stringable Create(TreeViewModel model)
        {
            Contract.Assert(model.Mode == LeftPaneMode.outline || model.Mode == LeftPaneMode.project || model.Mode == LeftPaneMode.namespaces);

            var id = model.Mode == LeftPaneMode.project ? "projectExplorer" : "documentOutline";

            using (Surround($"<div id=\"{id}\">", "</div>"))
            {
                foreach (var child in model.Root.Children)
                {
                    WriteTreeNode(child);
                }

                if (model.Properties.Count != 0)
                {
                    using (Surround($"<p class=\"projectInfo\">", "<p>"))
                    {
                        foreach (var entry in model.Properties)
                        {
                            var encodedKey = Html(entry.Key).Value.Replace(" ", "&nbsp;").AsEncoded();
                            Add($"{encodedKey}:&nbsp;{entry.Value}<br>");
                        }
                    }
                }
            }

            return Html;
        }

        public void WriteTreeNode(TreeNodeViewModel node)
        {
            var link = new HtmlElementInfo()
            {
                Name = "a",
                Link = node.NavigateAddress
            };

            ClearTemp();
            link.Write(Temp, (node.Kind != null ? $"<span class=\"k\">{Html(node.Kind)}</span>&nbsp;" : "") + Html(node.Name), encodeInnerText: false);

            var folderNameHtml = Temp.ToString().AsEncoded();
            ClearTemp();

            var icon = $"<img src=\"content/icons/{node.Icon}.png\" class=\"imageFolder\" />".AsEncoded();

            if (node.Children.Count != 0)
            {
                Add($"<div class=\"folderTitle {(node.Expanded ? "expanded" : "collapsed")}\" onclick=\"ToggleExpandCollapse(this);ToggleFolderIcon(this);\">{icon}{folderNameHtml}</div>");
                using (Surround($"<div class=\"folder\" style=\"display: {ExpansionDisplay(node)};\">", "</div>"))
                {
                    foreach (var child in node.Children)
                    {
                        WriteTreeNode(child);
                    }
                }
            }
            else
            {
                Add($"<div class=\"folderTitle\">{icon}{folderNameHtml}</div>");
            }
        }

        private static EncodedString ExpansionDisplay(TreeNodeViewModel node)
        {
            return (node.Expanded ? "block" : "none").AsEncoded();
        }

        private void ClearTemp()
        {
            Temp.GetStringBuilder().Clear();
        }

        public override Stringable Create(TextSpanSearchResultViewModel model)
        {
            using (Surround($"<a class=\"rL\" onclick=\"CxNav(this);return false;\" href=\"{model.NavigateAddress}\">", "</a>"))
            {
                Add($"<b>{model.LineNumberText.Replace(" ", "&nbsp;").AsEncoded()}</b>", false);
                foreach (var span in model.Spans)
                {
                    using var _ = span.Highlighted ? Surround($"<i>", "</i>", false) : null;
                    Add($"{span.Text}", newline: false);
                }
            }

            return Html;
        }

        public override Stringable Create(FileResultsViewModel model)
        {
            using (Surround($"<div class=\"rF\">", $"</div>"))
            {
                var glyph = $"url(\"content/icons/{model.Glyph}.png\");";
                Add($"<div class=\"rN\" style=\"background-image: {glyph}\">{model.Path} ({model.Counter})</div>");
                foreach (var item in model.Items)
                {
                    item.CreateView(this);
                }
            }
            return Html;
        }

        public override Stringable Create(SymbolResultViewModel model)
        {
            Add(GetSymbolText(model));
            return Html;
        }

        public void Create(ProjectGroupResultsViewModel model)
        {
            Add($"<div class=\"rA expanded\" onclick=\"ToggleExpandCollapse(this); return false;\">{model.ProjectName} ({model.Counter})</div>");
            using (Surround($"<div class=\"rG\" id=\"{model.ProjectName}\">", $"</div>"))
            {
                foreach (var item in model.Items)
                {
                    item.CreateView(this);
                }
            }
        }

        public override Stringable Create(ProjectResultsViewModel model)
        {
            foreach (var group in model.ProjectGroups)
            {
                Create(group);
            }
            return Html;
        }

        public override Stringable Create(CategorizedSearchResultsViewModel model)
        {
            using (Surround($"<div>", $"</div>"))
            {
                string resultCountText;
                if (model.ResultCount < model.Results.Total)
                {
                    resultCountText = $"Displaying top {model.ResultCount} results out of {model.Results.Total}:";
                }
                else if (model.ResultCount == 1)
                {
                    resultCountText = "1 result found:";
                }
                else
                {
                    resultCountText = $"{model.ResultCount} result found:";
                }
                //Add($"<div class=\"note\"><a class=\"blueLink\" href=\"/?query={Url(model.SearchString)}\">{resultCountText}</a></div>");
            }

            foreach (var category in model.Categories)
            {
                Create(category);
            }

            return Html;
        }

        public override Stringable Create(LeftPaneViewModel model)
        {
            if (!string.IsNullOrEmpty(model.SearchInfo))
            {
                using (Surround($"<div class=\"note\">", "</div>", newline: false))
                using (model.SearchInfoAddress != null ? Surround($"<a class=\"blueLink\" onclick=\"CxNav(this);return false;\" href=\"{model.SearchInfoAddress}\">", "</a>", newline: false) : null)
                {
                    Add($"{model.SearchInfo}", newline: false);
                }
            }

            model?.Content?.CreateView(this);

            return Html;
        }

        private void Create(CategoryGroupSearchResultsViewModel category)
        {
            Add($"<div class=\"rH expanded\" onclick=\"ToggleExpandCollapse(this);return false;\">{category.Header}:</div>");

            using (Surround($"<div class=\"rK\">", $"</div>"))
            {
                foreach (var definition in category.RelatedDefinitions)
                {
                    Create(definition);
                }

                category.ProjectResults.CreateView(this);
            }
        }

        public static FormattableString GetSymbolText(SymbolResultViewModel searchResult)
        {
            FormattableString resultText = $@"<a onclick=""CxNav(this);return false;"" href=""{searchResult.NavigateAddress}"">
 <div class=""resultItem"">
 <img src=""content/icons/{searchResult.GlyphIcon}.png"" height=""16"" width=""16"" /><div class=""resultKind"">{searchResult.SymbolKind}</div><div class=""resultName"">{searchResult.ShortName}</div><div class=""resultDescription"">{searchResult.DisplayName}</div>
 </div>
 </a>";

            return resultText;
        }

        public void Add(FormattableString value, bool newline = true)
        {
            var args = value.GetArguments();
            for (int i = 0; i < args.Length; i++)
            {
                args[i] = HtmlEncode(args[i]);
            }

            Html.Write(value.Format, args);
            if (newline)
            {
                Html.WriteLine();
            }
        }

        public IDisposable Surround(FormattableString value, string endValue, bool newline = true)
        {
            Add(value, newline: newline);
            return new DisposeAction(() =>
            {
                if (newline) Html.WriteLine(endValue);
                else Html.Write(endValue);
            });
        }

        public override Stringable Create(RightPaneViewModel model)
        {
            if (!string.IsNullOrEmpty(model.Error))
            {
                using (Surround($"<div class=\"note\">", "</div>", newline: false))
                {
                    Add($"{model.Error}", newline: false);
                }

                return Html;
            }
            else if (model.Html != null)
            {
                return model.Html;
            }
            else if (model.SourceView is { } sourceView)
            {
                var renderer = new SourceFileRenderer(sourceView);
                return renderer.Render().GetHtml();
            }

            return string.Empty;
        }
    }
}

using Codex.View;
using System;
using System.Collections.Generic;
using System.Text;

namespace Codex.Web.Common
{
    public abstract class ViewFactory<TView>
    {
        public abstract TView Create(TextSpanSearchResultViewModel model);

        public abstract TView Create(FileResultsViewModel model);

        public abstract TView Create(SymbolResultViewModel model);

        public abstract TView Create(ProjectResultsViewModel model);

        public abstract TView Create(CategorizedSearchResultsViewModel model);

        public abstract TView Create(LeftPaneViewModel model);

        public abstract TView Create(RightPaneViewModel model);

        public abstract TView Create(TreeViewModel model);
    }
}

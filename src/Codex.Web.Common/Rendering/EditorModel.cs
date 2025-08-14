using Codex.View;

namespace Codex.Web.Mvc.Rendering
{
    using static ViewModelAddress;
    using static ViewUtilities;

    public class EditorModel
    {
        public string ProjectId { get; set; }
        public string FilePath { get; set; }
        public string RepoRelativePath { get; set; }
        public string ProjectRelativePath { get; set; }
        public string WebLink { get; set; }
        public string Text { get; set; }
        public string LineNumberText { get; set; }
        public string Error { get; set; }
        public string RepoName { get; set; }
        public string IndexName { get; set; }
        public string IndexedOn { get; set; }
        public string DownloadLink { get; set; }

        public ViewModelAddress OpenFileLink { get; set; }

        public EditorModel Model => this;

        public string GetHtml()
        {
            return $@"
    <div id='editorPaneWrapper'>
        <div id='editorPane' class='cz' data-filepath='{Attr(FilePath)}'>
            <table class='tb' cellpadding='0' cellspacing='0'>
                <tr>
                    <td valign='top' align='right'><pre id='ln'>{LineNumberText}</pre></td>
                    <td valign='top' align='left'><pre id='sourceCode'>{Text}</pre></td>
                </tr>
            </table>
        </div>
    </div>
    <div id='bottomPane' class='dH'>
        <table style='width: 100%'>
            <tbody>
                <tr>
                    <td>
                        File:&nbsp;<a id='filePathLink' class='blueLink' href='{GoToFile(ProjectId, FilePath)}' target='_blank' title='Click to open file in a new tab'>{Html(FilePath)}</a>&nbsp;(<a id='fileDownloadLink' class='blueLink' href='{DownloadLink}' title='Click to download the file'>Download</a>)
                    </td>
                    {GetWebLinkHtml()}
                </tr>
                <tr>
                    <td>
                        Project:&nbsp;<a id='projectExplorerLink' class='blueLink' href='{ShowProjectExplorer(ProjectId)}' onclick='CxNav(this);return false;'>{Html(ProjectId)}</a>
                    </td>
                    <td style='text-align: right;'>
                        <div style='margin-right: 16px;' title='Index: {Attr(IndexName)}'>Indexed on: {Html(IndexedOn)}</div>
                    </td>
                </tr>
            </tbody>
        </table>
    </div>";
        }

        private string GetWebLinkHtml()
        {
            if (!string.IsNullOrEmpty(WebLink))
            {
                var repoPath = RepoRelativePath ?? "Source Control";
                return $@"<td style='text-align: right;'>
                    <a id='webAccessLink' style='margin-right: 16px;' class='blueLink' href='{WebLink}' title='Repo: {Attr(RepoName)}' target='_blank'>{Html(repoPath)}</a>
                </td>";
            }
            else if (!string.IsNullOrEmpty(Model.RepoRelativePath))
            {
                return $@"<td style='text-align: right;'>
                    <div style='margin-right: 16px;' title='Repo: {Attr(RepoName)}'>{Html(RepoRelativePath)}</div>
                </td>";
            }

            return "";
        }
    }
}
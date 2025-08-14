var DefaultWindowTitle = "ReF12";

namespace CodexIpc {

    export class CodexPage implements ICodexPage {
        private leftPane: HTMLDivElement;
        private rightPane: HTMLDivElement;

        private WorkerClient;

        public Server: CodexIpc.IpcServer;

        constructor() {
            this.leftPane = document.getElementById("leftPaneContent") as HTMLDivElement;
            this.rightPane = document.getElementById("rightPane") as HTMLDivElement;

            this.Server = new CodexIpc.IpcServer(this);
        }
        setLeftPane(innerHtml?: string): void {
            if (!innerHtml) {
                innerHtml = "<div></div>";
            }
            this.leftPane.innerHTML = innerHtml;
        }

        setExplorerPane(innerHtml?: string): void {
            if (!innerHtml) {
                innerHtml = "";
            }
            this.leftPane.innerHTML = innerHtml;
        }

        setPageTitle(title?): void {
            if (!title) {
                title = DefaultWindowTitle;
            }

            document.title = title;
        }

        setNavigationBar(address: string, title: string): void {
            if (!title) {
                title = DefaultWindowTitle;
            }

            document.title = title;
            history.pushState(address, title, address);
        }

        setRightPane(innerHtml?: string): void {
            if (!innerHtml) {
                innerHtml = "<div></div>";
            }
            this.rightPane.innerHTML = innerHtml;
        }

        StartWorkerAsync() : Promise<IndexService> {
            if (!this.WorkerClient) {
                this.WorkerClient = new CodexIpc.WorkerClient<CodexIpc.IndexService>(this.Server, "/worker.main.js");
            }

            return Promise.resolve(this.WorkerClient.Target);
        }

        LogMessageAsync(args) {
            console.log("Called LogMessage with args: " + args);
            return "Responded";
        }
    }
}

CodexIpc.ServiceMode = "Page";

var CodexPage = new CodexIpc.CodexPage();
CodexIpc.Service = CodexPage;

CodexIpc.OnLoaded = function () {
    console.log("Blazor loaded in page");
}

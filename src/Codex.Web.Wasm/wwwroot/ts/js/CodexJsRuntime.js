import * as boot from './boot.js';
export function GetHRef() {
    return globalThis.window.location.href;
}
export function GetApplicationArgumentsJson() {
    return JSON.stringify({
        rootUrl: globalThis.window.rootUrl,
        startUrl: globalThis.window.location.href,
    });
}
export function SetLeftPane(innerHtml) {
    CodexPage.setLeftPane(innerHtml);
}
export function SetRightPane(innerHtml) {
    CodexPage.setRightPane(innerHtml);
}
export function GoToSymbolOrLineNumber(line, symbol) {
    GoToSymbolOrLineNumber(symbol, line);
}
export function SetNavigationBar(title, address) {
    CodexPage.setNavigationBar(address, title);
}
export function Navigate(url) {
    boot.codexClient.Navigate(url, false);
}
export async function SetupAsync() {
    const { getAssemblyExports } = await globalThis.getDotnetRuntime(0);
    var exports = await getAssemblyExports("Codex.Web.Wasm.dll");
    boot.setUpdateState(async function (requestJson) {
        const response = await exports.CodexApplicationExports.UpdateState(requestJson);
        var responseData = JSON.parse(response);
        return responseData;
    });
}
//# sourceMappingURL=CodexJsRuntime.js.map
using System.Threading.Tasks;
using Codex.View;
using Codex.Web.Common;

namespace Codex.Web.Wasm
{
    public class JSViewModelController : WebViewModelController
    {
        internal static IHttpClient Client => SdkFeatures.HttpClient;

        // TODO: This will need to change if we have multipage support
        public JSViewModelController()
            : base(MainController.App)
        {
        }
    }
}

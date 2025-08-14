namespace Codex.Search
{
    public class ClientContext<TClient>
        where TClient : IClient
    {
        // TODO: Disable
        public bool CaptureRequests = true;
        public TClient Client;
        public List<string> Requests;

        public ClientContext()
        {
            Requests = new List<string>();
        }

        public ClientContext(ClientContext<TClient> other)
        {
            CaptureRequests = other.CaptureRequests;
            Client = other.Client;
            Requests = other.Requests;
        }
    }
}

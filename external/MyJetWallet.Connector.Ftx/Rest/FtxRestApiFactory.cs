namespace FtxApi.Rest
{
    public static class FtxRestApiFactory
    {
        public static FtxRestApi CreateClient(string apiKey, string apiSecret)
        {
            var client = new Client(apiKey, apiSecret);
            var api = new FtxRestApi(client);

            return api;
        }
    }
}
namespace FtxApi
{
    public class Client
    {
        public string ApiKey { get; }

        public string ApiSecret { get; }

        public string? SubAccount { get; }

        public Client()
        {
            ApiKey = "";
            ApiSecret = "";
        }

        public Client(string apiKey, string apiSecret, string? subAccount = null)
        {
            ApiKey = apiKey;
            ApiSecret = apiSecret;
            SubAccount = string.IsNullOrEmpty(subAccount) ? null : subAccount;
        }

    }
}
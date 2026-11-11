namespace ZohoPowerBIConnector.Services.ZohoSyncService
{
    using Newtonsoft.Json.Linq;
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;
    using ZohoPowerBIConnector.Services.ZohoSyncService.Models;

    public class ZohoApiClient
    {
        private static readonly HttpClient client = new HttpClient();

        // Token cache per instance (per client)
        private string _accessToken = "";
        private DateTime _tokenExpiry = DateTime.MinValue;

        private readonly Client _client;

        // Inject client
        public ZohoApiClient(Client clientData)
        {
            _client = clientData;
        }

        public async Task<string> GetAccessToken()
        {
            try
            {
                // Use cached token
                if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiry)
                {
                    SyncLogger.Log($"[{_client.Name}] Using cached Zoho token");
                    return _accessToken;
                }

                SyncLogger.Log($"[{_client.Name}] Requesting new Zoho token");

                string url = $"https://accounts.zoho.com/oauth/v2/token" +
                             $"?refresh_token={_client.ZohoRefreshToken}" +
                             $"&client_id={_client.ZohoClientId}" +
                             $"&client_secret={_client.ZohoClientSecret}" +
                             $"&grant_type=refresh_token";

                var response = await client.PostAsync(url, null);
                string responseContent = await response.Content.ReadAsStringAsync();

                SyncLogger.Log($"[{_client.Name}] Token Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Zoho Token Error: {responseContent}");
                }

                var json = JObject.Parse(responseContent);

                _accessToken = json["access_token"]?.ToString();

                if (string.IsNullOrEmpty(_accessToken))
                    throw new Exception("Access token missing");

                int expiresIn = json["expires_in"] != null
                    ? json["expires_in"].ToObject<int>()
                    : 3600;

                _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - 60);

                SyncLogger.Log($"[{_client.Name}] Token generated. Expiry: {_tokenExpiry}");

                return _accessToken;
            }
            catch (Exception ex)
            {
                SyncLogger.Log(ex, $"[{_client.Name}] Error in GetAccessToken");
                throw;
            }
        }

        public async Task<JObject> GetModuleData(string module, DateTime modifiedTime, int page, long id = 0)
        {
            try
            {
                SyncLogger.Log($"[{_client.Name}] Fetching {module}, Page: {page}");

                var token = await GetAccessToken();

                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("Authorization", $"Zoho-oauthtoken {token}");

                var time = modifiedTime
                    .ToUniversalTime()
                    .ToString("yyyy-MM-ddTHH:mm:ss+0000");

                var encodedTime = Uri.EscapeDataString(time);

                // Use client-specific config
                var baseUrl = $"{_client.ApiBaseUrl}/{module}";
                var path = id != 0 ? $"/{id}" : string.Empty;

                var url = $"{baseUrl}{path}?organization_id={_client.OrganizationId}"
                        + (id == 0 ? $"&last_modified_time={encodedTime}" : string.Empty)
                        + $"&page={page}";

                if (module == "Vendors")
                {
                    url += "&contact_type=vendor";
                }

                SyncLogger.Log($"[{_client.Name}] URL: {url}");

                var response = await client.GetAsync(url);
                string responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Zoho API Error: {response.StatusCode}");
                }

                if (string.IsNullOrWhiteSpace(responseBody))
                {
                    throw new Exception("Empty response from Zoho API");
                }

                var json = JObject.Parse(responseBody);

                if (json["code"] != null && json["code"].ToString() != "0")
                {
                    SyncLogger.Log($"[{_client.Name}] Zoho API error: {json}");
                }

                SyncLogger.Log($"[{_client.Name}] {module} fetch success");

                return json;
            }
            catch (Exception ex)
            {
                SyncLogger.Log(ex, $"[{_client.Name}] Error in GetModuleData");
                throw;
            }
        }
    }
}

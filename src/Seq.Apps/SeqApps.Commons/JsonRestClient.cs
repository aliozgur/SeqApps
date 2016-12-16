using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SeqApps.Commons
{
    public class JsonRestClient
    {
        private WebClient client;

        public string Username { get; set; }
        public string Password { get; set; }

        public string BaseUrl { get; set; }
        public JsonRestClient(string baseUrl)
        {
            if (baseUrl.IsNullOrEmpty())
                throw new TypeInitializationException("SeqApps.Commons.SeqApps.Commons", new ApplicationException("BaseUrl is required"));

            BaseUrl = baseUrl.NormalizeHostOrFQDN() ;
        }

        public Uri GetUriForResource(string resource)
        {
            if (BaseUrl.IsNullOrEmpty())
                throw new ApplicationException("Base url is null or empty");

            if (string.IsNullOrWhiteSpace(resource))
                return new Uri(BaseUrl);

            return new Uri(BaseUrl + resource.Trim().TrimStart(StringSplits.ForwardSlash));
        }

        private string GetBasicAuthzValue()
        {
            if (Username.IsNullOrEmpty())
                return string.Empty;

            var enc = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{Username}:{Password}"));
            return $"{"Basic"} {enc}";
        }

        public async Task<TResponse> GetAsync<TResponse>(string resource)
        {
            client = client ?? new WebClient();
            client.Headers.Add(HttpRequestHeader.Accept, "application/json");
            client.Encoding = Encoding.UTF8;
            var authz = GetBasicAuthzValue();
            if (authz.HasValue())
                client.Headers.Add(HttpRequestHeader.Authorization, authz);

            var uri = GetUriForResource(resource);
            var responseBytes = await client.DownloadDataTaskAsync(uri).ConfigureAwait(false);

            string response = Encoding.UTF8.GetString(responseBytes);
            return JsonConvert.DeserializeObject<TResponse>(response);
        }

        public async Task<TResponse> PostAsync<TResponse, TData>(string resource, TData data) where TResponse : class
        {
            client = client ?? new WebClient();
            client.Headers.Add(HttpRequestHeader.ContentType, "application/json");
            client.Encoding = Encoding.UTF8;
            var authz = GetBasicAuthzValue();
            if (authz.HasValue())
                client.Headers.Add(HttpRequestHeader.Authorization, authz);


            var json = JsonConvert.SerializeObject(data);
            byte[] dataBytes = Encoding.UTF8.GetBytes(json);
            var uri = GetUriForResource(resource);
            var responseBytes = await client.UploadDataTaskAsync(uri, "POST", dataBytes).ConfigureAwait(false);
            string response = Encoding.UTF8.GetString(responseBytes);
            if (typeof(TResponse) == typeof(string))
                return response as TResponse;

            return JsonConvert.DeserializeObject<TResponse>(response);
        }

    }
}

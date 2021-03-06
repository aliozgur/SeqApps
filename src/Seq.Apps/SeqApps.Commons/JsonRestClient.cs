﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Serilog;

namespace SeqApps.Commons
{
    public class JsonRestClient
    {

        public string Username { get; set; }
        public string Password { get; set; }
        public bool DoNotAuthorize { get; set; } = false;

        public string BaseUrl { get; set; }
        public JsonRestClient(string baseUrl)
        {
            if (baseUrl.IsNullOrEmpty())
                throw new TypeInitializationException("SeqApps.Commons.SeqApps.Commons", new ApplicationException("BaseUrl is required"));

            BaseUrl = baseUrl.NormalizeHostOrFQDN() ;
        }

        static JsonRestClient()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

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
            if ((Username??"").IsNullOrEmpty())
                return string.Empty;

            var enc = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{Username}:{Password}"));
            return $"{"Basic"} {enc}";
        }

        private WebClient CreateWebClient(Dictionary<string, string> headers = null)
        {

            WebClient client = new WebClient();
            client.Headers.Add(HttpRequestHeader.Accept, "application/json");
            client.Headers.Add(HttpRequestHeader.ContentType, "application/json");
            client.Headers.Add(HttpRequestHeader.UserAgent,
                //"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/67.0.3396.99 Safari/537.36");
                "Seq Jira Issue Reactor");

            if ((headers?.Count ?? 0) > 0)
            {
                headers.Aggregate(client.Headers, (acc, kvp) =>
                {
                    acc.Add(kvp.Key, kvp.Value);
                    return acc;
                });
            }

            client.Encoding = Encoding.UTF8;
            var authz = GetBasicAuthzValue();
            if (!DoNotAuthorize && authz.HasValue())
                client.Headers.Add(HttpRequestHeader.Authorization, authz);
            return client;
        }

        public async Task<TResponse> GetAsync<TResponse>(string resource, Dictionary<string,string> headers=null)
        {
            using (WebClient client = CreateWebClient(headers))
            {
                var uri = GetUriForResource(resource);
                var responseBytes = await client.DownloadDataTaskAsync(uri).ConfigureAwait(false);
                string response = Encoding.UTF8.GetString(responseBytes);
                return JsonConvert.DeserializeObject<TResponse>(response);
            }
        }

        public async Task<TResponse> PostAsync<TResponse, TData>(string resource, TData data, Dictionary<string,string> headers=null) where TResponse : class
        {
            using (WebClient client = CreateWebClient(headers))
            {
                var json = JsonConvert.SerializeObject(data);
                byte[] dataBytes = Encoding.UTF8.GetBytes(json);
                var uri = GetUriForResource(resource);
                var responseBytes = await client.UploadDataTaskAsync(uri, "POST", dataBytes).ConfigureAwait(false);
                string response = Encoding.UTF8.GetString(responseBytes);
                if (typeof (TResponse) == typeof (string))
                    return response as TResponse;

                return JsonConvert.DeserializeObject<TResponse>(response);
            }
        }

    }
}

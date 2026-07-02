using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Hellio.Messaging.Tests
{
    /// <summary>
    /// A programmable <see cref="HttpMessageHandler"/> that records the last request
    /// and returns a canned response. Lets tests assert on method, URL and body
    /// without touching the network.
    /// </summary>
    public sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _responseBody;

        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }
        public List<HttpRequestMessage> Requests { get; } = new List<HttpRequestMessage>();

        public MockHttpMessageHandler(HttpStatusCode status, string responseBody)
        {
            _status = status;
            _responseBody = responseBody;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            Requests.Add(request);

            if (request.Content != null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync().ConfigureAwait(false);
            }

            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_responseBody, System.Text.Encoding.UTF8, "application/json"),
            };
        }
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Hamco.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hamco.Core.Tests.Services;

public class MailjetTransactionalEmailServiceTests
{
    [Fact]
    public async Task SendVerificationEmailAsync_UsesMailjetEndpointAndBasicAuth()
    {
        var handler = new CapturingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpClient = new HttpClient(handler);
        var factory = new FakeHttpClientFactory(httpClient);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MAILJET_API_KEY"] = "api-key",
                ["MAILJET_API_SECRET"] = "api-secret",
                ["DEFAULT_FROM_EMAIL"] = "noreply@hamco.test",
                ["DEFAULT_FROM_NAME"] = "HAMCO Test"
            })
            .Build();

        var service = new MailjetTransactionalEmailService(factory, config, NullLogger<MailjetTransactionalEmailService>.Instance);

        await service.SendVerificationEmailAsync("user@example.com", "https://hamco.test/auth/verify-email?token=abc");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.mailjet.com/v3.1/send", request.RequestUri?.ToString());

        Assert.NotNull(request.Headers.Authorization);
        Assert.Equal("Basic", request.Headers.Authorization!.Scheme);

        var expectedAuth = Convert.ToBase64String(Encoding.ASCII.GetBytes("api-key:api-secret"));
        Assert.Equal(expectedAuth, request.Headers.Authorization.Parameter);

        var body = await request.Content!.ReadAsStringAsync();
        Assert.Contains("user@example.com", body);
        Assert.Contains("Verify your HAMCO account", body);
        Assert.Contains("noreply@hamco.test", body);
    }

    [Fact]
    public async Task SendPasswordResetEmailAsync_WhenMailjetReturnsError_DoesNotThrow()
    {
        var handler = new CapturingHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"Error\":\"bad request\"}")
            });

        var httpClient = new HttpClient(handler);
        var factory = new FakeHttpClientFactory(httpClient);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MAILJET_API_KEY"] = "api-key",
                ["MAILJET_API_SECRET"] = "api-secret",
                ["DEFAULT_FROM_EMAIL"] = "noreply@hamco.test"
            })
            .Build();

        var service = new MailjetTransactionalEmailService(factory, config, NullLogger<MailjetTransactionalEmailService>.Instance);

        var exception = await Record.ExceptionAsync(() =>
            service.SendPasswordResetEmailAsync("user@example.com", "https://hamco.test/auth/reset-password?token=abc"));

        Assert.Null(exception);
        Assert.Single(handler.Requests);
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public FakeHttpClientFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;
        public List<HttpRequestMessage> Requests { get; } = new();

        public CapturingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var clone = CloneRequest(request);
            Requests.Add(clone);
            return Task.FromResult(_responseFactory(request));
        }

        private static HttpRequestMessage CloneRequest(HttpRequestMessage request)
        {
            var clone = new HttpRequestMessage(request.Method, request.RequestUri);

            if (request.Content != null)
            {
                var body = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                clone.Content = new StringContent(body, Encoding.UTF8);

                if (request.Content.Headers.ContentType != null)
                {
                    clone.Content.Headers.ContentType = request.Content.Headers.ContentType;
                }
            }

            foreach (var header in request.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return clone;
        }
    }
}

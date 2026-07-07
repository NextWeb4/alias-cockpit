using System.Net;
using System.Text;
using AliasCockpit.Core.Providers;
using AliasCockpit.Core.Secrets;
using AliasCockpit.Infrastructure.Providers;

namespace AliasCockpit.Infrastructure.Tests.Providers;

public sealed class AddyIoHttpProviderAdapterTests
{
    [Fact]
    public async Task ValidateCredentialsUsesBearerToken()
    {
        var handler = new RecordingHandler(
            JsonResponse(HttpStatusCode.OK, """{"name":"Desktop Client","created_at":"2019-10-01 09:00:00","expires_at":null}"""));
        var adapter = CreateAdapter(handler);
        var account = ProviderAccount.Create(ProviderTypes.AddyIo, "addy.io", DateTimeOffset.UtcNow)
            .MarkSecretStored(DateTimeOffset.UtcNow);
        var secrets = new InMemorySecretStore();
        await secrets.SetSecretAsync(account.SecretRef, "addy-token");

        var result = await adapter.ValidateCredentialsAsync(account, secrets);

        Assert.True(result.IsConfigured);
        Assert.True(result.CanAttemptNetwork);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v1/api-token-details", request.PathAndQuery);
        Assert.Equal("Bearer addy-token", request.AuthorizationHeader);
        Assert.Equal("XMLHttpRequest", request.RequestedWithHeader);
    }

    [Fact]
    public async Task CreateRandomAliasUsesUuidFormatAndDefaultRecipient()
    {
        var handler = new RecordingHandler(
            JsonResponse(HttpStatusCode.OK, """{"name":"Desktop Client","created_at":"2019-10-01 09:00:00","expires_at":null}"""),
            JsonResponse(HttpStatusCode.Created, """
                {
                  "data": {
                    "id": "50c9e585-e7f5-41c4-9016-9014c15454bc",
                    "email": "50c9e585-e7f5-41c4-9016-9014c15454bc@anonaddy.me",
                    "active": true,
                    "description": "For example.com",
                    "created_at": "2019-10-01 09:00:00"
                  }
                }
                """));
        var adapter = CreateAdapter(handler);
        var now = DateTimeOffset.Parse("2026-07-05T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var account = ProviderAccount.Create(ProviderTypes.AddyIo, "addy.io", now).MarkSecretStored(now);
        var secrets = new InMemorySecretStore();
        await secrets.SetSecretAsync(account.SecretRef, "addy-token");

        var result = await adapter.CreateAliasAsync(
            account,
            ProviderAliasCreateRequest.Random("example.com", "anonaddy.me", "For example.com", now),
            secrets);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Alias);
        Assert.Equal("50c9e585-e7f5-41c4-9016-9014c15454bc", result.Alias.RemoteId);
        Assert.Equal("50c9e585-e7f5-41c4-9016-9014c15454bc@anonaddy.me", result.Alias.Address);
        Assert.Equal("For example.com", result.Alias.Description);

        Assert.Equal(2, handler.Requests.Count);
        var post = handler.Requests[1];
        Assert.Equal(HttpMethod.Post, post.Method);
        Assert.Equal("/api/v1/aliases", post.PathAndQuery);
        Assert.Equal("Bearer addy-token", post.AuthorizationHeader);
        Assert.Contains("\"domain\":\"anonaddy.me\"", post.Body, StringComparison.Ordinal);
        Assert.Contains("\"format\":\"uuid\"", post.Body, StringComparison.Ordinal);
        Assert.Contains("\"description\":\"For example.com\"", post.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("local_part", post.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("recipient_ids", post.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("addy-token", post.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateCustomAliasUsesCustomFormatAndLocalPart()
    {
        var handler = new RecordingHandler(
            JsonResponse(HttpStatusCode.OK, """{"name":"Desktop Client","created_at":"2019-10-01 09:00:00","expires_at":null}"""),
            JsonResponse(HttpStatusCode.Created, """
                {
                  "data": {
                    "id": "alias-id",
                    "email": "shop@johndoe.anonaddy.com",
                    "active": true,
                    "description": "Shopping",
                    "created_at": "2019-10-01 09:00:00"
                  }
                }
                """));
        var adapter = CreateAdapter(handler);
        var now = DateTimeOffset.UtcNow;
        var account = ProviderAccount.Create(ProviderTypes.AddyIo, "addy.io", now).MarkSecretStored(now);
        var secrets = new InMemorySecretStore();
        await secrets.SetSecretAsync(account.SecretRef, "addy-token");

        var result = await adapter.CreateAliasAsync(
            account,
            ProviderAliasCreateRequest.Custom("shop.example", "shop", "johndoe.anonaddy.com", "Shopping", now),
            secrets);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Alias);
        Assert.Equal("shop@johndoe.anonaddy.com", result.Alias.Address);

        var post = handler.Requests[1];
        Assert.Contains("\"format\":\"custom\"", post.Body, StringComparison.Ordinal);
        Assert.Contains("\"local_part\":\"shop\"", post.Body, StringComparison.Ordinal);
        Assert.Contains("\"domain\":\"johndoe.anonaddy.com\"", post.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("recipient_ids", post.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProviderErrorIsMappedWithoutLeakingToken()
    {
        var handler = new RecordingHandler(
            JsonResponse(HttpStatusCode.OK, """{"name":"Desktop Client","created_at":"2019-10-01 09:00:00","expires_at":null}"""),
            JsonResponse((HttpStatusCode)422, """{"message":"The domain field is required."}"""));
        var adapter = CreateAdapter(handler);
        var now = DateTimeOffset.UtcNow;
        var account = ProviderAccount.Create(ProviderTypes.AddyIo, "addy.io", now).MarkSecretStored(now);
        var secrets = new InMemorySecretStore();
        await secrets.SetSecretAsync(account.SecretRef, "addy-token");

        var result = await adapter.CreateAliasAsync(
            account,
            ProviderAliasCreateRequest.Random("example.com", "anonaddy.me", null, now),
            secrets);

        Assert.False(result.Succeeded);
        Assert.Equal("http.422", result.ErrorCode);
        Assert.Equal(["The domain field is required."], result.Warnings);
        Assert.DoesNotContain("addy-token", string.Join('\n', result.Warnings), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DisableAliasUsesActiveAliasesDeleteEndpoint()
    {
        var handler = new RecordingHandler(
            JsonResponse(HttpStatusCode.OK, """{"name":"Desktop Client","created_at":"2019-10-01 09:00:00","expires_at":null}"""),
            new HttpResponseMessage(HttpStatusCode.NoContent));
        var adapter = CreateAdapter(handler);
        var now = DateTimeOffset.UtcNow;
        var account = ProviderAccount.Create(ProviderTypes.AddyIo, "addy.io", now).MarkSecretStored(now);
        var secrets = new InMemorySecretStore();
        await secrets.SetSecretAsync(account.SecretRef, "addy-token");

        var result = await adapter.DisableAliasAsync(
            account,
            new ProviderAliasReference("alias-id", "alias@anonaddy.me", now),
            secrets);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Alias);
        Assert.Equal(AliasCockpit.Core.Aliases.AliasStatus.Disabled, result.Alias.Status);
        Assert.Equal(HttpMethod.Delete, handler.Requests[1].Method);
        Assert.Equal("/api/v1/active-aliases/alias-id", handler.Requests[1].PathAndQuery);
        Assert.Equal("Bearer addy-token", handler.Requests[1].AuthorizationHeader);
        Assert.DoesNotContain("addy-token", handler.Requests[1].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteAliasUsesAliasesDeleteEndpoint()
    {
        var handler = new RecordingHandler(
            JsonResponse(HttpStatusCode.OK, """{"name":"Desktop Client","created_at":"2019-10-01 09:00:00","expires_at":null}"""),
            new HttpResponseMessage(HttpStatusCode.NoContent));
        var adapter = CreateAdapter(handler);
        var now = DateTimeOffset.UtcNow;
        var account = ProviderAccount.Create(ProviderTypes.AddyIo, "addy.io", now).MarkSecretStored(now);
        var secrets = new InMemorySecretStore();
        await secrets.SetSecretAsync(account.SecretRef, "addy-token");

        var result = await adapter.DeleteAliasAsync(
            account,
            new ProviderAliasReference("alias-id", "alias@anonaddy.me", now),
            secrets);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Alias);
        Assert.Equal(AliasCockpit.Core.Aliases.AliasStatus.Deleted, result.Alias.Status);
        Assert.Equal(HttpMethod.Delete, handler.Requests[1].Method);
        Assert.Equal("/api/v1/aliases/alias-id", handler.Requests[1].PathAndQuery);
    }

    private static AddyIoHttpProviderAdapter CreateAdapter(RecordingHandler handler)
    {
        return new AddyIoHttpProviderAdapter(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://app.addy.io"),
        });
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    private sealed class RecordingHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new CapturedRequest(
                request.Method,
                request.RequestUri?.PathAndQuery ?? string.Empty,
                request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues("X-Requested-With", out var values) ? values.SingleOrDefault() : null,
                body));

            return _responses.Count == 0
                ? throw new InvalidOperationException("No fake HTTP response queued.")
                : _responses.Dequeue();
        }
    }

    private sealed record CapturedRequest(
        HttpMethod Method,
        string PathAndQuery,
        string? AuthorizationHeader,
        string? RequestedWithHeader,
        string Body);

    private sealed class InMemorySecretStore : ISecretStore
    {
        private readonly Dictionary<string, string> _secrets = new(StringComparer.Ordinal);

        public Task SetSecretAsync(string key, string secret, CancellationToken cancellationToken = default)
        {
            SecretKey.Validate(key);
            _secrets[key] = secret;
            return Task.CompletedTask;
        }

        public Task<string?> GetSecretAsync(string key, CancellationToken cancellationToken = default)
        {
            SecretKey.Validate(key);
            return Task.FromResult(_secrets.TryGetValue(key, out var secret) ? secret : null);
        }

        public Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default)
        {
            SecretKey.Validate(key);
            _secrets.Remove(key);
            return Task.CompletedTask;
        }
    }
}

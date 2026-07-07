using System.Net;
using System.Text;
using AliasCockpit.Core.Providers;
using AliasCockpit.Core.Secrets;
using AliasCockpit.Infrastructure.Providers;

namespace AliasCockpit.Infrastructure.Tests.Providers;

public sealed class SimpleLoginHttpProviderAdapterTests
{
    [Fact]
    public async Task ValidateCredentialsUsesAuthenticationHeader()
    {
        var handler = new RecordingHandler(
            JsonResponse(HttpStatusCode.OK, """{"name":"Test","is_premium":false}"""));
        var adapter = CreateAdapter(handler);
        var account = ProviderAccount.Create(ProviderTypes.SimpleLogin, "SimpleLogin", DateTimeOffset.UtcNow)
            .MarkSecretStored(DateTimeOffset.UtcNow);
        var secrets = new InMemorySecretStore();
        await secrets.SetSecretAsync(account.SecretRef, "secret-from-store");

        var result = await adapter.ValidateCredentialsAsync(account, secrets);

        Assert.True(result.IsConfigured);
        Assert.True(result.CanAttemptNetwork);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/user_info", request.PathAndQuery);
        Assert.Equal("secret-from-store", request.AuthenticationHeader);
    }

    [Fact]
    public async Task CreateRandomAliasPostsNoteAndMapsAliasResponse()
    {
        var handler = new RecordingHandler(
            JsonResponse(HttpStatusCode.OK, """{"name":"Test","is_premium":true}"""),
            JsonResponse(HttpStatusCode.Created, """{"id":42,"email":"alias@sl.example","enabled":true,"creation_timestamp":1783209600,"note":"shopping"}"""));
        var adapter = CreateAdapter(handler);
        var now = DateTimeOffset.Parse("2026-07-05T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var account = ProviderAccount.Create(ProviderTypes.SimpleLogin, "SimpleLogin", now).MarkSecretStored(now);
        var secrets = new InMemorySecretStore();
        await secrets.SetSecretAsync(account.SecretRef, "secret-from-store");
        var create = ProviderAliasCreateRequest.Random("shop.example", "sl.example", "shopping", now);

        var result = await adapter.CreateAliasAsync(account, create, secrets);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Alias);
        Assert.Equal("42", result.Alias.RemoteId);
        Assert.Equal("alias@sl.example", result.Alias.Address);
        Assert.Equal("shopping", result.Alias.Description);
        Assert.Equal(now, result.Alias.CreatedAt);

        Assert.Equal(2, handler.Requests.Count);
        var post = handler.Requests[1];
        Assert.Equal(HttpMethod.Post, post.Method);
        Assert.Equal("/api/alias/random/new?hostname=shop.example", post.PathAndQuery);
        Assert.Equal("secret-from-store", post.AuthenticationHeader);
        Assert.Contains("\"note\":\"shopping\"", post.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-from-store", post.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateRandomAliasMapsProviderErrorWithoutLeakingSecret()
    {
        var handler = new RecordingHandler(
            JsonResponse(HttpStatusCode.OK, """{"name":"Test","is_premium":true}"""),
            JsonResponse((HttpStatusCode)429, """{"error":"alias quota reached"}"""));
        var adapter = CreateAdapter(handler);
        var now = DateTimeOffset.UtcNow;
        var account = ProviderAccount.Create(ProviderTypes.SimpleLogin, "SimpleLogin", now).MarkSecretStored(now);
        var secrets = new InMemorySecretStore();
        await secrets.SetSecretAsync(account.SecretRef, "secret-from-store");

        var result = await adapter.CreateAliasAsync(
            account,
            ProviderAliasCreateRequest.Random("shop.example", "sl.example", null, now),
            secrets);

        Assert.False(result.Succeeded);
        Assert.Equal("http.429", result.ErrorCode);
        Assert.Equal(["alias quota reached"], result.Warnings);
        Assert.DoesNotContain("secret-from-store", string.Join('\n', result.Warnings), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateCustomAliasUsesSignedSuffixAndDefaultMailbox()
    {
        var handler = new RecordingHandler(
            JsonResponse(HttpStatusCode.OK, """{"name":"Test","is_premium":true}"""),
            JsonResponse(HttpStatusCode.OK, """
                {
                  "can_create": true,
                  "prefix_suggestion": "shop",
                  "suffixes": [
                    {"signed_suffix": ".free@other.example.SIGNED","suffix": ".free@other.example","is_custom": false,"is_premium": false},
                    {"signed_suffix": ".cloak@sl.example.SIGNED","suffix": ".cloak@sl.example","is_custom": true,"is_premium": false}
                  ]
                }
                """),
            JsonResponse(HttpStatusCode.OK, """
                {
                  "mailboxes": [
                    {"id": 9, "email": "secondary@example.test", "default": false, "verified": true},
                    {"id": 7, "email": "primary@example.test", "default": true, "verified": true}
                  ]
                }
                """),
            JsonResponse(HttpStatusCode.Created, """{"id":77,"email":"shop.cloak@sl.example","enabled":true,"creation_timestamp":1783209600,"note":"shopping"}"""));
        var adapter = CreateAdapter(handler);
        var now = DateTimeOffset.Parse("2026-07-05T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var account = ProviderAccount.Create(ProviderTypes.SimpleLogin, "SimpleLogin", now).MarkSecretStored(now);
        var secrets = new InMemorySecretStore();
        await secrets.SetSecretAsync(account.SecretRef, "secret-from-store");

        var result = await adapter.CreateAliasAsync(
            account,
            ProviderAliasCreateRequest.Custom("shop.example", "shop", "sl.example", "shopping", now),
            secrets);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Alias);
        Assert.Equal("77", result.Alias.RemoteId);
        Assert.Equal("shop.cloak@sl.example", result.Alias.Address);

        Assert.Equal(4, handler.Requests.Count);
        Assert.Equal("/api/user_info", handler.Requests[0].PathAndQuery);
        Assert.Equal("/api/v5/alias/options?hostname=shop.example", handler.Requests[1].PathAndQuery);
        Assert.Equal("/api/v2/mailboxes", handler.Requests[2].PathAndQuery);

        var post = handler.Requests[3];
        Assert.Equal(HttpMethod.Post, post.Method);
        Assert.Equal("/api/v3/alias/custom/new?hostname=shop.example", post.PathAndQuery);
        Assert.Contains("\"alias_prefix\":\"shop\"", post.Body, StringComparison.Ordinal);
        Assert.Contains("\"signed_suffix\":\".cloak@sl.example.SIGNED\"", post.Body, StringComparison.Ordinal);
        Assert.Contains("\"mailbox_ids\":[7]", post.Body, StringComparison.Ordinal);
        Assert.Contains("\"note\":\"shopping\"", post.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-from-store", post.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateCustomAliasFailsWhenDomainSuffixIsMissing()
    {
        var handler = new RecordingHandler(
            JsonResponse(HttpStatusCode.OK, """{"name":"Test","is_premium":true}"""),
            JsonResponse(HttpStatusCode.OK, """
                {
                  "can_create": true,
                  "prefix_suggestion": "shop",
                  "suffixes": [
                    {"signed_suffix": ".free@other.example.SIGNED","suffix": ".free@other.example","is_custom": false,"is_premium": false}
                  ]
                }
                """));
        var adapter = CreateAdapter(handler);
        var now = DateTimeOffset.UtcNow;
        var account = ProviderAccount.Create(ProviderTypes.SimpleLogin, "SimpleLogin", now).MarkSecretStored(now);
        var secrets = new InMemorySecretStore();
        await secrets.SetSecretAsync(account.SecretRef, "secret-from-store");

        var result = await adapter.CreateAliasAsync(
            account,
            ProviderAliasCreateRequest.Custom("shop.example", "shop", "sl.example", null, now),
            secrets);

        Assert.False(result.Succeeded);
        Assert.Equal("provider.suffix_not_found", result.ErrorCode);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task CreateCustomAliasStopsWhenSimpleLoginCannotCreate()
    {
        var handler = new RecordingHandler(
            JsonResponse(HttpStatusCode.OK, """{"name":"Test","is_premium":true}"""),
            JsonResponse(HttpStatusCode.OK, """{"can_create":false,"prefix_suggestion":"shop","suffixes":[]}"""));
        var adapter = CreateAdapter(handler);
        var now = DateTimeOffset.UtcNow;
        var account = ProviderAccount.Create(ProviderTypes.SimpleLogin, "SimpleLogin", now).MarkSecretStored(now);
        var secrets = new InMemorySecretStore();
        await secrets.SetSecretAsync(account.SecretRef, "secret-from-store");

        var result = await adapter.CreateAliasAsync(
            account,
            ProviderAliasCreateRequest.Custom("shop.example", "shop", "sl.example", null, now),
            secrets);

        Assert.False(result.Succeeded);
        Assert.Equal("provider.alias_create_not_allowed", result.ErrorCode);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task DisableAliasReadsStateBeforeToggle()
    {
        var handler = new RecordingHandler(
            JsonResponse(HttpStatusCode.OK, """{"name":"Test","is_premium":true}"""),
            JsonResponse(HttpStatusCode.OK, """{"id":42,"email":"alias@sl.example","enabled":true,"creation_timestamp":1783209600,"note":"shopping"}"""),
            JsonResponse(HttpStatusCode.OK, """{"enabled":false}"""));
        var adapter = CreateAdapter(handler);
        var now = DateTimeOffset.Parse("2026-07-05T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var account = ProviderAccount.Create(ProviderTypes.SimpleLogin, "SimpleLogin", now).MarkSecretStored(now);
        var secrets = new InMemorySecretStore();
        await secrets.SetSecretAsync(account.SecretRef, "secret-from-store");

        var result = await adapter.DisableAliasAsync(
            account,
            new ProviderAliasReference("42", "alias@sl.example", now),
            secrets);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Alias);
        Assert.Equal(AliasCockpit.Core.Aliases.AliasStatus.Disabled, result.Alias.Status);
        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal("/api/user_info", handler.Requests[0].PathAndQuery);
        Assert.Equal("/api/aliases/42", handler.Requests[1].PathAndQuery);
        Assert.Equal("/api/aliases/42/toggle", handler.Requests[2].PathAndQuery);
        Assert.Equal(HttpMethod.Post, handler.Requests[2].Method);
    }

    [Fact]
    public async Task DisableAliasDoesNotToggleWhenAlreadyDisabled()
    {
        var handler = new RecordingHandler(
            JsonResponse(HttpStatusCode.OK, """{"name":"Test","is_premium":true}"""),
            JsonResponse(HttpStatusCode.OK, """{"id":42,"email":"alias@sl.example","enabled":false,"creation_timestamp":1783209600,"note":"shopping"}"""));
        var adapter = CreateAdapter(handler);
        var now = DateTimeOffset.Parse("2026-07-05T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var account = ProviderAccount.Create(ProviderTypes.SimpleLogin, "SimpleLogin", now).MarkSecretStored(now);
        var secrets = new InMemorySecretStore();
        await secrets.SetSecretAsync(account.SecretRef, "secret-from-store");

        var result = await adapter.DisableAliasAsync(
            account,
            new ProviderAliasReference("42", "alias@sl.example", now),
            secrets);

        Assert.True(result.Succeeded);
        Assert.Equal(2, handler.Requests.Count);
        Assert.DoesNotContain(handler.Requests, request => request.PathAndQuery.EndsWith("/toggle", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DeleteAliasUsesSimpleLoginDeleteEndpoint()
    {
        var handler = new RecordingHandler(
            JsonResponse(HttpStatusCode.OK, """{"name":"Test","is_premium":true}"""),
            JsonResponse(HttpStatusCode.OK, """{"deleted":true}"""));
        var adapter = CreateAdapter(handler);
        var now = DateTimeOffset.UtcNow;
        var account = ProviderAccount.Create(ProviderTypes.SimpleLogin, "SimpleLogin", now).MarkSecretStored(now);
        var secrets = new InMemorySecretStore();
        await secrets.SetSecretAsync(account.SecretRef, "secret-from-store");

        var result = await adapter.DeleteAliasAsync(
            account,
            new ProviderAliasReference("42", "alias@sl.example", now),
            secrets);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Alias);
        Assert.Equal(AliasCockpit.Core.Aliases.AliasStatus.Deleted, result.Alias.Status);
        Assert.Equal(HttpMethod.Delete, handler.Requests[1].Method);
        Assert.Equal("/api/aliases/42", handler.Requests[1].PathAndQuery);
    }

    private static SimpleLoginHttpProviderAdapter CreateAdapter(RecordingHandler handler)
    {
        return new SimpleLoginHttpProviderAdapter(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://app.simplelogin.io"),
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
                request.Headers.TryGetValues("Authentication", out var values) ? values.SingleOrDefault() : null,
                body));

            return _responses.Count == 0
                ? throw new InvalidOperationException("No fake HTTP response queued.")
                : _responses.Dequeue();
        }
    }

    private sealed record CapturedRequest(
        HttpMethod Method,
        string PathAndQuery,
        string? AuthenticationHeader,
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

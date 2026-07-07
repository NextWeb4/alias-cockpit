using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AliasCockpit.Core.Aliases;
using AliasCockpit.Core.Providers;
using AliasCockpit.Core.Secrets;

namespace AliasCockpit.Infrastructure.Providers;

public sealed class SimpleLoginHttpProviderAdapter : ProviderAdapterBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;

    public SimpleLoginHttpProviderAdapter(HttpClient httpClient)
        : base(SimpleLoginProviderProfile.Create())
    {
        _httpClient = httpClient;
    }

    public override async Task<ProviderCredentialValidationResult> ValidateCredentialsAsync(
        ProviderAccount account,
        ISecretStore secretStore,
        CancellationToken cancellationToken = default)
    {
        var localValidation = await base.ValidateCredentialsAsync(account, secretStore, cancellationToken);
        if (!localValidation.CanAttemptNetwork)
        {
            return localValidation;
        }

        var secret = await secretStore.GetSecretAsync(account.SecretRef, cancellationToken);
        using var request = CreateRequest(HttpMethod.Get, "/api/user_info", secret!);
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return ProviderCredentialValidationResult.Ready();
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return ProviderCredentialValidationResult.Failure("auth.invalid", canRetry: false);
        }

        return ProviderCredentialValidationResult.Failure($"http.{(int)response.StatusCode}", canRetry: true);
    }

    public override async Task<ProviderAliasOperationResult> CreateAliasAsync(
        ProviderAccount account,
        ProviderAliasCreateRequest request,
        ISecretStore secretStore,
        CancellationToken cancellationToken = default)
    {
        var plan = PlanCreateAlias(request);
        if (!plan.CanExecute)
        {
            return ProviderAliasOperationResult.Failure("validation.failed", plan.Warnings);
        }

        var credential = await ValidateCredentialsAsync(account, secretStore, cancellationToken);
        if (!credential.CanAttemptNetwork)
        {
            return ProviderAliasOperationResult.Failure(credential.ErrorCode ?? "auth.failed", []);
        }

        var secret = await secretStore.GetSecretAsync(account.SecretRef, cancellationToken);
        if (!request.PreferRandom)
        {
            return await CreateCustomAliasAsync(request, secret!, plan.Warnings, cancellationToken);
        }

        using var httpRequest = CreateRequest(HttpMethod.Post, BuildRandomAliasPath(request), secret!);
        httpRequest.Content = CreateJsonContent(new SimpleLoginRandomAliasRequest(
            string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim()));

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await ReadErrorAsync(response, cancellationToken);
            return ProviderAliasOperationResult.Failure(
                response.StatusCode == HttpStatusCode.Unauthorized ? "auth.invalid" : $"http.{(int)response.StatusCode}",
                string.IsNullOrWhiteSpace(error) ? [] : [error]);
        }

        var alias = await ReadAliasAsync(response, request, cancellationToken);
        return alias is null
            ? ProviderAliasOperationResult.Failure("provider.response.invalid", [])
            : ProviderAliasOperationResult.Success(alias, plan.Warnings);
    }

    public override async Task<ProviderAliasOperationResult> DisableAliasAsync(
        ProviderAccount account,
        ProviderAliasReference alias,
        ISecretStore secretStore,
        CancellationToken cancellationToken = default)
    {
        var plan = PlanDisableAlias(alias);
        if (!plan.CanExecute)
        {
            return ProviderAliasOperationResult.Failure("validation.failed", plan.Warnings);
        }

        var credential = await ValidateCredentialsAsync(account, secretStore, cancellationToken);
        if (!credential.CanAttemptNetwork)
        {
            return ProviderAliasOperationResult.Failure(credential.ErrorCode ?? "auth.failed", []);
        }

        var secret = await secretStore.GetSecretAsync(account.SecretRef, cancellationToken);
        using var readRequest = CreateRequest(HttpMethod.Get, $"/api/aliases/{Uri.EscapeDataString(alias.RemoteId)}", secret!);
        using var readResponse = await _httpClient.SendAsync(readRequest, cancellationToken);
        if (!readResponse.IsSuccessStatusCode)
        {
            return await FailureFromResponseAsync(readResponse, cancellationToken);
        }

        var current = await ReadAliasAsync(readResponse, alias.RequestedAt, cancellationToken);
        if (current?.Status == AliasStatus.Disabled)
        {
            return ProviderAliasOperationResult.Success(current, plan.Warnings);
        }

        using var toggleRequest = CreateRequest(HttpMethod.Post, $"/api/aliases/{Uri.EscapeDataString(alias.RemoteId)}/toggle", secret!);
        using var toggleResponse = await _httpClient.SendAsync(toggleRequest, cancellationToken);
        if (!toggleResponse.IsSuccessStatusCode)
        {
            return await FailureFromResponseAsync(toggleResponse, cancellationToken);
        }

        var toggled = await ReadToggleAsync(toggleResponse, alias, cancellationToken);
        return ProviderAliasOperationResult.Success(toggled, plan.Warnings);
    }

    public override async Task<ProviderAliasOperationResult> DeleteAliasAsync(
        ProviderAccount account,
        ProviderAliasReference alias,
        ISecretStore secretStore,
        CancellationToken cancellationToken = default)
    {
        var plan = PlanDeleteAlias(alias);
        if (!plan.CanExecute)
        {
            return ProviderAliasOperationResult.Failure("validation.failed", plan.Warnings);
        }

        var credential = await ValidateCredentialsAsync(account, secretStore, cancellationToken);
        if (!credential.CanAttemptNetwork)
        {
            return ProviderAliasOperationResult.Failure(credential.ErrorCode ?? "auth.failed", []);
        }

        var secret = await secretStore.GetSecretAsync(account.SecretRef, cancellationToken);
        using var request = CreateRequest(HttpMethod.Delete, $"/api/aliases/{Uri.EscapeDataString(alias.RemoteId)}", secret!);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return await FailureFromResponseAsync(response, cancellationToken);
        }

        return ProviderAliasOperationResult.Success(
            new ProviderAliasSnapshot(alias.RemoteId, alias.Address ?? alias.RemoteId, AliasStatus.Deleted, null, alias.RequestedAt),
            plan.Warnings);
    }

    protected override string EndpointFor(ProviderCapability capability)
    {
        return capability switch
        {
            ProviderCapability.AliasCreateRandom => "POST /api/alias/random/new",
            ProviderCapability.AliasCreateCustom => "GET /api/v5/alias/options + POST /api/v3/alias/custom/new",
            ProviderCapability.AliasUpdateMetadata => "PATCH /api/aliases/{alias_id}",
            ProviderCapability.AliasDisable => "POST /api/aliases/{alias_id}/toggle",
            ProviderCapability.AliasDelete => "DELETE /api/aliases/{alias_id}",
            ProviderCapability.StatsRead => "GET /api/stats",
            _ => "SimpleLogin API capability endpoint",
        };
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string path, string secret)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("Authentication", secret);
        return request;
    }

    private static HttpContent CreateJsonContent<T>(T body)
    {
        var json = JsonSerializer.Serialize(body, JsonOptions);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private static string BuildRandomAliasPath(ProviderAliasCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Hostname))
        {
            return "/api/alias/random/new";
        }

        return $"/api/alias/random/new?hostname={Uri.EscapeDataString(request.Hostname.Trim())}";
    }

    private async Task<ProviderAliasOperationResult> CreateCustomAliasAsync(
        ProviderAliasCreateRequest request,
        string secret,
        IReadOnlyList<string> planWarnings,
        CancellationToken cancellationToken)
    {
        var options = await GetAliasOptionsAsync(request, secret, cancellationToken);
        if (!options.Succeeded)
        {
            return ProviderAliasOperationResult.Failure(options.ErrorCode!, options.Warnings);
        }

        if (!options.Value!.CanCreate)
        {
            return ProviderAliasOperationResult.Failure(
                "provider.alias_create_not_allowed",
                ["SimpleLogin reported that this account cannot create a new alias for the requested hostname."]);
        }

        var selectedSuffix = SelectSuffix(options.Value!, request.Domain);
        if (selectedSuffix is null)
        {
            return ProviderAliasOperationResult.Failure(
                "provider.suffix_not_found",
                [$"SimpleLogin did not return a signed suffix for domain '{request.Domain}'."]);
        }

        var mailboxes = await GetMailboxesAsync(secret, cancellationToken);
        if (!mailboxes.Succeeded)
        {
            return ProviderAliasOperationResult.Failure(mailboxes.ErrorCode!, mailboxes.Warnings);
        }

        var mailbox = SelectMailbox(mailboxes.Value!);
        if (mailbox is null)
        {
            return ProviderAliasOperationResult.Failure(
                "provider.mailbox_not_found",
                ["SimpleLogin did not return a usable mailbox for custom alias creation."]);
        }

        using var httpRequest = CreateRequest(HttpMethod.Post, BuildCustomAliasPath(request), secret);
        httpRequest.Content = CreateJsonContent(new SimpleLoginCustomAliasRequest(
            request.LocalPart!.Trim(),
            selectedSuffix.SignedSuffix!,
            [mailbox.Id],
            string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Name: null));

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await ReadErrorAsync(response, cancellationToken);
            return ProviderAliasOperationResult.Failure(
                response.StatusCode == HttpStatusCode.Unauthorized ? "auth.invalid" : $"http.{(int)response.StatusCode}",
                string.IsNullOrWhiteSpace(error) ? [] : [error]);
        }

        var alias = await ReadAliasAsync(response, request, cancellationToken);
        return alias is null
            ? ProviderAliasOperationResult.Failure("provider.response.invalid", [])
            : ProviderAliasOperationResult.Success(alias, planWarnings);
    }

    private async Task<ProviderHttpResult<SimpleLoginAliasOptionsResponse>> GetAliasOptionsAsync(
        ProviderAliasCreateRequest request,
        string secret,
        CancellationToken cancellationToken)
    {
        using var httpRequest = CreateRequest(HttpMethod.Get, BuildAliasOptionsPath(request), secret);
        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await ReadErrorAsync(response, cancellationToken);
            return ProviderHttpResult<SimpleLoginAliasOptionsResponse>.Failure(
                response.StatusCode == HttpStatusCode.Unauthorized ? "auth.invalid" : $"http.{(int)response.StatusCode}",
                string.IsNullOrWhiteSpace(error) ? [] : [error]);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var options = await JsonSerializer.DeserializeAsync<SimpleLoginAliasOptionsResponse>(stream, JsonOptions, cancellationToken);
        return options is null
            ? ProviderHttpResult<SimpleLoginAliasOptionsResponse>.Failure("provider.response.invalid", [])
            : ProviderHttpResult<SimpleLoginAliasOptionsResponse>.Success(options);
    }

    private async Task<ProviderHttpResult<SimpleLoginMailboxListResponse>> GetMailboxesAsync(
        string secret,
        CancellationToken cancellationToken)
    {
        using var httpRequest = CreateRequest(HttpMethod.Get, "/api/v2/mailboxes", secret);
        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await ReadErrorAsync(response, cancellationToken);
            return ProviderHttpResult<SimpleLoginMailboxListResponse>.Failure(
                response.StatusCode == HttpStatusCode.Unauthorized ? "auth.invalid" : $"http.{(int)response.StatusCode}",
                string.IsNullOrWhiteSpace(error) ? [] : [error]);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var mailboxes = await JsonSerializer.DeserializeAsync<SimpleLoginMailboxListResponse>(stream, JsonOptions, cancellationToken);
        return mailboxes is null
            ? ProviderHttpResult<SimpleLoginMailboxListResponse>.Failure("provider.response.invalid", [])
            : ProviderHttpResult<SimpleLoginMailboxListResponse>.Success(mailboxes);
    }

    private static string BuildAliasOptionsPath(ProviderAliasCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Hostname))
        {
            return "/api/v5/alias/options";
        }

        return $"/api/v5/alias/options?hostname={Uri.EscapeDataString(request.Hostname.Trim())}";
    }

    private static string BuildCustomAliasPath(ProviderAliasCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Hostname))
        {
            return "/api/v3/alias/custom/new";
        }

        return $"/api/v3/alias/custom/new?hostname={Uri.EscapeDataString(request.Hostname.Trim())}";
    }

    private static SimpleLoginAliasSuffix? SelectSuffix(SimpleLoginAliasOptionsResponse options, string requestedDomain)
    {
        var suffixes = (options.Suffixes ?? [])
            .Where(suffix => !string.IsNullOrWhiteSpace(suffix.Suffix) && !string.IsNullOrWhiteSpace(suffix.SignedSuffix))
            .ToList();
        var domain = NormalizeDomainForSuffix(requestedDomain);

        return suffixes
            .Where(suffix => DomainMatchesSuffix(domain, suffix.Suffix!))
            .OrderByDescending(suffix => suffix.IsCustom == true)
            .ThenBy(suffix => suffix.IsPremium == true)
            .FirstOrDefault();
    }

    private static SimpleLoginMailbox? SelectMailbox(SimpleLoginMailboxListResponse mailboxes)
    {
        var usable = (mailboxes.Mailboxes ?? [])
            .Where(mailbox => mailbox.Id > 0)
            .ToList();

        return usable.FirstOrDefault(mailbox => mailbox is { Default: true, Verified: true })
            ?? usable.FirstOrDefault(mailbox => mailbox.Verified == true)
            ?? usable.FirstOrDefault(mailbox => mailbox.Default == true)
            ?? usable.FirstOrDefault();
    }

    private static bool DomainMatchesSuffix(string requestedDomain, string suffix)
    {
        var suffixDomain = NormalizeDomainForSuffix(suffix);
        var atIndex = suffixDomain.LastIndexOf('@');
        if (atIndex >= 0 && atIndex < suffixDomain.Length - 1)
        {
            suffixDomain = suffixDomain[(atIndex + 1)..];
        }

        return string.Equals(suffixDomain, requestedDomain, StringComparison.Ordinal);
    }

    private static string NormalizeDomainForSuffix(string domain)
    {
        return domain.Trim().TrimStart('@').ToLowerInvariant();
    }

    private static async Task<ProviderAliasSnapshot?> ReadAliasAsync(
        HttpResponseMessage response,
        ProviderAliasCreateRequest request,
        CancellationToken cancellationToken)
    {
        return await ReadAliasAsync(response, request.RequestedAt, cancellationToken);
    }

    private static async Task<ProviderAliasSnapshot?> ReadAliasAsync(
        HttpResponseMessage response,
        DateTimeOffset fallbackCreatedAt,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var alias = await JsonSerializer.DeserializeAsync<SimpleLoginAliasResponse>(stream, JsonOptions, cancellationToken);
        if (string.IsNullOrWhiteSpace(alias?.Email))
        {
            return null;
        }

        var createdAt = alias.CreationTimestamp is > 0
            ? DateTimeOffset.FromUnixTimeSeconds(alias.CreationTimestamp.Value)
            : fallbackCreatedAt;

        return new ProviderAliasSnapshot(
            alias.Id?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? alias.Email,
            alias.Email,
            alias.Enabled == false ? AliasStatus.Disabled : AliasStatus.Active,
            string.IsNullOrWhiteSpace(alias.Note) ? alias.Name : alias.Note,
            createdAt);
    }

    private static async Task<ProviderAliasSnapshot> ReadToggleAsync(
        HttpResponseMessage response,
        ProviderAliasReference alias,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var toggle = await JsonSerializer.DeserializeAsync<SimpleLoginToggleResponse>(stream, JsonOptions, cancellationToken);
        return new ProviderAliasSnapshot(
            alias.RemoteId,
            alias.Address ?? alias.RemoteId,
            toggle?.Enabled == true ? AliasStatus.Active : AliasStatus.Disabled,
            Description: null,
            alias.RequestedAt);
    }

    private static async Task<ProviderAliasOperationResult> FailureFromResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var error = await ReadErrorAsync(response, cancellationToken);
        return ProviderAliasOperationResult.Failure(
            response.StatusCode == HttpStatusCode.Unauthorized ? "auth.invalid" : $"http.{(int)response.StatusCode}",
            string.IsNullOrWhiteSpace(error) ? [] : [error]);
    }

    private static async Task<string?> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength == 0)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        try
        {
            var error = await JsonSerializer.DeserializeAsync<SimpleLoginErrorResponse>(stream, JsonOptions, cancellationToken);
            return string.IsNullOrWhiteSpace(error?.Error) ? null : error.Error;
        }
        catch (JsonException)
        {
            return response.ReasonPhrase;
        }
    }

    private sealed record SimpleLoginRandomAliasRequest(
        [property: JsonPropertyName("note")] string? Note);

    private sealed record SimpleLoginCustomAliasRequest(
        [property: JsonPropertyName("alias_prefix")] string AliasPrefix,
        [property: JsonPropertyName("signed_suffix")] string SignedSuffix,
        [property: JsonPropertyName("mailbox_ids")] IReadOnlyList<int> MailboxIds,
        [property: JsonPropertyName("note")] string? Note,
        [property: JsonPropertyName("name")] string? Name);

    private sealed record SimpleLoginAliasResponse(
        [property: JsonPropertyName("id")] int? Id,
        [property: JsonPropertyName("email")] string? Email,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("enabled")] bool? Enabled,
        [property: JsonPropertyName("creation_timestamp")] long? CreationTimestamp,
        [property: JsonPropertyName("note")] string? Note);

    private sealed record SimpleLoginToggleResponse(
        [property: JsonPropertyName("enabled")] bool? Enabled);

    private sealed record SimpleLoginErrorResponse(
        [property: JsonPropertyName("error")] string? Error);

    private sealed record SimpleLoginAliasOptionsResponse(
        [property: JsonPropertyName("can_create")] bool CanCreate,
        [property: JsonPropertyName("prefix_suggestion")] string? PrefixSuggestion,
        [property: JsonPropertyName("suffixes")] IReadOnlyList<SimpleLoginAliasSuffix>? Suffixes);

    private sealed record SimpleLoginAliasSuffix(
        [property: JsonPropertyName("signed_suffix")] string? SignedSuffix,
        [property: JsonPropertyName("suffix")] string? Suffix,
        [property: JsonPropertyName("is_custom")] bool? IsCustom,
        [property: JsonPropertyName("is_premium")] bool? IsPremium);

    private sealed record SimpleLoginMailboxListResponse(
        [property: JsonPropertyName("mailboxes")] IReadOnlyList<SimpleLoginMailbox>? Mailboxes);

    private sealed record SimpleLoginMailbox(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("email")] string? Email,
        [property: JsonPropertyName("default")] bool? Default,
        [property: JsonPropertyName("verified")] bool? Verified);

    private sealed record ProviderHttpResult<T>(
        bool Succeeded,
        T? Value,
        string? ErrorCode,
        IReadOnlyList<string> Warnings)
    {
        public static ProviderHttpResult<T> Success(T value)
        {
            return new ProviderHttpResult<T>(true, value, null, []);
        }

        public static ProviderHttpResult<T> Failure(string errorCode, IReadOnlyList<string> warnings)
        {
            return new ProviderHttpResult<T>(false, default, errorCode, warnings);
        }
    }
}

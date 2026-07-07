using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AliasCockpit.Core.Aliases;
using AliasCockpit.Core.Providers;
using AliasCockpit.Core.Secrets;

namespace AliasCockpit.Infrastructure.Providers;

public sealed class AddyIoHttpProviderAdapter : ProviderAdapterBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _httpClient;

    public AddyIoHttpProviderAdapter(HttpClient httpClient)
        : base(AddyIoProviderProfile.Create())
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
        using var request = CreateRequest(HttpMethod.Get, "/api/v1/api-token-details", secret!);
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
        using var httpRequest = CreateRequest(HttpMethod.Post, "/api/v1/aliases", secret!);
        httpRequest.Content = CreateJsonContent(new AddyIoAliasCreateRequest(
            request.Domain.Trim().TrimStart('@').ToLowerInvariant(),
            string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            request.PreferRandom ? "uuid" : "custom",
            request.PreferRandom ? null : request.LocalPart?.Trim(),
            RecipientIds: null));

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
        using var request = CreateRequest(HttpMethod.Delete, $"/api/v1/active-aliases/{Uri.EscapeDataString(alias.RemoteId)}", secret!);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return await FailureFromResponseAsync(response, cancellationToken);
        }

        return ProviderAliasOperationResult.Success(
            new ProviderAliasSnapshot(alias.RemoteId, alias.Address ?? alias.RemoteId, AliasStatus.Disabled, null, alias.RequestedAt),
            plan.Warnings);
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
        using var request = CreateRequest(HttpMethod.Delete, $"/api/v1/aliases/{Uri.EscapeDataString(alias.RemoteId)}", secret!);
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
            ProviderCapability.AliasCreateRandom or ProviderCapability.AliasCreateCustom => "POST /api/v1/aliases",
            ProviderCapability.AliasUpdateMetadata => "PATCH /api/v1/aliases/{id}",
            ProviderCapability.AliasDisable => "DELETE /api/v1/active-aliases/{id}",
            ProviderCapability.AliasDelete => "DELETE /api/v1/aliases/{id}",
            ProviderCapability.AliasRestore => "PATCH /api/v1/aliases/{id}/restore",
            ProviderCapability.RecipientManage => "GET/POST /api/v1/recipients",
            ProviderCapability.RulesManage => "GET/POST /api/v1/rules",
            ProviderCapability.WebhookReceive => "GET/POST /api/v1/webhooks",
            _ => "addy.io API capability endpoint",
        };
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string path, string secret)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
        return request;
    }

    private static HttpContent CreateJsonContent<T>(T body)
    {
        var json = JsonSerializer.Serialize(body, JsonOptions);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private static async Task<ProviderAliasSnapshot?> ReadAliasAsync(
        HttpResponseMessage response,
        ProviderAliasCreateRequest request,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var envelope = await JsonSerializer.DeserializeAsync<AddyIoAliasEnvelope>(stream, JsonOptions, cancellationToken);
        if (string.IsNullOrWhiteSpace(envelope?.Data?.Email))
        {
            return null;
        }

        var createdAt = ParseAddyTimestamp(envelope.Data.CreatedAt) ?? request.RequestedAt;
        return new ProviderAliasSnapshot(
            envelope.Data.Id ?? envelope.Data.Email,
            envelope.Data.Email,
            envelope.Data.Active == false ? AliasStatus.Disabled : AliasStatus.Active,
            string.IsNullOrWhiteSpace(envelope.Data.Description) ? null : envelope.Data.Description,
            createdAt);
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
            var error = await JsonSerializer.DeserializeAsync<AddyIoErrorResponse>(stream, JsonOptions, cancellationToken);
            return !string.IsNullOrWhiteSpace(error?.Message)
                ? error.Message
                : error?.Error;
        }
        catch (JsonException)
        {
            return response.ReasonPhrase;
        }
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

    private static DateTimeOffset? ParseAddyTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            value,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
    }

    private sealed record AddyIoAliasCreateRequest(
        [property: JsonPropertyName("domain")] string Domain,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("format")] string Format,
        [property: JsonPropertyName("local_part")] string? LocalPart,
        [property: JsonPropertyName("recipient_ids")] IReadOnlyList<string>? RecipientIds);

    private sealed record AddyIoAliasEnvelope(
        [property: JsonPropertyName("data")] AddyIoAliasResponse? Data);

    private sealed record AddyIoAliasResponse(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("email")] string? Email,
        [property: JsonPropertyName("active")] bool? Active,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("created_at")] string? CreatedAt);

    private sealed record AddyIoErrorResponse(
        [property: JsonPropertyName("message")] string? Message,
        [property: JsonPropertyName("error")] string? Error);
}

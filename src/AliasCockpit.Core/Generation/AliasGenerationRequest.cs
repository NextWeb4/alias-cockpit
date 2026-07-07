namespace AliasCockpit.Core.Generation;

public sealed record AliasGenerationRequest(string Domain, AliasGenerationStrategy Strategy)
{
    public int Count { get; init; } = 5;

    public int MinEntropyBits { get; init; } = 40;

    public string? Site { get; init; }

    public string? Purpose { get; init; }

    public string? Template { get; init; }

    public PrivacyLevel PrivacyLevel { get; init; } = PrivacyLevel.High;
}


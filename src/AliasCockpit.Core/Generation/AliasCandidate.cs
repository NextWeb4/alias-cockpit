namespace AliasCockpit.Core.Generation;

public sealed record AliasCandidate(
    string LocalPart,
    string Domain,
    AliasGenerationStrategy Strategy,
    double EntropyBits,
    PrivacyLevel PrivacyLevel,
    IReadOnlyList<string> Warnings)
{
    public string Address => $"{LocalPart}@{Domain}";
}


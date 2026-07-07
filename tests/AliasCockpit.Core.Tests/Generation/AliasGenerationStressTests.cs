using AliasCockpit.Core.Generation;

namespace AliasCockpit.Core.Tests.Generation;

public sealed class AliasGenerationStressTests
{
    [Fact]
    public void StrongRandomBatchGenerationHandlesThousandsWithoutDuplicates()
    {
        var aliases = new AliasGenerator().Generate(new AliasGenerationRequest("stress.example", AliasGenerationStrategy.StrongRandom)
        {
            Count = 2_000,
            MinEntropyBits = 60,
        });

        Assert.Equal(2_000, aliases.Count);
        Assert.Equal(2_000, aliases.Select(alias => alias.Address).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(aliases, alias => Assert.True(alias.EntropyBits >= 60));
    }
}

using AliasCockpit.Core.Generation;

namespace AliasCockpit.Core.Tests.Generation;

public sealed class AliasGeneratorTests
{
    private readonly AliasGenerator _generator = new();

    [Fact]
    public void StrongRandomGeneratesRequestedAliasesWithMinimumEntropy()
    {
        var aliases = _generator.Generate(new AliasGenerationRequest("Example.COM", AliasGenerationStrategy.StrongRandom)
        {
            Count = 25,
            MinEntropyBits = 60,
        });

        Assert.Equal(25, aliases.Count);
        Assert.Equal(25, aliases.Select(alias => alias.Address).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(aliases, alias =>
        {
            Assert.Equal("example.com", alias.Domain);
            Assert.True(alias.EntropyBits >= 60);
            Assert.DoesNotContain('0', alias.LocalPart);
            Assert.DoesNotContain('1', alias.LocalPart);
            Assert.EndsWith("@example.com", alias.Address);
        });
    }

    [Fact]
    public void ReadableRandomKeepsHumanWordsAndStillMeetsEntropyFloor()
    {
        var alias = _generator.Generate(new AliasGenerationRequest("example.com", AliasGenerationStrategy.ReadableRandom)
        {
            Count = 1,
            MinEntropyBits = 45,
        }).Single();

        Assert.Matches("^[a-z]+-[a-z]+-[a-z2-9]+$", alias.LocalPart);
        Assert.True(alias.EntropyBits >= 45);
        Assert.NotEmpty(alias.Warnings);
    }

    [Fact]
    public void SiteAwareUsesSiteLabelButCountsOnlyRandomSuffixAsEntropy()
    {
        var alias = _generator.Generate(new AliasGenerationRequest("alias.test", AliasGenerationStrategy.SiteAware)
        {
            Count = 1,
            MinEntropyBits = 50,
            Site = "https://github.com/openai/codex",
        }).Single();

        Assert.StartsWith("github-", alias.LocalPart);
        Assert.True(alias.EntropyBits >= 50);
        Assert.Contains(alias.Warnings, warning => warning.Contains("reveal", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RuleTemplateAppendsRandomnessWhenTemplateIsTooWeak()
    {
        var alias = _generator.Generate(new AliasGenerationRequest("example.com", AliasGenerationStrategy.RuleTemplate)
        {
            Count = 1,
            MinEntropyBits = 40,
            Site = "Bank.Example",
            Template = "{{site}}-{{year}}",
        }).Single();

        Assert.StartsWith("bank-", alias.LocalPart);
        Assert.True(alias.EntropyBits >= 40);
    }

    [Fact]
    public void RuleTemplateSupportsExplicitRandomTokenLength()
    {
        var alias = _generator.Generate(new AliasGenerationRequest("example.com", AliasGenerationStrategy.RuleTemplate)
        {
            Count = 1,
            MinEntropyBits = 40,
            Site = "docs.example",
            Template = "{{site}}-{{rand:10}}",
        }).Single();

        Assert.Matches("^docs-[a-z2-9]{10}$", alias.LocalPart);
        Assert.True(alias.EntropyBits >= 50);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a domain")]
    [InlineData("example")]
    [InlineData("bad_domain.com")]
    public void InvalidDomainsAreRejected(string domain)
    {
        Assert.Throws<ArgumentException>(() => _generator.Generate(new AliasGenerationRequest(domain, AliasGenerationStrategy.StrongRandom)));
    }
}


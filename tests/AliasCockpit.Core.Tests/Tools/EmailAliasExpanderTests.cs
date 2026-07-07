using AliasCockpit.Core.Tools;

namespace AliasCockpit.Core.Tests.Tools;

public sealed class EmailAliasExpanderTests
{
    private readonly EmailAliasExpander _expander = new();

    [Fact]
    public void GmailAliasesNormalizeDotsPlusAndGooglemailDomain()
    {
        var result = _expander.Expand(new EmailAliasExpansionRequest("Fi.Rst+old@googlemail.com")
        {
            Tags = "login,pay",
            Count = 6,
            UseDotAliases = true,
            UsePlusAliases = true,
        });

        Assert.True(result.IsValid);
        Assert.Equal("first@gmail.com", result.CanonicalAddress);
        Assert.Equal(["first@gmail.com", "f.irst@gmail.com", "fi.rst@gmail.com"], result.DotAliases.Take(3));
        Assert.Contains("first+login@gmail.com", result.PlusAliases);
        Assert.Contains("f.irst+login@gmail.com", result.PlusAliases);
        Assert.Equal(12, result.Aliases.Count);
        Assert.Equal(
            result.DotAliases.Concat(result.PlusAliases).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            result.Aliases.Count);
        Assert.Equal(result.Aliases.Count, result.Aliases.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void AllAliasesAreUnionOfDotAndPlusAliasesWithoutPerCategoryCap()
    {
        var result = _expander.Expand(new EmailAliasExpansionRequest("First.Last@gmail.com")
        {
            Tags = EmailAliasExpander.DefaultTags(),
            Count = 32,
            UseDotAliases = true,
            UsePlusAliases = true,
        });

        Assert.True(result.IsValid);
        Assert.Equal(32, result.DotAliases.Count);
        Assert.Equal(32, result.PlusAliases.Count);
        Assert.Equal(64, result.Aliases.Count);
        Assert.Equal(
            result.DotAliases.Concat(result.PlusAliases).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            result.Aliases.Count);
    }

    [Fact]
    public void OutlookLikeAliasesSupportPlusButNotDots()
    {
        var result = _expander.Expand(new EmailAliasExpansionRequest("Work.Mail+old@outlook.com")
        {
            Tags = "login, invoice",
            Count = 8,
            UseDotAliases = true,
            UsePlusAliases = true,
        });

        Assert.True(result.IsValid);
        Assert.Equal("work.mail@outlook.com", result.CanonicalAddress);
        Assert.False(result.SupportsDotAliases);
        Assert.Empty(result.DotAliases);
        Assert.Equal(["work.mail+login@outlook.com", "work.mail+invoice@outlook.com"], result.PlusAliases);
    }

    [Fact]
    public void UnsupportedDomainsAreRejectedWithoutResults()
    {
        var result = _expander.Expand(new EmailAliasExpansionRequest("name@example.com")
        {
            Tags = "login",
            Count = 4,
        });

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.ValidationMessage);
        Assert.Empty(result.Aliases);
    }

    [Fact]
    public void TagsAreSanitized()
    {
        var result = _expander.Expand(new EmailAliasExpansionRequest("name@gmail.com")
        {
            Tags = "Log In,账单,pay_ok",
            Count = 8,
            UseDotAliases = false,
            UsePlusAliases = true,
        });

        Assert.True(result.IsValid);
        Assert.Contains("name+login@gmail.com", result.PlusAliases);
        Assert.Contains("name+pay_ok@gmail.com", result.PlusAliases);
        Assert.DoesNotContain(result.PlusAliases, alias => alias.Contains("账单", StringComparison.Ordinal));
    }

    [Fact]
    public void CountIsClampedAtMaximum()
    {
        var result = _expander.Expand(new EmailAliasExpansionRequest("longusername@gmail.com")
        {
            Count = 999,
            UseDotAliases = true,
            UsePlusAliases = false,
        });

        Assert.True(result.IsValid);
        Assert.Equal(EmailAliasExpander.MaxCount, result.Aliases.Count);
        Assert.Equal(EmailAliasExpander.MaxCount, result.DotAliases.Count);
    }
}




using AliasCockpit.Core.Product;

namespace AliasCockpit.Core.Tests.Product;

public sealed class ProductCreatorInfoTests
{
    [Fact]
    public void CreatorInfoIsHardCoded()
    {
        Assert.Equal("HaoXiang Huang", ProductCreatorInfo.Name);
        Assert.Equal("https://nextweb4.github.io/", ProductCreatorInfo.Website);
        Assert.Equal("Rays688888@Gmail.com", ProductCreatorInfo.Email);
        Assert.Contains(ProductCreatorInfo.Name, ProductCreatorInfo.DisplayText, StringComparison.Ordinal);
        Assert.Contains(ProductCreatorInfo.Website, ProductCreatorInfo.DisplayText, StringComparison.Ordinal);
        Assert.Contains(ProductCreatorInfo.Email, ProductCreatorInfo.DisplayText, StringComparison.Ordinal);
    }
}

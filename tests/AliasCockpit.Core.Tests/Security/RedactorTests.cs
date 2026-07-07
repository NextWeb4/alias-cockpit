using AliasCockpit.Core.Security;

namespace AliasCockpit.Core.Tests.Security;

public sealed class RedactorTests
{
    [Theory]
    [InlineData("alice@example.com", "a***e@example.com")]
    [InlineData("ab@example.com", "a*@example.com")]
    [InlineData("not-email", "[redacted]")]
    public void RedactEmailHidesLocalPart(string value, string expected)
    {
        Assert.Equal(expected, Redactor.RedactEmail(value));
    }
}


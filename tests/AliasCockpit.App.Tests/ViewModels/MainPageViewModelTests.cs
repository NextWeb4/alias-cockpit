using AliasCockpit.App.ViewModels;
using AliasCockpit.Core.Aliases;
using Xunit;

namespace AliasCockpit.App.Tests.ViewModels;

public sealed class MainPageViewModelTests
{
    [Fact]
    public void AllFilterCountMatchesDotAndPlusUnion()
    {
        var viewModel = new MainPageViewModel();

        viewModel.EmailAddress = "first.last@gmail.com";
        viewModel.CountText = "32";
        viewModel.UseDotAliases = true;
        viewModel.UsePlusAliases = true;
        viewModel.SelectFilter("all");

        Assert.Equal("32", viewModel.DotAliasCountText);
        Assert.Equal("32", viewModel.PlusAliasCountText);
        Assert.EndsWith("64", viewModel.FilterAllText);
        Assert.Contains("64", viewModel.GeneratedSummaryText, StringComparison.Ordinal);
        Assert.Equal(64, viewModel.GeneratedAliases.Count);
    }

    [Fact]
    public async Task MarkedAndUnmarkedFiltersSplitGeneratedAliases()
    {
        var repository = new InMemoryAliasRepository();
        var viewModel = new MainPageViewModel(repository, now: () => DateTimeOffset.UnixEpoch);

        viewModel.EmailAddress = "test.alias@gmail.com";
        viewModel.TagsText = "login";
        viewModel.CountText = "2";
        await viewModel.InitializePersistenceAsync();

        var total = viewModel.GeneratedAliases.Count;
        Assert.True(total > 1);

        var first = viewModel.GeneratedAliases[0];
        await viewModel.SaveAliasMarkerAsync(first, "github.com", "login", AliasColor.Green);

        Assert.Equal("Marked 1", viewModel.FilterMarkedText);
        Assert.Equal($"Unmarked {total - 1}", viewModel.FilterUnmarkedText);

        viewModel.SelectFilter("marked");
        Assert.Single(viewModel.GeneratedAliases);
        Assert.All(viewModel.GeneratedAliases, row => Assert.True(row.IsMarked));

        viewModel.SelectFilter("unmarked");
        Assert.Equal(total - 1, viewModel.GeneratedAliases.Count);
        Assert.All(viewModel.GeneratedAliases, row => Assert.False(row.IsMarked));
    }

    private sealed class InMemoryAliasRepository : IAliasRepository
    {
        private readonly Dictionary<string, AliasRecord> _records = new(StringComparer.OrdinalIgnoreCase);

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task UpsertAsync(AliasRecord alias, CancellationToken cancellationToken = default)
        {
            _records[alias.Address] = alias;
            return Task.CompletedTask;
        }

        public Task UpsertManyAsync(IEnumerable<AliasRecord> aliases, CancellationToken cancellationToken = default)
        {
            foreach (var alias in aliases)
            {
                _records[alias.Address] = alias;
            }

            return Task.CompletedTask;
        }

        public Task<AliasRecord?> GetByAddressAsync(string address, CancellationToken cancellationToken = default)
        {
            _records.TryGetValue(address, out var alias);
            return Task.FromResult(alias);
        }

        public Task<IReadOnlyList<AliasRecord>> SearchAsync(AliasSearchQuery query, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<AliasRecord>>(_records.Values.ToArray());
        }

        public Task<int> CountAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_records.Count);
        }
    }
}

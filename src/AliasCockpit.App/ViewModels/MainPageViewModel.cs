using System.Collections.ObjectModel;
using AliasCockpit.Core.Aliases;
using AliasCockpit.Core.Tools;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AliasCockpit.App.ViewModels;

public partial class MainPageViewModel : ObservableObject
{
    private readonly IAliasRepository? _aliasRepository;
    private readonly ISavedEmailAddressRepository? _savedEmailAddressRepository;
    private readonly Func<DateTimeOffset> _now;
    private readonly EmailAliasExpander _expander = new();
    private readonly Dictionary<string, AliasRecord> _metadataByAddress = new(StringComparer.OrdinalIgnoreCase);
    private EmailAliasExpansionResult _result;
    private string _activeFilter = "all";

    public MainPageViewModel(
        IAliasRepository? aliasRepository = null,
        ISavedEmailAddressRepository? savedEmailAddressRepository = null,
        Func<DateTimeOffset>? now = null)
    {
        _aliasRepository = aliasRepository;
        _savedEmailAddressRepository = savedEmailAddressRepository;
        _now = now ?? (() => DateTimeOffset.Now);
        _result = ExpandCurrentRequest();
        RefreshVisibleAliases();
    }

    [ObservableProperty]
    public partial string EmailAddress { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TagsText { get; set; } = EmailAliasExpander.DefaultTags();

    [ObservableProperty]
    public partial string CountText { get; set; } = "32";

    [ObservableProperty]
    public partial bool UseDotAliases { get; set; } = true;

    [ObservableProperty]
    public partial bool UsePlusAliases { get; set; } = true;

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "输入邮箱后自动生成本地结果。";

    public ObservableCollection<string> SavedEmailAddresses { get; } = [];

    public ObservableCollection<GeneratedAliasRowViewModel> GeneratedAliases { get; } = [];

    public string GeneratedSummaryText => $"已生成 {_result.Aliases.Count} 个地址";

    public string DotAliasCountText => _result.DotAliases.Count.ToStringInvariant();

    public string PlusAliasCountText => _result.PlusAliases.Count.ToStringInvariant();

    public string FilterAllText => $"全部 {_result.Aliases.Count}";

    public string FilterDotsText => $"Gmail 点号 {_result.DotAliases.Count}";

    public string FilterPlusText => $"+标签 {_result.PlusAliases.Count}";

    public string FilterMarkedText => $"Marked {MarkedAliasCount}";

    public string FilterUnmarkedText => $"Unmarked {UnmarkedAliasCount}";

    public string CurrentResultText => string.Join(Environment.NewLine, CurrentAddresses());

    public bool CanCopyCurrentResults => CurrentAddresses().Any();

    public bool CanSaveCurrentEmailAddress => _result.IsValid && _savedEmailAddressRepository is not null;

    public bool HasValidationMessage => !string.IsNullOrWhiteSpace(_result.ValidationMessage);

    public string ValidationMessage => _result.ValidationMessage;

    public bool DotAliasCheckboxEnabled => string.IsNullOrWhiteSpace(EmailAddress) || _result.SupportsDotAliases;

    public string AddressAnalysisText => string.IsNullOrWhiteSpace(EmailAddress)
        ? "支持 gmail.com / googlemail.com / outlook.com / hotmail.com / live.com / msn.com。"
        : _result.IsValid
            ? $"{_result.CanonicalAddress} | 点号: {YesNo(_result.SupportsDotAliases)} | +标签: {YesNo(_result.SupportsPlusAliases)}"
            : "当前输入不支持裂变。";

    private int MarkedAliasCount => _result.Aliases.Count(IsMarkedAddress);

    private int UnmarkedAliasCount => _result.Aliases.Count(address => !IsMarkedAddress(address));

    [RelayCommand]
    private void Generate()
    {
        RefreshResult();
    }

    [RelayCommand]
    private void RandomizeTags()
    {
        TagsText = EmailAliasExpander.RandomTags();
    }

    public async Task InitializePersistenceAsync(CancellationToken cancellationToken = default)
    {
        if (_aliasRepository is not null)
        {
            await _aliasRepository.InitializeAsync(cancellationToken);
        }

        if (_savedEmailAddressRepository is not null)
        {
            await _savedEmailAddressRepository.InitializeAsync(cancellationToken);
            await LoadSavedEmailAddressesAsync(cancellationToken);
        }

        await RefreshPersistedMetadataAsync(cancellationToken);
    }

    public async Task SaveCurrentEmailAddressAsync(CancellationToken cancellationToken = default)
    {
        if (_savedEmailAddressRepository is null || !_result.IsValid)
        {
            return;
        }

        var saved = SavedEmailAddress.Create(_result.CanonicalAddress, _now());
        await _savedEmailAddressRepository.UpsertAsync(saved, cancellationToken);
        await LoadSavedEmailAddressesAsync(cancellationToken);
        EmailAddress = saved.Address;
        StatusMessage = $"已保存输入邮箱 {saved.Address}。";
        NotifyResultProperties();
    }

    public async Task UseSavedEmailAddressAsync(string? address, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return;
        }

        EmailAddress = address.Trim();
        if (_savedEmailAddressRepository is not null)
        {
            await _savedEmailAddressRepository.UpsertAsync(SavedEmailAddress.Create(EmailAddress, _now()), cancellationToken);
            await LoadSavedEmailAddressesAsync(cancellationToken);
        }

        StatusMessage = $"已载入保存邮箱 {EmailAddress}。";
        NotifyResultProperties();
    }

    public async Task SaveAliasMarkerAsync(
        GeneratedAliasRowViewModel row,
        string? site,
        string? purpose,
        AliasColor color,
        CancellationToken cancellationToken = default)
    {
        if (_aliasRepository is null)
        {
            return;
        }

        var now = _now();
        var existing = await _aliasRepository.GetByAddressAsync(row.Address, cancellationToken);
        var record = existing is null
            ? AliasRecord.Create(row.Address, AliasStatus.Active, "LocalExpander", site, purpose, row.Kind, 0, now, color)
            : existing with
            {
                Site = NormalizeOptional(site),
                Purpose = NormalizeOptional(purpose),
                Color = color,
                UpdatedAt = now,
            };

        await _aliasRepository.UpsertAsync(record, cancellationToken);
        _metadataByAddress[record.Address] = record;
        RefreshVisibleAliases();
        StatusMessage = record.Color == AliasColor.None && string.IsNullOrWhiteSpace(record.Site) && string.IsNullOrWhiteSpace(record.Purpose)
            ? $"已清除 {record.Address} 的标记。"
            : $"已保存 {record.Address} 的标记。";
        NotifyResultProperties();
    }

    public async Task RefreshPersistedMetadataAsync(CancellationToken cancellationToken = default)
    {
        _metadataByAddress.Clear();
        if (_aliasRepository is null)
        {
            RefreshVisibleAliases();
            NotifyResultProperties();
            return;
        }

        foreach (var address in _result.Aliases.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var alias = await _aliasRepository.GetByAddressAsync(address, cancellationToken);
            if (alias is not null)
            {
                _metadataByAddress[alias.Address] = alias;
            }
        }

        RefreshVisibleAliases();
        NotifyResultProperties();
    }

    public void ReportPersistenceError(Exception exception)
    {
        StatusMessage = $"本地保存不可用：{exception.Message}";
    }

    public void SelectFilter(string filter)
    {
        _activeFilter = filter is "dots" or "plus" or "marked" or "unmarked" ? filter : "all";
        RefreshVisibleAliases();
        NotifyResultProperties();
    }

    public void MarkCopied(int count)
    {
        StatusMessage = count == 1 ? "已复制 1 个地址。" : $"已复制 {count} 个地址。";
    }

    partial void OnEmailAddressChanged(string value)
    {
        RefreshResult();
    }

    partial void OnTagsTextChanged(string value)
    {
        RefreshResult();
    }

    partial void OnCountTextChanged(string value)
    {
        RefreshResult();
    }

    partial void OnUseDotAliasesChanged(bool value)
    {
        RefreshResult();
    }

    partial void OnUsePlusAliasesChanged(bool value)
    {
        RefreshResult();
    }

    private async Task LoadSavedEmailAddressesAsync(CancellationToken cancellationToken)
    {
        if (_savedEmailAddressRepository is null)
        {
            return;
        }

        var savedAddresses = await _savedEmailAddressRepository.ListAsync(cancellationToken: cancellationToken);
        SavedEmailAddresses.Clear();
        foreach (var savedAddress in savedAddresses)
        {
            SavedEmailAddresses.Add(savedAddress.Address);
        }
    }

    private void RefreshResult()
    {
        _result = ExpandCurrentRequest();

        if (!_result.SupportsDotAliases && UseDotAliases && !string.IsNullOrWhiteSpace(EmailAddress))
        {
            UseDotAliases = false;
            return;
        }

        RefreshVisibleAliases();
        StatusMessage = HasValidationMessage ? ValidationMessage : "结果仅在本机生成，未发起网络请求。";
        NotifyResultProperties();
    }

    private EmailAliasExpansionResult ExpandCurrentRequest()
    {
        return _expander.Expand(new EmailAliasExpansionRequest(EmailAddress)
        {
            Tags = TagsText,
            Count = ParseCount(CountText),
            UseDotAliases = UseDotAliases,
            UsePlusAliases = UsePlusAliases,
        });
    }

    private void RefreshVisibleAliases()
    {
        GeneratedAliases.Clear();
        var source = CurrentAddresses();
        foreach (var address in source)
        {
            _metadataByAddress.TryGetValue(address, out var metadata);
            GeneratedAliases.Add(new GeneratedAliasRowViewModel(
                address,
                ClassifyAddress(address),
                metadata?.Site,
                metadata?.Purpose,
                metadata?.Color ?? AliasColor.None));
        }
    }

    private IReadOnlyList<string> CurrentAddresses()
    {
        return _activeFilter switch
        {
            "dots" => _result.DotAliases,
            "plus" => _result.PlusAliases,
            "marked" => _result.Aliases.Where(IsMarkedAddress).ToArray(),
            "unmarked" => _result.Aliases.Where(address => !IsMarkedAddress(address)).ToArray(),
            _ => _result.Aliases,
        };
    }

    private bool IsMarkedAddress(string address)
    {
        return _metadataByAddress.TryGetValue(address, out var metadata)
            && (metadata.Color != AliasColor.None
                || !string.IsNullOrWhiteSpace(metadata.Site)
                || !string.IsNullOrWhiteSpace(metadata.Purpose));
    }

    private string ClassifyAddress(string address)
    {
        if (_result.PlusAliases.Contains(address, StringComparer.OrdinalIgnoreCase))
        {
            return "+标签";
        }

        if (_result.DotAliases.Contains(address, StringComparer.OrdinalIgnoreCase)
            && !string.Equals(address, _result.CanonicalAddress, StringComparison.OrdinalIgnoreCase))
        {
            return "点号";
        }

        return "基础";
    }

    private void NotifyResultProperties()
    {
        OnPropertyChanged(nameof(GeneratedSummaryText));
        OnPropertyChanged(nameof(DotAliasCountText));
        OnPropertyChanged(nameof(PlusAliasCountText));
        OnPropertyChanged(nameof(FilterAllText));
        OnPropertyChanged(nameof(FilterDotsText));
        OnPropertyChanged(nameof(FilterPlusText));
        OnPropertyChanged(nameof(FilterMarkedText));
        OnPropertyChanged(nameof(FilterUnmarkedText));
        OnPropertyChanged(nameof(CurrentResultText));
        OnPropertyChanged(nameof(CanCopyCurrentResults));
        OnPropertyChanged(nameof(CanSaveCurrentEmailAddress));
        OnPropertyChanged(nameof(HasValidationMessage));
        OnPropertyChanged(nameof(ValidationMessage));
        OnPropertyChanged(nameof(DotAliasCheckboxEnabled));
        OnPropertyChanged(nameof(AddressAnalysisText));
    }

    private static int ParseCount(string value)
    {
        return int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var count)
            ? count
            : 32;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string YesNo(bool value)
    {
        return value ? "支持" : "不支持";
    }
}

public sealed record GeneratedAliasRowViewModel(
    string Address,
    string Kind,
    string? Site,
    string? Purpose,
    AliasColor Color)
{
    public bool IsMarked => Color != AliasColor.None || !string.IsNullOrWhiteSpace(Site) || !string.IsNullOrWhiteSpace(Purpose);

    public string MarkerSummary
    {
        get
        {
            var parts = new[] { Site, Purpose }
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .Select(part => part!.Trim())
                .ToArray();
            return parts.Length == 0 ? "未标记" : string.Join(" | ", parts);
        }
    }
}

file static class InvariantFormatting
{
    public static string ToStringInvariant(this int value)
    {
        return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}

using AliasCockpit.App.ViewModels;
using AliasCockpit.Core.Aliases;
using AliasCockpit.Core.Product;
using AliasCockpit.Infrastructure.Storage;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;

namespace AliasCockpit.App;

public sealed partial class MainPage : Page
{
    private readonly SolidColorBrush _borderBrush = new(Colors.Gray);
    private bool _isRendering;

    private TextBlock _statusText = null!;
    private TextBlock _addressAnalysisText = null!;
    private TextBlock _validationText = null!;
    private TextBlock _generatedSummaryText = null!;
    private TextBlock _dotAliasCountText = null!;
    private TextBlock _plusAliasCountText = null!;
    private TextBox _emailBox = null!;
    private ComboBox _savedEmailBox = null!;
    private Button _saveEmailButton = null!;
    private TextBox _tagsBox = null!;
    private TextBox _countBox = null!;
    private CheckBox _dotAliasesBox = null!;
    private CheckBox _plusAliasesBox = null!;
    private Button _allFilterButton = null!;
    private Button _dotsFilterButton = null!;
    private Button _plusFilterButton = null!;
    private Button _markedFilterButton = null!;
    private Button _unmarkedFilterButton = null!;
    private TextBlock _selectedAliasText = null!;
    private TextBox _siteBox = null!;
    private TextBox _purposeBox = null!;
    private ComboBox _colorBox = null!;
    private Button _saveMarkerButton = null!;
    private Button _clearMarkerButton = null!;
    private ListView _resultsList = null!;

    public MainPageViewModel ViewModel { get; }

    public MainPage()
    {
        InitializeComponent();
        ViewModel = new MainPageViewModel(
            new SqliteAliasRepository(GetLocalDatabasePath()),
            new SqliteSavedEmailAddressRepository(GetLocalDatabasePath()));
        BuildLayout();
        RenderInputs();
        RenderResults();
        Loaded += MainPage_Loaded;
    }

    private void BuildLayout()
    {
        AutomationProperties.SetAutomationId(RootGrid, "MainPageRoot");
        AutomationProperties.SetName(RootGrid, $"Email Alias Expander by {ProductCreatorInfo.Name}");

        RootGrid.Padding = new Thickness(16);
        RootGrid.RowSpacing = 12;
        RootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        RootGrid.Children.Add(BuildHeader());

        var body = new Grid { ColumnSpacing = 12 };
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(body, 1);
        RootGrid.Children.Add(body);

        body.Children.Add(BuildInputPanel());
        var resultPanel = BuildResultPanel();
        Grid.SetColumn(resultPanel, 1);
        body.Children.Add(resultPanel);
    }

    private FrameworkElement BuildHeader()
    {
        var header = new Grid { ColumnSpacing = 12 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var mark = new Image
        {
            Source = new BitmapImage(new Uri("ms-appx:///Assets/Square44x44Logo.scale-200.png")),
            Width = 44,
            Height = 44,
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(mark, "Alias Cockpit privacy alias shield logo");
        header.Children.Add(mark);

        var textStack = new StackPanel { Spacing = 2 };
        textStack.Children.Add(new TextBlock
        {
            Text = "Email Alias Expander",
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
        });
        _statusText = new TextBlock { FontSize = 12, Opacity = 0.75 };
        AutomationProperties.SetAutomationId(_statusText, "StatusMessageText");
        textStack.Children.Add(_statusText);
        Grid.SetColumn(textStack, 1);
        header.Children.Add(textStack);

        var copySelected = CreateButton("\uE8C8", "Copy selected");
        AutomationProperties.SetAutomationId(copySelected, "CopySelectedButton");
        copySelected.Click += CopySelected_Click;
        Grid.SetColumn(copySelected, 2);
        header.Children.Add(copySelected);

        var copyAll = CreateButton("\uE8C8", "Copy all");
        AutomationProperties.SetAutomationId(copyAll, "CopyAllButton");
        copyAll.Click += CopyAll_Click;
        Grid.SetColumn(copyAll, 3);
        header.Children.Add(copyAll);

        return header;
    }

    private FrameworkElement BuildInputPanel()
    {
        var border = CreatePanelBorder();
        var scrollViewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var stack = new StackPanel { Spacing = 14 };

        var intro = new StackPanel { Spacing = 4 };
        intro.Children.Add(new TextBlock
        {
            Text = "Input",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
        });
        _addressAnalysisText = new TextBlock { FontSize = 12, Opacity = 0.75, TextWrapping = TextWrapping.Wrap };
        AutomationProperties.SetAutomationId(_addressAnalysisText, "AddressAnalysisText");
        intro.Children.Add(_addressAnalysisText);
        stack.Children.Add(intro);

        _emailBox = new TextBox
        {
            Header = "Email address",
            PlaceholderText = "name@gmail.com / name@outlook.com",
            IsSpellCheckEnabled = false,
        };
        AutomationProperties.SetAutomationId(_emailBox, "EmailAddressBox");
        AutomationProperties.SetName(_emailBox, "Email address");
        _emailBox.TextChanged += TextInput_Changed;
        stack.Children.Add(_emailBox);

        _tagsBox = new TextBox
        {
            Header = "Tags",
            AcceptsReturn = true,
            Height = 120,
            IsSpellCheckEnabled = false,
            TextWrapping = TextWrapping.Wrap,
        };
        AutomationProperties.SetAutomationId(_tagsBox, "TagsBox");
        AutomationProperties.SetName(_tagsBox, "Tags");
        _tagsBox.TextChanged += TextInput_Changed;
        stack.Children.Add(_tagsBox);

        var countGrid = new Grid { ColumnSpacing = 8 };
        countGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        countGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _countBox = new TextBox { Header = "Count" };
        AutomationProperties.SetAutomationId(_countBox, "CountBox");
        AutomationProperties.SetName(_countBox, "Count");
        _countBox.TextChanged += TextInput_Changed;
        countGrid.Children.Add(_countBox);

        var randomButton = CreateIconOnlyButton("\uE8B1");
        AutomationProperties.SetAutomationId(randomButton, "RandomizeTagsButton");
        AutomationProperties.SetName(randomButton, "Randomize tags");
        randomButton.Margin = new Thickness(0, 24, 0, 0);
        randomButton.Click += RandomizeTags_Click;
        Grid.SetColumn(randomButton, 1);
        countGrid.Children.Add(randomButton);
        stack.Children.Add(countGrid);

        var savedGrid = new Grid { ColumnSpacing = 8 };
        savedGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        savedGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _savedEmailBox = new ComboBox
        {
            Header = "Saved email addresses",
            PlaceholderText = "Select saved email",
            ItemsSource = ViewModel.SavedEmailAddresses,
            MinWidth = 240,
        };
        AutomationProperties.SetAutomationId(_savedEmailBox, "SavedEmailAddressBox");
        AutomationProperties.SetName(_savedEmailBox, "Saved email addresses");
        _savedEmailBox.SelectionChanged += SavedEmailBox_SelectionChanged;
        savedGrid.Children.Add(_savedEmailBox);

        _saveEmailButton = CreateButton("\uE74E", "Save");
        AutomationProperties.SetAutomationId(_saveEmailButton, "SaveEmailAddressButton");
        AutomationProperties.SetName(_saveEmailButton, "Save email address");
        _saveEmailButton.Margin = new Thickness(0, 24, 0, 0);
        _saveEmailButton.Click += SaveEmailButton_Click;
        Grid.SetColumn(_saveEmailButton, 1);
        savedGrid.Children.Add(_saveEmailButton);
        stack.Children.Add(savedGrid);

        _dotAliasesBox = new CheckBox { Content = "Gmail dot aliases" };
        AutomationProperties.SetAutomationId(_dotAliasesBox, "DotAliasesCheckBox");
        AutomationProperties.SetName(_dotAliasesBox, "Gmail dot aliases");
        _dotAliasesBox.Checked += CheckInput_Changed;
        _dotAliasesBox.Unchecked += CheckInput_Changed;
        stack.Children.Add(_dotAliasesBox);

        _plusAliasesBox = new CheckBox { Content = "+tag aliases" };
        AutomationProperties.SetAutomationId(_plusAliasesBox, "PlusAliasesCheckBox");
        AutomationProperties.SetName(_plusAliasesBox, "+tag aliases");
        _plusAliasesBox.Checked += CheckInput_Changed;
        _plusAliasesBox.Unchecked += CheckInput_Changed;
        stack.Children.Add(_plusAliasesBox);

        _validationText = new TextBlock
        {
            Foreground = new SolidColorBrush(Colors.DarkGoldenrod),
            TextWrapping = TextWrapping.Wrap,
        };
        AutomationProperties.SetAutomationId(_validationText, "ValidationMessageText");
        stack.Children.Add(_validationText);

        stack.Children.Add(BuildSummaryPanel());

        scrollViewer.Content = stack;
        border.Child = scrollViewer;
        return border;
    }

    private FrameworkElement BuildSummaryPanel()
    {
        var border = CreatePanelBorder();
        border.Padding = new Thickness(10);

        var grid = new Grid { ColumnSpacing = 8, RowSpacing = 8 };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        AddSummaryRow(grid, 0, "Results", out _generatedSummaryText);
        AddSummaryRow(grid, 1, "Gmail dot aliases", out _dotAliasCountText);
        AddSummaryRow(grid, 2, "+tag aliases", out _plusAliasCountText);
        AutomationProperties.SetAutomationId(_generatedSummaryText, "GeneratedSummaryText");
        AutomationProperties.SetAutomationId(_dotAliasCountText, "DotAliasCountText");
        AutomationProperties.SetAutomationId(_plusAliasCountText, "PlusAliasCountText");

        border.Child = grid;
        return border;
    }

    private FrameworkElement BuildResultPanel()
    {
        var border = CreatePanelBorder();
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new Grid { Padding = new Thickness(14, 12, 14, 12), ColumnSpacing = 12 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(new TextBlock
        {
            Text = "Results",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
        });
        var creatorStack = new StackPanel
        {
            Spacing = 2,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        creatorStack.Children.Add(new TextBlock
        {
            Text = $"Local only | By {ProductCreatorInfo.Name}",
            FontSize = 12,
            Opacity = 0.75,
            HorizontalAlignment = HorizontalAlignment.Right,
        });
        var creatorLinks = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        creatorLinks.Children.Add(CreateCreatorLink(
            ProductCreatorInfo.Website,
            ProductCreatorInfo.Website,
            "CreatorWebsiteLink"));
        creatorLinks.Children.Add(CreateCreatorLink(
            ProductCreatorInfo.Email,
            $"mailto:{ProductCreatorInfo.Email}",
            "CreatorEmailLink"));
        creatorStack.Children.Add(creatorLinks);
        Grid.SetColumn(creatorStack, 1);
        header.Children.Add(creatorStack);
        grid.Children.Add(header);

        var filters = new StackPanel
        {
            Padding = new Thickness(14, 0, 14, 12),
            Orientation = Orientation.Horizontal,
            Spacing = 8,
        };
        _allFilterButton = new Button();
        AutomationProperties.SetAutomationId(_allFilterButton, "AllFilterButton");
        AutomationProperties.SetName(_allFilterButton, "All results filter");
        _allFilterButton.Click += FilterAll_Click;
        filters.Children.Add(_allFilterButton);
        _dotsFilterButton = new Button();
        AutomationProperties.SetAutomationId(_dotsFilterButton, "DotsFilterButton");
        AutomationProperties.SetName(_dotsFilterButton, "Gmail dot aliases filter");
        _dotsFilterButton.Click += FilterDots_Click;
        filters.Children.Add(_dotsFilterButton);
        _plusFilterButton = new Button();
        AutomationProperties.SetAutomationId(_plusFilterButton, "PlusFilterButton");
        AutomationProperties.SetName(_plusFilterButton, "+tag aliases filter");
        _plusFilterButton.Click += FilterPlus_Click;
        filters.Children.Add(_plusFilterButton);
        _markedFilterButton = new Button();
        AutomationProperties.SetAutomationId(_markedFilterButton, "MarkedFilterButton");
        AutomationProperties.SetName(_markedFilterButton, "Marked aliases filter");
        _markedFilterButton.Click += FilterMarked_Click;
        filters.Children.Add(_markedFilterButton);
        _unmarkedFilterButton = new Button();
        AutomationProperties.SetAutomationId(_unmarkedFilterButton, "UnmarkedFilterButton");
        AutomationProperties.SetName(_unmarkedFilterButton, "Unmarked aliases filter");
        _unmarkedFilterButton.Click += FilterUnmarked_Click;
        filters.Children.Add(_unmarkedFilterButton);
        Grid.SetRow(filters, 1);
        grid.Children.Add(filters);

        var markerPanel = BuildMarkerPanel();
        Grid.SetRow(markerPanel, 2);
        grid.Children.Add(markerPanel);

        _resultsList = new ListView();
        AutomationProperties.SetAutomationId(_resultsList, "ResultsList");
        AutomationProperties.SetName(_resultsList, "Generated aliases");
        _resultsList.SelectionChanged += ResultsList_SelectionChanged;
        Grid.SetRow(_resultsList, 3);
        grid.Children.Add(_resultsList);

        border.Child = grid;
        return border;
    }

    private FrameworkElement BuildMarkerPanel()
    {
        var grid = new Grid
        {
            Padding = new Thickness(14, 0, 14, 12),
            ColumnSpacing = 8,
            RowSpacing = 8,
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _selectedAliasText = new TextBlock
        {
            Text = "-",
            FontSize = 12,
            Opacity = 0.75,
            TextWrapping = TextWrapping.Wrap,
        };
        AutomationProperties.SetAutomationId(_selectedAliasText, "SelectedAliasText");
        Grid.SetColumnSpan(_selectedAliasText, 5);
        grid.Children.Add(_selectedAliasText);

        _siteBox = new TextBox
        {
            Header = "Used at",
            PlaceholderText = "github.com",
            IsSpellCheckEnabled = false,
        };
        AutomationProperties.SetAutomationId(_siteBox, "AliasSiteBox");
        AutomationProperties.SetName(_siteBox, "Alias used at");
        Grid.SetRow(_siteBox, 1);
        grid.Children.Add(_siteBox);

        _purposeBox = new TextBox
        {
            Header = "Purpose",
            PlaceholderText = "login / billing",
            IsSpellCheckEnabled = false,
        };
        AutomationProperties.SetAutomationId(_purposeBox, "AliasPurposeBox");
        AutomationProperties.SetName(_purposeBox, "Alias purpose");
        Grid.SetRow(_purposeBox, 1);
        Grid.SetColumn(_purposeBox, 1);
        grid.Children.Add(_purposeBox);

        _colorBox = new ComboBox
        {
            Header = "Color",
            ItemsSource = ColorOptions,
            DisplayMemberPath = nameof(ColorOption.Label),
        };
        AutomationProperties.SetAutomationId(_colorBox, "AliasColorBox");
        AutomationProperties.SetName(_colorBox, "Alias color");
        Grid.SetRow(_colorBox, 1);
        Grid.SetColumn(_colorBox, 2);
        grid.Children.Add(_colorBox);

        _saveMarkerButton = CreateButton("\uE74E", "Save mark");
        AutomationProperties.SetAutomationId(_saveMarkerButton, "SaveAliasMarkerButton");
        AutomationProperties.SetName(_saveMarkerButton, "Save alias marker");
        _saveMarkerButton.Margin = new Thickness(0, 24, 0, 0);
        _saveMarkerButton.Click += SaveMarkerButton_Click;
        Grid.SetRow(_saveMarkerButton, 1);
        Grid.SetColumn(_saveMarkerButton, 3);
        grid.Children.Add(_saveMarkerButton);

        _clearMarkerButton = CreateButton("\uE74D", "Clear");
        AutomationProperties.SetAutomationId(_clearMarkerButton, "ClearAliasMarkerButton");
        AutomationProperties.SetName(_clearMarkerButton, "Clear alias marker");
        _clearMarkerButton.Margin = new Thickness(0, 24, 0, 0);
        _clearMarkerButton.Click += ClearMarkerButton_Click;
        Grid.SetRow(_clearMarkerButton, 1);
        Grid.SetColumn(_clearMarkerButton, 4);
        grid.Children.Add(_clearMarkerButton);

        return grid;
    }

    private async void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        await RunPersistenceActionAsync(
            async () => await ViewModel.InitializePersistenceAsync(),
            renderInputs: true);
    }

    private async void TextInput_Changed(object sender, TextChangedEventArgs e)
    {
        ApplyInputs();
        await RefreshMetadataAndRenderAsync();
    }

    private async void CheckInput_Changed(object sender, RoutedEventArgs e)
    {
        ApplyInputs();
        await RefreshMetadataAndRenderAsync();
    }

    private void ApplyInputs()
    {
        if (_isRendering)
        {
            return;
        }

        ViewModel.EmailAddress = _emailBox.Text;
        ViewModel.TagsText = _tagsBox.Text;
        ViewModel.CountText = _countBox.Text;
        ViewModel.UseDotAliases = _dotAliasesBox.IsChecked == true;
        ViewModel.UsePlusAliases = _plusAliasesBox.IsChecked == true;
        RenderResults();
    }

    private async void RandomizeTags_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.RandomizeTagsCommand.Execute(null);
        RenderInputs();
        RenderResults();
        await RefreshMetadataAndRenderAsync();
    }

    private void FilterAll_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectFilter("all");
        RenderResults();
    }

    private void FilterDots_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectFilter("dots");
        RenderResults();
    }

    private void FilterPlus_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectFilter("plus");
        RenderResults();
    }

    private void FilterMarked_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectFilter("marked");
        RenderResults();
    }

    private void FilterUnmarked_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectFilter("unmarked");
        RenderResults();
    }

    private async void SavedEmailBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRendering || _savedEmailBox.SelectedItem is not string address)
        {
            return;
        }

        await RunPersistenceActionAsync(async () =>
        {
            await ViewModel.UseSavedEmailAddressAsync(address);
            await ViewModel.RefreshPersistedMetadataAsync();
        }, renderInputs: true);
    }

    private async void SaveEmailButton_Click(object sender, RoutedEventArgs e)
    {
        await RunPersistenceActionAsync(async () => await ViewModel.SaveCurrentEmailAddressAsync(), renderInputs: true);
    }

    private void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RenderMarkerPanel();
    }

    private async void SaveMarkerButton_Click(object sender, RoutedEventArgs e)
    {
        if (GetSelectedAliasRow() is not { } row)
        {
            return;
        }

        var color = _colorBox.SelectedItem is ColorOption option ? option.Color : AliasColor.None;
        await RunPersistenceActionAsync(async () =>
        {
            await ViewModel.SaveAliasMarkerAsync(row, _siteBox.Text, _purposeBox.Text, color);
            await ViewModel.RefreshPersistedMetadataAsync();
        });
    }

    private async void ClearMarkerButton_Click(object sender, RoutedEventArgs e)
    {
        if (GetSelectedAliasRow() is not { } row)
        {
            return;
        }

        await RunPersistenceActionAsync(async () =>
        {
            await ViewModel.SaveAliasMarkerAsync(row, null, null, AliasColor.None);
            await ViewModel.RefreshPersistedMetadataAsync();
        });
    }

    private void CopyAll_Click(object sender, RoutedEventArgs e)
    {
        var text = ViewModel.CurrentResultText;
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        CopyText(text);
        ViewModel.MarkCopied(ViewModel.GeneratedAliases.Count);
        RenderResults();
    }

    private void CopySelected_Click(object sender, RoutedEventArgs e)
    {
        if (GetSelectedAliasRow() is not { } selected || string.IsNullOrWhiteSpace(selected.Address))
        {
            return;
        }

        CopyText(selected.Address);
        ViewModel.MarkCopied(1);
        RenderResults();
    }

    private async Task RefreshMetadataAndRenderAsync()
    {
        await RunPersistenceActionAsync(async () => await ViewModel.RefreshPersistedMetadataAsync());
    }

    private async Task RunPersistenceActionAsync(Func<Task> action, bool renderInputs = false)
    {
        if (_isRendering)
        {
            return;
        }

        try
        {
            await action();
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
        {
            ViewModel.ReportPersistenceError(ex);
        }

        if (renderInputs)
        {
            RenderInputs();
        }

        RenderResults();
    }

    private void RenderInputs()
    {
        _isRendering = true;
        try
        {
            _emailBox.Text = ViewModel.EmailAddress;
            _tagsBox.Text = ViewModel.TagsText;
            _countBox.Text = ViewModel.CountText;
            _dotAliasesBox.IsChecked = ViewModel.UseDotAliases;
            _plusAliasesBox.IsChecked = ViewModel.UsePlusAliases;
            _savedEmailBox.ItemsSource = ViewModel.SavedEmailAddresses;
            _savedEmailBox.SelectedItem = ViewModel.SavedEmailAddresses.FirstOrDefault(
                address => string.Equals(address, ViewModel.EmailAddress, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _isRendering = false;
        }
    }

    private void RenderResults()
    {
        _isRendering = true;
        try
        {
            _statusText.Text = ViewModel.StatusMessage;
            _addressAnalysisText.Text = ViewModel.AddressAnalysisText;
            _validationText.Text = ViewModel.ValidationMessage;
            _validationText.Visibility = ViewModel.HasValidationMessage ? Visibility.Visible : Visibility.Collapsed;
            _generatedSummaryText.Text = ViewModel.GeneratedSummaryText;
            _dotAliasCountText.Text = ViewModel.DotAliasCountText;
            _plusAliasCountText.Text = ViewModel.PlusAliasCountText;
            AutomationProperties.SetName(_statusText, ViewModel.StatusMessage);
            AutomationProperties.SetName(_addressAnalysisText, ViewModel.AddressAnalysisText);
            AutomationProperties.SetName(_validationText, ViewModel.ValidationMessage);
            AutomationProperties.SetName(_generatedSummaryText, ViewModel.GeneratedSummaryText);
            AutomationProperties.SetName(_dotAliasCountText, ViewModel.DotAliasCountText);
            AutomationProperties.SetName(_plusAliasCountText, ViewModel.PlusAliasCountText);
            _allFilterButton.Content = ViewModel.FilterAllText;
            _dotsFilterButton.Content = ViewModel.FilterDotsText;
            _plusFilterButton.Content = ViewModel.FilterPlusText;
            _markedFilterButton.Content = ViewModel.FilterMarkedText;
            _unmarkedFilterButton.Content = ViewModel.FilterUnmarkedText;
            AutomationProperties.SetName(_allFilterButton, ViewModel.FilterAllText);
            AutomationProperties.SetName(_dotsFilterButton, ViewModel.FilterDotsText);
            AutomationProperties.SetName(_plusFilterButton, ViewModel.FilterPlusText);
            AutomationProperties.SetName(_markedFilterButton, ViewModel.FilterMarkedText);
            AutomationProperties.SetName(_unmarkedFilterButton, ViewModel.FilterUnmarkedText);
            _dotAliasesBox.IsEnabled = ViewModel.DotAliasCheckboxEnabled;
            _dotAliasesBox.IsChecked = ViewModel.UseDotAliases;
            _plusAliasesBox.IsChecked = ViewModel.UsePlusAliases;
            _saveEmailButton.IsEnabled = ViewModel.CanSaveCurrentEmailAddress;
            RenderResultItems();
            RenderMarkerPanel();
        }
        finally
        {
            _isRendering = false;
        }
    }

    private void RenderResultItems()
    {
        var selectedAddress = GetSelectedAliasRow()?.Address;
        _resultsList.Items.Clear();

        ListViewItem? selectedItem = null;
        foreach (var row in ViewModel.GeneratedAliases)
        {
            var item = CreateAliasListItem(row);
            _resultsList.Items.Add(item);
            if (string.Equals(row.Address, selectedAddress, StringComparison.OrdinalIgnoreCase))
            {
                selectedItem = item;
            }
        }

        if (selectedItem is not null)
        {
            _resultsList.SelectedItem = selectedItem;
        }
    }

    private void RenderMarkerPanel()
    {
        var selected = GetSelectedAliasRow();
        _isRendering = true;
        try
        {
            _selectedAliasText.Text = selected is null ? "Selected alias: -" : $"Selected alias: {selected.Address}";
            AutomationProperties.SetName(_selectedAliasText, _selectedAliasText.Text);
            _siteBox.Text = selected?.Site ?? string.Empty;
            _purposeBox.Text = selected?.Purpose ?? string.Empty;
            _colorBox.SelectedItem = ColorOptions.First(option => option.Color == (selected?.Color ?? AliasColor.None));
            var enabled = selected is not null;
            _siteBox.IsEnabled = enabled;
            _purposeBox.IsEnabled = enabled;
            _colorBox.IsEnabled = enabled;
            _saveMarkerButton.IsEnabled = enabled;
            _clearMarkerButton.IsEnabled = enabled;
        }
        finally
        {
            _isRendering = false;
        }
    }

    private GeneratedAliasRowViewModel? GetSelectedAliasRow()
    {
        return _resultsList.SelectedItem is ListViewItem { Tag: GeneratedAliasRowViewModel row } ? row : null;
    }

    private Border CreatePanelBorder()
    {
        return new Border
        {
            Padding = new Thickness(14),
            BorderBrush = _borderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
        };
    }

    private static ListViewItem CreateAliasListItem(GeneratedAliasRowViewModel row)
    {
        var grid = new Grid
        {
            ColumnSpacing = 10,
            Padding = new Thickness(10, 8, 10, 8),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var swatch = new Border
        {
            Width = 6,
            CornerRadius = new CornerRadius(3),
            Background = CreateAccentBrush(row.Color),
        };
        grid.Children.Add(swatch);

        var textStack = new StackPanel { Spacing = 2 };
        textStack.Children.Add(new TextBlock
        {
            Text = row.Address,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
        });
        textStack.Children.Add(new TextBlock
        {
            Text = row.MarkerSummary,
            FontSize = 12,
            Opacity = row.IsMarked ? 0.85 : 0.55,
            TextWrapping = TextWrapping.Wrap,
        });
        Grid.SetColumn(textStack, 1);
        grid.Children.Add(textStack);

        var stateStack = new StackPanel
        {
            Spacing = 4,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var kindText = new TextBlock
        {
            Text = row.Kind,
            FontSize = 12,
            Opacity = 0.65,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        stateStack.Children.Add(kindText);

        var markerState = row.IsMarked ? "Marked" : "Unmarked";
        var markerBadge = new Border
        {
            Padding = new Thickness(8, 3, 8, 3),
            CornerRadius = new CornerRadius(4),
            Background = CreateMarkerBadgeBrush(row.IsMarked),
            Child = new TextBlock
            {
                Text = markerState,
                FontSize = 11,
            },
        };
        stateStack.Children.Add(markerBadge);
        Grid.SetColumn(stateStack, 2);
        grid.Children.Add(stateStack);

        var item = new ListViewItem
        {
            Content = grid,
            Tag = row,
            Background = CreateBackgroundBrush(row.Color),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetName(item, $"{row.Address} {markerState} {row.MarkerSummary}");
        return item;
    }

    private static SolidColorBrush CreateAccentBrush(AliasColor color)
    {
        return color switch
        {
            AliasColor.Blue => new SolidColorBrush(Colors.DodgerBlue),
            AliasColor.Green => new SolidColorBrush(Colors.SeaGreen),
            AliasColor.Amber => new SolidColorBrush(Colors.DarkGoldenrod),
            AliasColor.Red => new SolidColorBrush(Colors.IndianRed),
            AliasColor.Purple => new SolidColorBrush(Colors.MediumPurple),
            _ => new SolidColorBrush(Colors.LightGray),
        };
    }

    private static SolidColorBrush CreateBackgroundBrush(AliasColor color)
    {
        return color switch
        {
            AliasColor.Blue => new SolidColorBrush(Color.FromArgb(34, 30, 144, 255)),
            AliasColor.Green => new SolidColorBrush(Color.FromArgb(34, 46, 139, 87)),
            AliasColor.Amber => new SolidColorBrush(Color.FromArgb(42, 184, 134, 11)),
            AliasColor.Red => new SolidColorBrush(Color.FromArgb(34, 205, 92, 92)),
            AliasColor.Purple => new SolidColorBrush(Color.FromArgb(34, 147, 112, 219)),
            _ => new SolidColorBrush(Colors.Transparent),
        };
    }

    private static SolidColorBrush CreateMarkerBadgeBrush(bool isMarked)
    {
        return isMarked
            ? new SolidColorBrush(Color.FromArgb(48, 46, 139, 87))
            : new SolidColorBrush(Color.FromArgb(36, 128, 128, 128));
    }

    private static Button CreateButton(string glyph, string text)
    {
        var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        stack.Children.Add(new FontIcon { Glyph = glyph, FontSize = 14 });
        stack.Children.Add(new TextBlock { Text = text });
        return new Button { Content = stack };
    }

    private static HyperlinkButton CreateCreatorLink(string text, string uri, string automationId)
    {
        var link = new HyperlinkButton
        {
            Content = text,
            NavigateUri = new Uri(uri),
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        AutomationProperties.SetAutomationId(link, automationId);
        AutomationProperties.SetName(link, text);
        return link;
    }

    private static Button CreateIconOnlyButton(string glyph)
    {
        return new Button
        {
            Content = new FontIcon { Glyph = glyph, FontSize = 14 },
        };
    }

    private static void AddSummaryRow(Grid grid, int row, string label, out TextBlock valueText)
    {
        var labelText = new TextBlock { Text = label };
        Grid.SetRow(labelText, row);
        grid.Children.Add(labelText);

        valueText = new TextBlock();
        Grid.SetRow(valueText, row);
        Grid.SetColumn(valueText, 1);
        grid.Children.Add(valueText);
    }

    private static void CopyText(string text)
    {
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
    }

    private static string GetLocalDatabasePath()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AliasCockpit");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "aliases.sqlite");
    }

    private static readonly ColorOption[] ColorOptions =
    [
        new(AliasColor.None, "None"),
        new(AliasColor.Blue, "Blue"),
        new(AliasColor.Green, "Green"),
        new(AliasColor.Amber, "Amber"),
        new(AliasColor.Red, "Red"),
        new(AliasColor.Purple, "Purple"),
    ];

    private sealed record ColorOption(AliasColor Color, string Label);
}

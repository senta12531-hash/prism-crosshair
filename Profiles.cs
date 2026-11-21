using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace Prism;

public partial class MainWindow
{
    private const int ProfileNameLength = 48;
    private const int ProfileFileVersion = 1;
    private const int MaximumProfileFileSize = 256 * 1024;
    private const string ProfileFileFilter = "Профиль Prism (*.json)|*.json";

    private static readonly Preset CatPreset = new("Котик", "Контур с ушками",
        new Reticle { Shape = ReticleShape.Cat, Color = "#FFC4D2", Length = 16, Gap = 3, Thickness = 2, RoundedEnds = true, CenterDot = false });

    private static readonly Preset[] BuiltIns =
    {
        new("Призма", "Классический", new Reticle()),
        new("Искра", "Точка", new Reticle { Shape = ReticleShape.Dot, Color = "#FF92AA", DotSize = 5 }),
        new("Орбита", "Кольцо", new Reticle { Shape = ReticleShape.Ring, Color = "#B5A3FF", Length = 19, CenterDot = false }),
        new("Вектор", "Т-образный", new Reticle { Shape = ReticleShape.TShape, Color = "#6DE3FF", Length = 13, Gap = 3, CenterDot = false }),
        new("Штрих", "Скруглённые лучи", new Reticle { Color = "#D9C59F", Length = 14, VerticalLength = 7, Thickness = 4, RoundedEnds = true, CenterDot = false }),
        new("Ромб", "Диагональ с точкой", new Reticle { Shape = ReticleShape.Diagonal, Color = "#E8E5DF", SquareDot = true, DotSize = 4, Gap = 6 }),
        CatPreset
    };

    private enum ProfileFilter { All, BuiltIn, Custom, Favorites }
    private sealed record ProfileFile(int Version, Preset Preset);

    private readonly List<(Button Button, Preset Preset)> _presetButtons = new();
    private readonly List<(Button Button, ProfileFilter Filter)> _profileFilters = new();
    private TextBox _profileSearch = null!;
    private ComboBox _profileSort = null!;
    private WrapPanel _profileCards = null!;
    private ScrollViewer _profileScroll = null!;
    private TextBlock _profileCount = null!;
    private TextBlock _profileEmpty = null!;
    private ProfileFilter _profileFilter;
    private string? _activePresetName;

    private void ConfigureProfiles()
    {
        ProfilesHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        ProfilesHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        ProfilesHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        ProfilesHost.RowDefinitions.Add(new RowDefinition());

        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 19) };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(actions, Dock.Right);
        var importButton = new Button { Content = "Импортировать JSON", MinHeight = 38, Margin = new Thickness(0, 0, 8, 0) };
        importButton.Click += (_, _) => ImportProfile();
        var saveButton = new Button { Content = "Сохранить текущий прицел", Tag = "primary", MinHeight = 38 };
        saveButton.Click += SavePresetClick;
        actions.Children.Add(importButton);
        actions.Children.Add(saveButton);
        header.Children.Add(actions);
        header.Children.Add(new TextBlock
        {
            Text = "Профили", Style = (Style)FindResource("PageHeading"), VerticalAlignment = VerticalAlignment.Center
        });
        ProfilesHost.Children.Add(header);

        var filters = new DockPanel { Margin = new Thickness(0, 0, 0, 13) };
        var sort = new ComboBox
        {
            ItemsSource = new[] { "По умолчанию", "Название: А–Я", "Название: Я–А", "Избранные сначала" },
            SelectedIndex = 0, MinWidth = 158, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0)
        };
        _profileSort = sort;
        AutomationProperties.SetName(sort, "Сортировка профилей");
        DockPanel.SetDock(sort, Dock.Right);
        filters.Children.Add(sort);

        var categories = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 14, 0) };
        (string Title, ProfileFilter Filter)[] filterOptions =
        {
            ("Все", ProfileFilter.All), ("Встроенные", ProfileFilter.BuiltIn),
            ("Мои", ProfileFilter.Custom), ("Избранное", ProfileFilter.Favorites)
        };
        foreach (var (title, filter) in filterOptions)
        {
            var button = new Button { Content = title, MinHeight = 36, Margin = new Thickness(0, 0, 5, 0) };
            AutomationProperties.SetName(button, $"Профили: {title}");
            button.Click += (_, _) => SetProfileFilter(filter);
            _profileFilters.Add((button, filter));
            categories.Children.Add(button);
        }
        DockPanel.SetDock(categories, Dock.Left);
        filters.Children.Add(categories);

        var searchHost = new Grid();
        _profileSearch = new TextBox { MinHeight = 36, Padding = new Thickness(11, 3, 39, 3) };
        AutomationProperties.SetName(_profileSearch, "Поиск профилей");
        searchHost.Children.Add(_profileSearch);
        var placeholder = ProfileText("Найти по имени или форме", 13, "MutedBrush");
        placeholder.VerticalAlignment = VerticalAlignment.Center;
        placeholder.Margin = new Thickness(12, 0, 7, 0);
        placeholder.IsHitTestVisible = false;
        searchHost.Children.Add(placeholder);
        var clear = new Button
        {
            Content = "×", FontSize = 17, Width = 31, MinHeight = 24, Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0), Visibility = Visibility.Collapsed
        };
        AutomationProperties.SetName(clear, "Очистить поиск");
        clear.Click += (_, _) => { _profileSearch.Clear(); _profileSearch.Focus(); };
        searchHost.Children.Add(clear);
        filters.Children.Add(searchHost);
        Grid.SetRow(filters, 1);
        ProfilesHost.Children.Add(filters);

        _profileCount = ProfileText("", 12, "MutedBrush");
        _profileCount.Margin = new Thickness(1, 0, 0, 10);
        AutomationProperties.SetLiveSetting(_profileCount, AutomationLiveSetting.Polite);
        Grid.SetRow(_profileCount, 2);
        ProfilesHost.Children.Add(_profileCount);

        _profileCards = new WrapPanel();
        _profileScroll = new ScrollViewer { Content = _profileCards, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetRow(_profileScroll, 3);
        ProfilesHost.Children.Add(_profileScroll);
        _profileEmpty = ProfileText("", 16, "MutedBrush");
        _profileEmpty.Margin = new Thickness(0, 52, 0, 0);
        _profileEmpty.HorizontalAlignment = HorizontalAlignment.Center;
        _profileEmpty.VerticalAlignment = VerticalAlignment.Top;
        _profileEmpty.TextWrapping = TextWrapping.Wrap;
        _profileEmpty.TextAlignment = TextAlignment.Center;
        Grid.SetRow(_profileEmpty, 3);
        ProfilesHost.Children.Add(_profileEmpty);

        _profileSearch.TextChanged += (_, _) =>
        {
            bool empty = _profileSearch.Text.Length == 0;
            placeholder.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
            clear.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
            BuildPresetCards();
        };
        sort.SelectionChanged += (_, _) => BuildPresetCards();
        SetProfileFilter(ProfileFilter.All);
    }

    private void SetProfileFilter(ProfileFilter filter)
    {
        _profileFilter = filter;
        foreach (var (button, category) in _profileFilters)
        {
            bool active = category == filter;
            button.Tag = active ? "selected" : null;
            AutomationProperties.SetItemStatus(button, active ? "Выбрано" : "");
        }
        BuildPresetCards();
    }

    private void BuildPresetCards()
    {
        _profileCards.Children.Clear();
        _presetButtons.Clear();
        IEnumerable<Preset> profiles = _profileFilter switch
        {
            ProfileFilter.BuiltIn => BuiltIns,
            ProfileFilter.Custom => _settings.Presets,
            ProfileFilter.Favorites => GetFavoritePresets(),
            _ => BuiltIns.Concat(_settings.Presets)
        };
        string search = _profileSearch.Text.Trim();
        if (search.Length > 0)
            profiles = profiles.Where(preset =>
                CultureInfo.CurrentCulture.CompareInfo.IndexOf(preset.Name, search, CompareOptions.IgnoreCase) >= 0 ||
                CultureInfo.CurrentCulture.CompareInfo.IndexOf(ProfileShape(preset), search, CompareOptions.IgnoreCase) >= 0);

        profiles = _profileSort.SelectedIndex switch
        {
            1 => profiles.OrderBy(profile => profile.Name, StringComparer.Create(CultureInfo.GetCultureInfo("ru"), true)),
            2 => profiles.OrderByDescending(profile => profile.Name, StringComparer.Create(CultureInfo.GetCultureInfo("ru"), true)),
            3 => profiles.OrderByDescending(IsFavorite),
            _ => profiles
        };
        foreach (var profile in profiles)
            AddPresetCard(profile);

        _profileCount.Text = $"Профилей: {_presetButtons.Count}";
        _profileEmpty.Visibility = _presetButtons.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _profileEmpty.Text = search.Length > 0 ? "Ничего не найдено. Попробуйте изменить запрос." : _profileFilter switch
        {
            ProfileFilter.Favorites => "Нажмите на звезду в карточке, чтобы добавить профиль в избранное.",
            ProfileFilter.Custom => "Сохраните текущий прицел или импортируйте профиль.",
            _ => "Профилей пока нет."
        };
        RefreshProfileSelection(_settings.Reticle);
    }

    private void AddPresetCard(Preset preset)
    {
        bool custom = !BuiltIns.Any(profile => ReferenceEquals(profile, preset));
        var card = new Grid { Width = 229, Margin = new Thickness(0, 0, 12, 12) };
        var tile = new Grid();
        tile.RowDefinitions.Add(new RowDefinition { Height = new GridLength(100) });
        tile.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var preview = new ProfileThumbnail(preset.Reticle);
        tile.Children.Add(preview);
        var details = new StackPanel { Margin = new Thickness(12, 10, 12, 12) };
        var title = ProfileText(preset.Name, 15, "TextBrush");
        title.FontWeight = FontWeights.SemiBold;
        details.Children.Add(title);
        var description = ProfileText($"{ProfileShape(preset)}  ·  {(custom ? "Пользовательский" : "Встроенный")}", 11, "MutedBrush");
        description.Margin = new Thickness(0, 4, 0, 0);
        description.IsHitTestVisible = false;
        details.Children.Add(description);
        Grid.SetRow(details, 1);
        tile.Children.Add(details);

        var load = new Button { Content = tile, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(3) };
        AutomationProperties.SetName(load, $"Загрузить пресет {preset.Name}");
        load.ToolTip = preset.Description;
        load.Click += (_, _) => ApplyPreset(preset);
        card.Children.Add(load);

        var tileActions = new StackPanel
        {
            Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 9, 9, 0)
        };
        var favorite = ProfileIconButton(IsFavorite(preset) ? "★" : "☆",
            (IsFavorite(preset) ? "Убрать из избранного: " : "В избранное: ") + preset.Name);
        favorite.Click += (_, _) =>
        {
            if (IsFavorite(preset))
                _settings.FavoriteProfileNames.RemoveAll(name => ProfileNamesEqual(name, preset.Name));
            else
                _settings.FavoriteProfileNames.Add(preset.Name);
            ScheduleSave();
            BuildPresetCards();
            if (!FocusProfile(preset))
                _profileFilters.Find(entry => entry.Filter == _profileFilter).Button?.Focus();
        };
        tileActions.Children.Add(favorite);

        var more = ProfileIconButton("⋯", $"Действия с профилем {preset.Name}");
        more.Margin = new Thickness(4, 0, 0, 0);
        var menu = new ContextMenu
        {
            PlacementTarget = more, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
            Background = (Brush)FindResource("PanelBrush"), Foreground = (Brush)FindResource("TextBrush"),
            BorderBrush = (Brush)FindResource("LineBrush")
        };
        void AddAction(string label, Action action)
        {
            var item = new MenuItem { Header = label };
            item.Click += (_, _) => action();
            menu.Items.Add(item);
        }
        AddAction("Создать копию", () => DuplicateProfile(preset));
        AddAction("Экспортировать JSON", () => ExportProfile(preset));
        if (custom)
        {
            AddAction("Переименовать", () => RenameProfile(preset));
            AddAction("Удалить…", () => DeleteProfile(preset));
        }
        more.ContextMenu = menu;
        more.Click += (_, _) => { menu.PlacementTarget = more; menu.IsOpen = true; };
        tileActions.Children.Add(more);
        card.Children.Add(tileActions);
        _profileCards.Children.Add(card);
        _presetButtons.Add((load, preset));
    }

    private TextBlock ProfileText(string text, double size, string brushKey) =>
        new() { Text = text, FontSize = size, Foreground = (Brush)FindResource(brushKey), TextTrimming = TextTrimming.CharacterEllipsis };

    private static Button ProfileIconButton(string icon, string label)
    {
        var button = new Button
        {
            Content = icon, FontSize = 16, Width = 31, MinHeight = 28,
            Padding = new Thickness(0), ToolTip = label
        };
        AutomationProperties.SetName(button, label);
        return button;
    }

    private static string ProfileShape(Preset profile) => ShapeNames[(int)profile.Reticle.Shape];
    private static bool ProfileNamesEqual(string left, string right) => StringComparer.OrdinalIgnoreCase.Equals(left, right);
    private bool IsFavorite(Preset preset) => _settings.FavoriteProfileNames.Any(name => ProfileNamesEqual(name, preset.Name));
    private IEnumerable<Preset> GetFavoritePresets() => BuiltIns.Concat(_settings.Presets).Where(IsFavorite);

    private void RefreshProfileSelection(Reticle reticle)
    {
        var matching = BuiltIns.Concat(_settings.Presets)
            .FirstOrDefault(preset => preset.Reticle == reticle && preset.Name == _activePresetName)
            ?? BuiltIns.Concat(_settings.Presets).FirstOrDefault(preset => preset.Reticle == reticle);
        foreach (var (button, preset) in _presetButtons)
            button.Tag = ReferenceEquals(preset, matching) ? "selected" : null;
        ProfileCaption.Text = matching is null ? "Свой прицел" : $"Профиль «{matching.Name}»";
    }

    private void ApplyPreset(Preset preset, bool navigateToEditor = true)
    {
        _activePresetName = preset.Name;
        SetReticle(preset.Reticle);
        if (navigateToEditor)
        {
            ShowPage(EditorPanel, EditorTab);
            EditorTab.Focus();
        }
        Notice.Text = $"Пресет «{preset.Name}» загружен.";
    }

    private void SavePresetClick(object sender, RoutedEventArgs e)
    {
        var name = PromptForProfileName("Новый пресет", UniqueProfileName("Мой прицел"));
        if (name is null)
            return;

        _activePresetName = name;
        AddCustomProfile(new Preset(name, "Свой прицел", _settings.Reticle), $"Пресет «{name}» сохранён.");
    }

    private void DuplicateProfile(Preset preset)
    {
        var copy = preset with { Name = UniqueProfileName($"{preset.Name} (копия)") };
        AddCustomProfile(copy, $"Создана копия «{copy.Name}».");
    }

    private void AddCustomProfile(Preset preset, string feedback)
    {
        _settings.Presets.Insert(0, preset);
        _profileSearch.Clear();
        SetProfileFilter(ProfileFilter.Custom);
        _profileScroll.ScrollToTop();
        FocusProfile(preset);
        ScheduleSave();
        Notice.Text = feedback;
    }

    private bool FocusProfile(Preset preset)
    {
        var button = _presetButtons.Find(entry => ReferenceEquals(preset, entry.Preset)).Button;
        if (button is null)
            return false;

        button.Focus();
        button.BringIntoView();
        return true;
    }

    private void RenameProfile(Preset preset)
    {
        var name = PromptForProfileName("Переименовать профиль", preset.Name, preset);
        if (name is null || name == preset.Name)
            return;

        var renamed = preset with { Name = name };
        int index = _settings.Presets.IndexOf(preset);
        if (index < 0)
            return;
        _settings.Presets[index] = renamed;
        if (_settings.FavoriteProfileNames.RemoveAll(item => ProfileNamesEqual(item, preset.Name)) > 0)
            _settings.FavoriteProfileNames.Add(name);
        if (_activePresetName == preset.Name)
            _activePresetName = name;
        _profileSearch.Clear();
        RefreshProfiles("Профиль переименован.");
        FocusProfile(renamed);
    }

    private void DeleteProfile(Preset preset)
    {
        var popup = new Window
        {
            Title = "Удаление профиля", Owner = this, Width = 390, SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false, Background = (Brush)FindResource("WindowBrush"), FontFamily = FontFamily
        };
        var content = new StackPanel { Margin = new Thickness(20) };
        var prompt = ProfileText($"Удалить профиль «{preset.Name}»?", 15, "TextBrush");
        prompt.TextWrapping = TextWrapping.Wrap;
        content.Children.Add(prompt);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        var cancel = new Button { Content = "Отмена", IsCancel = true, IsDefault = true, Margin = new Thickness(0, 0, 7, 0) };
        var confirm = new Button { Content = "Удалить", Foreground = (Brush)FindResource("DangerBrush") };
        confirm.Click += (_, _) => popup.DialogResult = true;
        actions.Children.Add(cancel);
        actions.Children.Add(confirm);
        content.Children.Add(actions);
        popup.Content = content;
        popup.Loaded += (_, _) => cancel.Focus();
        if (popup.ShowDialog() != true || !_settings.Presets.Remove(preset))
            return;

        _settings.FavoriteProfileNames.RemoveAll(name => ProfileNamesEqual(name, preset.Name));
        if (_activePresetName == preset.Name)
            _activePresetName = null;
        RefreshProfiles($"Профиль «{preset.Name}» удалён.");
        _profileFilters.Find(entry => entry.Filter == _profileFilter).Button?.Focus();
    }

    private void RefreshProfiles(string feedback)
    {
        BuildPresetCards();
        ScheduleSave();
        Notice.Text = feedback;
    }

    private string? PromptForProfileName(string title, string initialName, Preset? original = null)
    {
        var dialog = new Window
        {
            Owner = this, Title = title, Width = 390, SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false, ResizeMode = ResizeMode.NoResize,
            Background = (Brush)FindResource("WindowBrush"), FontFamily = FontFamily, FontSize = FontSize, Icon = Icon
        };
        var content = new StackPanel { Margin = new Thickness(20) };
        var label = ProfileText("Название профиля", 13, "TextBrush");
        label.FontWeight = FontWeights.SemiBold;
        label.Margin = new Thickness(0, 0, 0, 7);
        var name = new TextBox { MaxLength = ProfileNameLength, Text = initialName };
        AutomationProperties.SetName(name, "Название профиля");
        AutomationProperties.SetLabeledBy(name, label);
        var error = ProfileText("", 12, "DangerBrush");
        error.Margin = new Thickness(0, 7, 0, 0);
        AutomationProperties.SetLiveSetting(error, AutomationLiveSetting.Polite);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var cancel = new Button { Content = "Отмена", IsCancel = true, Margin = new Thickness(0, 0, 6, 0) };
        var save = new Button { Content = "Сохранить", Tag = "primary", IsDefault = true };
        actions.Children.Add(cancel);
        actions.Children.Add(save);
        content.Children.Add(label);
        content.Children.Add(name);
        content.Children.Add(error);
        content.Children.Add(actions);
        dialog.Content = content;
        dialog.Loaded += (_, _) => { name.Focus(); name.SelectAll(); };

        save.Click += (_, _) =>
        {
            var candidate = name.Text.Trim();
            if (candidate.Length == 0)
                error.Text = "Введите название.";
            else if (ProfileNameExists(candidate, original))
                error.Text = "Профиль с таким названием уже существует.";
            else
                dialog.DialogResult = true;
        };
        return dialog.ShowDialog() == true ? name.Text.Trim() : null;
    }

    private bool ProfileNameExists(string name, Preset? original = null) =>
        BuiltIns.Concat(_settings.Presets).Any(preset => !ReferenceEquals(preset, original) && ProfileNamesEqual(preset.Name, name));

    private string UniqueProfileName(string proposed)
    {
        string name = TrimProfileName(proposed.Trim(), ProfileNameLength);
        string candidate = name;
        int copy = 2;
        while (ProfileNameExists(candidate))
        {
            string suffix = $" ({copy++})";
            candidate = TrimProfileName(name, ProfileNameLength - suffix.Length) + suffix;
        }
        return candidate;
    }

    private static string TrimProfileName(string value, int maxLength)
    {
        if (value.Length <= maxLength)
            return value;
        if (char.IsHighSurrogate(value[maxLength - 1]))
            maxLength--;
        return value[..maxLength].TrimEnd();
    }

    private void ImportProfile()
    {
        var open = new OpenFileDialog { Title = "Импорт профиля Prism", Filter = ProfileFileFilter, CheckFileExists = true };
        if (open.ShowDialog(this) != true)
            return;

        try
        {
            using var file = new FileStream(open.FileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (file.Length > MaximumProfileFileSize)
                throw new JsonException();
            var data = JsonSerializer.Deserialize<ProfileFile>(file);
            if (data is not { Version: ProfileFileVersion, Preset.Reticle: not null } ||
                string.IsNullOrWhiteSpace(data.Preset.Name))
                throw new JsonException();

            var preset = data.Preset with
            {
                Name = UniqueProfileName(data.Preset.Name),
                Description = "Импортированный прицел",
                Reticle = data.Preset.Reticle.Normalize()
            };
            AddCustomProfile(preset, $"Профиль «{preset.Name}» импортирован.");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            Notice.Text = "Импортируйте JSON, экспортированный из Prism.";
        }
    }

    private void ExportProfile(Preset preset)
    {
        var forbidden = Path.GetInvalidFileNameChars();
        string filename = string.Concat(preset.Name.Select(symbol => forbidden.Contains(symbol) ? '_' : symbol));
        var save = new SaveFileDialog
        {
            Title = "Экспорт профиля Prism", Filter = ProfileFileFilter,
            FileName = $"Prism - {filename}.json", DefaultExt = ".json", AddExtension = true, OverwritePrompt = true
        };
        if (save.ShowDialog(this) != true)
            return;

        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(new ProfileFile(ProfileFileVersion, preset), options);
            File.WriteAllText(save.FileName, json);
            Notice.Text = $"Профиль «{preset.Name}» экспортирован.";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Notice.Text = "Не удалось сохранить файл профиля.";
        }
    }
}

internal sealed class ProfileThumbnail(Reticle reticle) : FrameworkElement
{
    private static readonly Brush Surface = ReticleDrawing.Brush("#171D20");
    private static readonly Brush Dot = ReticleDrawing.Brush("#364245");
    private static readonly Pen Axis = new(ReticleDrawing.Brush("#293538"), 1);

    protected override void OnRender(DrawingContext dc)
    {
        var bounds = new Rect(RenderSize);
        if (bounds.Width < 1 || bounds.Height < 1)
            return;
        var center = new Point(bounds.Width / 2, bounds.Height / 2);
        dc.DrawRoundedRectangle(Surface, null, bounds, 6, 6);
        dc.PushClip(new RectangleGeometry(bounds, 6, 6));
        for (double x = center.X % 17; x < bounds.Width; x += 17)
        for (double y = center.Y % 17; y < bounds.Height; y += 17)
            dc.DrawEllipse(Dot, null, new Point(x, y), 0.65, 0.65);
        dc.DrawLine(Axis, new Point(center.X, 7), new Point(center.X, bounds.Bottom - 7));
        dc.DrawLine(Axis, new Point(7, center.Y), new Point(bounds.Right - 7, center.Y));
        double extent = Math.Max(reticle.DotSize / 2.0, Math.Max(reticle.Length, reticle.VerticalLength ?? reticle.Length)
            + reticle.Gap + reticle.Thickness / 2.0) + reticle.OutlineWidth;
        double zoom = Math.Min(3, (Math.Min(bounds.Width, bounds.Height) / 2 - 9) / extent);
        if (zoom > 0)
            ReticleDrawing.Draw(dc, reticle, center, zoom, zoom);
        dc.Pop();
    }
}

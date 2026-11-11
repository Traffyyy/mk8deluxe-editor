using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using Mk8Editor.Core;

namespace Mk8Editor.Desktop;

public sealed class MainWindow : Window
{
    public EditorState State { get; } = new();
    public StatisticsTable Table { get; }
    public HexView Hex { get; }
    private readonly PortableAudio audio = new();
    private readonly TextBox search = new() { PlaceholderText = "Search items…", Width = 260, MinHeight = 38 };
    private readonly TextBlock status = new() { Text = "Ready · works offline", TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock song = new() { Text = PortableAudio.TrackTitle, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock selection = new();
    private readonly Button open, save, undo, reset, theme, speaker;
    private readonly Button[] tabs = new Button[5];
    private readonly Border empty;
    private readonly Grid workspace = new();
    private readonly Slider volume = new() { Minimum = 0, Maximum = 100, Value = 35, Width = 200 };
    private readonly TextBlock volumeText = new() { Text = "Volume · 35%" };
    private readonly Popup volumePopup;
    private readonly DispatcherTimer volumeTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private bool closeConfirmed, closePromptPending;
    private readonly bool verification;
    public bool SongVisible => song.IsVisible;
    public bool VolumeVisible => volumePopup.IsOpen;
    public string StatusText => status.Text ?? "";

    public MainWindow(bool verification = false)
    {
        this.verification = verification;
        Title = "mk8deluxe editor"; Width = 1440; Height = 850; MinWidth = 960; MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterScreen; FontSize = 13;
        using (var iconStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Mk8Editor.App.ico"))
            if (iconStream != null) Icon = new WindowIcon(iconStream);
        open = IconButton(Appearance.Open, "Open Performance.bin", OpenAsync);
        save = IconButton(Appearance.Save, "Save · Ctrl/⌘+S", SaveAsync);
        undo = IconButton(Appearance.Undo, "Undo · Ctrl/⌘+Z", () => { Undo(); return Task.CompletedTask; });
        reset = IconButton(Appearance.Reset, "Reset all changes", ResetAsync);
        theme = IconButton(Appearance.Moon, "Switch to dark theme", () => { ToggleTheme(); return Task.CompletedTask; });
        speaker = IconButton(Appearance.Speaker, "Turn audio off · hover for volume", () => { audio.Toggle(); return Task.CompletedTask; });
        song.IsVisible = false;
        var audioGroup = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { speaker, song } };
        var primary = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right, Children = { audioGroup, theme, open, save } };
        var header = new Border { Padding = new Thickness(24, 18), Child = primary, BorderThickness = new Thickness(0, 0, 0, 1) };
        var tabPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            tabs[i] = new Button { Content = i < 4 ? EditorState.Categories[i] : "Hex", Padding = new Thickness(18, 12) };
            tabs[i].Click += (_, _) => ShowCategory(index);
            tabPanel.Children.Add(tabs[i]);
        }
        var commands = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { search, undo, reset } };
        var navigation = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(24, 16, 24, 12) };
        navigation.Children.Add(tabPanel); Grid.SetColumn(commands, 1); navigation.Children.Add(commands);
        search.TextChanged += (_, _) => { State.Query = search.Text ?? ""; if (State.Category < 4) { State.ClearSelection(); RefreshTable(); } else Hex.Filter(State.Query); };
        var actions = new Button { Content = "•••", Width = 38, Height = 30, Padding = new Thickness(4) };
        AutomationProperties.SetName(actions, "Statistics actions");
        actions.Click += (_, _) => { actions.ContextMenu = BuildActionsMenu(); actions.ContextMenu.Open(actions); };
        var hint = new TextBlock { Text = "Double-click or F2 to edit · Shift: rectangle · Ctrl/⌘: add cells", VerticalAlignment = VerticalAlignment.Center };
        var secondary = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), Margin = new Thickness(24, 0, 24, 10) };
        secondary.Children.Add(hint); Grid.SetColumn(selection, 1); selection.VerticalAlignment = VerticalAlignment.Center;
        selection.Margin = new Thickness(8, 0, 12, 0); secondary.Children.Add(selection);
        Grid.SetColumn(actions, 2); secondary.Children.Add(actions);
        Table = new StatisticsTable(State) { CreateContextMenu = BuildActionsMenu };
        Table.SelectionChanged += RefreshCommands;
        Table.EditRequested += () => Run(EditAsync);
        Hex = new HexView(State);
        Hex.NavigateRequested += (recordIndex, statisticIndex) =>
        {
            var record = State.Records[recordIndex];
            State.Category = Array.IndexOf(EditorState.Sections, record.Entry.Section);
            search.Text = ""; State.Query = "";
            State.Select(record.Offset + statisticIndex * 4, false, false);
            ShowCategory(State.Category, false); Table.RevealActive();
        };
        empty = new Border { Child = new StackPanel { Spacing = 12, HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center, MaxWidth = 500, Children = {
                new TextBlock { Text = "Start with your file.", FontSize = 25, TextAlignment = TextAlignment.Center },
                new TextBlock { Text = "Open Performance.bin to view images and statistics. Save updates the opened file; Save As chooses a destination.", TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center }
            } } };
        workspace.Children.Add(Table); workspace.Children.Add(Hex); workspace.Children.Add(empty);
        workspace.Margin = new Thickness(24, 0, 24, 0);
        var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(24, 14) };
        footer.Children.Add(status);
        var legend = new TextBlock { Text = "Green: increased · pink: decreased", Margin = new Thickness(18, 0, 0, 0) };
        Grid.SetColumn(legend, 1); footer.Children.Add(legend);
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*,Auto") };
        root.Children.Add(header); Grid.SetRow(navigation, 1); root.Children.Add(navigation);
        Grid.SetRow(secondary, 2); root.Children.Add(secondary); Grid.SetRow(workspace, 3); root.Children.Add(workspace);
        Grid.SetRow(footer, 4); root.Children.Add(footer); Content = root;
        var volumePanel = new Border { Padding = new Thickness(16), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
            Child = new StackPanel { Spacing = 12, Children = { volumeText, volume } } };
        volumePopup = new Popup { PlacementTarget = speaker, Placement = PlacementMode.Bottom,
            IsLightDismissEnabled = false, Child = volumePanel };
        root.Children.Add(volumePopup);
        void ShowVolume() { volumeTimer.Stop(); volumePopup.IsOpen = true; }
        speaker.PointerEntered += (_, _) => ShowVolume();
        speaker.PointerExited += (_, _) => volumeTimer.Start();
        volumePanel.PointerEntered += (_, _) => volumeTimer.Stop();
        volumePanel.PointerExited += (_, _) => volumeTimer.Start();
        volumeTimer.Tick += (_, _) =>
        {
            volumeTimer.Stop();
            if (!speaker.IsPointerOver && !volumePanel.IsPointerOver && !volumePanel.IsKeyboardFocusWithin) volumePopup.IsOpen = false;
        };
        speaker.KeyDown += (_, e) => { if (e.Key == Key.Down) { ShowVolume(); volume.Focus(); e.Handled = true; } };
        volume.ValueChanged += (_, _) => { audio.SetVolume(volume.Value / 100); volumeText.Text = $"Volume · {Math.Round(volume.Value)}%"; };
        audio.StateChanged += RefreshAudio;
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        Deactivated += (_, _) => volumePopup.IsOpen = false;
        Opened += (_, _) => { if (!verification) audio.Start(); };
        Closing += OnClosing;
        Closed += (_, _) => { volumeTimer.Stop(); audio.Dispose(); };
        ApplyTheme(); RefreshCommands(); RefreshAudio();
    }

    private Button IconButton(string geometry, string name, Func<Task> action)
    {
        var button = new Button { Width = 44, Height = 42, Padding = new Thickness(9), Content = Appearance.Icon(geometry) };
        ToolTip.SetTip(button, name); AutomationProperties.SetName(button, name);
        button.Click += (_, _) => Run(action);
        return button;
    }
    private void Run(Func<Task> action) => _ = RunAsync(action);
    private async Task RunAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception error) { status.Text = error.Message; if (!verification) await EditorDialogs.Message(this, "Unable to complete action", error.Message); }
    }
    public async Task LoadPathAsync(string path)
    {
        if (!await ConfirmDiscardAsync()) return;
        try { LoadForVerification(path); }
        catch (Exception error) { status.Text = error.Message; if (!verification) await EditorDialogs.Message(this, "Unable to open file", error.Message); else throw; }
    }
    public void LoadForVerification(string path)
    {
        State.Load(path); search.Text = ""; State.Category = 0;
        RefreshTable(); Hex.Refresh();
        Title = "mk8deluxe editor · " + Path.GetFileName(path);
        status.Text = Path.GetFileName(path) + " · backup created when overwriting";
    }
    private async Task<bool> ConfirmDiscardAsync() => State.Session?.HasUnexportedChanges != true ||
        await EditorDialogs.Confirm(this, "Unsaved changes", "These changes have not been saved to a new copy. Discard them?", "Discard changes");
    public async Task OpenAsync()
    {
        if (!StorageProvider.CanOpen) throw new InvalidOperationException("A native file picker is not available.");
        var paths = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Open Performance.bin", AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Performance binary") { Patterns = ["*.bin"] }, FilePickerFileTypes.All] });
        if (paths.Count == 0) return;
        string path = paths[0].TryGetLocalPath() ?? throw new InvalidOperationException("Choose a file stored on this computer.");
        await LoadPathAsync(path);
    }
    public Task SaveAsync()
    {
        if (State.Session is { } session)
        {
            session.Save(); RefreshCommands();
            status.Text = "Saved · " + session.SourcePath + " · Backup: " + session.LastBackupPath;
        }
        return Task.CompletedTask;
    }
    public async Task SaveAsAsync()
    {
        if (State.Session is not { } session) return;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Save As", SuggestedFileName = Path.GetFileName(session.SourcePath),
            DefaultExtension = "bin", ShowOverwritePrompt = true,
            FileTypeChoices = [new FilePickerFileType("Performance binary") { Patterns = ["*.bin"] }] });
        if (file == null) return;
        string path = file.TryGetLocalPath() ?? throw new InvalidOperationException("Choose a local destination.");
        session.SaveAs(path); status.Text = "Saved and verified · " + path; RefreshCommands();
    }
    public void ShowCategory(int category, bool clearSelection = true)
    {
        if (clearSelection && category != 4 && category != State.Category) State.ClearSelection();
        State.Category = category;
        RefreshTable(); if (category == 4) { Hex.Refresh(); Hex.RevealSelection(); }
    }
    public void ToggleTheme() { State.Dark = !State.Dark; ApplyTheme(); RefreshTable(); Hex.Refresh(); }
    private void ApplyTheme()
    {
        RequestedThemeVariant = State.Dark ? ThemeVariant.Dark : ThemeVariant.Light;
        Background = Appearance.Surface(State.Dark); Foreground = Appearance.Text(State.Dark);
        theme.Content = Appearance.Icon(State.Dark ? Appearance.Sun : Appearance.Moon, Foreground);
        ToolTip.SetTip(theme, State.Dark ? "Switch to light theme" : "Switch to dark theme");
        open.Content = Appearance.Icon(Appearance.Open, Foreground); save.Content = Appearance.Icon(Appearance.Save, Foreground);
        undo.Content = Appearance.Icon(Appearance.Undo, Foreground); reset.Content = Appearance.Icon(Appearance.Reset, Foreground);
        if (volumePopup.Child is Border panel) { panel.Background = Appearance.Neutral(State.Dark); panel.BorderBrush = Appearance.Border(State.Dark); }
    }
    public void RefreshTable()
    {
        Table.IsVisible = State.Session != null && State.Category < 4;
        Hex.IsVisible = State.Session != null && State.Category == 4;
        empty.IsVisible = State.Session == null;
        if (State.Category < 4) Table.Rebuild();
        search.IsEnabled = State.Session != null;
        search.PlaceholderText = State.Category == 4 ? "Search bookmarks or 0x offset…" : "Search items…";
        for (int i = 0; i < tabs.Length; i++)
        {
            tabs[i].Background = i == State.Category ? Appearance.Brush(State.Dark ? "#315F91" : "#D6E7FA") : Brushes.Transparent;
            tabs[i].FontWeight = i == State.Category ? FontWeight.Bold : FontWeight.Normal;
            if (i < 4 && State.Session != null) tabs[i].Content = $"{EditorState.Categories[i]} · {State.Records.Count(r => r.Entry.Section == EditorState.Sections[i])}";
        }
        RefreshCommands();
    }
    public void RefreshAfterEdit()
    {
        Table.RefreshValues(); Hex.Refresh(); RefreshCommands();
        status.Text = $"{Path.GetFileName(State.Session?.SourcePath)} · {State.Session?.ChangedCount ?? 0} changed statistics · backup created when overwriting";
    }
    private void RefreshCommands()
    {
        save.IsEnabled = State.Session != null;
        undo.IsEnabled = State.Session?.CanUndo == true; reset.IsEnabled = State.Session?.ChangedCount > 0;
        selection.Text = State.SelectedOffsets.Count == 0 ? "" : $"{State.SelectedOffsets.Count} selected";
    }
    private void RefreshAudio()
    {
        bool enabled = audio.Enabled && audio.Error == null;
        song.IsVisible = enabled;
        speaker.Background = Appearance.Brush(enabled ? "#147D44" : "#BF303D");
        speaker.Content = Appearance.Icon(Appearance.Speaker, Brushes.White);
        string description = audio.Error ?? (enabled ? "Turn audio off" : "Turn audio on");
        ToolTip.SetTip(speaker, description + " · hover for volume"); AutomationProperties.SetName(speaker, description);
        if (audio.Error != null) status.Text = audio.Error;
    }
    public void Undo() { State.Session?.Undo(); RefreshAfterEdit(); }
    private async Task ResetAsync()
    {
        if (State.Session?.ChangedCount > 0 && await EditorDialogs.Confirm(this, "Reset changes", "Restore all statistics to the opened file? This also clears Undo history.", "Reset all changes"))
        { State.Session.Reset(); RefreshAfterEdit(); }
    }
    public async Task EditAsync()
    {
        if (State.SelectedOffsets.Count == 0 || State.Session == null) return;
        uint initial = State.Session.GetValue(State.SelectedOffsets.First());
        uint? value = await EditorDialogs.Value(this, State.SelectedOffsets.Count, initial);
        if (value.HasValue) { State.SetSelected(value.Value); RefreshAfterEdit(); }
    }
    public async Task CopyAsync()
    {
        string text = State.CopyAll();
        var clipboard = Clipboard ?? throw new InvalidOperationException("The clipboard is not available.");
        await clipboard.SetTextAsync(text); status.Text = "Copied all 14 statistics from the active item.";
    }
    public async Task PasteAsync()
    {
        var clipboard = Clipboard ?? throw new InvalidOperationException("The clipboard is not available.");
        State.PasteAll(await clipboard.TryGetTextAsync() ?? ""); RefreshAfterEdit();
    }
    public void JumpToHex()
    {
        if (!State.ActiveOffset.HasValue) return;
        State.Category = 4; search.Text = ""; State.Query = ""; ShowCategory(4, false);
    }
    private ContextMenu BuildActionsMenu()
    {
        MenuItem Item(string label, Func<Task> action)
        {
            var item = new MenuItem { Header = label };
            item.Click += (_, _) => Run(action); return item;
        }
        return new ContextMenu { ItemsSource = new Control[] {
            Item("Copy all 14 statistics    Ctrl/⌘+C", CopyAsync),
            Item("Paste to selected items    Ctrl/⌘+V", PasteAsync),
            new Separator(), Item("Save As…    Ctrl/⌘+Shift+S", SaveAsAsync), Item("Edit selected statistics…    F2", EditAsync),
            Item("Show statistic in Hex    Ctrl/⌘+H", () => { JumpToHex(); return Task.CompletedTask; }) } };
    }
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        bool command = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
        bool typing = e.Source is TextBox;
        if (e.Key == Key.Escape && volumePopup.IsOpen) { volumePopup.IsOpen = false; speaker.Focus(); e.Handled = true; return; }
        if (command)
        {
            if (e.Key == Key.O) Run(OpenAsync);
            else if (e.Key == Key.S) Run(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? SaveAsAsync : SaveAsync);
            else if (e.Key == Key.F) { search.Focus(); search.SelectAll(); }
            else if (e.Key == Key.H) JumpToHex();
            else if (!typing && e.Key == Key.Z) Undo();
            else if (!typing && e.Key == Key.C) Run(CopyAsync);
            else if (!typing && e.Key == Key.V) Run(PasteAsync);
            else return;
            e.Handled = true; return;
        }
        if (typing) return;
        if (e.Key is Key.F2 or Key.Enter && State.SelectedOffsets.Count > 0) { Run(EditAsync); e.Handled = true; }
        else if (State.Category < 4 && Table.IsKeyboardFocusWithin && e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        { Table.MoveSelection(e.Key, e.KeyModifiers.HasFlag(KeyModifiers.Shift)); e.Handled = true; }
    }
    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (verification || closeConfirmed || State.Session?.HasUnexportedChanges != true) return;
        e.Cancel = true;
        if (closePromptPending) return;
        closePromptPending = true;
        try { if (await ConfirmDiscardAsync()) { closeConfirmed = true; Close(); } }
        finally { closePromptPending = false; }
    }
}



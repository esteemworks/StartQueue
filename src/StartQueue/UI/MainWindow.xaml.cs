using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Microsoft.Win32;
using StartQueue.Core;

namespace StartQueue.UI;

public partial class MainWindow : Window
{
    enum View { Order, Windows, Services, Settings }

    readonly TrayHost host;
    readonly ObservableCollection<EntryVm> entries = new();
    View view = View.Order;
    View lastTab = View.Order;
    bool closingForGood;
    bool editing;
    Point dragStart;
    EntryVm? dragCandidate;
    AddAppModel? addModel;
    Guid? shownProfile;
    /// <summary>The profile editor's and the pause dialog's fields, the same in every app of the family.</summary>
    bool offScreen; // Diagnostics window: never takes the keyboard focus.

    /// <summary>
    /// Our own title bar leaves out the glass frame, and with it the rounded corners Windows 11 gives windows. Asked
    /// for here, Windows rounds them again whenever the window is not maximized. Older Windows ignore it.
    /// </summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var round = 2; // DWMWCP_ROUND
        _ = DwmSetWindowAttribute(hwnd, 33 /* DWMWA_WINDOW_CORNER_PREFERENCE */, ref round, sizeof(int));
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>
    /// Virtualized lists that scroll by pixel keep the widest width they have seen and go on laying out their rows
    /// in it when the window gets narrower again (after maximizing and restoring, say), until they scroll. Their
    /// panel is held to the visible width instead. That width comes from the area the rows are shown in, not from
    /// the scroll viewer's viewport: the panel reports the viewport itself, so a viewport of 0 at the moment of
    /// binding held the rows at 0 px for good, and the list looked empty.
    /// </summary>
    void PinListWidths(bool pinned = true)
    {
        foreach (var list in new ListBox[] { EntryList, ServicesList, AddList })
        {
            if (FindChild<VirtualizingStackPanel>(list) is not { } panel || FindScrollViewer(list) is not { } scroller) continue;
            if (pinned)
                PinWidth(panel, scroller);
            else
            {
                System.Windows.Data.BindingOperations.ClearBinding(panel, WidthProperty);
                panel.Width = double.NaN;
            }
        }
    }

    readonly HashSet<ListBox> pinnedLists = [];

    void Pin(ListBox list)
    {
        if (pinnedLists.Contains(list) || FindChild<VirtualizingStackPanel>(list) is not { } panel || FindScrollViewer(list) is not { } scroller) return;
        PinWidth(panel, scroller);
        pinnedLists.Add(list);
    }

    static void PinWidth(VirtualizingStackPanel panel, ScrollViewer scroller)
    {
        var area = scroller.Template?.FindName("PART_ScrollContentPresenter", scroller) as FrameworkElement
            ?? FindChild<ScrollContentPresenter>(scroller);
        if (area == null) return;
        panel.SetBinding(WidthProperty, new System.Windows.Data.Binding(nameof(ActualWidth)) { Source = area });
    }

    static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found) return found;
            if (FindChild<T>(child) is { } deeper) return deeper;
        }
        return null;
    }

    // The tabs, made by the kit's frame.
    readonly RadioButton TabOrder, TabWindows, TabServices;
    bool followingFrame;

    public MainWindow(TrayHost host)
    {
        this.host = host;
        InitializeComponent();
        KitMenuButton.Opening += MenuButton_Opening;
        Closed += (_, _) => KitMenuButton.Opening -= MenuButton_Opening;
        // The family's window from the kit: title bar, title with the line of facts, actions, tabs with the gear
        // (STANDARD, "The window"); the frame keeps the rules of tabs and gear and tells which page to show.
        Frame.Attach(this);
        TabOrder = Frame.AddTab("order", Strings.TabOrder);
        TabWindows = Frame.AddTab("windows", Strings.TabWindows);
        TabServices = Frame.AddTab("services", Strings.TabServices);
        Frame.PageChanged += FramePageChanged;
        PlacePreparedPair();
        // The family's About, Troubleshooting and More from EsteemWorks; "Privacy and notices" sits beside the data folder.
        // Privacy and notices on the kit's page: StartQueue's own sections, each with its icon.
        AboutPage.Sections = () => Strings.LegalSections(Paths.Data).Select((section, i) => (LegalIcons[Math.Min(i, LegalIcons.Length - 1)], section.Title, section.Text));
        AboutPage.Build(AboutHost);
        // The kit's dialogs over the whole window: questions, the cards of Add app and of explanations, notifications.
        dialog = new KitDialog(DialogLayer);
        // Check is the family's "Check now": the icon turns, F5 does the same, a notification says what turned up.
        Frame.AddCheck(Strings.ScanTip, ScanAsync);
        // A list's panel exists once it has made its first rows; from then on it is held to its visible width.
        foreach (var list in new ListBox[] { EntryList, ServicesList, AddList })
            list.ItemContainerGenerator.StatusChanged += (_, _) =>
            {
                if (list.ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated) Pin(list);
            };
        // The list starts with the switches of the profile that applies now.
        shownProfile = Profiles.Active(DateTime.Now)?.Id;
        FontWeight = Theme.BodyWeight;
        EntryList.ItemsSource = entries;
        // F5, Ctrl+N and Esc on the settings are the kit's frame's; the kit's dialogs keep the keys while open.
        Frame.New = OpenAddDialog;
        Frame.KeysBlocked = () => dialog.IsOpen;
        // Scrolling with the middle mouse button; the kit draws the marker at the starting point.
        _ = new MiddleScroll(this);
        Closing += (_, e) =>
        {
            if (host.Exiting || closingForGood) return;
            e.Cancel = true;
            CloseOverlay();
            Hide();
        };
        StartFirstStart();
    }

    /// <summary>Shut down and restart with prepared apps: one pair on the tab line, left of the gear (the kit's LineActions).</summary>
    void PlacePreparedPair()
    {
        Root.Children.Remove(PreparedPair);
        PreparedPair.HorizontalAlignment = HorizontalAlignment.Left;
        Frame.LineActions.Add(PreparedPair);
    }

    /// <summary>A tab was chosen or the settings opened or closed in the frame: the view for it.</summary>
    void FramePageChanged()
    {
        if (followingFrame) return;
        var next = Frame.SettingsOpen ? View.Settings : Frame.Page switch
        {
            "windows" => View.Windows,
            "services" => View.Services,
            _ => View.Order,
        };
        if (next != view) ShowView(next);
    }

    public bool ShowsWindowsView => IsVisible && view == View.Windows;

    /// <summary>Closes instead of hiding, used when the window is rebuilt in another language.</summary>
    public void CloseForGood()
    {
        closingForGood = true;
        Close();
    }

    public void ShowSettings() => ShowView(View.Settings);

    // ---------- First start ----------

    IntroView? intro;

    /// <summary>
    /// A new installation not switched on yet (STANDARD.md, "First start"): the kit's frame shows StartQueue's steps in
    /// place of the tabs, and looking around with its note and "Switch on". Where the user stands is kept in config.json,
    /// so the window opens the same way again. Installations from before and switched on ones see none of it.
    /// </summary>
    void StartFirstStart()
    {
        var config = host.Config;
        if (config.SwitchedOn) return;
        // The kit's mark of a new installation and StartQueue's list say the same; should only the list say it (the mark
        // went, a list from before it), the kit is told in this process, writing nothing.
        if (!KitIntro.Pending) KitIntro.ForSnapshot = true;
        intro = new IntroView(host);
        Frame.StartIntro(
        [
            new IntroStep("welcome", () => Strings.IntroStepWelcome, () => intro.Page(IntroView.Welcome)),
            // Wide, as the design has it: the list left, the cards for all users and for new entries right.
            new IntroStep("takeover", () => Strings.IntroStepTakeOver, () => intro.Page(IntroView.TakeOver), Wide: true),
            new IntroStep("ready", () => Strings.IntroStepSwitchOn, () => intro.Page(IntroView.Ready)),
        ]);
        if (config.FirstStart == FirstStart.LookingAround) Frame.LookAround();
        // From here on what the user does is kept; opening the window as it was is no change.
        Frame.IntroChanged += IntroChanged;
    }

    /// <summary>The introduction moved on, was left to look around, or ended: header, actions and the saved state.</summary>
    void IntroChanged()
    {
        // The window drawn for pictures changes nothing.
        if (!offScreen)
        {
            if (Frame.InIntro) host.SetFirstStart(FirstStart.Welcome);
            else if (Frame.LookingAround) host.SetFirstStart(FirstStart.LookingAround);
        }
        Sync();
    }

    /// <summary>What the first start changes outside the frame: the actions of the header and the line of facts.</summary>
    void SyncFirstStart(Config config)
    {
        var looking = Frame.LookingAround;
        // While looking around "Switch on" is the main action, added by the kit at the end.
        AddButton.Visibility = looking ? Visibility.Collapsed : Visibility.Visible;
        // Shutting down with prepared apps prepares what StartQueue starts: nothing before it is switched on.
        PreparedPair.Visibility = config.SwitchedOn ? Visibility.Visible : Visibility.Collapsed;
        if (Frame.InIntro) intro?.Sync();
    }

    /// <summary>Draws every view into PNG files from a window placed off screen, so nothing takes the focus.</summary>
    public async Task SnapshotAsync(string folder, bool maximizeTest = false, bool colorsOnly = false)
    {
        try
        {
            Directory.CreateDirectory(folder);
            offScreen = true;
            ShowActivated = false;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.Manual;
            (Left, Top, Width, Height) = (-20000, -20000, 1280, 820);
            Show();
            Sync();
            if (entries.Count > 0) EntryList.SelectedIndex = 0;
            // Every profile color on the window, to compare how buttons and bars stand out; nothing else then.
            if (colorsOnly)
            {
                ShowView(View.Order);
                foreach (var color in Palette.All)
                {
                    Theme.Show(color);
                    await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                    await Task.Delay(150);
                    SavePng(Root, Path.Combine(folder, $"color-{color.Id}.png"));
                }
                return;
            }
            // A new install not switched on yet: the three steps of the introduction first.
            if (!host.Config.SwitchedOn)
            {
                for (var waited = 0; host.Report == null && waited < 20; waited++) await Task.Delay(500);
                foreach (var introStep in new[] { IntroView.Welcome, IntroView.TakeOver, IntroView.Ready })
                {
                    Frame.ShowStep(introStep);
                    await Task.Delay(1200); // icons
                    intro?.Sync();
                    await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                    SavePng(Root, Path.Combine(folder, $"intro-{introStep + 1}.png"));
                }
                // The views below as while looking around.
                Frame.LookAround();
            }
            foreach (var next in new[] { View.Order, View.Windows, View.Services, View.Settings })
            {
                ShowView(next);
                // Lets the report and the icons arrive before drawing.
                await Task.Delay(1500);
                for (var waited = 0; host.Report == null && waited < 20; waited++) await Task.Delay(500);
                for (var waited = 0; next == View.Services && !services.Loaded && waited < 40; waited++) await Task.Delay(250);
                Sync();
                await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                SavePng(Root, Path.Combine(folder, $"{next.ToString().ToLowerInvariant()}.png"));
            }
            // The settings screen by screen after settings.png, the last one as settings-end, so no card in the middle
            // goes unseen (Backup and restore under About).
            var step = Math.Max(100, SettingsView.ViewportHeight - 60);
            for (var n = 2; SettingsView.VerticalOffset < SettingsView.ScrollableHeight - 0.5 && n < 40; n++)
            {
                SettingsView.ScrollToVerticalOffset(Math.Min(SettingsView.ScrollableHeight, SettingsView.VerticalOffset + step));
                await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                var last = SettingsView.VerticalOffset >= SettingsView.ScrollableHeight - 0.5;
                SavePng(Root, Path.Combine(folder, last ? "settings-end.png" : $"settings-{n}.png"));
            }
            // The card of the sign-in sequence at the top, with the prepared shutdown in it.
            SettingsView.ScrollToHome();
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            var cardTop = SequenceCard.TranslatePoint(new Point(0, 0), SettingsView).Y + SettingsView.VerticalOffset;
            SettingsView.ScrollToVerticalOffset(Math.Max(0, cardTop - 8));
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            SavePng(Root, Path.Combine(folder, "settings-startup.png"));
            // The kit's pages under About, each opened from its row and left with Back: Updates, Back up settings,
            // Troubleshooting, Privacy and notices.
            var pageRows = new List<NavRow>();
            void FindRows(DependencyObject parent)
            {
                for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
                {
                    var child = VisualTreeHelper.GetChild(parent, i);
                    if (child is NavRow { Tile: null } row) pageRows.Add(row);
                    else FindRows(child);
                }
            }
            FindRows(AboutHost);
            for (var i = 0; i < pageRows.Count; i++)
            {
                pageRows[i].RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                SavePng(Root, Path.Combine(folder, $"settings-page-{i + 1}.png"));
                KitSubpage.Back(SettingsView, focus: false);
                await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
            // What "Shut down, prepare apps" asks first, on a device where it can work.
            ShowView(View.Order);
            _ = ConfirmAsync(Strings.PreparedShutDownTitle, Strings.PreparedBody, Strings.PreparedNotePossible,
                Strings.PreparedShutDown, danger: false, icon: "");
            await Task.Delay(400);
            SavePng(Root, Path.Combine(folder, "prepared.png"));
            CloseOverlay();
            ShowView(View.Order);
            OpenAddDialog();
            for (var waited = 0; AddList.ItemsSource == null && waited < 100; waited++) await Task.Delay(100);
            await Task.Delay(1500); // icons
            SavePng(Root, Path.Combine(folder, "add.png"));
            CloseOverlay();
            ShowView(View.Order);
            // Shown only, never saved: a profile with hours, then one all day; the kit's pages of the tab.
            KitProfiles.OpenEditor(ProfileBarHost, ProfileHost, null, new FamilyProfile
            {
                Name = Strings.T("Office", "Büro"), From = "07:00", To = "18:00",
                Days = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
            });
            await PagePicture(folder, "profile.png");
            KitSubpage.Back(OrderHost, focus: false);
            // The other pages of the tab (all day, order, pause) are pictured from the window in use, in the sandbox:
            // in this second window a page did not give way to the next one (12.11.2026, still open).
            OpenInfo(Strings.LegalTitle, Strings.LegalSections(Paths.Data));
            await Task.Delay(400);
            SavePng(Root, Path.Combine(folder, "legal.png"));
            CloseOverlay();
            OpenInfo(Strings.ParamInfoTitle, Strings.ParamInfoSections);
            await Task.Delay(400);
            SavePng(Root, Path.Combine(folder, "params-info.png"));
            CloseOverlay();
            // The Windows section of the services, unfolded, with the warning a change there brings.
            ShowView(View.Services);
            if (services.Rows.OfType<ServiceHeader>().FirstOrDefault(h => h.IsWindows) is { } windowsHeader)
            {
                services.Toggle(windowsHeader);
                (ServicesList.ItemsSource as System.Windows.Data.ListCollectionView)?.Refresh();
                if (ServicesList.ItemsSource is System.Windows.Data.ListCollectionView view && view.Cast<object>().OfType<ServiceVm>().FirstOrDefault(s => s.IsWindows) is { } first)
                    ServicesList.ScrollIntoView(first);
                await Task.Delay(600);
                SavePng(Root, Path.Combine(folder, "services-windows.png"));
                if (services.Rows.OfType<ServiceVm>().FirstOrDefault(s => s.Essential) is { } essential)
                {
                    _ = ConfirmAsync(Strings.F(Strings.ServiceWindowsTitle, essential.Name),
                        Strings.F(Strings.ServiceEssentialBody, essential.Name, Strings.ServiceDoStop), "", Strings.ServiceChangeAnyway, danger: true, icon: "\uE7BA");
                    await Task.Delay(400);
                    SavePng(Root, Path.Combine(folder, "service-confirm.png"));
                    CloseOverlay();
                }
                services.Toggle(windowsHeader);
                // Sorted by start type, only delayed ones; then back to how it was.
                ServicesSortBox.SelectedIndex = 1;
                var delayed = ServicesFilterMenu.Items.OfType<MenuItem>().First(i => (i.CommandParameter as string) == "AutomaticDelayed");
                delayed.IsChecked = true;
                ServicesFilterItem_Click(delayed, new RoutedEventArgs());
                ServicesList.ScrollIntoView(services.Rows[0]);
                await Task.Delay(600);
                SavePng(Root, Path.Combine(folder, "services-filter.png"));
                ServicesFilterReset_Click(this, new RoutedEventArgs());
                ServicesSortBox.SelectedIndex = 0;
            }
            ShowView(View.Order);
            ShowView(View.Order);
            if (entries.Count > 0)
            {
                var vm = entries[0];
                _ = ConfirmAsync(Strings.F(Strings.RemoveTitle, vm.Name), Strings.F(Strings.RemoveBody, vm.Name),
                    vm.HasOrigin ? Strings.RemoveNote : "", Strings.Remove, danger: true);
                await Task.Delay(400);
                SavePng(Root, Path.Combine(folder, "confirm.png"));
                CloseOverlay();
            }
            // Maximizing and restoring for real, without and with the lists held to their width. The window shows on
            // screen for it, so this runs only when asked for, in Windows Sandbox.
            if (maximizeTest)
            {
                (Left, Top, Width, Height) = (40, 40, 1100, 760);
                foreach (var pinned in new[] { false, true })
                {
                    PinListWidths(pinned);
                    foreach (var shown in new[] { View.Order, View.Services })
                    {
                        ShowView(shown);
                        await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                        await Task.Delay(500);
                        WindowState = WindowState.Maximized;
                        await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                        await Task.Delay(700);
                        WindowState = WindowState.Normal;
                        await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                        await Task.Delay(700);
                        SavePng(Root, Path.Combine(folder, $"maximize-{(pinned ? "held" : "loose")}-{shown.ToString().ToLowerInvariant()}.png"));
                    }
                }
                (Left, Top, Width, Height) = (-20000, -20000, 1280, 820);
            }
            // The menus, drawn on their own: opened, they would show on screen.
            ShowView(View.Order);
            if (entries.Count > 0)
            {
                var rowMenu = (ContextMenu)FindResource("RowMenu");
                rowMenu.DataContext = entries[0];
                SaveLoose(rowMenu, Path.Combine(folder, "menu-row.png"));
            }
            // The menu of the first profile's chip in the kit's bar.
            if (FindChip(ProfileBarHost) is { ContextMenu: { } profileMenu }) SaveLoose(profileMenu, Path.Combine(folder, "menu-profile.png"));
            // The filter menu belongs to its button; a copy of its entries is drawn.
            ShowView(View.Services);
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            var filterCopy = new ContextMenu();
            foreach (var item in ServicesFilterMenu.Items)
                filterCopy.Items.Add(item switch
                {
                    // "Automatic (delayed start)" ticked, so the picture shows the check mark.
                    MenuItem m => new MenuItem { Header = m.Header, IsCheckable = m.IsCheckable, IsChecked = m.IsChecked || (m.CommandParameter as string) == "AutomaticDelayed", Tag = m.Tag, Style = m.Style, IsEnabled = m.IsEnabled, CommandParameter = m.CommandParameter },
                    Separator => new Separator(),
                    _ => new Separator(),
                });
            SaveLoose(filterCopy, Path.Combine(folder, "menu-filter.png"));
            SaveLoose(new ToolTip { Content = Strings.ScanTip }, Path.Combine(folder, "tooltip.png"));
            // The kit's notifications at the bottom of the window: a plain one and Undo in front of it.
            ShowView(View.Order);
            Notices.Show(Strings.F(Strings.ScanTakenOver, "Spotify, Steam") + "\n" + Strings.F(Strings.ScanNewService, "Contoso Update Service"), NoticeKind.Done);
            if (entries.Count > 0) ShowUndo(Strings.F(Strings.RemovedText, entries[0].Name), () => { });
            await Task.Delay(700);
            SavePng(Root, Path.Combine(folder, "notices.png"));
            // Wide and back again, as when maximizing and restoring: the rows have to follow the width.
            foreach (var wide in new[] { View.Order, View.Services })
            {
                ShowView(wide);
                await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                // Scrolled down, where the rows kept the wide width before.
                if (FindScrollViewer(wide == View.Order ? EntryList : ServicesList) is { } scroller) scroller.ScrollToVerticalOffset(scroller.ScrollableHeight / 2);
                await Task.Delay(300);
                Width = 1900;
                await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                await Task.Delay(300);
                Width = 1280;
                await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                await Task.Delay(300);
                SavePng(Root, Path.Combine(folder, $"narrow-again-{wide.ToString().ToLowerInvariant()}.png"));
            }
            // A low window shows how the scroll areas end at the bottom edge.
            Height = 560;
            ShowView(View.Order);
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            SavePng(Root, Path.Combine(folder, "order-low.png"));
        }
        catch (Exception ex)
        {
            Log.Warning($"Snapshot failed: {ex.Message}");
        }
        finally
        {
            CloseForGood();
        }
    }

    /// <summary>Diagnostics: times typical steps in a window placed off screen and writes the results to a file.</summary>
    public async Task PerfAsync(string file)
    {
        var report = new System.Text.StringBuilder();
        var clock = new Stopwatch();
        async Task Idle() => await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        async Task Time(string step, Func<Task> action)
        {
            clock.Restart();
            await action();
            await Idle();
            report.AppendLine($"{step}: {clock.ElapsedMilliseconds} ms");
        }
        async Task Scroll(string step, ScrollViewer? viewer)
        {
            if (viewer == null) return;
            viewer.ScrollToTop();
            await Idle();
            var frames = new List<double>();
            var last = TimeSpan.Zero;
            var done = new TaskCompletionSource();
            void OnFrame(object? s, EventArgs e)
            {
                var now = ((RenderingEventArgs)e).RenderingTime;
                if (now == last) return;
                if (last != TimeSpan.Zero) frames.Add((now - last).TotalMilliseconds);
                last = now;
                viewer.ScrollToVerticalOffset(viewer.VerticalOffset + 12);
                if (frames.Count >= 120 || viewer.VerticalOffset >= viewer.ScrollableHeight) done.TrySetResult();
            }
            CompositionTarget.Rendering += OnFrame;
            await done.Task;
            CompositionTarget.Rendering -= OnFrame;
            if (frames.Count == 0) return;
            report.AppendLine($"{step}: {frames.Count} frames, avg {frames.Average():0.0} ms, max {frames.Max():0.0} ms, over 25 ms: {frames.Count(f => f > 25)}");
        }

        try
        {
            offScreen = true;
            ShowActivated = false;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.Manual;
            (Left, Top, Width, Height) = (-20000, -20000, 1140, 740);
            await Time("show window", () => { Show(); Sync(); return Task.CompletedTask; });
            await Time("sync x10 (unchanged)", () => { for (var i = 0; i < 10; i++) Sync(); return Task.CompletedTask; });
            await Time("select row", () => { EntryList.SelectedIndex = Math.Min(3, entries.Count - 1); return Task.CompletedTask; });
            await Scroll("scroll order list", FindScrollViewer(EntryList));
            await Scroll("scroll order list again", FindScrollViewer(EntryList));
            await Time("to windows view", () => { ShowView(View.Windows); return Task.CompletedTask; });
            await Time("to settings", () => { ShowView(View.Settings); return Task.CompletedTask; });
            await Scroll("scroll settings", SettingsView);
            await Time("to order view", () => { ShowView(View.Order); return Task.CompletedTask; });
            // Frames while the dialog opens: a long one is a visible stall in the fade-in.
            var openFrames = new List<double>();
            var lastFrame = TimeSpan.Zero;
            void OnOpenFrame(object? s, EventArgs e)
            {
                var now = ((RenderingEventArgs)e).RenderingTime;
                if (now == lastFrame) return;
                if (lastFrame != TimeSpan.Zero) openFrames.Add((now - lastFrame).TotalMilliseconds);
                lastFrame = now;
            }
            CompositionTarget.Rendering += OnOpenFrame;
            await Time("open add dialog", async () =>
            {
                OpenAddDialog();
                while (AddList.ItemsSource == null) await Task.Delay(20);
            });
            await Task.Delay(400);
            CompositionTarget.Rendering -= OnOpenFrame;
            if (openFrames.Count > 0)
                report.AppendLine($"opening frames: {openFrames.Count}, max {openFrames.Max():0} ms, over 25 ms: {openFrames.Count(f => f > 25)}");
            report.AppendLine($"add dialog rows: {AddList.Items.Count}");
            await Task.Delay(1500); // icons
            await Scroll("scroll add dialog", FindScrollViewer(AddList));
            foreach (var text in new[] { "o", "on", "one", "" })
                await Time($"search \"{text}\"", () => { AddSearch.Text = text; return Task.CompletedTask; });
            await Time("close add dialog", () => { CloseOverlay(); return Task.CompletedTask; });
        }
        catch (Exception ex)
        {
            report.AppendLine($"failed: {ex}");
        }
        finally
        {
            File.WriteAllText(file, report.ToString());
            CloseForGood();
        }
    }

    /// <summary>Snapshots draw at this many times the screen's pixels.</summary>
    public int SnapshotScale { get; init; } = 1;

    /// <summary>Draws an element that is not shown (a menu), at its own size, with room for its shadow.</summary>
    void SaveLoose(FrameworkElement element, string file)
    {
        try
        {
            // A menu may have no parent, so it is laid out on its own and drawn onto a frame by a brush.
            element.Measure(new Size(600, 900));
            element.Arrange(new Rect(element.DesiredSize));
            element.UpdateLayout();
            var frame = new Border
            {
                Padding = new Thickness(24),
                Background = (Brush)FindResource("Bg"),
                Child = new Border
                {
                    Width = element.ActualWidth,
                    Height = element.ActualHeight,
                    // Fixed to the element's own bounds, as in SavePng: its shadow reaching past them would shift it.
                    Background = new VisualBrush(element)
                    {
                        Stretch = Stretch.None,
                        ViewboxUnits = BrushMappingMode.Absolute,
                        Viewbox = new Rect(0, 0, element.ActualWidth, element.ActualHeight),
                        ViewportUnits = BrushMappingMode.Absolute,
                        Viewport = new Rect(0, 0, element.ActualWidth, element.ActualHeight),
                    },
                },
            };
            frame.Measure(new Size(700, 1000));
            frame.Arrange(new Rect(frame.DesiredSize));
            frame.UpdateLayout();
            SavePng(frame, file);
        }
        catch (Exception ex)
        {
            Log.Warning($"Snapshot of {Path.GetFileName(file)} failed: {ex.Message}");
        }
    }

    /// <summary>A page of the startup order tab (KitSubpage) for the snapshot: laid out first, then drawn.</summary>
    async Task PagePicture(string folder, string name)
    {
        OrderHost.UpdateLayout();
        await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        Root.UpdateLayout();
        Log.Info($"Snapshot {name}: {KitSubpage.Depth(OrderHost)} page(s) open");
        SavePng(Root, Path.Combine(folder, name));
    }

    /// <summary>The first chip of a profile (not No profile) in the bar, for the snapshot.</summary>
    static RadioButton? FindChip(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is RadioButton { Tag: Guid } chip) return chip;
            if (FindChip(child) is { } found) return found;
        }
        return null;
    }

    void SavePng(FrameworkElement element, string file)
    {
        var screen = VisualTreeHelper.GetDpi(this);
        var dpi = new DpiScale(screen.DpiScaleX * SnapshotScale, screen.DpiScaleY * SnapshotScale);
        var size = new Size(element.ActualWidth, element.ActualHeight);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle((Brush)FindResource("Bg"), null, new Rect(size));
            // Fixed to the element's own bounds: anything reaching past its edge would otherwise shrink the whole picture.
            dc.DrawRectangle(new VisualBrush(element)
            {
                Stretch = Stretch.None,
                ViewboxUnits = BrushMappingMode.Absolute,
                Viewbox = new Rect(size),
                ViewportUnits = BrushMappingMode.Absolute,
                Viewport = new Rect(size),
            }, null, new Rect(size));
        }
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
            (int)(size.Width * dpi.DpiScaleX), (int)(size.Height * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var stream = File.Create(file);
        encoder.Save(stream);
    }

    // ---------- Showing state ----------

    public void Sync()
    {
        var config = host.Config;
        var run = host.LastRun;
        var selected = (EntryList.SelectedItem as EntryVm)?.Id;
        // The profile whose switches the list shows; a deleted one falls back to Standard.
        var shown = Profiles.Find(shownProfile);
        if (shown == null) shownProfile = null;
        var shownOn = shown == null ? null : Profiles.On(config, shown.Id);

        for (var i = 0; i < config.Entries.Count; i++)
        {
            var entry = config.Entries[i];
            var vm = entries.FirstOrDefault(v => v.Id == entry.Id);
            if (vm == null)
            {
                vm = new EntryVm(entry.Clone(), Save, (v, on) => host.SetOnInProfile(shownProfile, v.Id, on));
                entries.Insert(i, vm);
            }
            else
            {
                vm.Load(entry.Clone());
                var index = entries.IndexOf(vm);
                if (index != i) entries.Move(index, i);
            }
            vm.Position = i + 1;
            vm.ShowProfile(shownOn);
            vm.SetLastRun(run?.Items.LastOrDefault(x => x.Id == entry.Id));
        }
        while (entries.Count > config.Entries.Count) entries.RemoveAt(entries.Count - 1);
        // Icons of rows further down load in the background now, so scrolling there does not wait for them.
        foreach (var vm in entries) _ = vm.Icon;

        if (selected != null) EntryList.SelectedItem = entries.FirstOrDefault(v => v.Id == selected);
        if (EmptyOrder.Children.Count == 0) Ui.Empty(EmptyOrder, Strings.EmptyTitle, Strings.EmptyText, Strings.AddApp, (_, _) => OpenAddDialog());
        EmptyOrder.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        SyncProfileBar(config, shown);
        SyncHeader(config, run);
        SyncWindows();
        SyncFirstStart(config);
    }

    ProfileHost? profileHost;
    ProfileBar? profileBar;
    FamilyProfile? editingProfile;

    /// <summary>
    /// What StartQueue gives the kit's profile pieces: the profile the list shows, which apps start in a profile
    /// (StartQueue's own, in config.json) and its own questions and notices.
    /// </summary>
    ProfileHost ProfileHost => profileHost ??= new ProfileHost
    {
        App = Profiles.App,
        Shown = () => shownProfile,
        Show = id =>
        {
            shownProfile = id;
            Sync();
        },
        NoProfileHelp = Strings.ProfileStandardHelp,
        // A new profile starts with the apps switched on as in the profile the list showed.
        Added = (id, copyOf) => host.CopyProfileEntries(id, copyOf),
        Moved = (from, to) => host.MoveProfileEntries(from, to),
        Removed = id => host.RemoveProfileEntries(id),
        Change = change => host.ChangeFamilyNow(change),
        Editing = profile =>
        {
            editingProfile = profile;
            ShowProfileColor();
        },
        Confirm = (title, body, action, danger) => ConfirmAsync(title, body, "", action, danger: danger),
        Choose = (title, body, second, main) => ChooseAsync(title, body, second, main),
    };

    /// <summary>
    /// The profile bar above the list, the kit's: every profile left to right in order of precedence, No profile last.
    /// The one selected is the frame for the list and the details: their switches are its switches.
    /// </summary>
    void SyncProfileBar(Config config, FamilyProfile? shown)
    {
        if (profileBar == null)
        {
            profileBar = KitProfiles.Bar(ProfileHost);
            ProfileBarHost.Content = profileBar;
        }
        else profileBar.Refresh();
        ShowProfileColor(shown);
        // The details say which profile their switch belongs to, once there is more than No profile.
        DetailProfileNote.Text = Strings.F(Strings.ProfileInProfile, shown?.Name ?? Strings.ProfileStandard);
        DetailProfileNote.Visibility = Profiles.All.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>With the accent "Like the profile": the color of the profile edited, else shown, else the one that applies.</summary>
    void ShowProfileColor(FamilyProfile? shown = null)
    {
        shown ??= Profiles.Find(shownProfile);
        Theme.Show(ProfileColors.Of(KitProfiles.ColorSource(editingProfile, shown, Profiles.Active(DateTime.Now))));
    }

    void SyncHeader(Config config, LastRun? run)
    {
        var total = config.Entries.Count;
        // Counts for the profile chosen in the bar, like the list below; says which one applies now when that is another.
        var active = Profiles.Active(DateTime.Now);
        var shown = Profiles.Find(shownProfile);
        var enabled = config.Entries.Count(e => Profiles.IsOn(config, e, shown));
        var summary = Profiles.All.Count == 0 ? Strings.F(Strings.Summary, enabled, total)
            : Strings.F(Strings.SummaryProfile, enabled, total, shown?.Name ?? Strings.ProfileStandard)
              + (active?.Id == shown?.Id ? Strings.SummaryAppliesNow : Strings.F(Strings.SummaryOtherApplies, active?.Name ?? Strings.ProfileStandard));
        // Before switching on: what would happen, never a count of what StartQueue starts.
        if (config.FirstStart == FirstStart.Welcome) summary = Strings.IntroNotOn;
        else if (config.FirstStart == FirstStart.LookingAround) summary = enabled == 1 ? Strings.PreviewSummaryOne : Strings.F(Strings.PreviewSummary, enabled);
        // The last run is a fact of its own, so it gets a line of its own under the summary.
        var lastRun = run is { Items.Count: > 0 } && config.SwitchedOn
            ? Strings.F(run.AtSignIn ? Strings.SummaryLastSignIn : Strings.SummaryLastRun, run.StartedAt, run.Items.Count, run.Items.Max(i => i.Seconds))
              + (run.Items.Any(i => i.Failed) ? Strings.F(Strings.SummaryFailed, run.Items.Count(i => i.Failed)) : "")
            : "";
        Frame.Summary.Text = lastRun.Length > 0 ? summary + "\n" + lastRun : summary;
        Frame.Summary.LineHeight = 19;
        Frame.SetCount(TabOrder, Strings.TabOrder, total);
        Frame.SetCount(TabWindows, Strings.TabWindows, host.Report?.Items.Count);
        if (TabServices.Content == null) Frame.SetCount(TabServices, Strings.TabServices, null);

        var newCount = config.Entries.Count(e => e.IsNew);
        var leftovers = Elevation.AdminMode ? host.LastSweep?.Leftovers ?? 0 : 0;
        if (newCount > 0)
            Frame.SetStatus(newCount == 1 ? Strings.StatusOneNew : Strings.F(Strings.StatusNew, newCount), Strings.StatusNewTip, GoToStatus);
        else if (leftovers > 0)
            Frame.SetStatus(leftovers == 1 ? Strings.StatusOneLeft : Strings.F(Strings.StatusLeft, leftovers), Strings.StatusLeftTip, GoToStatus);
        else
            Frame.SetStatus(null);
        MarkSeenButton.Visibility = newCount > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    void GoToStatus()
    {
        var first = host.Config.Entries.FirstOrDefault(x => x.IsNew);
        if (first != null) Select(first.Id);
        else ShowView(View.Windows);
    }

    void SyncWindows()
    {
        var report = host.Report;
        WindowsLoading.Visibility = report == null ? Visibility.Visible : Visibility.Collapsed;
        if (report == null)
        {
            TakeOverAllButton.Visibility = TakeOverAllTasksButton.Visibility = Visibility.Collapsed;
            return;
        }

        var items = report.Items.Select(i => new WindowsItemVm(i)).ToList();
        WindowsList.ItemsSource = items;
        WindowsCount.Text = items.Count.ToString();
        WindowsEmpty.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        TakeOverAllButton.Visibility = items.Any(i => i.CanTakeOver) ? Visibility.Visible : Visibility.Collapsed;
        WindowsHintText.Text = Strings.F(Strings.WindowsHint, report.At);

        var tasks = report.Tasks.Select(t => new TaskItemVm(t)).ToList();
        TasksList.ItemsSource = tasks;
        TasksCount.Text = tasks.Count.ToString();
        TakeOverAllTasksButton.Visibility = tasks.Any(t => t.CanTakeOver) ? Visibility.Visible : Visibility.Collapsed;
        TasksEmpty.Visibility = report.Tasks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Frame.SetCount(TabWindows, Strings.TabWindows, items.Count);
    }

    public void Select(Guid id)
    {
        ShowView(View.Order);
        var vm = entries.FirstOrDefault(v => v.Id == id);
        if (vm == null) return;
        EntryList.SelectedItem = vm;
        EntryList.ScrollIntoView(vm);
    }

    void Save(EntryVm vm) => host.SaveEntry(vm.Model);

    EntryVm? Selected => EntryList.SelectedItem as EntryVm;

    static EntryVm? VmOf(object sender) => (sender as FrameworkElement)?.DataContext as EntryVm;

    // ---------- Views ----------

    void ShowView(View next)
    {
        if (next != View.Settings) lastTab = next;
        view = next;
        OrderHost.Visibility = next == View.Order ? Visibility.Visible : Visibility.Collapsed;
        WindowsView.Visibility = next == View.Windows ? Visibility.Visible : Visibility.Collapsed;
        ServicesView.Visibility = next == View.Services ? Visibility.Visible : Visibility.Collapsed;
        SettingsView.Visibility = next == View.Settings ? Visibility.Visible : Visibility.Collapsed;
        // The frame shows the same: its tab, or the settings with the gear as × (it keeps the rules of both).
        followingFrame = true;
        try
        {
            if (next == View.Settings) Frame.SettingsOpen = true;
            else Frame.ShowTab(next switch { View.Windows => "windows", View.Services => "services", _ => "order" });
        }
        finally
        {
            followingFrame = false;
        }
        if (next == View.Windows) _ = host.RefreshReportAsync();
        if (next == View.Services) _ = LoadServicesAsync();
        if (next == View.Settings)
        {
            var settings = new SettingsVm(host);
            SettingsView.DataContext = settings;
            // General from the kit: color mode, accent, language, set up; StartQueue starts at sign-in by its own task,
            // so it has no "Start with Windows".
            GeneralHost.Children.Clear();
            KitSettingsPage.General(GeneralHost, new GeneralSettings
            {
                Theme = host.Config.Settings.Theme,
                SetTheme = settings.PickTheme,
                Accent = host.Config.Settings.Accent.ToKit(),
                SetAccent = settings.PickAccent,
                Profiles = true,
                Language = settings.LanguageCode,
                SetLanguage = code => settings.LanguageCode = code,
                LanguageNote = Strings.LanguageHelp,
                Setup = new GeneralSetup
                {
                    IsFull = () => Elevation.AdminMode,
                    CanSetUp = () => settings.CanSetUpAdmin,
                    SetUpFully = () =>
                    {
                        host.SetUpAdminFeatures();
                        return Task.CompletedTask;
                    },
                    FullAdds = Strings.SetupFullAdds,
                    FullGives = Strings.SetupFullGives,
                    Info = settings.SetupStateText,
                },
            });
        }
        SyncFirstStart(host.Config);
    }

    // ---------- Selection and keyboard ----------

    void EntryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var vm = Selected;
        Detail.DataContext = vm;
        DetailScroll.Visibility = vm == null ? Visibility.Collapsed : Visibility.Visible;
        NoSelection.Visibility = vm == null ? Visibility.Visible : Visibility.Collapsed;
        SetEditing(false);
        if (vm == null) return;
        DetailScroll.ScrollToTop();
        if (vm.IsNew && IsActive) vm.IsNew = false; // Looking at it counts as seen.
    }

    void EntryList_KeyDown(object sender, KeyEventArgs e)
    {
        var vm = Selected;
        if (vm == null || e.OriginalSource is TextBox) return;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (Keyboard.Modifiers == ModifierKeys.Alt && key is Key.Up or Key.Down)
        {
            MoveTo(vm, entries.IndexOf(vm) + (key == Key.Up ? -1 : 1));
            e.Handled = true;
        }
        else if (key == Key.Delete)
        {
            _ = RemoveAsync(vm);
            e.Handled = true;
        }
        else if (key == Key.Enter)
        {
            host.StartNow(vm.Model);
            e.Handled = true;
        }
        else if (key == Key.Space && e.OriginalSource is ListBoxItem)
        {
            vm.Enabled = !vm.Enabled;
            e.Handled = true;
        }
    }

    void CommitOnEnter(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox box) return;
        box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        e.Handled = true;
    }

    void Edit_Click(object sender, RoutedEventArgs e) => SetEditing(!editing);

    void SetEditing(bool on)
    {
        editing = on;
        EditPanel.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        WhatText.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
        EditButton.Content = on ? Strings.DoneEditing : Strings.EditDetails;
    }

    // ---------- Row actions and menu ----------

    void StartNow_Click(object sender, RoutedEventArgs e)
    {
        if (VmOf(sender) is { } vm) host.StartNow(vm.Model);
    }

    void RowRemove_Click(object sender, RoutedEventArgs e)
    {
        if (VmOf(sender) is { } vm) _ = RemoveAsync(vm);
    }

    /// <summary>A row's ⋯ or "More actions" opens the row menu (a kit menu button): the row is chosen first.</summary>
    void MenuButton_Opening(System.Windows.Controls.Primitives.ButtonBase button, ContextMenu menu)
    {
        if (Window.GetWindow(button) != this) return;
        if (VmOf(button) is { } vm)
        {
            EntryList.SelectedItem = vm;
            menu.DataContext = vm;
        }
    }

    void Menu_Start(object sender, RoutedEventArgs e) => StartNow_Click(sender, e);

    void Menu_Toggle(object sender, RoutedEventArgs e)
    {
        if (VmOf(sender) is { } vm) vm.Enabled = !vm.Enabled;
    }

    void Menu_Up(object sender, RoutedEventArgs e) { if (VmOf(sender) is { } vm) MoveTo(vm, entries.IndexOf(vm) - 1); }
    void Menu_Down(object sender, RoutedEventArgs e) { if (VmOf(sender) is { } vm) MoveTo(vm, entries.IndexOf(vm) + 1); }
    void Menu_Top(object sender, RoutedEventArgs e) { if (VmOf(sender) is { } vm) MoveTo(vm, 0); }
    void Menu_End(object sender, RoutedEventArgs e) { if (VmOf(sender) is { } vm) MoveTo(vm, entries.Count - 1); }

    void Menu_ShowFile(object sender, RoutedEventArgs e)
    {
        if (VmOf(sender) is { HasFileLocation: true } vm)
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{vm.LocationFile}\"") { UseShellExecute = true });
    }

    void Menu_Return(object sender, RoutedEventArgs e)
    {
        if (VmOf(sender) is { HasOrigin: true } vm) _ = ReturnAsync(vm);
    }

    void Menu_Remove(object sender, RoutedEventArgs e)
    {
        if (VmOf(sender) is { } vm) _ = RemoveAsync(vm);
    }

    async Task RemoveAsync(EntryVm vm)
    {
        var ok = await ConfirmAsync(Strings.F(Strings.RemoveTitle, vm.Name), Strings.F(Strings.RemoveBody, vm.Name),
            vm.HasOrigin ? Strings.RemoveNote : "", Strings.Remove, danger: true);
        if (!ok) return;
        var undo = host.Remove(vm.Id);
        if (undo != null) ShowUndo(Strings.F(Strings.RemovedText, vm.Name), undo);
    }

    // ---------- Notifications, Undo among them ----------

    readonly KitDialog dialog;

    /// <summary>The kit's notifications at the bottom of the window: at most three, the time stops while hovered.</summary>
    KitNotices Notices => dialog.Notices;

    void ShowUndo(string text, Action undo) => Notices.ShowUndo(text, undo);

    async Task ReturnAsync(EntryVm vm)
    {
        var ok = await ConfirmAsync(Strings.F(Strings.ReturnTitle, vm.Name), Strings.ReturnBody, Strings.ReturnNote, Strings.ReturnConfirm, danger: false);
        if (!ok) return;
        try
        {
            host.ReturnToWindows(vm.Model);
        }
        catch (Exception ex)
        {
            await ShowMessageAsync(Strings.ReturnFailed, ex.Message);
        }
    }

    void Browse_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } vm) return;
        var dialog = new OpenFileDialog { Title = Strings.ChooseProgram, Filter = Strings.ProgramFilter };
        if (File.Exists(vm.Target)) dialog.InitialDirectory = Path.GetDirectoryName(vm.Target);
        if (dialog.ShowDialog(this) == true) vm.Target = dialog.FileName;
    }

    // ---------- Reordering ----------

    void EntryList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        dragStart = e.GetPosition(null);
        dragCandidate = IsInsideControl(e.OriginalSource as DependencyObject) ? null : ItemAt(e.OriginalSource as DependencyObject);
    }

    void EntryList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (dragCandidate == null || e.LeftButton != MouseButtonState.Pressed) return;
        var delta = e.GetPosition(null) - dragStart;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        var item = dragCandidate;
        dragCandidate = null;
        // On the item, not its row: rows are reused for other items while the list scrolls.
        item.IsDragging = true;
        try
        {
            DragDrop.DoDragDrop(EntryList, item, DragDropEffects.Move);
        }
        finally
        {
            item.IsDragging = false;
            DropMarker.Visibility = Visibility.Collapsed;
        }
    }

    void EntryList_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(EntryVm))) return; // Drops from outside bubble up to the window.
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
        AutoScroll(e);
        ShowDropMarker(DropIndex(e));
    }

    void EntryList_DragLeave(object sender, DragEventArgs e)
    {
        // DragLeave also fires when moving between rows; hide only when really outside the list.
        var position = e.GetPosition(EntryList);
        if (position.X < 0 || position.Y < 0 || position.X > EntryList.ActualWidth || position.Y > EntryList.ActualHeight)
            DropMarker.Visibility = Visibility.Collapsed;
    }

    void EntryList_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(EntryVm)) is not EntryVm source) return;
        e.Handled = true;
        DropMarker.Visibility = Visibility.Collapsed;
        var insert = DropIndex(e);
        var from = entries.IndexOf(source);
        MoveTo(source, insert > from ? insert - 1 : insert);
    }

    /// <summary>The gap the pointer is over: before a row in its upper half, after it in its lower half.</summary>
    int DropIndex(DragEventArgs e)
    {
        var target = ItemAt(e.OriginalSource as DependencyObject);
        if (target == null) return entries.Count;
        var container = (FrameworkElement)EntryList.ItemContainerGenerator.ContainerFromItem(target);
        var index = entries.IndexOf(target);
        return e.GetPosition(container).Y > container.ActualHeight / 2 ? index + 1 : index;
    }

    void ShowDropMarker(int insert)
    {
        // Rows keep a 6 px gap below them; the line sits in the middle of the gap.
        var anchorIndex = Math.Min(insert, entries.Count - 1);
        if (anchorIndex < 0 || EntryList.ItemContainerGenerator.ContainerFromIndex(anchorIndex) is not FrameworkElement row)
        {
            DropMarker.Visibility = Visibility.Collapsed;
            return;
        }
        var below = insert >= entries.Count;
        var point = row.TranslatePoint(new Point(0, below ? row.ActualHeight + 3 : -3), DropLayer);
        Canvas.SetLeft(DropMarker, point.X - 5);
        Canvas.SetTop(DropMarker, point.Y - DropMarker.Height / 2);
        DropMarker.Width = row.ActualWidth + 5;
        DropMarker.Visibility = Visibility.Visible;
    }

    /// <summary>Scrolls the list while dragging near its top or bottom edge.</summary>
    void AutoScroll(DragEventArgs e)
    {
        if (FindScrollViewer(EntryList) is not { } scroller) return;
        var y = e.GetPosition(EntryList).Y;
        const double edge = 40;
        if (y < edge) scroller.ScrollToVerticalOffset(scroller.VerticalOffset - 20);
        else if (y > EntryList.ActualHeight - edge) scroller.ScrollToVerticalOffset(scroller.VerticalOffset + 20);
    }

    static ScrollViewer? FindScrollViewer(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is ScrollViewer viewer) return viewer;
            if (FindScrollViewer(child) is { } found) return found;
        }
        return null;
    }

    // Apps dragged in from the Start menu, the desktop or Explorer.
    void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = ShellDrop.CanRead(e.Data) ? DragDropEffects.Copy | DragDropEffects.Link : DragDropEffects.None;
        e.Handled = true;
    }

    void Window_Drop(object sender, DragEventArgs e)
    {
        var items = ShellDrop.Read(e.Data);
        Log.Debug($"Drop with {string.Join(", ", e.Data.GetFormats(false))}: {string.Join(" | ", items.Select(i => i.Id))}", "drop");
        foreach (var (name, id) in items) host.AddDropped(name, id);
        e.Handled = true;
    }

    void MoveTo(EntryVm vm, int index)
    {
        var from = entries.IndexOf(vm);
        index = Math.Clamp(index, 0, entries.Count - 1);
        if (from < 0 || from == index) return;
        entries.Move(from, index);
        for (var i = 0; i < entries.Count; i++) entries[i].Position = i + 1;
        EntryList.SelectedItem = vm;
        EntryList.ScrollIntoView(vm);
        host.Reorder(entries.Select(v => v.Id).ToList());
    }

    static EntryVm? ItemAt(DependencyObject? element)
    {
        while (element != null && element is not ListBoxItem) element = ParentOf(element);
        return (element as ListBoxItem)?.DataContext as EntryVm;
    }

    static bool IsInsideControl(DependencyObject? element)
    {
        while (element != null && element is not ListBoxItem)
        {
            if (element is ButtonBase or TextBox) return true;
            element = ParentOf(element);
        }
        return false;
    }

    static DependencyObject? ParentOf(DependencyObject element) =>
        element is Visual or Visual3D ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);

    // ---------- Header and toolbar ----------

    void Scan_Click(object sender, RoutedEventArgs e) => _ = ScanAsync();

    /// <summary>The check behind the kit's "Check now": what turned up since before, one line each.</summary>
    async Task<IReadOnlyList<string>> ScanAsync()
    {
        var before = Found();
        // New entries first, so the Windows autostart list is read after them; the services meanwhile.
        var servicesRead = LoadServicesAsync(force: true);
        await host.SweepAsync(userTriggered: true);
        await Task.WhenAll(host.RefreshReportAsync(), servicesRead);
        return ScanFindings(before, Found());
    }

    /// <summary>What a check can turn up, by key with the name to show.</summary>
    sealed record Findings(Dictionary<Guid, string> Entries, Dictionary<string, string> Windows, Dictionary<string, string> Tasks, Dictionary<string, string>? Services);

    Findings Found() => new(
        host.Config.Entries.ToDictionary(e => e.Id, e => e.Name),
        (host.Report?.Items ?? []).GroupBy(i => i.Item.Origin.Key).ToDictionary(g => g.Key, g => g.First().Item.DisplayName),
        (host.Report?.Tasks ?? []).GroupBy(t => t.Path).ToDictionary(g => g.Key, g => g.First().Name),
        // Services read for the first time are not "new".
        services.Loaded ? services.Rows.OfType<ServiceVm>().GroupBy(s => s.Info.Name).ToDictionary(g => g.Key, g => g.First().Name) : null);

    /// <summary>What turned up since before, one line each; empty when nothing did.</summary>
    static List<string> ScanFindings(Findings before, Findings after)
    {
        static string Names(IEnumerable<string> names)
        {
            var list = names.ToList();
            return list.Count <= 3 ? string.Join(", ", list) : Strings.F(Strings.ScanAndMore, string.Join(", ", list.Take(3)), list.Count - 3);
        }
        IEnumerable<string> Added<TKey>(Dictionary<TKey, string> was, Dictionary<TKey, string> now) where TKey : notnull =>
            now.Where(p => !was.ContainsKey(p.Key)).Select(p => p.Value);

        var lines = new List<string>();
        if (Added(before.Entries, after.Entries).ToList() is { Count: > 0 } entries)
            lines.Add(Strings.F(entries.Count == 1 ? Strings.ScanTakenOverOne : Strings.ScanTakenOver, Names(entries)));
        if (Added(before.Windows, after.Windows).ToList() is { Count: > 0 } windows)
            lines.Add(Strings.F(Strings.ScanNewInWindows, Names(windows)));
        if (Added(before.Tasks, after.Tasks).ToList() is { Count: > 0 } tasks)
            lines.Add(Strings.F(tasks.Count == 1 ? Strings.ScanNewTask : Strings.ScanNewTasks, Names(tasks)));
        if (before.Services != null && after.Services != null && Added(before.Services, after.Services).ToList() is { Count: > 0 } newServices)
            lines.Add(Strings.F(newServices.Count == 1 ? Strings.ScanNewService : Strings.ScanNewServices, Names(newServices)));

        return lines;
    }

    void MarkSeen_Click(object sender, RoutedEventArgs e) => host.MarkAllSeen();

    void RunSequence_Click(object sender, RoutedEventArgs e) => host.RunSequenceNow();


    // ---------- Windows startup delay ----------

    async void WindowsDelay_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox box) return;
        var off = box.IsChecked == true;
        box.IsChecked = !off; // The switch moves only once confirmed.
        var ok = off
            ? await ConfirmAsync(Strings.WindowsDelayOffTitle, Strings.WindowsDelayOffBody, Strings.WindowsDelayOffNote, Strings.WindowsDelayOffConfirm, danger: false)
            : await ConfirmAsync(Strings.WindowsDelayOnTitle, Strings.WindowsDelayOnBody, "", Strings.WindowsDelayOnConfirm, danger: false);
        if (!ok) return;
        try
        {
            WindowsDelay.SetOff(off);
        }
        catch (Exception ex)
        {
            Log.Warning($"Changing the Windows startup delay failed: {ex.Message}");
        }
        box.IsChecked = WindowsDelay.IsOff;
    }

    // ---------- Shutting down with prepared apps ----------

    void ShutDownPrepared_Click(object sender, RoutedEventArgs e) => host.ShutDownPrepared(restart: false);

    void RestartPrepared_Click(object sender, RoutedEventArgs e) => host.ShutDownPrepared(restart: true);

    async void Prepared_Click(object sender, RoutedEventArgs e)
    {
        var undo = host.Config.Settings.PreparedUndo != null;
        var passwordless = PreparedSignIn.State() == PreparedSignInState.Passwordless;
        var ok = undo
            ? await ConfirmAsync(Strings.PreparedUndoTitle, Strings.PreparedUndoBody, "", Strings.PreparedUndoButton, danger: false)
            : await ConfirmAsync(Strings.PreparedSetUpTitle, Strings.PreparedSetUpBody,
                passwordless ? Strings.PreparedSetUpNotePasswordless : Strings.PreparedSetUpNote, Strings.PreparedSetUpButton, danger: false);
        if (!ok) return;
        // The UAC prompt waits for the user; the window stays responsive meanwhile.
        var done = await Task.Run(() => undo ? PreparedSignIn.Undo() : PreparedSignIn.SetUp());
        host.Reload();
        (SettingsView.DataContext as SettingsVm)?.RefreshPrepared();
        if (!done) await ShowMessageAsync(Strings.PreparedSetUpFailed, Strings.PreparedSetUpFailedText);
    }

    // ---------- Services ----------

    readonly ServicesModel services = new();
    DateTime servicesLoadedAt;
    bool servicesLoading;

    /// <summary>Reads the services in the background; on every visit, but not more often than every few seconds.</summary>
    async Task LoadServicesAsync(bool force = false)
    {
        if (servicesLoading || (!force && DateTime.Now - servicesLoadedAt < TimeSpan.FromSeconds(5))) return;
        servicesLoading = true;
        try
        {
            var list = await Task.Run(Services.List);
            services.Load(list, ChangeService);
            if (ServicesList.ItemsSource == null)
                ServicesList.ItemsSource = new System.Windows.Data.ListCollectionView(services.Rows) { Filter = row => ((ServiceRow)row).IsShown };
            else (ServicesList.ItemsSource as System.Windows.Data.ListCollectionView)?.Refresh();
            Frame.SetCount(TabServices, Strings.TabServices, list.Count);
            servicesLoadedAt = DateTime.Now;
        }
        catch (Exception ex)
        {
            Log.Warning($"Reading the services failed: {ex.Message}");
        }
        finally
        {
            servicesLoading = false;
            ServicesLoading.Visibility = services.Loaded ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    /// <summary>Reads the services while the window opens, so the tab shows them at once.</summary>
    public Task PreloadServicesAsync() => LoadServicesAsync();

    void ServicesSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        services.Filter(ServicesSearch.Text);
        (ServicesList.ItemsSource as System.Windows.Data.ListCollectionView)?.Refresh();
    }

    void ServicesSort_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (ServicesList == null) return;
        services.SortBy((ServiceSort)Math.Max(0, ServicesSortBox.SelectedIndex));
        (ServicesList.ItemsSource as System.Windows.Data.ListCollectionView)?.Refresh();
    }

    /// <summary>Ticked start types and states narrow the list; none ticked in a group means all of that group.</summary>
    void ServicesFilterItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { CommandParameter: string tag } item) return;
        switch (tag)
        {
            case "Running": services.OnlyRunning = item.IsChecked; break;
            case "Stopped": services.OnlyStopped = item.IsChecked; break;
            default:
                var start = Enum.Parse<ServiceStart>(tag);
                if (item.IsChecked) services.StartTypes.Add(start);
                else services.StartTypes.Remove(start);
                break;
        }
        ApplyServicesFilter();
    }

    void ServicesFilterReset_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in ServicesFilterMenu.Items.OfType<MenuItem>().Where(i => i.IsCheckable)) item.IsChecked = false;
        services.StartTypes.Clear();
        services.OnlyRunning = services.OnlyStopped = false;
        ApplyServicesFilter();
    }

    void ApplyServicesFilter()
    {
        services.Filter(ServicesSearch.Text);
        (ServicesList.ItemsSource as System.Windows.Data.ListCollectionView)?.Refresh();
        var active = services.ActiveFilters;
        ServicesFilterText.Text = active == 0 ? Strings.ServicesFilter : Strings.F(Strings.ServicesFilterCount, active);
    }

    void ServiceSection_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ServiceHeader header) return;
        services.Toggle(header);
        (ServicesList.ItemsSource as System.Windows.Data.ListCollectionView)?.Refresh();
    }

    static ServiceVm? ServiceOf(object sender) => (sender as FrameworkElement)?.DataContext as ServiceVm;

    void ServiceStart_Click(object sender, RoutedEventArgs e) { if (ServiceOf(sender) is { } vm) ChangeService(vm, ServiceAction.Start); }
    void ServiceStop_Click(object sender, RoutedEventArgs e) { if (ServiceOf(sender) is { } vm) ChangeService(vm, ServiceAction.Stop); }
    void ServiceRestart_Click(object sender, RoutedEventArgs e) { if (ServiceOf(sender) is { } vm) ChangeService(vm, ServiceAction.Restart); }

    /// <summary>
    /// Windows services first say what can go wrong; stopping also names the services that stop along with it.
    /// Then the change runs, through UAC when StartQueue has no administrator rights.
    /// </summary>
    async void ChangeService(ServiceVm vm, ServiceAction action)
    {
        var info = vm.Info;
        var stopping = action is ServiceAction.Stop or ServiceAction.Restart;
        var dependents = stopping ? await Task.Run(() => Services.RunningDependents(info.Name)) : [];
        var risky = info.IsWindows && action is not ServiceAction.Start;
        if (risky || dependents.Count > 0)
        {
            var what = action switch
            {
                ServiceAction.Stop => Strings.ServiceDoStop,
                ServiceAction.Restart => Strings.ServiceDoRestart,
                _ => Strings.F(Strings.ServiceDoStartType, ServiceVm.StartTypes[(int)action]),
            };
            var body = !risky ? Strings.F(Strings.ServiceConfirmBody, info.DisplayName, what)
                : info.Essential ? Strings.F(Strings.ServiceEssentialBody, info.DisplayName, what)
                : Strings.F(Strings.ServiceWindowsBody, info.DisplayName, what);
            var note = dependents.Count > 0 ? Strings.F(Strings.ServiceDependents, string.Join(", ", dependents)) : "";
            var ok = await ConfirmAsync(risky ? Strings.F(Strings.ServiceWindowsTitle, info.DisplayName) : Strings.F(Strings.ServiceConfirmTitle, info.DisplayName),
                body, note, Strings.ServiceChangeAnyway, danger: risky, icon: "\uE7BA");
            if (!ok)
            {
                vm.Reset();
                return;
            }
        }

        vm.Busy = true;
        var error = await Services.ChangeAsync(info.Name, action);
        vm.Busy = false;
        if (error != null)
        {
            vm.Reset();
            await ConfirmAsync(Strings.F(Strings.ServiceFailedTitle, info.DisplayName), error, "", Strings.Close, danger: false, showCancel: false);
        }
        await LoadServicesAsync(force: true);
    }

    /// <summary>Privacy, made with AI, what it changes in Windows, publisher, liability, third parties.</summary>
    static readonly string[] LegalIcons = ["\uE72E", "\uE946", "\uE713", "\uE77B", "\uE7BA", "\uE8A5"];

    void ParametersInfo_Click(object sender, RoutedEventArgs e) => OpenInfo(Strings.ParamInfoTitle, Strings.ParamInfoSections);

    /// <summary>A longer explanation on the kit's card: a heading and a paragraph per section.</summary>
    void OpenInfo(string heading, (string Title, string Text)[] sections) => _ = dialog.ShowInfoAsync(heading, sections);

    // ---------- Windows autostart ----------

    async void TakeOverOne_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is WindowsItemVm item) await host.TakeOverFromWindowsAsync(new[] { item.Item });
    }

    async void TakeOverTask_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is TaskItemVm task) await host.TakeOverTaskAsync(task.Task);
    }

    async void TakeOverAllTasks_Click(object sender, RoutedEventArgs e)
    {
        if (TasksList.ItemsSource is not IEnumerable<TaskItemVm> tasks) return;
        foreach (var task in tasks.Where(t => t.CanTakeOver).ToList()) await host.TakeOverTaskAsync(task.Task);
    }

    async void TakeOverAll_Click(object sender, RoutedEventArgs e)
    {
        if (WindowsList.ItemsSource is IEnumerable<WindowsItemVm> items)
            await host.TakeOverFromWindowsAsync(items.Where(i => i.CanTakeOver).Select(i => i.Item).ToList());
    }

    // ---------- In-app dialogs ----------

    /// <summary>A question on the kit's card (KitDialog): the button says what happens, a removal is red with Cancel first.</summary>
    public Task<bool> ConfirmAsync(string title, string body, string note, string okText, bool danger, bool showCancel = true, string? icon = null) =>
        dialog.ConfirmAsync(title, body, okText, danger, string.IsNullOrEmpty(note) ? null : note, showCancel, icon);

    public Task ShowMessageAsync(string title, string text)
    {
        if (!IsVisible) host.ShowWindow();
        return dialog.ShowMessageAsync(title, text);
    }

    /// <summary>A question with two ways on besides Cancel, as the kit's dialog has it.</summary>
    public Task<int> ChooseAsync(string title, string body, string secondText, string mainText) => dialog.ChooseAsync(title, body, secondText, mainText);

    /// <summary>Closes the open question (as Cancel) or card.</summary>
    void CloseOverlay() => dialog.Close();

    // ---------- Add app ----------

    void Add_Click(object sender, RoutedEventArgs e) => OpenAddDialog();

    bool addOpen;

    async void OpenAddDialog()
    {
        foreach (var part in new FrameworkElement[] { AddSearch, AddBrowse, AddBody }) Detach(part);
        AddSearch.Text = "";
        AddList.ItemsSource = null;
        AddLoading.Visibility = Visibility.Visible;
        addOpen = true;
        var closed = dialog.ShowCardAsync(new DialogCard
        {
            Title = Strings.PickerTitle,
            Top = AddSearch,
            Content = AddBody,
            FooterStart = AddBrowse,
            DoneText = Strings.DoneEditing,
            CloseButton = true,
            Fill = true,
        });
        _ = closed.ContinueWith(_ => addOpen = false, TaskScheduler.FromCurrentSynchronizationContext());
        // Into the search field, after the card has put the focus on its main button.
        if (!offScreen) _ = Dispatcher.BeginInvoke(() => AddSearch.FocusSearch(), System.Windows.Threading.DispatcherPriority.Input);

        // The rows come in after the fade-in, so building them does not stall it.
        var fadeIn = Task.Delay(Motion.Enabled ? Motion.DialogTime : TimeSpan.Zero);
        var candidates = await host.LoadCandidatesAsync();
        await fadeIn;
        if (!addOpen) return;
        addModel = new AddAppModel(candidates);
        AddLoading.Visibility = Visibility.Collapsed;
        addModel.Filter(AddSearch.Text.Trim());
        var rows = new System.Windows.Data.ListCollectionView(addModel.Rows) { Filter = row => ((AddRow)row).IsShown };
        AddList.ItemsSource = rows;
    }

    /// <summary>Takes a part of Add app from where it was (its holder or the card shown last), so the card can show it.</summary>
    static void Detach(FrameworkElement part)
    {
        if (part.Parent is Panel panel) panel.Children.Remove(part);
    }

    // Filtering the view: only the rows that fit the window are built, also while typing.
    void AddSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (addModel == null || AddList.ItemsSource is not System.Windows.Data.ListCollectionView rows) return;
        addModel.Filter(AddSearch.Text.Trim());
        rows.Refresh();
        if (FindScrollViewer(AddList) is { } scroller) scroller.ScrollToTop();
    }

    void AddCandidate_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not CandidateVm vm || addModel == null) return;
        if (host.AddCandidate(vm.Candidate)) addModel.MarkAdded(vm.Candidate);
    }

    void AddBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = Strings.AddDialogTitle, Filter = Strings.AddDialogFilter, DereferenceLinks = false };
        if (dialog.ShowDialog(this) != true) return;
        CloseOverlay();
        host.AddPath(dialog.FileName);
    }

}

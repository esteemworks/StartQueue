using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using EsteemWorks.Kit.UI;
using StartQueue.Core;

namespace StartQueue.UI;

/// <summary>One entry the sweep found in the Windows autostart, in the step "Take over".</summary>
public sealed class IntroItem : INotifyPropertyChanged
{
    readonly Entry entry;
    readonly Action changed;
    ImageSource? icon;
    bool iconAsked;
    bool chosen;

    public event PropertyChangedEventHandler? PropertyChanged;

    public IntroItem(Entry entry, bool chosen, Action changed)
    {
        this.entry = entry;
        this.chosen = chosen;
        this.changed = changed;
    }

    public Guid Id => entry.Id;
    public string Name => entry.Name;
    public string Path => entry.Arguments.Length > 0 ? $"{entry.Target} {entry.Arguments}" : entry.Target;

    public string Where => entry.Origin?.Kind switch
    {
        OriginKind.RunKey => Strings.IntroWhereRun,
        OriginKind.StartupFolder => Strings.IntroWhereFolder,
        OriginKind.StartupTask => Strings.IntroWhereStore,
        _ => Strings.IntroWhereTask,
    };

    public bool Chosen
    {
        get => chosen;
        set
        {
            if (chosen == value) return;
            chosen = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Chosen)));
            changed();
        }
    }

    public ImageSource? Icon
    {
        get
        {
            if (!iconAsked)
            {
                iconAsked = true;
                icon = IconLoader.Get(ShellIcons.NameFor(entry), loaded =>
                {
                    icon = loaded;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Icon)));
                });
            }
            return icon;
        }
    }
}

/// <summary>One line of the summary before switching on: a number and a name, or "and 4 more".</summary>
public sealed record ReadyItem(int Number, string Name)
{
    public bool HasNumber => Number > 0;
}

/// <summary>
/// The steps of the first start of a new install (STANDARD.md, "First start"): Welcome, Take over, Switch on. The kit's
/// KitFrame.StartIntro shows them in place of the tabs, with "Back", "Look around first", "Next" and at the end "Switch
/// StartQueue on"; nothing changes in Windows before that. What is unticked here and the choice for new entries go to
/// the tray (<see cref="TrayHost.IntroLeave"/>, <see cref="TrayHost.IntroMode"/>), which applies them when switched on.
/// </summary>
public partial class IntroView : UserControl
{
    public const int Welcome = 0, TakeOver = 1, Ready = 2;
    const int ReadyListMost = 12;

    readonly TrayHost host;
    HashSet<Guid> leave => host.IntroLeave;
    NewEntryMode mode
    {
        get => host.IntroMode;
        set => host.IntroMode = value;
    }
    bool loading;
    string? foundKey;

    /// <summary>The step shown, 0 to 2.</summary>
    public int Step { get; private set; }

    // Filled by Sync: the note of Welcome, the facts of Switch on.
    TextBlock welcomeFound = null!;
    readonly UniformGrid readyList = new() { Columns = 2 };
    // When StartQueue starts nothing yet: one sentence over the whole width, wrapped, like the other values.
    readonly TextBlock readyNothing = Value();
    readonly StackPanel readyStarts = new();
    readonly TextBlock readyWindows = Value();
    readonly TextBlock readyNew = Value();
    readonly TextBlock readySetUp = Value();

    public IntroView(TrayHost host)
    {
        this.host = host;
        InitializeComponent();
        BuildPieces();
    }

    static TextBlock Value()
    {
        var value = new TextBlock { TextWrapping = TextWrapping.Wrap };
        value.SetResourceReference(TextBlock.ForegroundProperty, "Text2");
        return value;
    }

    /// <summary>The steps' heading, lead, features, facts and notes: the kit's pieces, as in every EsteemWorks app.</summary>
    void BuildPieces()
    {
        WelcomePage.Children.Add(IntroParts.Heading(Strings.IntroWelcomeTitle));
        WelcomePage.Children.Add(IntroParts.Lead(Strings.IntroWelcomeText));
        WelcomePage.Children.Add(IntroParts.Features(
            ("\uE8FD", Strings.IntroPointListTitle, Strings.IntroPointListText),
            ("\uE823", Strings.IntroPointFirstTitle, Strings.IntroPointFirstText),
            ("\uE7A7", Strings.IntroPointBackTitle, Strings.IntroPointBackText)));
        var found = IntroParts.Notice("");
        welcomeFound = IntroParts.Note(found);
        WelcomePage.Children.Add(found);

        TakeOverHead.Children.Add(IntroParts.Heading(Strings.IntroTakeOverTitle));
        TakeOverHead.Children.Add(IntroParts.Lead(Strings.IntroTakeOverText));
        SetUpFullyHost.Content = IntroParts.SetUpFully(host.SetUpAdminFeatures, Strings.IntroSetUpFullyTip);

        readyStarts.Children.Add(readyList);
        readyStarts.Children.Add(readyNothing);
        ReadyPage.Children.Add(IntroParts.Heading(Strings.IntroReadyTitle));
        ReadyPage.Children.Add(IntroParts.Lead(Strings.IntroReadyText));
        ReadyPage.Children.Add(IntroParts.Facts(
        [
            (Strings.IntroReadyStarts, readyStarts),
            (Strings.IntroReadyWindows, readyWindows),
            (Strings.IntroReadyNew, readyNew),
            (Strings.IntroReadySetUp, readySetUp),
        ]));
        ReadyPage.Children.Add(IntroParts.Notice(Strings.IntroReadyGiveBack, "\uE7A7"));
    }

    /// <summary>One app in the summary: its number in a grey circle and its name; "and 4 more" without a number.</summary>
    static FrameworkElement ReadyLine(ReadyItem item)
    {
        var line = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 16, 8) };
        if (item.HasNumber)
        {
            var number = new TextBlock { Text = item.Number.ToString(), FontSize = (double)Application.Current.FindResource("TextSmall"), FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            number.SetResourceReference(TextBlock.ForegroundProperty, "Text2");
            var circle = new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(12), Child = number, Margin = new Thickness(0, 0, 8, 0) };
            circle.SetResourceReference(Border.BackgroundProperty, "ChipBg");
            line.Children.Add(circle);
        }
        var name = new TextBlock { Text = item.Name, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        name.SetResourceReference(TextBlock.ForegroundProperty, item.HasNumber ? "Text1" : "Text2");
        line.Children.Add(name);
        return line;
    }

    /// <summary>The step <paramref name="step"/>, for the kit's frame to show (IntroStep.Build).</summary>
    public FrameworkElement Page(int step)
    {
        Step = Math.Clamp(step, Welcome, Ready);
        foundKey = null;
        WelcomePage.Visibility = Step == Welcome ? Visibility.Visible : Visibility.Collapsed;
        TakeOverPage.Visibility = Step == TakeOver ? Visibility.Visible : Visibility.Collapsed;
        ReadyPage.Visibility = Step == Ready ? Visibility.Visible : Visibility.Collapsed;
        Sync();
        return this;
    }

    /// <summary>Fills the step from what the sweep found and the Windows autostart report; called by the window's Sync.</summary>
    public void Sync()
    {
        var config = host.Config;
        var found = Introduction.Found(config);
        // Machine-wide entries StartQueue cannot take over in this mode; with the admin helper they are in the list.
        var forAll = host.Report?.Items.Where(i => i.Status == WindowsItemStatus.MachineLocked).Select(i => i.Item.DisplayName).ToList() ?? [];
        leave.IntersectWith(found.Select(e => e.Id));

        welcomeFound.Text = found.Count + forAll.Count == 1 ? Strings.IntroWelcomeFoundOne : Strings.F(Strings.IntroWelcomeFound, found.Count + forAll.Count);

        if (Step == TakeOver)
        {
            loading = true;
            // Sweeps come and go while the step is open; the list is made again only when what it holds changed.
            var shownKey = string.Join("|", found.Select(e => $"{e.Id}:{e.Name}:{e.Target}"));
            if (shownKey != foundKey)
            {
                foundKey = shownKey;
                FoundList.ItemsSource = found.Select(e => new IntroItem(e, !leave.Contains(e.Id), () => Toggle())).ToList();
            }
            ModeEnabled.IsChecked = mode == NewEntryMode.TakeOverEnabled;
            ModeDisabled.IsChecked = mode == NewEntryMode.TakeOverDisabled;
            ModeNotify.IsChecked = mode == NewEntryMode.NotifyOnly;
            loading = false;
            FoundLabel.Text = Strings.F(Strings.IntroAtSignIn, found.Count);
            NoneFound.Visibility = found.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateChosen();

            AllUsersCard.Visibility = forAll.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            AllUsersLabel.Text = Strings.F(Strings.IntroForAllUsers, forAll.Count);
            AllUsersList.ItemsSource = forAll;
            AllUsersText.Text = Setup.UserIsAdmin ? Strings.IntroForAllUsersText : Strings.IntroForAllUsersNoAdmin;
            SetUpFullyHost.Visibility = Setup.UserIsAdmin && !Elevation.AdminMode ? Visibility.Visible : Visibility.Collapsed;
        }

        if (Step == Ready)
        {
            var starts = found.Where(e => !leave.Contains(e.Id)).ToList();
            var shown = starts.Count > ReadyListMost ? starts.Take(ReadyListMost - 1).ToList() : starts;
            var lines = shown.Select((e, i) => new ReadyItem(i + 1, e.Name)).ToList();
            if (shown.Count < starts.Count) lines.Add(new ReadyItem(0, Strings.F(Strings.IntroReadyMore, starts.Count - shown.Count)));
            readyNothing.Text = Strings.IntroReadyNothing;
            readyNothing.Visibility = lines.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            readyList.Visibility = lines.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            readyList.Children.Clear();
            foreach (var line in lines) readyList.Children.Add(ReadyLine(line));

            var windows = new List<string>();
            if (forAll.Count > 0) windows.Add(forAll.Count == 1 ? Strings.IntroReadyWindowsAllOne : Strings.F(Strings.IntroReadyWindowsAll, forAll.Count));
            if (leave.Count > 0) windows.Add(leave.Count == 1 ? Strings.IntroReadyWindowsLeftOne : Strings.F(Strings.IntroReadyWindowsLeft, leave.Count));
            readyWindows.Text = windows.Count == 0 ? Strings.IntroReadyWindowsNone : string.Join("\n", windows);
            readyNew.Text = mode switch
            {
                NewEntryMode.TakeOverDisabled => Strings.IntroReadyNewDisabled,
                NewEntryMode.NotifyOnly => Strings.IntroReadyNewNotify,
                _ => Strings.IntroReadyNewEnabled,
            };
            readySetUp.Text = Elevation.AdminMode ? Strings.IntroReadyFull : Strings.IntroReadyBasis;
        }
    }

    void Toggle()
    {
        if (FoundList.ItemsSource is not IEnumerable<IntroItem> items) return;
        foreach (var item in items)
        {
            if (item.Chosen) leave.Remove(item.Id);
            else leave.Add(item.Id);
        }
        UpdateChosen();
    }

    void UpdateChosen()
    {
        var total = (FoundList.ItemsSource as IEnumerable<IntroItem>)?.Count() ?? 0;
        ChosenText.Text = total == 0 ? "" : Strings.F(Strings.IntroChosen, total - leave.Count);
    }

    void Mode_Checked(object sender, RoutedEventArgs e)
    {
        if (loading) return;
        mode = ModeDisabled.IsChecked == true ? NewEntryMode.TakeOverDisabled
            : ModeNotify.IsChecked == true ? NewEntryMode.NotifyOnly
            : NewEntryMode.TakeOverEnabled;
    }

}

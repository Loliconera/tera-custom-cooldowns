﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using Dragablz;
using Nostrum;
using Nostrum.WPF;
using Nostrum.WPF.Extensions;
using Nostrum.WPF.ThreadSafe;
using TCC.Data;
using TCC.Data.Chat;
using TCC.Settings.WindowSettings;
using TCC.UI;
using TCC.UI.Windows;
using TCC.Utils;
using FocusManager = TCC.UI.FocusManager;

namespace TCC.ViewModels.Widgets;

public class ImportantRemovedArgs : EventArgs
{
    public ActionType Action { get; }
    public ChatMessage? Item { get; }
    public enum ActionType
    {
        Remove,
        Clear
    }

    public ImportantRemovedArgs(ActionType action, ChatMessage? item = null)
    {
        Action = action;
        Item = item;
    }

}
public class ChatViewModel : ThreadSafeObservableObject
{
    public event Action? ForceSizePosUpdateEvent;

    private bool _paused;
    private bool _visible = true;
    private readonly DispatcherTimer _hideTimer;
    private bool _collapsed;
    private bool _mouseOver;
    private Tab? _currentTab;
    private bool _showCollapsedSettingsButton;

    public bool Paused
    {
        get => _paused;
        set => RaiseAndSetIfChanged(value, ref _paused);
    }
    public bool Visible
    {
        get => _visible;
        set => RaiseAndSetIfChanged(value, ref _visible);
    }
    public bool Collapsed
    {
        get => WindowSettings.CanCollapse && _collapsed;
        set
        {
            if (!RaiseAndSetIfChanged(value, ref _collapsed)) return;
            if (_collapsed && WindowSettings.StaysCollapsed)
            {
                WindowSettings.H = 84;
                WindowSettings.Y += 84;
                ForceSizePosUpdateEvent?.Invoke();
            }
        }
    }
    public bool MouseOver
    {
        get => _mouseOver;
        set => RaiseAndSetIfChanged(value, ref _mouseOver);
    }
    public bool ShowCollapsedSettingsButton
    {
        get => _showCollapsedSettingsButton;
        set => RaiseAndSetIfChanged(value, ref _showCollapsedSettingsButton);
    }

        
    public Tab? CurrentTab
    {
        get => _currentTab;
        set => RaiseAndSetIfChanged(value, ref _currentTab);
    }
    public ICommand MakeGlobalCommand { get; }
    public ICommand OpenSysMsgSettingsCommand { get; }
    public ICommand JumpToPresentCommand { get; }

    public ChatWindowSettings WindowSettings { get; }

    public ThreadSafeObservableCollection<TabViewModel> TabVMs { get; set; }
    public ThreadSafeObservableCollection<Lfg> LFGs => ChatManager.Instance.LFGs;
    public IInterTabClient InterTabClient { get; }

    public List<Tab> Tabs => TabVMs.ToArray().Select(x => x.Content).Cast<Tab>().ToList();

    public Func<TabViewModel> AddNewTabCommand
    {
        get
        {
            return () =>
            {
                // invoked on main thread because TabData is created there by JSON settings deserialization
                //return App.BaseDispatcher.Invoke(() =>
                //{
                var t = new TabViewModel();
                var content = new Tab(new TabInfo("New tab"));
                content.PropertyChanged += (_, ev) =>
                {
                    if (ev.PropertyName == nameof(TabInfoVM.TabName)) t.Header = content.TabInfoVM.TabName;
                };
                t.Content = content;
                return t;
                //});
            };
        }
    }

    public ChatManager Manager => ChatManager.Instance;
    public ChatViewModel(ChatWindowSettings s)
    {
        WindowSettings = s;
        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(WindowSettings.HideTimeout) };
        _hideTimer.Tick += OnHideTimerTick;
        WindowSettings.TimeoutChanged += ChangeTimerInterval;
        InterTabClient = new ChatTabClient();
        TabVMs = new ThreadSafeObservableCollection<TabViewModel>();

        MakeGlobalCommand = new RelayCommand(_ => WindowSettings.MakePositionsGlobal());
        OpenSysMsgSettingsCommand = new RelayCommand(_ => new SystemMessagesConfigWindow { ShowActivated = true, Topmost = true }.Show());
        JumpToPresentCommand = new RelayCommand(_ => ChatManager.Instance.ScrollToBottom());

        ChatManager.Instance.NewMessage += CheckAttention;
        Game.GameUiModeChanged += CheckCollapsed;
        Game.ChatModeChanged += CheckCollapsed;
        WindowSettings.CanCollapseChanged += () => InvokePropertyChanged(nameof(Collapsed));
        WindowSettings.StaysCollapsedChanged += () => InvokePropertyChanged(nameof(Collapsed));
        if (WindowSettings.StaysCollapsed) _collapsed = true;
    }

    private void CheckCollapsed()
    {
        Collapsed = !(Game.InGameUiOn || Game.InGameChatOpen) || WindowSettings.StaysCollapsed;
        ShowCollapsedSettingsButton = Game.InGameUiOn;
    }

    private void ChangeTimerInterval()
    {
        _hideTimer.Interval = TimeSpan.FromSeconds(WindowSettings.HideTimeout);
        _hideTimer.Refresh();
    }

    private void OnHideTimerTick(object? sender, EventArgs e)
    {
        if (!WindowSettings.FadeOut)
        {
            if (!Visible) Visible = true;
            return;
        }

        if (MouseOver) return;
        Visible = false;
        _hideTimer.Stop();
    }

    private void CheckAttention(ChatMessage chatMessage)
    {
        if (!WindowSettings.ShowImportant) return;
        if (!chatMessage.ContainsPlayerName && chatMessage.Channel != ChatChannel.ReceivedWhisper) return;
        foreach (var t in TabVMs
            .Select(x => x.Content)
            .Cast<Tab>()
            .Where(tab => tab.Dispatcher.Invoke(() => tab.Messages.Contains(chatMessage))))
        {
            t.AddImportantMessage(chatMessage);
        }
    }
    public void HandleIsDraggingChanged(bool isDragging, IEnumerable<DragablzItem> newOrder)
    {
        if (isDragging)
        {
            FocusManager.ForceFocused = true;
            return;
        }

        var old = new TabViewModel[TabVMs.Count];
        TabVMs.CopyTo(old, 0);
        var items = newOrder.ToList();
        var same = !items.Where((_, i) => old[i].Header != items.ToList()[i].Content).Any();
        if (same)
        {
            FocusManager.ForceFocused = false;
            return;
        }
        TabVMs.Clear();
        foreach (var foundTab in items.Select(tab => old.FirstOrDefault(x => x.Header == tab.Content))
                                      .Where(foundTab => foundTab != null))
        {
            if (foundTab != null)
                TabVMs.Add(foundTab);
        }
        FocusManager.ForceFocused = false;
    }
    public void UpdateSettings(double left, double top)
    {
        WindowSettings.Tabs.Clear();
        //_windowSettings.Tabs.AddRange(Tabs);
        foreach (var t in Tabs) WindowSettings.Tabs.Add(t.TabInfo);
        WindowSettings.X = (left + FocusManager.TeraScreen.Bounds.Left) / WindowManager.ScreenSize.Width;
        WindowSettings.Y = (top + FocusManager.TeraScreen.Bounds.Top) / WindowManager.ScreenSize.Height;
        Task.Run(() =>
        {
            var v = App.Settings.ChatWindowsSettings;
            var s = v.FirstOrDefault(x => x == WindowSettings);
            if (s == null) v.Add(WindowSettings);
        });
    }
    public void RefreshHideTimer()
    {
        _hideTimer.Refresh();
    }
    public void StopHideTimer()
    {
        _hideTimer.Stop();
        Visible = true;
    }
    public void LoadTabs(IEnumerable<TabInfo>? tabs = null)
    {
        if (tabs != null)
        {
            foreach (var tabInfo in tabs)
            {
                TabVMs.Add(new TabViewModel(tabInfo.Name, new Tab(tabInfo)));
            }
        }
        if (TabVMs.Count != 0) return;
        var allTabData = new TabInfo("All");
        allTabData.HiddenAuthors.Add("System");
        var all = new Tab(allTabData);

        var guildTabData = new TabInfo("Guild");
        guildTabData.ShowedChannels.Add(ChatChannel.Guild);
        guildTabData.ShowedChannels.Add(ChatChannel.GuildNotice);
        var guild = new Tab(guildTabData);

        var groupTabData = new TabInfo("Group");
        groupTabData.ShowedChannels.Add(ChatChannel.Party);
        groupTabData.ShowedChannels.Add(ChatChannel.PartyNotice);
        groupTabData.ShowedChannels.Add(ChatChannel.Raid);
        groupTabData.ShowedChannels.Add(ChatChannel.RaidLeader);
        groupTabData.ShowedChannels.Add(ChatChannel.Ress);
        groupTabData.ShowedChannels.Add(ChatChannel.Death);
        groupTabData.ShowedChannels.Add(ChatChannel.Group);
        groupTabData.ShowedChannels.Add(ChatChannel.GroupAlerts);
        var group = new Tab(groupTabData);

        var whisperData = new TabInfo("Whisper");
        whisperData.ShowedChannels.Add(ChatChannel.ReceivedWhisper);
        whisperData.ShowedChannels.Add(ChatChannel.SentWhisper);
        var w = new Tab(whisperData);

        var sysData = new TabInfo("System");
        sysData.ShowedAuthors.Add("System");

        var sys = new Tab(sysData);

        TabVMs.Add(new TabViewModel(all.TabInfoVM.TabName, all));
        TabVMs.Add(new TabViewModel(guild.TabInfoVM.TabName, guild));
        TabVMs.Add(new TabViewModel(group.TabInfoVM.TabName, group));
        TabVMs.Add(new TabViewModel(w.TabInfoVM.TabName, w));
        TabVMs.Add(new TabViewModel(sys.TabInfoVM.TabName, sys));
        CurrentTab = TabVMs[0].Content as Tab;
        foreach (var t in Tabs) WindowSettings.Tabs.Add(t.TabInfo);
    }
    public void RemoveTab(Tab dc)
    {
        var t = TabVMs.FirstOrDefault(x => x.Content == dc);
        if (t != null) TabVMs.Remove(t);
    }
    public void CheckVisibility(IList newItems)
    {
        CurrentTab ??= TabVMs[0].Content as Tab;

        var chatMessage = (ChatMessage?) newItems[0];
        if (chatMessage == null) return;
        if (CurrentTab != null && !CurrentTab.Filter(chatMessage)) return;
        RefreshHideTimer();
        Visible = true;

    }

    public IEnumerable<ClickThruMode> ClickThruModes => EnumUtils.ListFromEnum<ClickThruMode>();

}
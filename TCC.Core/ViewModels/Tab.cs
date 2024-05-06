﻿using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using Newtonsoft.Json;
using Nostrum.WPF;
using Nostrum.WPF.ThreadSafe;
using TCC.Data.Chat;
using TCC.Utilities;
using TCC.Utils;
using TCC.ViewModels.Widgets;

namespace TCC.ViewModels;

public class TabInfo
{
    public string Name { get; set; }
    public List<string> ShowedAuthors { get; }
    public List<string> HiddenAuthors { get; }
    public List<string> ShowedKeywords { get; }
    public List<string> HiddenKeywords { get; }
    public List<ChatChannel> ShowedChannels { get; }
    public List<ChatChannel> HiddenChannels { get; }

    public TabInfo(string name)
    {
        ShowedAuthors = new List<string>();
        HiddenAuthors = new List<string>();
        ShowedKeywords = new List<string>();
        HiddenKeywords = new List<string>();
        ShowedChannels = new List<ChatChannel>();
        HiddenChannels = new List<ChatChannel>();

        Name = name;
    }
}
public class TabInfoVM : ThreadSafeObservableObject
{
    private string _tabName = "";
    public string TabName
    {
        get => _tabName;
        set => RaiseAndSetIfChanged(value, ref _tabName);
    }

    public ThreadSafeObservableCollection<string> Authors { get; set; }

    public ThreadSafeObservableCollection<string> ExcludedAuthors { get; set; }

    public ThreadSafeObservableCollection<ChatChannel> ShowedChannels { get; set; }

    public ThreadSafeObservableCollection<ChatChannel> ExcludedChannels { get; set; }

    public ThreadSafeObservableCollection<string> Keywords { get; set; }

    public ThreadSafeObservableCollection<string> ExcludedKeywords { get; set; }

    public TabInfoVM()
    {
        Authors = new ThreadSafeObservableCollection<string>(_dispatcher);
        ExcludedAuthors = new ThreadSafeObservableCollection<string>(_dispatcher);
        Keywords = new ThreadSafeObservableCollection<string>(_dispatcher);
        ExcludedKeywords = new ThreadSafeObservableCollection<string>(_dispatcher);
        ShowedChannels = new ThreadSafeObservableCollection<ChatChannel>(_dispatcher);
        ExcludedChannels = new ThreadSafeObservableCollection<ChatChannel>(_dispatcher);
    }

    public TabInfoVM(TabInfo info) : this()
    {
        TabName = info.Name;
        foreach (var a in info.ShowedAuthors) Authors.Add(a);
        foreach (var a in info.HiddenAuthors) ExcludedAuthors.Add(a);
        foreach (var k in info.ShowedKeywords) Keywords.Add(k);
        foreach (var k in info.HiddenKeywords) ExcludedKeywords.Add(k);
        foreach (var c in info.ShowedChannels) ShowedChannels.Add(c);
        foreach (var c in info.HiddenChannels) ExcludedChannels.Add(c);

        Authors.CollectionChanged += (_, ev) =>
        {
            switch (ev.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    info.ShowedAuthors.AddRange(ev.NewItems!.Cast<string>());
                    break;
                case NotifyCollectionChangedAction.Remove:
                    foreach (var i in ev.OldItems!.Cast<string>()) info.ShowedAuthors.Remove(i);
                    break;
            }
        };
        ExcludedAuthors.CollectionChanged += (_, ev) =>
        {
            switch (ev.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    info.HiddenAuthors.AddRange(ev.NewItems!.Cast<string>());
                    break;
                case NotifyCollectionChangedAction.Remove:
                    foreach (var i in ev.OldItems!.Cast<string>()) info.HiddenAuthors.Remove(i);
                    break;
            }
        };
        Keywords.CollectionChanged += (_, ev) =>
        {
            switch (ev.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    info.ShowedKeywords.AddRange(ev.NewItems!.Cast<string>());
                    break;
                case NotifyCollectionChangedAction.Remove:
                    foreach (var i in ev.OldItems!.Cast<string>()) info.ShowedKeywords.Remove(i);
                    break;
            }
        };
        ExcludedKeywords.CollectionChanged += (_, ev) =>
        {
            switch (ev.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    info.HiddenKeywords.AddRange(ev.NewItems!.Cast<string>());
                    break;
                case NotifyCollectionChangedAction.Remove:
                    foreach (var i in ev.OldItems!.Cast<string>()) info.HiddenKeywords.Remove(i);
                    break;
            }
        };
        ShowedChannels.CollectionChanged += (_, ev) =>
        {
            switch (ev.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    info.ShowedChannels.AddRange(ev.NewItems!.Cast<ChatChannel>());
                    break;
                case NotifyCollectionChangedAction.Remove:
                    foreach (var i in ev.OldItems!.Cast<ChatChannel>()) info.ShowedChannels.Remove(i);
                    break;
            }
        };
        ExcludedChannels.CollectionChanged += (_, ev) =>
        {
            switch (ev.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    info.HiddenChannels.AddRange(ev.NewItems!.Cast<ChatChannel>());
                    break;
                case NotifyCollectionChangedAction.Remove:
                    foreach (var i in ev.OldItems!.Cast<ChatChannel>()) info.HiddenChannels.Remove(i);
                    break;
            }
        };
    }
}
public class Tab : ThreadSafeObservableObject
{
    public TabInfo TabInfo { get; }
    public TabInfoVM TabInfoVM { get; set; }

    private ChatMessage? _pinnedMessage;


    public string TabName
    {
        get => TabInfo.Name;
        set
        {
            if (TabInfo.Name == value) return;
            TabInfo.Name = value;
            TabInfoVM.TabName = value;
            InvokePropertyChanged();
        }
    }

    [JsonIgnore]
    public List<ChatChannelOnOff> AllChannels => TccUtils.GetEnabledChannelsList(); // needed for combobox in settings
    [JsonIgnore]
    public ICommand ScrollToMessageCommand { get; }
    [JsonIgnore]
    public ICommand RemoveImportantMessageCommand { get; }
    [JsonIgnore]
    public ICommand ClearAllCommand { get; }
    [JsonIgnore]
    public string ImportantMessagesLabel => ImportantMessages.Count > 9 ? "!" : ImportantMessages.Count.ToString();

    [JsonIgnore]
    public bool Attention => ImportantMessages.Count > 0;

    [JsonIgnore]
    public ThreadSafeObservableCollection<ChatMessage> ImportantMessages { get; set; }
    [JsonIgnore]
    public ICollectionView Messages { get; }

    [JsonIgnore]
    public ChatMessage? PinnedMessage
    {
        get => _pinnedMessage;
        set => RaiseAndSetIfChanged(value, ref _pinnedMessage);
    }
    public void Refresh()
    {
        Messages.Refresh();
    }

    public Tab(TabInfo tabInfo)
    {
        Messages = new ListCollectionView(ChatManager.Instance.ChatMessages);
        ImportantMessages = new ThreadSafeObservableCollection<ChatMessage>(_dispatcher);
        RemoveImportantMessageCommand = new RelayCommand(msg =>
        {
            RemoveImportantMessage((ChatMessage?)msg);
            TabViewModel.InvokeImportantRemoved(this, new ImportantRemovedArgs(ImportantRemovedArgs.ActionType.Remove, (ChatMessage?)msg));
        });
        ClearAllCommand = new RelayCommand(_ =>
        {
            ClearImportant();
            TabViewModel.InvokeImportantRemoved(this, new ImportantRemovedArgs(ImportantRemovedArgs.ActionType.Clear));
        });
        ScrollToMessageCommand = new RelayCommand<ChatMessage>(msg =>
        {
            if (msg != null) ChatManager.Instance.ScrollToMessage(this, msg);
        });
        TabViewModel.ImportantRemoved += SyncImportant;

        TabInfo = tabInfo;
        TabInfoVM = new TabInfoVM(TabInfo);
        ApplyFilter();
    }

    //public Tab(string n, ChatChannel[] ch, ChatChannel[] ex, string[] a, string[] exa) : this()
    //{
    //    if (n == null || ch == null || ex == null || a == null || exa == null) return;
    //    TabName = n;
    //    foreach (var auth in a)
    //    {
    //        Authors.Add(auth);
    //    }
    //    foreach (var auth in exa)
    //    {
    //        ExcludedAuthors.Add(auth);
    //    }
    //    foreach (var chan in ch)
    //    {
    //        Channels.Add(chan);
    //    }
    //    foreach (var chan in ex)
    //    {
    //        ExcludedChannels.Add(chan);
    //    }

    //    ApplyFilter();
    //}

    private void SyncImportant(Tab source, ImportantRemovedArgs e)
    {
        if (source == this) return;
        switch (e.Action)
        {
            case ImportantRemovedArgs.ActionType.Remove:
                RemoveImportantMessage(e.Item);
                break;
            case ImportantRemovedArgs.ActionType.Clear:
                ClearImportant();
                break;
        }
    }

    public bool Filter(ChatMessage m)
    {
        return (TabInfoVM.Authors.Count == 0 || TabInfoVM.Authors.Any(x => x == m.Author)) &&
               (TabInfoVM.ShowedChannels.Count == 0 || TabInfoVM.ShowedChannels.Any(x => x == m.Channel)) &&
               (TabInfoVM.ExcludedChannels.Count == 0 || TabInfoVM.ExcludedChannels.All(x => x != m.Channel)) &&
               (TabInfoVM.ExcludedAuthors.Count == 0 || TabInfoVM.ExcludedAuthors.All(x => x != m.Author)) &&
               (TabInfoVM.Keywords.Count == 0 || TabInfoVM.Keywords.Any(x => m.PlainMessage.ToLower().Contains(x.ToLower()))) &&
               (TabInfoVM.ExcludedKeywords.Count == 0 || TabInfoVM.ExcludedKeywords.Any(x => !m.PlainMessage.ToLower().Contains(x.ToLower())));

    }
    public void ApplyFilter()
    {
        //if (Channels?.Count == 0 && 
        //    Authors?.Count == 0 && 
        //    ExcludedChannels?.Count == 0 && 
        //    ExcludedAuthors?.Count == 0)
        //{
        //    Messages.Filter = null;
        //}
        //else
        //{
        //}
        _dispatcher.Invoke(() =>
        {
            Messages.Filter = f =>
            {
                var m = (ChatMessage)f;
                return Filter(m);
            };
        });
    }

    public void AddImportantMessage(ChatMessage chatMessage)
    {
        ImportantMessages.Add(chatMessage);
        InvokePropertyChanged(nameof(Attention));
        InvokePropertyChanged(nameof(ImportantMessagesLabel));
    }

    public void RemoveImportantMessage(ChatMessage? msg)
    {
        if (msg != null) ImportantMessages.Remove(msg);
        InvokePropertyChanged(nameof(Attention));
        InvokePropertyChanged(nameof(ImportantMessagesLabel));
    }
    public void ClearImportant()
    {
        ImportantMessages.Clear();
        InvokePropertyChanged(nameof(Attention));
        InvokePropertyChanged(nameof(ImportantMessagesLabel));
    }
}
﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xml.Linq;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Nostrum.WPF;
using Nostrum.WPF.Extensions;
using Nostrum.WPF.Factories;
using Nostrum.WPF.ThreadSafe;
using TCC.Data;
using TCC.Data.Abnormalities;
using TCC.Data.Pc;
using TCC.Settings;
using TCC.Settings.WindowSettings;
using TCC.UI;
using TCC.UI.Controls.Dashboard;
using TCC.UI.Windows;
using TCC.Utilities;
using TCC.Utils;
using TeraDataLite;
using TeraPacketParser.Analysis;
using TeraPacketParser.Messages;
using MessageBoxImage = TCC.Data.MessageBoxImage;

namespace TCC.ViewModels;

[TccModule]
[UsedImplicitly]
public class DashboardViewModel : TccWindowViewModel
{
    /* -- Fields ----------------------------------------------- */

    private bool _discardFirstVanguardPacket = true;
    private ICollectionViewLiveShaping? _sortedColumns;
    private ObservableCollection<DungeonColumnViewModel>? _columns;
    private Character? _selectedCharacter;
    private readonly object _lock = new();
    private readonly Timer _tabFlushTimer;
    private readonly List<Dictionary<uint, ItemAmount>> _pendingTabs;
    private bool _showDetails;


    /* -- Properties ------------------------------------------- */

    public Character? CurrentCharacter => Game.Account.CurrentCharacter;
    public Character? SelectedCharacter
    {
        get => _selectedCharacter;
        set => RaiseAndSetIfChanged(value, ref _selectedCharacter);
    }

    public bool ShowElleonMarks => App.Settings.LastLanguage.Contains("EU");

    private ThreadSafeObservableCollection<Character> _characters { get; }
    public ICollectionViewLiveShaping SortedCharacters { get; }
    public ICollectionViewLiveShaping HiddenCharacters { get; }
    public ICollectionViewLiveShaping SortedColumns// { get; }
    {
        get
        {
            return _sortedColumns ??= CollectionViewFactory.CreateLiveCollectionView(Columns,
                                          o => o.IsVisible,
                                          [$"{nameof(DungeonColumnViewModel.IsVisible)}", $"{nameof(DungeonColumnViewModel.Dungeon)}.{nameof(Dungeon.Index)}"],
                                          [new SortDescription($"{nameof(DungeonColumnViewModel.Dungeon)}.{nameof(Dungeon.Index)}", ListSortDirection.Ascending)])
                                      ?? throw new Exception("Failed to create LiveCollectionView");
        }
    }
    public ICollectionViewLiveShaping? SelectedCharacterInventory { get; set; }
    public ICollectionViewLiveShaping CharacterViewModelsView { get; set; }

    public ObservableCollection<InventoryItem> InventoryViewList
    {
        get
        {
            var ret = new ObservableCollection<InventoryItem>();
            Task.Factory.StartNew(() =>
            {
                if (SelectedCharacter == null) return;
                foreach (var item in SelectedCharacter.Inventory.ToList())
                {
                    App.BaseDispatcher.InvokeAsync(() =>
                    {
                        ret.Add(item);
                    }, DispatcherPriority.Background);
                }
            });
            return ret;
        }
    }

    public int TotalElleonMarks => Game.Account.Characters.ToSyncList().Sum(c => c.ElleonMarks);

    public int TotalVanguardCredits => Game.Account.Characters.ToSyncList().Sum(c => c.VanguardInfo.Credits);

    public int TotalGuardianCredits => Game.Account.Characters.ToSyncList().Sum(c => c.GuardianInfo.Credits);

    public ThreadSafeObservableCollection<CharacterViewModel> CharacterViewModels
    {
        get;
        //{
        //    if (_characters == null) _characters = new ObservableCollection<CharacterViewModel>();
        //    _characters.Clear();
        //    foreach (var o in Characters)
        //    {
        //        _characters.Add(new CharacterViewModel { Character = o });
        //    }
        //    return _characters;
        //}
    }

    private void SyncViewModel(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                foreach (Character? item in e.NewItems!)
                {
                    if (item != null) _characters.Add(item);
                }
                break;
            case NotifyCollectionChangedAction.Remove:
                foreach (Character? item in e.OldItems!)
                {
                    if (item != null) _characters.Remove(item);
                }
                break;
            case NotifyCollectionChangedAction.Reset:
                _characters.Clear();
                break;
        }
    }

    public ObservableCollection<DungeonColumnViewModel> Columns
    {
        get
        {
            if (_columns != null) return _columns;
            _columns = new ObservableCollection<DungeonColumnViewModel>();
            return _columns;
        }
    }
    public ICommand LoadDungeonsCommand { get; }
    public ICommand OpenMergedInventoryCommand { get; }
    public ICommand RemoveCharacterCommand { get; }
    public ICommand HideDetailsCommand { get; }

    public bool ShowDetails
    {
        get => _showDetails;
        set
        {
            if (!RaiseAndSetIfChanged(value, ref _showDetails)) return;
            if (!value)
            {
                InventoryFilter = "";
            }
        }
    }

    public string InventoryFilter
    {
        get => _inventoryFilter;
        set
        {
            if (!RaiseAndSetIfChanged(value, ref _inventoryFilter)) return;
            FilterInventory();
        }
    }

    private void FilterInventory()
    {
        var view = (ICollectionView?)SelectedCharacterInventory;
        if (view == null) return;
        view.Filter = o =>
        {
            var item = ((InventoryItem)o).Item;
            var name = item.Name;
            return name.Contains(InventoryFilter, StringComparison.InvariantCultureIgnoreCase);
        };
        view.Refresh();
    }

    /* -- Constructor ------------------------------------------ */
    private bool _loaded;
    private string _inventoryFilter = "";

    public DashboardViewModel(WindowSettingsBase? settings) : base(settings)
    {
        KeyboardHook.Instance.RegisterCallback(App.Settings.DashboardHotkey, OnShowDashboardHotkeyPressed);
        _characters = new ThreadSafeObservableCollection<Character>();
        CharacterViewModels = new ThreadSafeObservableCollection<CharacterViewModel>();
        EventGroups = new ThreadSafeObservableCollection<EventGroup>();
        Markers = new ThreadSafeObservableCollection<TimeMarker>();
        SpecialEvents = new ThreadSafeObservableCollection<DailyEvent>();
        LoadDungeonsCommand = new RelayCommand(_ =>
        {
            if (_loaded) return;

            Task.Factory.StartNew(() =>
            {
                foreach (var dungeon in Game.DB!.DungeonDatabase.Dungeons.Values)
                {
                    App.BaseDispatcher.InvokeAsync(() =>
                    {
                        var dvc = new DungeonColumnViewModel(dungeon);
                        foreach (var charVm in CharacterViewModels.ToArray())
                        {
                            dvc.DungeonsList.Add(new DungeonCooldownViewModel(
                                charVm.Character.DungeonInfo.DungeonList.FirstOrDefault(x => x.Dungeon.Id == dungeon.Id) ?? throw new NullReferenceException("Dungeon not found!"),
                                charVm.Character));
                        }

                        _columns?.Add(dvc);
                    }, DispatcherPriority.Background);
                }
            });
            _loaded = true;
        }, _ => !_loaded);
        OpenMergedInventoryCommand = new RelayCommand(_ =>
        {
            new MergedInventoryWindow
            {
                Topmost = true,
                Owner = WindowManager.DashboardWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.ShowDialog();
        });
        RemoveCharacterCommand = new RelayCommand(_ =>
        {
            ShowDetails = false;
            if (SelectedCharacter == null) return;
            SelectedCharacter.Hidden = true;
        });
        HideDetailsCommand = new RelayCommand(_ =>
        {
            ShowDetails = false;
        });

        LoadCharacters();

        foreach (var c in Game.Account.Characters)
        {
            _characters.Add(c);
            CharacterViewModels.Add(new CharacterViewModel(c));
        }

        Game.Account.Characters.CollectionChanged += SyncViewModel;

        SortedCharacters = CollectionViewFactory.CreateLiveCollectionView(_characters,
                               character => !character.Hidden,
                               [nameof(Character.Hidden)],
                               [new SortDescription(nameof(Character.Position), ListSortDirection.Ascending)])
                           ?? throw new Exception("Failed to create LiveCollectionView");

        HiddenCharacters = CollectionViewFactory.CreateLiveCollectionView(_characters,
                               character => character.Hidden,
                               [nameof(Character.Hidden)],
                               [new SortDescription(nameof(Character.Position), ListSortDirection.Ascending)])
                           ?? throw new Exception("Failed to create LiveCollectionView");


        CharacterViewModelsView = CollectionViewFactory.CreateLiveCollectionView(CharacterViewModels,
                                      characterVM => !characterVM.Character.Hidden,
                                      [$"{nameof(CharacterViewModel.Character)}.{nameof(Character.Hidden)}"],
                                      [new SortDescription($"{nameof(CharacterViewModel.Character)}.{nameof(Character.Position)}", ListSortDirection.Ascending)])
                                  ?? throw new Exception("Failed to create LiveCollectionView");


        _pendingTabs = new List<Dictionary<uint, ItemAmount>>();
        _tabFlushTimer = new Timer(1000)
        {
            AutoReset = false
        };
        _tabFlushTimer.Elapsed += OnTabFlushTimerElapsed;
        //SortedColumns = CollectionViewFactory.CreateLiveView(Columns,
        //    o => o.Dungeon.Show,
        //    new[] { $"{nameof(Dungeon)}.{nameof(Dungeon.Index)}" },
        //    new[] { new SortDescription($"{nameof(Dungeon)}.{nameof(Dungeon.Index)}", ListSortDirection.Ascending) });
    }

    private void OnTabFlushTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        try
        {
            CurrentCharacter?.Inventory.Clear();
            _dispatcher.InvokeAsync(() =>
            {
                lock (_lock)
                {
                    foreach (var pendingTab in _pendingTabs) //TODO: lock on list   
                    {
                        foreach (var keyVal in pendingTab)
                        {
                            var existing = CurrentCharacter?.Inventory.FirstOrDefault(x => x.Item.Id == keyVal.Value.Id);
                            if (existing != null)
                            {
                                existing.Amount = keyVal.Value.Amount;
                                continue;
                            }
                            CurrentCharacter?.Inventory.Add(new InventoryItem(keyVal.Key, keyVal.Value.Id, keyVal.Value.Amount));
                        }
                    }
                    _pendingTabs.Clear();
                }
            });
        }
        catch
        {
            // ignored
        }
    }

    private void OnShowDashboardHotkeyPressed()
    {
        if (WindowManager.DashboardWindow.IsVisible) WindowManager.DashboardWindow.HideWindow();
        else WindowManager.DashboardWindow.ShowWindow();
    }


    /* -- Methods ---------------------------------------------- */

    public void SaveCharacters()
    {
        App.BaseDispatcher.BeginInvoke(() =>
        {
            var account = (Account)Game.Account.Clone();
            foreach (var ch in account.Characters.ToSyncList())
            {
                var dungs = new Dictionary<uint, DungeonCooldownData>();
                foreach (var dgcd in ch.DungeonInfo.DungeonList)
                {
                    dungs[dgcd.Id] = dgcd;
                }
                ch.DungeonInfo.DungeonList.Clear();
                foreach (var dg in dungs.Values)
                {
                    ch.DungeonInfo.DungeonList.Add(dg);
                }
            }
            var json = JsonConvert.SerializeObject(account, Formatting.Indented, TccUtils.GetDefaultJsonSerializerSettings());
            File.WriteAllText(SettingsGlobals.CharacterJsonPath, json);
            if (File.Exists(SettingsGlobals.CharacterXmlPath))
                File.Delete(SettingsGlobals.CharacterXmlPath);
        });
    }

    private void LoadCharacters()
    {
        try
        {
            if (!File.Exists(SettingsGlobals.CharacterJsonPath)) return;
            App.BaseDispatcher.Invoke(() =>
            {
                var account = JsonConvert.DeserializeObject<Account>(File.ReadAllText(SettingsGlobals.CharacterJsonPath), TccUtils.GetDefaultJsonSerializerSettings());
                Game.Account = account ?? throw new FileFormatException(new Uri(SettingsGlobals.CharacterJsonPath));
            });
        }
        catch (Exception e)
        {
            var res = TccMessageBox.Show("TCC", SR.CannotReadCharacters, MessageBoxButton.OKCancel);
            Log.F($"Cannot read characters file: {e}");
            if (res == MessageBoxResult.OK)
            {
                LoadCharacters();
            }
            else
            {
                File.Delete(SettingsGlobals.CharacterXmlPath); // todo: remove after merge
                File.Delete(SettingsGlobals.CharacterJsonPath);
                LoadCharacters();
            }
        }
    }
    public void SetLoggedIn(uint id)
    {
        _discardFirstVanguardPacket = true;
        foreach (var x in Game.Account.Characters) x.IsLoggedIn = x.Id == id;
    }
    public void SetDungeons(Dictionary<uint, short> dungeonCooldowns)
    {
        CurrentCharacter?.DungeonInfo.UpdateEntries(dungeonCooldowns);

    }
    public void SetDungeons(uint charId, Dictionary<uint, short> dungeonCooldowns)
    {
        Game.Account.Characters.FirstOrDefault(x => x.Id == charId)?.DungeonInfo.UpdateEntries(dungeonCooldowns);

    }
    public void SetVanguard(int weeklyDone, int weeklymax, int dailyDone, int vanguardCredits)
    {
        if (_discardFirstVanguardPacket)
        {
            _discardFirstVanguardPacket = false;
            return;
        }

        if (CurrentCharacter == null) return;
        CurrentCharacter.VanguardInfo.WeekliesDone = weeklyDone;
        CurrentCharacter.VanguardInfo.WeekliesMax = weeklymax;
        CurrentCharacter.VanguardInfo.DailiesDone = dailyDone;
        CurrentCharacter.VanguardInfo.Credits = vanguardCredits;
        SaveCharacters();
        InvokePropertyChanged(nameof(TotalVanguardCredits));
    }
    public void SetVanguardCredits(int pCredits)
    {
        if (CurrentCharacter == null) return;

        CurrentCharacter.VanguardInfo.Credits = pCredits;
        InvokePropertyChanged(nameof(TotalVanguardCredits));
    }
    public void SetGuardianCredits(int pCredits)
    {
        if (CurrentCharacter == null) return;

        CurrentCharacter.GuardianInfo.Credits = pCredits;
        InvokePropertyChanged(nameof(TotalGuardianCredits));
    }
    public void SetElleonMarks(int val)
    {
        if (CurrentCharacter == null) return;

        CurrentCharacter.ElleonMarks = val;
        InvokePropertyChanged(nameof(TotalElleonMarks));
    }

    public void SelectCharacter(Character character)
    {
        try
        {
            ((ICollectionView?)SelectedCharacterInventory)?.Free();

            SelectedCharacter = character;
            SelectedCharacterInventory = CollectionViewFactory.CreateLiveCollectionView(character.Inventory,
                sortFilters:
                [
                    new SortDescription($"{nameof(Item)}.{nameof(Item.RareGrade)}", ListSortDirection.Ascending),
                    new SortDescription($"{nameof(Item)}.{nameof(Item.Id)}", ListSortDirection.Ascending)
                ]);

            //WindowManager.DashboardWindow.ShowDetails();
            ShowDetails = true;
            Task.Delay(300).ContinueWith(_ => Task.Factory.StartNew(() => InvokePropertyChanged(nameof(SelectedCharacterInventory))));
        }
        catch (Exception e)
        {
            Log.F($"Failed to select character: {e}");
        }
    }

    protected override void InstallHooks()
    {
        PacketAnalyzer.Sniffer.EndConnection += OnDisconnected;
        PacketAnalyzer.Processor.Hook<S_UPDATE_NPCGUILD>(OnUpdateNpcGuild);
        PacketAnalyzer.Processor.Hook<S_NPCGUILD_LIST>(OnNpcGuildList);
        PacketAnalyzer.Processor.Hook<S_ITEMLIST>(OnItemList);
        PacketAnalyzer.Processor.Hook<S_PLAYER_STAT_UPDATE>(OnPlayerStatUpdate);
        PacketAnalyzer.Processor.Hook<S_GET_USER_LIST>(OnGetUserList);
        PacketAnalyzer.Processor.Hook<S_LOGIN>(OnLogin);
        PacketAnalyzer.Processor.Hook<S_RETURN_TO_LOBBY>(OnReturnToLobby);
        PacketAnalyzer.Processor.Hook<S_DUNGEON_COOL_TIME_LIST>(OnDungeonCoolTimeList);
        //PacketAnalyzer.Processor.Hook<S_FIELD_POINT_INFO>(OnFieldPointInfo);
        PacketAnalyzer.Processor.Hook<S_AVAILABLE_EVENT_MATCHING_LIST>(OnAvailableEventMatchingList);
        PacketAnalyzer.Processor.Hook<S_DUNGEON_CLEAR_COUNT_LIST>(OnDungeonClearCountList);
    }
    protected override void RemoveHooks()
    {
        PacketAnalyzer.Processor.Unhook<S_UPDATE_NPCGUILD>(OnUpdateNpcGuild);
        PacketAnalyzer.Processor.Unhook<S_NPCGUILD_LIST>(OnNpcGuildList);
        PacketAnalyzer.Processor.Unhook<S_ITEMLIST>(OnItemList);
        PacketAnalyzer.Processor.Unhook<S_PLAYER_STAT_UPDATE>(OnPlayerStatUpdate);
        PacketAnalyzer.Processor.Unhook<S_GET_USER_LIST>(OnGetUserList);
        PacketAnalyzer.Processor.Unhook<S_LOGIN>(OnLogin);
        PacketAnalyzer.Processor.Unhook<S_RETURN_TO_LOBBY>(OnReturnToLobby);
        PacketAnalyzer.Processor.Unhook<S_DUNGEON_COOL_TIME_LIST>(OnDungeonCoolTimeList);
        //PacketAnalyzer.Processor.Unhook<S_FIELD_POINT_INFO>(OnFieldPointInfo);
        PacketAnalyzer.Processor.Unhook<S_AVAILABLE_EVENT_MATCHING_LIST>(OnAvailableEventMatchingList);
        PacketAnalyzer.Processor.Unhook<S_DUNGEON_CLEAR_COUNT_LIST>(OnDungeonClearCountList);
    }

    private void OnDisconnected()
    {
        UpdateBuffs();
        SaveCharacters();
    }

    private void OnDungeonClearCountList(S_DUNGEON_CLEAR_COUNT_LIST m)
    {
        if (CurrentCharacter == null) return;
        if (m.Failed) return;
        if (m.PlayerId != Game.Me.PlayerId) return;
        foreach (var dg in m.DungeonClears)
        {
            CurrentCharacter.DungeonInfo.UpdateClears(dg.Key, dg.Value);
        }
    }

    private void OnAvailableEventMatchingList(S_AVAILABLE_EVENT_MATCHING_LIST m)
    {
        SetVanguard(m.WeeklyDone, m.WeeklyMax, m.DailyDone, m.VanguardCredits);
    }

    private void OnDungeonCoolTimeList(S_DUNGEON_COOL_TIME_LIST m)
    {
        SetDungeons(m.DungeonCooldowns);
    }

    private void OnReturnToLobby(S_RETURN_TO_LOBBY m)
    {
        UpdateBuffs();
    }

    private void OnLogin(S_LOGIN m)
    {
        SetLoggedIn(m.PlayerId);
        SetGuildBamTime(false);
    }

    private void OnGetUserList(S_GET_USER_LIST m)
    {
        try
        {
            UpdateBuffs();
        }
        catch (Exception e)
        {
            Log.F($"Failed to update buffs: {e.Message}");
        }
        Dispatcher.BeginInvoke(SaveCharacters);
    }

    private void OnPlayerStatUpdate(S_PLAYER_STAT_UPDATE m)
    {
        if (CurrentCharacter == null) return;
        CurrentCharacter.Coins = m.Coins;
        CurrentCharacter.MaxCoins = m.MaxCoins;
        CurrentCharacter.ItemLevel = m.Ilvl;
        CurrentCharacter.Level = m.Level;
    }

    private void OnItemList(S_ITEMLIST m)
    {
        if (m.Failed || m.Container == 14) return;
        lock (_lock)
        {
            _pendingTabs.Add(m.Items);
        }
        _tabFlushTimer.Stop();
        _tabFlushTimer.Start();
        //UpdateInventory(m.Items, m.Pocket, m.NumPockets);
        //UpdateInventory(m.Items);
    }

    private void OnNpcGuildList(S_NPCGUILD_LIST m)
    {
        if (!Game.IsMe(m.UserId)) return;
        foreach (var (guild, credits) in m.NpcGuildList)
        {
            switch (guild)
            {
                case (int)NpcGuild.Vanguard:
                    SetVanguardCredits(credits);
                    break;
                case (int)NpcGuild.Guardian:
                    SetGuardianCredits(credits);
                    break;
            }
        }
    }

    private void OnUpdateNpcGuild(S_UPDATE_NPCGUILD m)
    {
        switch (m.Guild)
        {
            case NpcGuild.Vanguard:
                SetVanguardCredits(m.Credits);
                break;
            case NpcGuild.Guardian:
                SetGuardianCredits(m.Credits);
                break;
        }
    }


    /* -- TODO EVENTS: TO BE REFACTORED ------------------------- */

    private readonly object _eventLock = new object();
    public ThreadSafeObservableCollection<EventGroup> EventGroups { get; }
    public ThreadSafeObservableCollection<TimeMarker> Markers { get; }
    public ThreadSafeObservableCollection<DailyEvent> SpecialEvents { get; }


    public void LoadEvents(DayOfWeek today, string region)
    {
        ClearEvents();
        LoadEventFile(today, region);
        if (Game.Logged) SetGuildBamTime(false);

    }
    public void ClearEvents()
    {
        lock (_eventLock)
        {
            EventGroups.Clear();
        }
        SpecialEvents.Clear();
    }

    private void LoadEventFile(DayOfWeek today, string region)
    {
        var yesterday = today - 1;
        if (region.StartsWith("EU")) region = "EU";
        var path = Path.Combine(App.ResourcesPath, $"config/events/events-{region}.xml");
        if (!File.Exists(path))
        {
            var root = new XElement("Events");
            var eg = new XElement("EventGroup", new XAttribute("name", "Example event group"));
            var ev = new XElement("Event",
                new XAttribute("name", "Example Event"),
                new XAttribute("days", "*"),
                new XAttribute("start", "12:00"),
                new XAttribute("end", "15:00"),
                new XAttribute("color", "ff5566"));
            var ev2 = new XElement("Event",
                new XAttribute("name", "Example event 2"),
                new XAttribute("days", "*"),
                new XAttribute("start", "16:00"),
                new XAttribute("duration", "3:00"),
                new XAttribute("color", "ff5566"));
            eg.Add(ev);
            eg.Add(ev2);
            root.Add(eg);
            if (!Directory.Exists(Path.Combine(App.ResourcesPath, "config/events")))
                Directory.CreateDirectory(Path.Combine(App.ResourcesPath, "config/events"));

            //if(!Utils.IsFileLocked(path, FileAccess.ReadWrite))
            root.Save(path);
        }

        try
        {
            var d = XDocument.Load(path);
            foreach (var egElement in d.Descendants().Where(x => x.Name == "EventGroup"))
            {
                var egName = egElement.Attribute("name")?.Value ?? "Event";
                var egRc = egElement.Attribute("remote") != null && bool.Parse(egElement.Attribute("remote")!.Value);
                var egStart = egElement.Attribute("start") != null
                    ? DateTime.Parse(egElement.Attribute("start")!.Value)
                    : DateTime.MinValue;
                var egEnd = egElement.Attribute("end") != null
                    ? DateTime.Parse(egElement.Attribute("end")!.Value).AddDays(1)
                    : DateTime.MaxValue;

                if (GameEventManager.Instance.CurrentServerTime < egStart ||
                    GameEventManager.Instance.CurrentServerTime > egEnd) continue;

                var eg = new EventGroup(egName, egStart, egEnd, egRc);
                foreach (var evElement in egElement.Descendants().Where(x => x.Name == "Event"))
                {
                    var isYesterday = false;
                    var isToday = false;

                    if (evElement.Attribute("days")!.Value != "*")
                    {
                        if (evElement.Attribute("days")!.Value.Contains(','))
                        {
                            var days = evElement.Attribute("days")!.Value.Split(',');
                            foreach (var dayString in days)
                            {
                                var day = (DayOfWeek)Enum.Parse(typeof(DayOfWeek), dayString);
                                if (day == today) isToday = true;
                                if (day == yesterday) isYesterday = true;
                            }
                        }
                        else
                        {
                            var eventDay = (DayOfWeek)Enum.Parse(typeof(DayOfWeek), evElement.Attribute("days")!.Value);
                            isToday = eventDay == today;
                            isYesterday = eventDay == yesterday;
                        }
                    }
                    else
                    {
                        isToday = true;
                        isYesterday = true;
                    }

                    if (!isToday && !isYesterday) continue;

                    var name = evElement.Attribute("name")!.Value;
                    var parsedStart = DateTime.Parse(evElement.Attribute("start")!.Value, CultureInfo.InvariantCulture);
                    var parsedDuration = TimeSpan.Zero;
                    var parsedEnd = DateTime.Now;
                    bool isDuration;
                    if (evElement.Attribute("duration") != null)
                    {
                        parsedDuration = TimeSpan.Parse(evElement.Attribute("duration")!.Value, CultureInfo.InvariantCulture);
                        isDuration = true;
                    }
                    else if (evElement.Attribute("end") != null)
                    {
                        parsedEnd = DateTime.Parse(evElement.Attribute("end")!.Value, CultureInfo.InvariantCulture);
                        isDuration = false;
                    }
                    else
                    {
                        parsedDuration = TimeSpan.Zero;
                        parsedEnd = parsedStart;
                        isDuration = true;
                    }

                    var color = "5599ff";

                    var start = parsedStart.Hour + parsedStart.Minute / 60D;
                    var end = isDuration ? parsedDuration.Hours + parsedDuration.Minutes / 60D : parsedEnd.Hour + parsedEnd.Minute / 60D;

                    if (evElement.Attribute("color") != null)
                    {
                        color = evElement.Attribute("color")!.Value;
                    }
                    if (isYesterday)
                    {
                        if (!EventUtils.EndsToday(start, end, isDuration))
                        {
                            var e1 = new DailyEvent(name, parsedStart.Hour, 24, 0, color, false);
                            end = start + end - 24;
                            var e2 = new DailyEvent(name, parsedStart.Hour, parsedStart.Minute, end, color, isDuration);
                            if (isToday) eg.AddEvent(e1);
                            eg.AddEvent(e2);
                        }
                        else if (isToday)
                        {
                            var ev = new DailyEvent(name, parsedStart.Hour, parsedStart.Minute, end, color, isDuration);
                            eg.AddEvent(ev);
                        }
                    }
                    else
                    {
                        var ev = new DailyEvent(name, parsedStart.Hour, parsedStart.Minute, end, color, isDuration);
                        eg.AddEvent(ev);
                    }
                }
                if (eg.Events.Count != 0) AddEventGroup(eg);
            }
            SpecialEvents.Add(new DailyEvent("Reset", GameEventManager.Instance.ResetHour, 0, 0, "ff0000"));

        }
        catch (Exception)
        {
            var res = TccMessageBox.Show("TCC", SR.CannotReadEventsFile(region), MessageBoxButton.OKCancel, MessageBoxImage.Warning);

            if (res == MessageBoxResult.Cancel) File.Delete(path);
            LoadEventFile(today, region);
        }
    }
    public void AddEventGroup(EventGroup eg)
    {
        lock (_eventLock)
        {
            var g = EventGroups.ToSyncList().FirstOrDefault(x => x.Name == eg.Name);
            if (g != null)
            {
                foreach (var ev in eg.Events)
                {
                    g.AddEvent(ev);
                }
            }
            else
            {
                EventGroups.Add(eg);
            }
        }
    }

    public void UpdateBuffs()
    {
        if (CurrentCharacter == null) return;
        Task.Run(() =>
        {
            CurrentCharacter.Buffs.Clear();
            foreach (var b in Game.Me.Buffs.ToSyncList())
            {
                CurrentCharacter?.Buffs.Add(new AbnormalityData { Id = b.Abnormality.Id, Duration = b.DurationLeft, Stacks = b.Stacks });
            }

            foreach (var b in Game.Me.Debuffs.ToSyncList())
            {
                CurrentCharacter?.Buffs.Add(new AbnormalityData { Id = b.Abnormality.Id, Duration = b.DurationLeft, Stacks = b.Stacks });
            }
        });
    }

    public void ResetDailyData()
    {
        foreach (var ch in Game.Account.Characters.ToList()) ch.ResetDailyData();
        ChatManager.Instance.AddTccMessage("Daily data has been reset.");
    }

    public void ResetWeeklyDungeons()
    {
        foreach (var ch in Game.Account.Characters.ToSyncList()) ch.DungeonInfo.ResetAll(ResetMode.Weekly);
        ChatManager.Instance.AddTccMessage("Weekly dungeon entries have been reset.");
    }

    public void ResetVanguardWeekly()
    {
        foreach (var ch in Game.Account.Characters.ToSyncList()) ch.VanguardInfo.WeekliesDone = 0;
        ChatManager.Instance.AddTccMessage("Weekly vanguard data has been reset.");
    }

    public void RefreshDungeons()
    {
        _columns?.Clear();
        Task.Factory.StartNew(() =>
        {
            foreach (var dungeon in Game.DB!.DungeonDatabase.Dungeons.Values.Where(d => d.HasDef))
            {
                App.BaseDispatcher.InvokeAsync(() =>
                {
                    var dvc = new DungeonColumnViewModel(dungeon);
                    foreach (var charVm in CharacterViewModels.ToArray())
                    {
                        dvc.DungeonsList.Add(
                            new DungeonCooldownViewModel(
                                charVm.Character.DungeonInfo.DungeonList.FirstOrDefault(x => x.Dungeon.Id == dungeon.Id) ?? throw new NullReferenceException("Dungeon not found!"),
                                charVm.Character
                            ));
                    }

                    _columns?.Add(dvc);
                }, DispatcherPriority.Background);
            }
        });
    }

    public void SetGuildBamTime(bool force)
    {
        lock (_eventLock)
        {
            foreach (var eg in EventGroups.ToSyncList().Where(x => x.RemoteCheck))
            {
                foreach (var ev in eg.Events.ToSyncList())
                {
                    ev.UpdateFromServer(force);
                }
            }
        }
    }
}
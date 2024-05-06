﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Nostrum;
using Nostrum.WPF;
using Nostrum.WPF.ThreadSafe;
using TCC.Data;
using TCC.Data.Databases;
using TCC.Interop;
using TCC.Interop.Proxy;
using TCC.Settings.WindowSettings;
using TCC.UI;
using TCC.UI.Windows;
using TCC.Update;
using TCC.Utils;
using TeraPacketParser;
using CaptureMode = TeraPacketParser.CaptureMode;
using MessageBoxImage = TCC.Data.MessageBoxImage;

namespace TCC.ViewModels;

public class SettingsWindowViewModel : ThreadSafeObservableObject
{
    public static event Action? ChatShowChannelChanged;
    public static event Action? ChatShowTimestampChanged;
    public static event Action? AbnormalityShapeChanged;
    public static event Action? SkillShapeChanged;
    public static event Action? FontSizeChanged;
    public static event Action? TranslationModeChanged;
    public static event Action? IntegratedGpuSleepWorkaroundChanged;

    public bool Beta => App.Beta;
    public bool ToolboxMode => App.ToolboxMode;

    public CooldownWindowSettings CooldownWindowSettings => App.Settings.CooldownWindowSettings;
    public ClassWindowSettings ClassWindowSettings => App.Settings.ClassWindowSettings;
    public GroupWindowSettings GroupWindowSettings => App.Settings.GroupWindowSettings;
    public BuffWindowSettings BuffWindowSettings => App.Settings.BuffWindowSettings;
    public CharacterWindowSettings CharacterWindowSettings => App.Settings.CharacterWindowSettings;
    public NpcWindowSettings NpcWindowSettings => App.Settings.NpcWindowSettings;
    public FlightWindowSettings FlightWindowSettings => App.Settings.FlightGaugeWindowSettings;
    public FloatingButtonWindowSettings FloatingButtonSettings => App.Settings.FloatingButtonSettings;
    public CivilUnrestWindowSettings CuWindowSettings => App.Settings.CivilUnrestWindowSettings;
    public LfgWindowSettings LfgWindowSettings => App.Settings.LfgWindowSettings;
    public NotificationAreaSettings NotificationAreaSettings => App.Settings.NotificationAreaSettings;
    public LootDistributionWindowSettings LootDistributionWindowSettings => App.Settings.LootDistributionWindowSettings;
    public WindowSettingsBase PerfMonitorSettings => App.Settings.PerfMonitorSettings;

    public ICommand BrowseUrlCommand { get; }
    public ICommand RegisterWebhookCommand { get; }
    public ICommand OpenWindowCommand { get; }
    public ICommand DownloadBetaCommand { get; }
    public ICommand ResetChatPositionsCommand { get; }
    public ICommand MakePositionsGlobalCommand { get; }
    public ICommand ResetWindowPositionsCommand { get; }
    public ICommand OpenResourcesFolderCommand { get; }
    public ICommand OpenWelcomeWindowCommand { get; }
    public ICommand OpenConfigureLfgWindowCommand { get; }
    public ICommand ClearChatCommand { get; }

    public bool EthicalMode
    {
        get => App.Settings.EthicalMode;
        set
        {
            if (App.Settings.EthicalMode == value) return;
            App.Settings.EthicalMode = value;
            InvokePropertyChanged();
        }
    }
    public bool UseHotkeys
    {
        get => App.Settings.UseHotkeys;
        set
        {
            if (App.Settings.UseHotkeys == value) return;
            App.Settings.UseHotkeys = value;
            if (value) KeyboardHook.Instance.Enable();
            else KeyboardHook.Instance.Disable();
            InvokePropertyChanged();
        }
    }

    public HotKey SettingsHotkey
    {
        get => App.Settings.SettingsHotkey;
        set
        {
            if (App.Settings.SettingsHotkey.Equals(value)) return;
            KeyboardHook.Instance.ChangeHotkey(App.Settings.SettingsHotkey, value);
            App.Settings.SettingsHotkey = value;
            InvokePropertyChanged();
        }
    }
    public HotKey RollHotkey
    {
        get => LootDistributionWindowSettings.RollHotKey;
        set
        {
            if (LootDistributionWindowSettings.RollHotKey.Equals(value)) return;
            LootDistributionWindowSettings.RollHotKey = value;
            InvokePropertyChanged();
        }
    }
    public HotKey PassHotkey
    {
        get => LootDistributionWindowSettings.PassHotKey;
        set
        {
            if (LootDistributionWindowSettings.PassHotKey.Equals(value)) return;
            LootDistributionWindowSettings.PassHotKey = value;
            InvokePropertyChanged();
        }
    }
    public HotKey SkillSettingsHotkey
    {
        get => App.Settings.SkillSettingsHotkey;
        set
        {
            if (App.Settings.SkillSettingsHotkey.Equals(value)) return;
            KeyboardHook.Instance.ChangeHotkey(App.Settings.SkillSettingsHotkey, value);
            App.Settings.SkillSettingsHotkey = value;
            InvokePropertyChanged();
        }
    }
    public HotKey AbnormalSettingsHotkey
    {
        get => App.Settings.AbnormalSettingsHotkey;
        set
        {
            if (App.Settings.AbnormalSettingsHotkey.Equals(value)) return;
            KeyboardHook.Instance.ChangeHotkey(App.Settings.AbnormalSettingsHotkey, value);
            App.Settings.AbnormalSettingsHotkey = value;
            InvokePropertyChanged();
        }
    }
    public HotKey ForceClickableChatHotkey
    {
        get => App.Settings.ForceClickableChatHotkey;
        set
        {
            if (App.Settings.ForceClickableChatHotkey.Equals(value)) return;
            KeyboardHook.Instance.ChangeHotkey(App.Settings.ForceClickableChatHotkey, value);
            App.Settings.ForceClickableChatHotkey = value;
            InvokePropertyChanged();
        }
    }
    public HotKey DashboardHotkey
    {
        get => App.Settings.DashboardHotkey;
        set
        {
            if (App.Settings.DashboardHotkey.Equals(value)) return;
            KeyboardHook.Instance.ChangeHotkey(App.Settings.DashboardHotkey, value);
            App.Settings.DashboardHotkey = value;
            InvokePropertyChanged();
        }
    }
    public HotKey LfgHotkey
    {
        get => App.Settings.LfgHotkey;
        set
        {
            if (App.Settings.LfgHotkey.Equals(value)) return;
            KeyboardHook.Instance.ChangeHotkey(App.Settings.LfgHotkey, value);
            App.Settings.LfgHotkey = value;
            InvokePropertyChanged();
        }
    }
    public HotKey ReturnToLobbyHotkey
    {
        get => App.Settings.ReturnToLobbyHotkey;
        set
        {
            if (App.Settings.ReturnToLobbyHotkey.Equals(value)) return;
            KeyboardHook.Instance.ChangeHotkey(App.Settings.ReturnToLobbyHotkey, value);
            App.Settings.ReturnToLobbyHotkey = value;
            InvokePropertyChanged();
        }
    }
    public HotKey ToggleBoundariesHotkey
    {
        get => App.Settings.ToggleBoundariesHotkey;
        set
        {
            if (App.Settings.ToggleBoundariesHotkey.Equals(value)) return;
            KeyboardHook.Instance.ChangeHotkey(App.Settings.ToggleBoundariesHotkey, value);
            App.Settings.ToggleBoundariesHotkey = value;
            InvokePropertyChanged();
        }
    }
    public HotKey ToggleHideAllHotkey
    {
        get => App.Settings.ToggleHideAllHotkey;
        set
        {
            if (App.Settings.ToggleHideAllHotkey.Equals(value)) return;
            KeyboardHook.Instance.ChangeHotkey(App.Settings.ToggleHideAllHotkey, value);
            App.Settings.ToggleHideAllHotkey = value;
            InvokePropertyChanged();
        }
    }

    private int _khCount;
    private bool _kh;
    public bool KylosHelper
    {
        get => _kh;
        // ReSharper disable once ValueParameterNotUsed
        set
        {
            _kh = true;
            switch (_khCount)
            {
                case 0:
                    Log.N("Exploit alert", "Are you sure you want to enable this?", NotificationType.Warning);
                    break;
                case 1:
                    Log.N(":thinking:", "You shouldn't use this °L° Are you really sure?", NotificationType.Warning, 3000);
                    break;
                case 2:
                    Log.N("omegalul", "There's actually no Kylos helper lol. Just memeing. Have fun o/", NotificationType.Warning, 6000);
                    Utils.Utilities.OpenUrl("https://www.youtube.com/watch?v=dQw4w9WgXcQ");
                    break;
            }
            InvokePropertyChanged();

            _khCount++;
            if (_khCount > 2) _khCount = 0;
            _kh = false;
            InvokePropertyChanged();
        }
    }
    public bool DisableLfgChatMessages
    {
        get => App.Settings.DisableLfgChatMessages;
        set
        {
            if (App.Settings.DisableLfgChatMessages == value) return;
            App.Settings.DisableLfgChatMessages = value;
            InvokePropertyChanged();
        }
    }
    public bool BetaNotification
    {
        get => App.Settings.BetaNotification;
        set
        {
            if (App.Settings.BetaNotification == value) return;
            App.Settings.BetaNotification = value;
            InvokePropertyChanged();
        }
    }
    public bool CheckOpcodesHash
    {
        get => App.Settings.CheckOpcodesHash;
        set
        {
            if (App.Settings.CheckOpcodesHash == value) return;
            App.Settings.CheckOpcodesHash = value;
            InvokePropertyChanged();
        }
    }
    public bool CheckGuildBamWithoutOpcode  // by HQ 20190324
    {
        get => App.Settings.CheckGuildBamWithoutOpcode;
        set
        {
            if (App.Settings.CheckGuildBamWithoutOpcode == value) return;
            App.Settings.CheckGuildBamWithoutOpcode = value;
            InvokePropertyChanged();
        }
    }
    public bool IntegratedGpuSleepWorkaround
    {
        get => App.Settings.IntegratedGpuSleepWorkaround;
        set
        {
            if (App.Settings.IntegratedGpuSleepWorkaround == value) return;
            App.Settings.IntegratedGpuSleepWorkaround = value;
            IntegratedGpuSleepWorkaroundChanged?.Invoke();
            InvokePropertyChanged();
        }
    }


    public ControlShape AbnormalityShape
    {
        get => App.Settings.AbnormalityShape;
        set
        {
            if (App.Settings.AbnormalityShape == value) return;
            App.Settings.AbnormalityShape = value;
            AbnormalityShapeChanged?.Invoke();
            InvokePropertyChanged();
        }
    }
    public ControlShape SkillShape
    {
        get => App.Settings.SkillShape;
        set
        {
            if (App.Settings.SkillShape == value) return;
            App.Settings.SkillShape = value;
            SkillShapeChanged?.Invoke();
            InvokePropertyChanged();
        }
    }


    //public bool ChatFadeOut
    //{
    //    get => Settings.Settings.ChatFadeOut;
    //    set
    //    {
    //        if (Settings.Settings.ChatFadeOut == value) return;
    //        Settings.Settings.ChatFadeOut = value;
    //        if (value) ChatWindowManager.Instance.ForceHideTimerRefresh();
    //        NPC(nameof(ChatFadeOut));
    //    }
    //}
    public LanguageOverride RegionOverride
    {
        get => App.Settings.LanguageOverride;
        set
        {
            if (App.Settings.LanguageOverride == value) return;
            App.Settings.LanguageOverride = value;
            if (value == LanguageOverride.None) App.Settings.LastLanguage = "EU-EN";
            InvokePropertyChanged();
        }
    }
    public int MaxMessages
    {
        get => App.Settings.MaxMessages;
        set
        {
            if (App.Settings.MaxMessages == value) return;
            App.Settings.MaxMessages = value == 0 ? int.MaxValue : value;
            InvokePropertyChanged();
        }
    }
    public int ChatScrollAmount
    {
        get => App.Settings.ChatScrollAmount;
        set
        {
            if (App.Settings.ChatScrollAmount == value) return;
            App.Settings.ChatScrollAmount = value;
            InvokePropertyChanged();
        }
    }
    public int SpamThreshold
    {
        get => App.Settings.SpamThreshold;
        set
        {
            if (App.Settings.SpamThreshold == value) return;
            App.Settings.SpamThreshold = value;
            InvokePropertyChanged();
        }
    }
    public bool ShowTimestamp
    {
        get => App.Settings.ShowTimestamp;
        set
        {
            if (App.Settings.ShowTimestamp == value) return;
            App.Settings.ShowTimestamp = value;
            InvokePropertyChanged();
            ChatShowTimestampChanged?.Invoke();
        }

    }
    public bool ChatTimestampSeconds
    {
        get => App.Settings.ChatTimestampSeconds;
        set
        {
            if (App.Settings.ChatTimestampSeconds == value) return;
            App.Settings.ChatTimestampSeconds = value;
            InvokePropertyChanged();
        }

    }
    public bool ShowChannel
    {
        get => App.Settings.ShowChannel;
        set
        {
            if (App.Settings.ShowChannel == value) return;
            App.Settings.ShowChannel = value;
            ChatShowChannelChanged?.Invoke();
            InvokePropertyChanged();
        }

    }


    public bool FpsAtGuardian
    {
        get => App.Settings.FpsAtGuardian;
        set
        {
            if (App.Settings.FpsAtGuardian == value) return;
            App.Settings.FpsAtGuardian = value;
            InvokePropertyChanged();
        }
    }
    public bool EnableProxy
    {
        get => App.Settings.EnableProxy;
        set
        {
            if (App.Settings.EnableProxy == value) return;
            App.Settings.EnableProxy = value;
            InvokePropertyChanged();
            InvokePropertyChanged(nameof(ClickThruModes));
            StubInterface.Instance.StubClient.UpdateSetting("EnableProxy", App.Settings.ChatEnabled);

        }
    }
    public bool HideHandles
    {
        get => App.Settings.HideHandles;
        set
        {
            if (App.Settings.HideHandles == value) return;
            App.Settings.HideHandles = value;
            InvokePropertyChanged();
        }
    }
    public CooldownDecimalMode CooldownsDecimalMode
    {
        get => App.Settings.CooldownsDecimalMode;
        set
        {
            if (App.Settings.CooldownsDecimalMode == value) return;
            App.Settings.CooldownsDecimalMode = value;
            InvokePropertyChanged();
        }
    }
    public bool AnimateChatMessages
    {
        get => App.Settings.AnimateChatMessages;
        set
        {
            if (App.Settings.AnimateChatMessages == value) return;
            App.Settings.AnimateChatMessages = value;
            InvokePropertyChanged();
        }
    }
    public bool BackgroundNotifications
    {
        get => App.Settings.BackgroundNotifications;
        set
        {
            if (App.Settings.BackgroundNotifications == value) return;
            App.Settings.BackgroundNotifications = value;
            InvokePropertyChanged();
        }
    }

    public bool WebhookEnabledGuildBam
    {
        get => App.Settings.WebhookEnabledGuildBam;
        set
        {
            if (App.Settings.WebhookEnabledGuildBam == value) return;
            App.Settings.WebhookEnabledGuildBam = value;
            InvokePropertyChanged();
        }
    }
    public bool WebhookEnabledFieldBoss
    {
        get => App.Settings.WebhookEnabledFieldBoss;
        set
        {
            if (App.Settings.WebhookEnabledFieldBoss == value) return;
            App.Settings.WebhookEnabledFieldBoss = value;
            InvokePropertyChanged();
        }
    }
    public bool WebhookEnabledMentions
    {
        get => App.Settings.WebhookEnabledMentions;
        set
        {
            if (App.Settings.WebhookEnabledMentions == value) return;
            App.Settings.WebhookEnabledMentions = value;
            InvokePropertyChanged();
        }
    }
    public string WebhookUrlGuildBam
    {
        get => App.Settings.WebhookUrlGuildBam;
        set
        {
            if (value == App.Settings.WebhookUrlGuildBam) return;
            App.Settings.WebhookUrlGuildBam = value;
            InvokePropertyChanged();
        }
    }
    public string WebhookUrlFieldBoss
    {
        get => App.Settings.WebhookUrlFieldBoss;
        set
        {
            if (value == App.Settings.WebhookUrlFieldBoss) return;
            App.Settings.WebhookUrlFieldBoss = value;
            InvokePropertyChanged();
        }
    }
    public string WebhookUrlMentions
    {
        get => App.Settings.WebhookUrlMentions;
        set
        {
            if (value == App.Settings.WebhookUrlMentions) return;
            App.Settings.WebhookUrlMentions = value;
            InvokePropertyChanged();
        }
    }
    public string WebhookMessageGuildBam
    {
        get => App.Settings.WebhookMessageGuildBam;
        set
        {
            if (value == App.Settings.WebhookMessageGuildBam) return;
            App.Settings.WebhookMessageGuildBam = value;
            InvokePropertyChanged();
        }
    }
    public string WebhookMessageFieldBossSpawn
    {
        get => App.Settings.WebhookMessageFieldBossSpawn;
        set
        {
            if (value == App.Settings.WebhookMessageFieldBossSpawn) return;
            App.Settings.WebhookMessageFieldBossSpawn = value;
            InvokePropertyChanged();
        }
    }
    public string WebhookMessageFieldBossDie
    {
        get => App.Settings.WebhookMessageFieldBossDie;
        set
        {
            if (value == App.Settings.WebhookMessageFieldBossDie) return;
            App.Settings.WebhookMessageFieldBossDie = value;
            InvokePropertyChanged();
        }
    }

    public string TwitchUsername
    {
        get => App.Settings.TwitchName;
        set
        {
            if (value == App.Settings.TwitchName) return;
            App.Settings.TwitchName = value;
            InvokePropertyChanged();
        }
    }
    public string TwitchToken
    {
        get => App.Settings.TwitchToken;
        set
        {
            if (value == App.Settings.TwitchToken) return;
            App.Settings.TwitchToken = value;
            InvokePropertyChanged();
        }
    }
    public string TwitchChannelName
    {
        get => App.Settings.TwitchChannelName;
        set
        {
            if (value == App.Settings.TwitchChannelName) return;
            App.Settings.TwitchChannelName = value;
            InvokePropertyChanged();
        }
    }

    public CaptureMode CaptureMode
    {
        get => App.Settings.CaptureMode;
        set
        {
            if (App.Settings.CaptureMode == value) return;
            var res = TccMessageBox.Show("TCC", SR.RestartToApplySetting, MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (res == MessageBoxResult.Cancel) return;
            App.Settings.CaptureMode = value;
            InvokePropertyChanged();
            if (res == MessageBoxResult.OK) App.Restart();
        }
    }
    public MentionMode MentionMode
    {
        get => App.Settings.MentionMode;
        set
        {
            if (App.Settings.MentionMode == value) return;
            App.Settings.MentionMode = value;
            InvokePropertyChanged();
        }
    }
    public int FontSize
    {
        get => App.Settings.FontSize;
        set
        {
            if (App.Settings.FontSize == value) return;
            var val = value;
            if (val < 10) val = 10;
            App.Settings.FontSize = val;
            FontSizeChanged?.Invoke();
            InvokePropertyChanged();
        }
    }

    public bool ChatWindowEnabled
    {
        get => App.Settings.ChatEnabled;
        set
        {
            if (App.Settings.ChatEnabled == value) return;
            App.Settings.ChatEnabled = value;
            ChatManager.Instance.NotifyEnabledChanged(value);
            StubInterface.Instance.StubClient.UpdateSetting("TccChatEnabled", value);
            InvokePropertyChanged();
        }
    }
    public bool ForceSoftwareRendering
    {
        get => App.Settings.ForceSoftwareRendering;
        set
        {
            if (App.Settings.ForceSoftwareRendering == value) return;
            App.Settings.ForceSoftwareRendering = value;
            InvokePropertyChanged();
            RenderOptions.ProcessRenderMode = value ? RenderMode.SoftwareOnly : RenderMode.Default;
        }
    }
    public bool HighPriority
    {
        get => App.Settings.HighPriority;
        set
        {
            if (App.Settings.HighPriority == value) return;
            App.Settings.HighPriority = value;
            InvokePropertyChanged();
            Process.GetCurrentProcess().PriorityClass = value ? ProcessPriorityClass.High : ProcessPriorityClass.Normal;
        }
    }

    //public bool ShowConsole
    //{
    //    get => App.Settings.ShowConsole;
    //    set
    //    {
    //        if (App.Settings.ShowConsole == value) return;
    //        App.Settings.ShowConsole = value;
    //        N();

    //        if (value)
    //        {
    //            TccUtils.CreateConsole();
    //            Log.CW("Console opened");
    //        }
    //        else Kernel32.FreeConsole();
    //    }
    //}
    public TranslationMode TranslationMode
    {
        get => App.Settings.TranslationMode;
        set
        {
            if (App.Settings.TranslationMode == value) return;
            App.Settings.TranslationMode = value;
            TranslationModeChanged?.Invoke();
            InvokePropertyChanged();
        }
    }
    public IEnumerable<ClickThruMode> ClickThruModes
    {
        get
        {
            var ret = EnumUtils.ListFromEnum<ClickThruMode>();
            if (!App.Settings.EnableProxy
#if TERA_X64
                    || PacketAnalyzer.Factory?.ReleaseVersion/100 >= 97
#endif
               ) ret.Remove(ClickThruMode.GameDriven);
            return ret;
        }
    }

    //TODO: https://stackoverflow.com/a/17405771 (in Nostrum)
    public IEnumerable<CooldownBarMode> CooldownBarModes => EnumUtils.ListFromEnum<CooldownBarMode>();
    public IEnumerable<FlowDirection> FlowDirections => EnumUtils.ListFromEnum<FlowDirection>();
    public IEnumerable<EnrageLabelMode> EnrageLabelModes => EnumUtils.ListFromEnum<EnrageLabelMode>();
    public IEnumerable<WarriorEdgeMode> WarriorEdgeModes => EnumUtils.ListFromEnum<WarriorEdgeMode>();
    public IEnumerable<ControlShape> ControlShapes => EnumUtils.ListFromEnum<ControlShape>();
    public IEnumerable<GroupWindowLayout> GroupWindowLayouts => EnumUtils.ListFromEnum<GroupWindowLayout>();
    public IEnumerable<GroupHpLabelMode> GroupHpLabelModes => EnumUtils.ListFromEnum<GroupHpLabelMode>();
    public IEnumerable<CaptureMode> CaptureModes => EnumUtils.ListFromEnum<CaptureMode>();
    public IEnumerable<MentionMode> MentionModes => EnumUtils.ListFromEnum<MentionMode>();
    public IEnumerable<LanguageOverride> LanguageOverrides => EnumUtils.ListFromEnum<LanguageOverride>();
    public IEnumerable<CooldownDecimalMode> CooldownDecimalModes => EnumUtils.ListFromEnum<CooldownDecimalMode>();
    public IEnumerable<TranslationMode> TranslationModes => EnumUtils.ListFromEnum<TranslationMode>();


    private ThreadSafeObservableCollection<BlacklistedMonsterVM>? _blacklistedMonsters;
    private bool _showDebugSettings;

    public ThreadSafeObservableCollection<BlacklistedMonsterVM> BlacklistedMonsters
    {
        get
        {
            _blacklistedMonsters ??= new ThreadSafeObservableCollection<BlacklistedMonsterVM>(_dispatcher);
            var bl = Game.DB?.MonsterDatabase.GetBlacklistedMonsters() ?? [];
            foreach (var m in bl.Where(m => _blacklistedMonsters.All(x => x.Monster != m)))
            {
                _blacklistedMonsters.Add(new BlacklistedMonsterVM(m));
            }

            foreach (var vm in _blacklistedMonsters.ToSyncList().Where(vm => !bl.Contains(vm.Monster)))
            {
                _blacklistedMonsters.Remove(vm);
            }

            return _blacklistedMonsters;
        }
    }

    public bool EnablePlayerMenu
    {
        get => App.Settings.EnablePlayerMenu;
        set
        {
            if (App.Settings.EnablePlayerMenu == value) return;
            App.Settings.EnablePlayerMenu = value;
            InvokePropertyChanged();
            StubInterface.Instance.StubClient.UpdateSetting("EnablePlayerMenu", App.Settings.EnablePlayerMenu);
        }
    }

    public bool ShowIngameChat
    {
        get => App.Settings.ShowIngameChat;
        set
        {
            if (App.Settings.ShowIngameChat == value) return;
            App.Settings.ShowIngameChat = value;
            InvokePropertyChanged();
            StubInterface.Instance.StubClient.UpdateSetting("ShowIngameChat", App.Settings.ShowIngameChat);
        }
    }

    public bool ShowDebugSettings
    {
        get => _showDebugSettings;
        set => RaiseAndSetIfChanged(value, ref _showDebugSettings);
    }


    public SettingsWindowViewModel()
    {
        KeyboardHook.Instance.RegisterCallback(App.Settings.SettingsHotkey, OnShowSettingsWindowHotkeyPressed);

        BrowseUrlCommand = new RelayCommand(url =>
        {
            var strUrl = url?.ToString();
            if (strUrl == null) return;
            Utils.Utilities.OpenUrl(strUrl);
        });
        RegisterWebhookCommand = new RelayCommand(webhook => Firebase.RegisterWebhook(webhook?.ToString(), true, Game.CurrentAccountNameHash),
            _ => Game.Logged);
        OpenWindowCommand = new RelayCommand(winType =>
        {
            if (winType == null)
            {
                //Log.CW("Failed to open window with null type");
                return;
            }
            var t = (Type)winType;
            if (TccWindow.Exists(t)) return;
            var win = Activator.CreateInstance(t, null) as TccWindow;
            win?.ShowWindow();
        });
        OpenWelcomeWindowCommand = new RelayCommand(_ =>
        {
            new WelcomeWindow().Show();
        });
        DownloadBetaCommand = new RelayCommand(async _ =>
        {
            if (TccMessageBox.Show(SR.BetaUnstableWarning, MessageBoxType.ConfirmationWithYesNo) == MessageBoxResult.Yes)
            {
                await Task.Factory.StartNew(UpdateManager.ForceUpdateToBeta);
            }
        });
        ResetChatPositionsCommand = new RelayCommand(_ =>
        {
            foreach (var cw in ChatManager.Instance.ChatWindows)
            {
                cw.ResetToCenter();
            }
        });
        MakePositionsGlobalCommand = new RelayCommand(_ => WindowManager.MakeGlobal());
        ResetWindowPositionsCommand = new RelayCommand(_ => WindowManager.ResetToCenter());
        OpenResourcesFolderCommand = new RelayCommand(_ => Process.Start(new ProcessStartInfo(Path.Combine(App.ResourcesPath, "config")) { UseShellExecute = true }));
        ClearChatCommand = new RelayCommand(_ => ChatManager.Instance.ClearMessages());
        OpenConfigureLfgWindowCommand = new RelayCommand(_ =>
        {
            new LfgFilterConfigWindow(WindowManager.ViewModels.LfgVM)
            {
                Owner = WindowManager.SettingsWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.ShowDialog();
        });
        MonsterDatabase.BlacklistChangedEvent += MonsterDatabase_BlacklistChangedEvent;
        MessageFactory.ReleaseVersionChanged += OnReleaseVersionChanged;
    }

    private void OnReleaseVersionChanged(int obj)
    {
        InvokePropertyChanged(nameof(ClickThruModes));
    }

    private void MonsterDatabase_BlacklistChangedEvent(uint arg1, uint arg2, bool arg3)
    {
        InvokePropertyChanged(nameof(BlacklistedMonsters));
    }

    private void OnShowSettingsWindowHotkeyPressed()
    {
        if (WindowManager.SettingsWindow.IsVisible) WindowManager.SettingsWindow.HideWindow();
        else WindowManager.SettingsWindow.ShowWindow();
    }
    public static void PrintEventsData()
    {
        //Log.CW($"ChatShowChannelChanged: {ChatShowChannelChanged?.GetInvocationList().Length}");
        //Log.CW($"ChatShowTimestampChanged: {ChatShowTimestampChanged?.GetInvocationList().Length}");
        //Log.CW($"FontSizeChanged: {FontSizeChanged?.GetInvocationList().Length}");
    }

}

public class BlacklistedMonsterVM : ThreadSafeObservableObject
{
    public readonly Monster Monster;
    public string Name => Monster.Name;
    public bool IsBoss => Monster.IsBoss;
    public bool IsHidden
    {
        get => Monster.IsHidden;
        set
        {
            if (Monster.IsHidden == value) return;
            Monster.IsHidden = value;
            if (!value) Game.DB?.MonsterDatabase.Blacklist(Monster, false);
            InvokePropertyChanged();
        }
    }

    public BlacklistedMonsterVM(Monster m)
    {
        Monster = m;
    }
}
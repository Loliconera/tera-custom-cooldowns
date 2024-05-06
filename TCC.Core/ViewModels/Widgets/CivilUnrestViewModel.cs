using System;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Nostrum.WPF.Factories;
using Nostrum.WPF.ThreadSafe;
using TCC.Settings.WindowSettings;
using TCC.UI;
using TCC.Utils;
using TeraDataLite;
using TeraPacketParser.Analysis;
using TeraPacketParser.Messages;

namespace TCC.ViewModels.Widgets;

public class CivilUnrestGuild : ThreadSafeObservableObject
{
    private float _towerHp;
    private uint _towersDestroyed;
    private string _name;

    public CivilUnrestGuild(uint id, string name, float towerHp, uint towersDestroyed)
    {
        _towerHp = towerHp;
        _towersDestroyed = towersDestroyed;
        _name = name;
        Id = id;
    }

    public string Name
    {
        get => _name;
        set => RaiseAndSetIfChanged(value, ref _name);
    }

    public uint Id { get; set; }
    public float TowerHp
    {
        get => _towerHp;
        set => RaiseAndSetIfChanged(value, ref _towerHp);
    }
    public uint TowersDestroyed
    {
        get => _towersDestroyed;
        set => RaiseAndSetIfChanged(value, ref _towersDestroyed);
    }
}

[TccModule(false)]
public class CivilUnrestViewModel : TccWindowViewModel
{
    public bool CivilUnrest => Game.CivilUnrestZone;
    private readonly ThreadSafeObservableCollection<CivilUnrestGuild> _guilds;

    public ICollectionViewLiveShaping Guilds //TODO: fix getter
    {
        get
        { 
            var ret = CollectionViewFactory.CreateLiveCollectionView(_guilds,
                sortFilters:
                [
                    new SortDescription(nameof(CivilUnrestGuild.TowerHp), ListSortDirection.Descending),
                    new SortDescription(nameof(CivilUnrestGuild.TowersDestroyed), ListSortDirection.Descending)
                ]) ?? throw new Exception("Failed to create LiveCollectionView");
            return ret;
        }
    }
    public void CopyToClipboard()
    {
        var sb = new StringBuilder("Civil Unrest temporary rank (top 5):\\");
        var limit = 5;
        limit = _guilds.Count > limit ? limit : _guilds.Count;
        for (var i = 0; i < limit; i++)
        {
            var item =
                WindowManager.CivilUnrestWindow.GuildList.Items[i] as CivilUnrestGuild;
            sb.Append(item?.Name);
            sb.Append(" | ");
            sb.Append($"HP: {item?.TowerHp:##0%}");
            sb.Append(" | ");
            sb.Append($"Towers: {item?.TowersDestroyed}");
            sb.Append("\\");
        }
        try
        {
            Clipboard.SetText(sb.ToString());
        }
        catch
        {
            Log.N("Boss window", "Failed to copy boss HP to clipboard.", NotificationType.Error);
            ChatManager.Instance.AddTccMessage("Failed to copy boss HP.");
        }
    }

    public CivilUnrestViewModel(WindowSettingsBase settings) : base(settings)
    {
        _guilds = new ThreadSafeObservableCollection<CivilUnrestGuild>();
    }

    protected override void InstallHooks()
    {
        Game.Teleported += NotifyTeleported;
        PacketAnalyzer.Processor.Hook<S_REQUEST_CITY_WAR_MAP_INFO_DETAIL>(OnRequestCityWarMapInfoDetail);
        PacketAnalyzer.Processor.Hook<S_REQUEST_CITY_WAR_MAP_INFO>(OnRequestCityWarMapInfo);
        PacketAnalyzer.Processor.Hook<S_DESTROY_GUILD_TOWER>(OnDestroyGuildTower);
    }

    protected override void RemoveHooks()
    {
        Game.Teleported -= NotifyTeleported;
        PacketAnalyzer.Processor.Unhook<S_REQUEST_CITY_WAR_MAP_INFO_DETAIL>(OnRequestCityWarMapInfoDetail);
        PacketAnalyzer.Processor.Unhook<S_REQUEST_CITY_WAR_MAP_INFO>(OnRequestCityWarMapInfo);
        PacketAnalyzer.Processor.Unhook<S_DESTROY_GUILD_TOWER>(OnDestroyGuildTower);
    }

    private void OnDestroyGuildTower(S_DESTROY_GUILD_TOWER m)
    {
        Task.Run(() =>
        {
            try
            {
                AddDestroyedGuildTower(m.SourceGuildId);
            }
            catch
            {
                // ignored
            }
        });
    }

    private void OnRequestCityWarMapInfo(S_REQUEST_CITY_WAR_MAP_INFO m)
    {
        Task.Run(() =>
        {
            try
            {
                foreach (var guild in m.Guilds)
                    WindowManager.ViewModels.CivilUnrestVM.AddGuild(guild);
            }
            catch
            {
                // ignored
            }
        });
    }

    private void OnRequestCityWarMapInfoDetail(S_REQUEST_CITY_WAR_MAP_INFO_DETAIL m)
    {
        try
        {
            foreach (var (id, name) in m.GuildDetails.ToList())
                WindowManager.ViewModels.CivilUnrestVM.SetGuildName(id, name);
        }
        catch
        {
            // ignored
        }
    }

    public void AddGuild(CityWarGuildData guildInfo)
    {
        var g = _guilds.FirstOrDefault(x => x.Id == guildInfo.Id);
        if (g != null)
        {
            g.TowerHp = guildInfo.TowerHp;
            if (g.Name != "") return;
            if (!guildInfo.Self) return;
            if (WindowManager.ViewModels.DashboardVM.CurrentCharacter != null)
                g.Name = WindowManager.ViewModels.DashboardVM.CurrentCharacter.GuildName;
            //TODO: add kills and deaths?
        }
        else
        {
            var name = "";
            if (guildInfo.Self && WindowManager.ViewModels.DashboardVM.CurrentCharacter != null) 
                name = WindowManager.ViewModels.DashboardVM.CurrentCharacter.GuildName;
            _guilds.Add(new CivilUnrestGuild(guildInfo.Id, name, guildInfo.TowerHp, 0));
        }
    }
    public void SetGuildName(uint id, string name)
    {
        var g = _guilds.FirstOrDefault(x => x.Id == id);
        if (g != null)
        {
            g.Name = name;
        }
    }
    public void AddDestroyedGuildTower(uint id)
    {
        var g = _guilds.FirstOrDefault(x => x.Id == id);
        if (g != null)
        {
            g.TowersDestroyed++;
        }
    }

    private void NotifyTeleported()
    {
        InvokePropertyChanged(nameof(CivilUnrest));
    }
}
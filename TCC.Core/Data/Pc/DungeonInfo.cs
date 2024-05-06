﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Newtonsoft.Json;
using Nostrum.WPF.Factories;

namespace TCC.Data.Pc;

public class DungeonInfo
{
    [JsonIgnore]
    public ICollectionViewLiveShaping VisibleDungeonsView { get; }
    public List<DungeonCooldownData> DungeonList { get; }

    public DungeonInfo()
    {
        DungeonList = Game.DB!.DungeonDatabase.Dungeons.Values
            .Where(d => d.HasDef)
            .Select(d => new DungeonCooldownData(d.Id))
            .ToList();

        VisibleDungeonsView = CollectionViewFactory.CreateLiveCollectionView(DungeonList,
                                  sortFilters: [new SortDescription($"{nameof(Dungeon)}.{nameof(Dungeon.Index)}", ListSortDirection.Ascending)])
                              ?? throw new Exception("Failed to create LiveCollectionView");
    }

    public void Engage(uint dgId)
    {
        var dg = DungeonList.FirstOrDefault(x => x.Dungeon.Id == dgId);
        if (dg == null) return;

        dg.Entries = dg.Entries == 0
            ? dg.Dungeon.MaxEntries - 1
            : dg.Entries - 1;
    }

    public void ResetAll(ResetMode mode)
    {
        foreach (var dg in DungeonList.Where(d => d.Dungeon.ResetMode == mode)) 
            dg.Reset();
    }

    public void UpdateEntries(Dictionary<uint, short> dungeonCooldowns)
    {
        foreach (var kv in dungeonCooldowns.Where(kv => DungeonList.All(d => d.Id != kv.Key)))
        {
            DungeonList.Add(new DungeonCooldownData(kv.Key) { Entries = kv.Value });
        }

        foreach (var dung in DungeonList)
        {
            if (dungeonCooldowns.TryGetValue(dung.Dungeon.Id, out var entries))
            {
                dung.Entries = entries;
            }
            else
            {
                dung.Reset();
            }
        }
    }

    public void UpdateClears(uint dgId, int runs)
    {
        var dg = DungeonList.FirstOrDefault(d => d.Dungeon.Id == dgId);
        if (dg != null)
        {
            dg.Clears = runs;
        }

        var dgd = DungeonList.FirstOrDefault(d => d.Id == dgId); //todo: what is this?
        if (dgd != null)
        {
            dgd.Clears = runs;
        }
    }

    public void UpdateAvailableEntries(uint coins, uint maxCoins)
    {
        foreach (var x in DungeonList) x.UpdateAvailableEntries(coins, maxCoins);
    }
}
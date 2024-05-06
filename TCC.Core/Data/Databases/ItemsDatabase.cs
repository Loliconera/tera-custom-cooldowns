﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using TCC.Data.Skills;
using TeraDataLite;

namespace TCC.Data.Databases;

public class ItemsDatabase : DatabaseBase
{
    protected override string FolderName => "items";
    protected override string Extension => "tsv";

    public readonly Dictionary<uint, Item> Items = [];
    public IEnumerable<Item> ItemSkills => Items.Values.Where(x => x.Cooldown > 0).ToList();

    public ItemsDatabase(string lang) : base(lang)
    {
    }

    public string GetItemName(uint id) => Items.TryGetValue(id, out var item) ? item.Name : "Unknown";

    public bool TryGetItem(uint itemId, out Item item)
    {
        if (!Items.TryGetValue(itemId, out var found))
        {
            item = new Item(0, "Unknown", RareGrade.Common, 0, 0, "");
            return false;
        }

        item = found;
        return true;
    }

    public bool TryGetItemSkill(uint itemId, out Skill sk)
    {
        sk = new Skill(0, Class.None, string.Empty, string.Empty);

        if (!Items.TryGetValue(itemId, out var item)) return false;

        sk = new Skill(itemId, Class.Common, item.Name, "") { IconName = item.IconName };
        return true;
    }

    public override void Load()
    {
        Items.Clear();
        var lines = File.ReadAllLines(FullPath);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) break;

            var s = line.Split('\t');

            uint id;
            uint grad;
            string name;
            uint expId = 0;
            uint cd;
            string icon;

            if (s.Length == 6)
            {
                id = uint.Parse(s[0]);
                grad = uint.Parse(s[1]);
                name = s[2];
                expId = uint.Parse(s[3]);
                cd = uint.Parse(s[4]);
                icon = s[5];
            }
            else // removed itemExp
            {
                id = uint.Parse(s[0]);
                grad = uint.Parse(s[1]);
                name = s[2];
                cd = uint.Parse(s[3]);
                icon = s[4];
            }

            var item = new Item(id, name, (RareGrade)grad, expId, cd, icon);
            Items[id] = item;
        }
        AddOverride(new Item(149644, "Harrowhold Rejuvenation Potion", RareGrade.Uncommon, 0, 30, "icon_items.potion1_tex"));
        AddOverride(new Item(139520, "Minify", RareGrade.Common, 0, 3, "icon_items.icon_janggoe_item_tex_minus"));
    }

    private void AddOverride(Item item)
    {
        Items[item.Id] = item;
    }
}
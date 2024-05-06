﻿using System;
using System.Linq;
using Newtonsoft.Json;
using Nostrum.WPF.ThreadSafe;
using TCC.Data.Pc;

namespace TCC.Data;

public class Account : ICloneable
{
    [JsonIgnore]
    public Character? CurrentCharacter { get; private set; }
    public ThreadSafeObservableCollection<Character> Characters { get; } = [];
    public bool IsElite { get; set; }

    public void LoginCharacter(uint id)
    {
        CurrentCharacter = Characters.ToSyncList().FirstOrDefault(x => x.Id == id);
    }

    /// <summary>
    /// Returns a copy of the Account object to avoid concurrency.
    /// </summary>
    public object Clone()
    {
        var account = new Account { IsElite = IsElite };
        foreach (var c in Characters.ToSyncList()) account.Characters.Add(c);
        return account;
    }
}
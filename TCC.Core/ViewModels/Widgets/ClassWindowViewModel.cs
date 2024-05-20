﻿using JetBrains.Annotations;
using System;
using TCC.Data.Skills;
using TCC.Settings.WindowSettings;
using TCC.Utilities;
using TCC.Utils;
using TCC.ViewModels.ClassManagers;
using TeraDataLite;
using TeraPacketParser.Analysis;
using TeraPacketParser.Messages;

namespace TCC.ViewModels.Widgets;

[TccModule]
[UsedImplicitly]
public class ClassWindowViewModel : TccWindowViewModel
{
    private Class _currentClass = Class.None;
    private BaseClassLayoutViewModel _currentManager = new NullClassLayoutViewModel();

    public Class CurrentClass
    {
        get => _currentClass;
        set
        {
            if (!RaiseAndSetIfChanged(value, ref _currentClass)) return;

            _dispatcher.Invoke(() =>
            {
                CurrentManager.Dispose();
                CurrentManager = _currentClass switch
                {
                    Class.Warrior => new WarriorLayoutViewModel(),
                    Class.Valkyrie => new ValkyrieLayoutViewModel(),
                    Class.Archer => new ArcherLayoutViewModel(),
                    Class.Lancer => new LancerLayoutViewModel(),
                    Class.Priest => new PriestLayoutViewModel(),
                    Class.Mystic => new MysticLayoutViewModel(),
                    Class.Slayer => new SlayerLayoutViewModel(),
                    Class.Berserker => new BerserkerLayoutViewModel(),
                    Class.Sorcerer => new SorcererLayoutViewModel(),
                    Class.Reaper => new ReaperLayoutViewModel(),
                    Class.Gunner => new GunnerLayoutViewModel(),
                    Class.Brawler => new BrawlerLayoutViewModel(),
                    Class.Ninja => new NinjaLayoutViewModel(),
                    _ => new NullClassLayoutViewModel()
                };
            });
        }
    }

    public BaseClassLayoutViewModel CurrentManager
    {
        get => _currentManager;
        set => RaiseAndSetIfChanged(value, ref _currentManager);
    }

    public ClassWindowViewModel(ClassWindowSettings settings) : base(settings)
    {
        if (!settings.Enabled) return;
        settings.WarriorShowEdgeChanged += OnWarriorShowEdgeChanged;
        settings.WarriorShowTraverseCutChanged += OnWarriorShowTraverseCutChanged;
        settings.WarriorShowInfuriateChanged += OnWarriorShowInfuriateChanged;
        settings.WarriorEdgeModeChanged += OnWarriorEdgeModeChanged;
        settings.ValkyrieShowGodsfallChanged += OnValkyrieShowGodsfallChanged;
        settings.ValkyrieShowRagnarokChanged += OnValkyrieShowRagnarokChanged;
    }

    protected override void OnEnabledChanged(bool enabled)
    {
        base.OnEnabledChanged(enabled);
        if (!enabled)
        {
            ((ClassWindowSettings)Settings!).WarriorShowEdgeChanged -= OnWarriorShowEdgeChanged;
            ((ClassWindowSettings)Settings).WarriorShowTraverseCutChanged -= OnWarriorShowTraverseCutChanged;
            ((ClassWindowSettings)Settings).WarriorShowInfuriateChanged -= OnWarriorShowInfuriateChanged;
            ((ClassWindowSettings)Settings).WarriorEdgeModeChanged -= OnWarriorEdgeModeChanged;
            ((ClassWindowSettings)Settings).ValkyrieShowGodsfallChanged -= OnValkyrieShowGodsfallChanged;
            ((ClassWindowSettings)Settings).ValkyrieShowRagnarokChanged -= OnValkyrieShowRagnarokChanged;
            CurrentClass = Class.None;
        }
        else
        {
            ((ClassWindowSettings)Settings!).WarriorShowEdgeChanged += OnWarriorShowEdgeChanged;
            ((ClassWindowSettings)Settings).WarriorShowTraverseCutChanged += OnWarriorShowTraverseCutChanged;
            ((ClassWindowSettings)Settings).WarriorShowInfuriateChanged += OnWarriorShowInfuriateChanged;
            ((ClassWindowSettings)Settings).WarriorEdgeModeChanged += OnWarriorEdgeModeChanged;
            ((ClassWindowSettings)Settings).ValkyrieShowGodsfallChanged += OnValkyrieShowGodsfallChanged;
            ((ClassWindowSettings)Settings).ValkyrieShowRagnarokChanged += OnValkyrieShowRagnarokChanged;
            CurrentClass = Game.Me.Class;
        }
    }

    private void OnValkyrieShowRagnarokChanged()
    {
        TccUtils.CurrentClassVM<ValkyrieLayoutViewModel>()?.ExN(nameof(ValkyrieLayoutViewModel.ShowRagnarok));
    }

    private void OnValkyrieShowGodsfallChanged()
    {
        TccUtils.CurrentClassVM<ValkyrieLayoutViewModel>()?.ExN(nameof(ValkyrieLayoutViewModel.ShowGodsfall));
    }

    private void OnWarriorEdgeModeChanged()
    {
        TccUtils.CurrentClassVM<WarriorLayoutViewModel>()?.ExN(nameof(WarriorLayoutViewModel.WarriorEdgeMode));
    }

    private void OnWarriorShowTraverseCutChanged()
    {
        TccUtils.CurrentClassVM<WarriorLayoutViewModel>()?.ExN(nameof(WarriorLayoutViewModel.ShowTraverseCut));
    }

    private void OnWarriorShowInfuriateChanged()
    {
        TccUtils.CurrentClassVM<WarriorLayoutViewModel>()?.ExN(nameof(WarriorLayoutViewModel.ShowInfuriate));
    }

    private void OnWarriorShowEdgeChanged()
    {
        TccUtils.CurrentClassVM<WarriorLayoutViewModel>()?.ExN(nameof(WarriorLayoutViewModel.ShowEdge));
    }


    protected override void InstallHooks()
    {
        PacketAnalyzer.Processor.Hook<S_LOGIN>(OnLogin);
        PacketAnalyzer.Processor.Hook<S_GET_USER_LIST>(OnGetUserList);
        PacketAnalyzer.Processor.Hook<S_PLAYER_STAT_UPDATE>(OnPlayerStatUpdate);
        PacketAnalyzer.Processor.Hook<S_PLAYER_CHANGE_STAMINA>(OnPlayerChangeStamina);
        PacketAnalyzer.Processor.Hook<S_START_COOLTIME_SKILL>(OnStartCooltimeSkill);
        PacketAnalyzer.Processor.Hook<S_DECREASE_COOLTIME_SKILL>(OnDecreaseCooltimeSkill);
        PacketAnalyzer.Processor.Hook<S_CREST_MESSAGE>(OnCrestMessage);
    }

    protected override void RemoveHooks()
    {
        PacketAnalyzer.Processor.Unhook<S_LOGIN>(OnLogin);
        PacketAnalyzer.Processor.Unhook<S_GET_USER_LIST>(OnGetUserList);
        PacketAnalyzer.Processor.Unhook<S_PLAYER_STAT_UPDATE>(OnPlayerStatUpdate);
        PacketAnalyzer.Processor.Unhook<S_PLAYER_CHANGE_STAMINA>(OnPlayerChangeStamina);
        PacketAnalyzer.Processor.Unhook<S_START_COOLTIME_SKILL>(OnStartCooltimeSkill);
        PacketAnalyzer.Processor.Unhook<S_DECREASE_COOLTIME_SKILL>(OnDecreaseCooltimeSkill);
        PacketAnalyzer.Processor.Unhook<S_CREST_MESSAGE>(OnCrestMessage);
    }

    private void OnLogin(S_LOGIN m)
    {
        _dispatcher.InvokeAsync(() =>
        {
            CurrentClass = m.CharacterClass;

            if (m.CharacterClass is Class.Warrior)
            {
                TccUtils.CurrentClassVM<WarriorLayoutViewModel>()?.EdgeCounter.SetClass(m.CharacterClass);
            }
        });

        if (m.CharacterClass == Class.Valkyrie)
            PacketAnalyzer.Processor.Hook<S_WEAK_POINT>(OnWeakPoint);
        else
            PacketAnalyzer.Processor.Unhook<S_WEAK_POINT>(OnWeakPoint);
    }

    private void OnGetUserList(S_GET_USER_LIST m)
    {
        CurrentClass = Class.None;
    }

    private void OnPlayerStatUpdate(S_PLAYER_STAT_UPDATE m)
    {
        // check enabled?
        switch (CurrentClass)
        {
            case Class.Sorcerer when CurrentManager is SorcererLayoutViewModel sm:
                sm.SetElements(TccUtils.BoolsToElements(m.Fire, m.Ice, m.Arcane));

                break;
            case Class.Warrior when CurrentManager is WarriorLayoutViewModel wm:
                wm.EdgeCounter.Val = m.Edge;
                break;
        }
    }

    private void OnPlayerChangeStamina(S_PLAYER_CHANGE_STAMINA m)
    {
        CurrentManager.SetMaxST(Convert.ToInt32(m.MaxST));
        CurrentManager.SetST(Convert.ToInt32(m.CurrentST));
    }

    private void OnWeakPoint(S_WEAK_POINT p)
    {
        if (CurrentManager is not ValkyrieLayoutViewModel vvm) return;
        vvm.RunemarksCounter.Val = p.TotalRunemarks;
    }

    private void OnStartCooltimeSkill(S_START_COOLTIME_SKILL m)
    {
        if (!Game.DB!.SkillsDatabase.TryGetSkill(m.SkillId, Game.Me.Class, out var skill)) return;
        CurrentManager.StartSpecialSkill(new Cooldown(skill, m.Cooldown));
    }

    private void OnDecreaseCooltimeSkill(S_DECREASE_COOLTIME_SKILL m)
    {
        if (!Game.DB!.SkillsDatabase.TryGetSkill(m.SkillId, Game.Me.Class, out var skill)) return;
        CurrentManager.ChangeSpecialSkill(skill, m.Cooldown);
    }

    private void OnCrestMessage(S_CREST_MESSAGE m)
    {
        if (m.Type != 6) return;
        if (!Game.DB!.SkillsDatabase.TryGetSkill(m.SkillId, Game.Me.Class, out var skill)) return;
        CurrentManager.ResetSpecialSkill(skill);
    }
}
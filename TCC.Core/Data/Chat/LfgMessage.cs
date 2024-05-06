﻿using Nostrum.WPF.Factories;
using Nostrum.WPF.ThreadSafe;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using TCC.Data.Pc;
using TCC.UI;
using TCC.Utils;

namespace TCC.Data.Chat;

public class LfgMessage : ChatMessage
{
    private int _tries = 10;
    private readonly ThreadSafeObservableCollection<User> _members = [];
    private readonly Timer _timer;
    private Listing? _linkedListing;

    public uint AuthorId { get; }
    public uint ServerId { get; } //TODO: should be added to base class and assigned from S_CHAT, S_WHISPER, etc
    public bool ShowMembers => LinkedListing is { Players.Count: <= 7 };
    public Listing? LinkedListing
    {
        get => _linkedListing;
        private set
        {
            if (_linkedListing == value) return;
            if (value == null)
            {
                _members.Clear();
                if (_linkedListing != null) _linkedListing.Players.CollectionChanged -= OnMembersChanged;
            }
            else
            {
                value.Players.CollectionChanged += OnMembersChanged;
                foreach (var p in value.Players.ToSyncList())
                {
                    _members.Add(p);
                }
            }
            _linkedListing = value;
            InvokePropertyChanged();
        }
    }
    public ICollectionView MembersView { get; }

    public LfgMessage(uint authorId, string author, string msg, uint serverId) : base(ChatChannel.LFG, author, msg, 0, false, authorId, serverId)
    {
        AuthorId = authorId;
        ServerId = serverId;
        _timer = new Timer(1500);
        _timer.Elapsed += OnTimerTick;

        MembersView = CollectionViewFactory.CreateCollectionView(_members, null, new List<SortDescription>());
    }

    protected override void DisposeImpl()
    {
        _timer.Elapsed -= OnTimerTick;
        _timer.Dispose();
        if (_linkedListing != null)
        {
            _linkedListing.Players.CollectionChanged -= OnMembersChanged;
        }
        base.DisposeImpl();
    }

    private void OnMembersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Task.Run(() =>
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (var item in e.NewItems?.Cast<User>() ?? []) _members.Add(item);
                    break;

                case NotifyCollectionChangedAction.Remove:
                    foreach (var item in e.OldItems?.Cast<User>() ?? []) _members.Remove(item);
                    break;

                case NotifyCollectionChangedAction.Reset:
                    _members.Clear();
                    break;
            }
        });
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (_tries == 0)
        {
            //Log.CW($"Unable to find linked listing for [{Author}]{RawMessage}, stopping tries.");
            _timer.Stop();
            return;
        }
        LinkedListing = FindListing();
        if (LinkedListing == null)
        {
            //Log.CW($"Linked listing ({Author}/{AuthorId}) is still null! ({_tries})");
            _tries--;
            return;
        }
        _timer.Stop();
        WindowManager.ViewModels.LfgVM.EnqueueRequest(LinkedListing.LeaderId, LinkedListing.ServerId);
    }

    private Listing? FindListing()
    {
        return WindowManager.ViewModels.LfgVM.Listings.ToSyncList().FirstOrDefault(x =>
            x.Players.ToSyncList().Any(p => p.Name == Author) ||
            x.LeaderName == Author ||
            x.Message == RawMessage);
    }

    public void LinkListing()
    {
        _dispatcher.InvokeAsync(() =>
        {
            LinkedListing = FindListing();
            if (LinkedListing != null) return;

            //Log.CW($"Linked listing ({Author}/{AuthorId}) is null! Requesting list.");
            WindowManager.ViewModels.LfgVM.EnqueueListRequest();
            _timer.Start();
        });
    }
}
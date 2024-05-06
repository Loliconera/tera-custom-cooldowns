﻿using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Nostrum.WPF.Factories;
using Nostrum.WPF.ThreadSafe;
using TCC.Data;
using TCC.Data.Pc;

namespace TCC.UI.Windows;

public class MergedInventoryViewModel : ThreadSafeObservableObject
{
    public ThreadSafeObservableCollection<MergedInventoryItem> MergedInventory { get; }
    public ICollectionViewLiveShaping MergedInventoryView { get; }
    public MergedInventoryViewModel()
    {
        MergedInventory = new ThreadSafeObservableCollection<MergedInventoryItem>();
        MergedInventoryView = CollectionViewFactory.CreateLiveCollectionView(MergedInventory,
                                  sortFilters:
                                  [
                                      new SortDescription($"{nameof(MergedInventoryItem.Item)}.{nameof(InventoryItem.Item)}.{nameof(Item.Id)}", ListSortDirection.Ascending),
                                      new SortDescription($"{nameof(MergedInventoryItem.Item)}.{nameof(InventoryItem.Item)}.{nameof(Item.RareGrade)}", ListSortDirection.Ascending)
                                  ])
                              ?? throw new Exception("Failed to create LiveCollectionView");
    }

    private double _totalProgress;

    public double TotalProgress
    {
        get => _totalProgress * 100;
        set => RaiseAndSetIfChanged(value, ref _totalProgress);
    }

    public void LoadItems()
    {
        var totalItemsAmount = Game.Account.Characters.ToArray().Where(c => !c.Hidden).Sum(ch => ch.Inventory.Count);
        var itemsParsed = 0;
        Task.Factory.StartNew(() =>
        {
            foreach (var ch in Game.Account.Characters.ToArray().Where(c => !c.Hidden))
            {
                _dispatcher.InvokeAsync(() =>
                {
                    foreach (var item in ch.Inventory.ToArray())
                    {
                        _dispatcher.InvokeAsync(() =>
                        {
                            var existing = MergedInventory.FirstOrDefault(x => x.Item?.Item.Id == item.Item.Id);
                            if (existing == null)
                            {
                                var newItem = new MergedInventoryItem();
                                newItem.Items.Add(new InventoryItemWithOwner(item, ch));
                                MergedInventory.Add(newItem);
                            }
                            else
                            {
                                var ex = existing.Items.FirstOrDefault(x => x.Owner == ch);
                                if (ex != null)
                                {
                                    ex.Item.Amount = item.Amount;
                                }
                                else
                                {
                                    existing.Items.Add(new InventoryItemWithOwner(item, ch));
                                }
                            }
                            itemsParsed++;
                            TotalProgress = itemsParsed / (double)totalItemsAmount;
                        }, DispatcherPriority.DataBind);
                    }
                }, DispatcherPriority.Background);
            }
        });
    }
}
public class InventoryItemWithOwner
{
    public InventoryItem Item { get; }
    public Character Owner { get; }
    public InventoryItemWithOwner(InventoryItem i, Character o)
    {
        Item = i;
        Owner = o;
    }
}
public class MergedInventoryItem : ThreadSafeObservableObject
{
    public InventoryItem? Item => Items.Count > 0 ? Items[0].Item : null;
    public ThreadSafeObservableCollection<InventoryItemWithOwner> Items { get; }
    public int TotalAmount => Items.ToArray().Sum(i => i.Item.Amount);

    public MergedInventoryItem()
    {
        Items = new ThreadSafeObservableCollection<InventoryItemWithOwner>();
        Items.CollectionChanged += (_, _) => InvokePropertyChanged(nameof(TotalAmount));
    }
}
public partial class MergedInventoryWindow
{
    public MergedInventoryWindow()
    {
        InitializeComponent();
        DataContext = new MergedInventoryViewModel();
        ((MergedInventoryViewModel)DataContext).Dispatcher = Dispatcher;
        Loaded += OnLoaded;

    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ((MergedInventoryViewModel)DataContext).LoadItems();
    }

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();

    }

    private void OnCloseButtonClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void FilterInventory(object sender, TextChangedEventArgs e)
    {
        Dispatcher?.InvokeAsync(() =>
        {
            var view = (ICollectionView)((MergedInventoryViewModel)DataContext).MergedInventoryView;
            view.Filter = o =>
            {
                var item = ((MergedInventoryItem)o).Item?.Item;
                var name = item?.Name;
                return name != null && name.IndexOf(((TextBox)sender).Text, StringComparison.InvariantCultureIgnoreCase) != -1;
            };
            view.Refresh();
        }, DispatcherPriority.DataBind);

    }
}
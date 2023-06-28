using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Game.Inventory;
using Dalamud.Game.Inventory.InventoryEventArgTypes;

namespace TPie.Helpers
{
    internal class ItemsHelper
    {
        #region Singleton
        private ItemsHelper()
        {
            ExcelSheet<Item>? itemsSheet = Plugin.DataManager.GetExcelSheet<Item>();
            List<Item> validItems = itemsSheet?.Where(item => item.ItemAction.RowId > 0).ToList() ?? new List<Item>();

            ExcelSheet<EventItem>? eventItemsSheet = Plugin.DataManager.GetExcelSheet<EventItem>();
            List<EventItem> validEventItems = eventItemsSheet?.Where(item => item.Action.RowId > 0).ToList() ?? new List<EventItem>();

            _usableSet = validItems.Select(i => i.RowId).Concat(validEventItems.Select(e => e.RowId)).ToHashSet();

            RefreshInventories();
            Plugin.GameInventory.InventoryChanged += OnInventoryChanged;
        }

        public static void Initialize() { Instance = new ItemsHelper(); }

        public static ItemsHelper Instance { get; private set; } = null!;

        ~ItemsHelper()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            Instance = null!;
        }
        #endregion

        private readonly IntPtr _useItemPtr;
        private readonly HashSet<uint> _usableSet;
        private readonly Dictionary<(uint, bool), UsableItem> _usableItems = new();

        public unsafe void RefreshInventories()
        {
            _usableItems.Clear();

            try
            {
                Plugin.Logger.Debug("Refreshing inventories");
                InventoryManager* manager = InventoryManager.Instance();
                CheckItems(manager, InventoryType.Inventory1);
                CheckItems(manager, InventoryType.Inventory2);
                CheckItems(manager, InventoryType.Inventory3);
                CheckItems(manager, InventoryType.Inventory4);
                CheckItems(manager, InventoryType.KeyItems);
            }
            catch { }
        }

        private unsafe void CheckItems(InventoryManager* manager, InventoryType inventoryType)
        {
            InventoryContainer* container = manager->GetInventoryContainer(inventoryType);
            if (container == null) return;

            uint size = container->Size;
            for (int i = 0; i < size; i++) {
                try {
                    InventoryItem* item = &container->Items[i];
                    if (item->Quantity == 0) continue;

                    uint itemId = item->ItemId;
                    if (_usableSet.Contains(itemId)) {
                        bool hq = (item->Flags & InventoryItem.ItemFlags.HighQuality) != 0;
                        (uint itemId, bool hq) key = (itemId, hq);

                        if (_usableItems.TryGetValue(key, out UsableItem? usableItem)) {
                            usableItem.Count += item->Quantity;
                        }
                        else {
                            _usableItems.Add(key, new UsableItem(item->Quantity, inventoryType == InventoryType.KeyItems));
                        }
                    }
                }
                catch { }
            }
        }

        public UsableItem? GetUsableItem(uint itemId, bool hq)
        {
            return _usableItems.GetValueOrDefault((itemId, hq));
        }

        public List<UsableItem> GetUsableItems()
        {
            return _usableItems.Values.ToList();
        }

        public unsafe void Use(uint itemId)
        {
            AgentInventoryContext.Instance()->UseItem(itemId, 4);
        }

        private void OnInventoryChanged(IReadOnlyCollection<InventoryEventArgs> events)
        {
            foreach (InventoryEventArgs inventoryEvent in events)
            {
                if (inventoryEvent.Item.ContainerType is GameInventoryType.KeyItems or <= GameInventoryType.Inventory4)
                {
                    RefreshInventories();
                    return;
                }
            }
        }
    }

    public record UsableItem(int Count, bool IsKey)
    {
        public int Count = Count;
    }
}

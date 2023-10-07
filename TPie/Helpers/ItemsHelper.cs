using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace TPie.Helpers
{
    internal class ItemsHelper
    {
        private delegate void UseItem(IntPtr agent, uint itemId, uint unk1, uint unk2, short unk3);

        #region Singleton
        private ItemsHelper()
        {
            _useItemPtr = Plugin.SigScanner.ScanText("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 4C 89 74 24 ??");

            ExcelSheet<Item>? itemsSheet = Plugin.DataManager.GetExcelSheet<Item>();
            List<Item> validItems = itemsSheet?.Where(item => item.ItemAction.Row > 0).ToList() ?? new List<Item>();

            ExcelSheet<EventItem>? eventItemsSheet = Plugin.DataManager.GetExcelSheet<EventItem>();
            List<EventItem> validEventItems = eventItemsSheet?.Where(item => item.Action.Row > 0).ToList() ?? new List<EventItem>();

            _usableSet = validItems.Select(i => i.RowId).Concat(validEventItems.Select(e => e.RowId)).ToHashSet();
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

        private readonly Dictionary<(uint, bool), UsableItem> UsableItems = new();

        public unsafe void CalculateUsableItems()
        {
            UsableItems.Clear();

            try
            {
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
                        var key = (itemId, hq);

                        if (UsableItems.TryGetValue(key, out UsableItem? usableItem)) {
                            usableItem.Count += item->Quantity;
                        }
                        else {
                            UsableItems.Add(key, new UsableItem(item->Quantity, inventoryType == InventoryType.KeyItems));
                        }
                    }
                }
                catch { }
            }
        }

        public UsableItem? GetUsableItem(uint itemId, bool hq)
        {
            var key = (itemId, hq);

            if (UsableItems.TryGetValue(key, out UsableItem? value))
            {
                return value;
            }

            return null;
        }

        public List<UsableItem> GetUsableItems()
        {
            return UsableItems.Values.ToList();
        }

        public unsafe void Use(uint itemId)
        {
            if (_useItemPtr == IntPtr.Zero) return;

            AgentModule* agentModule = (AgentModule*)Plugin.GameGui.GetUIModule();
            IntPtr agent = (IntPtr)agentModule->GetAgentByInternalId((AgentId)10);

            UseItem usetItemDelegate = Marshal.GetDelegateForFunctionPointer<UseItem>(_useItemPtr);
            usetItemDelegate(agent, itemId, 999, 0, 0);
        }
    }

    public record UsableItem(uint Count, bool IsKey)
    {
        public uint Count = Count;
        public readonly bool IsKey = IsKey;
    }
}

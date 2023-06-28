using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Game.Network;
using Dalamud.Hooking;

namespace TPie.Helpers
{
    internal class ItemsHelper
    {
        private delegate void UseItem(IntPtr agent, uint itemId, uint unk1, uint unk2, short unk3);
        private delegate uint GetActionID(uint unk, uint itemId);
        private delegate uint ProcessInventoryTransactionDelegate(IntPtr unk0, IntPtr unk1);

        private unsafe delegate void ShuffleDelegate(ulong* unk0, IntPtr unk1);

        private unsafe delegate void SomeDelegate(IntPtr unk0, uint* unk1);
        private delegate void WowDelegate(IntPtr unk0, IntPtr unk1, IntPtr unk2);
        private delegate void MegaDelegate(IntPtr unk0, IntPtr unk1, IntPtr unk2);
        private delegate uint InventoryTransactionSuperDelegate(uint unk0, IntPtr unk1);
        private delegate uint InventoryActionAckSuperDelegate(uint unk0, IntPtr unk1);

        // InventoryTransactionSuper
        // __int64 __fastcall sub_14076B1A0(unsigned int a1, __int64 a2)
        // E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 8B D3 8B CE E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 8B D3 8B CE E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 8D 53 10
        //
        // InventoryActionAckSuper
        // __int64 __fastcall sub_140769740(unsigned int a1, __int64 a2)
        // E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 8D 53 10 8B CE E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 8B D3

        #region Singleton
        private ItemsHelper()
        {
            goto SKIP_DEBUG;

            Plugin.GameNetwork.NetworkMessage += GameNetworkOnNetworkMessage;

            _useItemPtr = Plugin.SigScanner.ScanText("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 89 7C 24 38");

            if (Plugin.SigScanner.TryScanText("48 89 5C 24 ?? 57 48 83 EC 20 48 8B DA 48 8B F9 48 85 D2 0F 84 ?? ?? ?? ?? 0F 1F 80", out var itPtr)) // parent
            {
                ProcessInventoryTransactionHook = Hook<ProcessInventoryTransactionDelegate>.FromAddress(itPtr, ProcessInventoryTransactionDetour);
                ProcessInventoryTransactionHook.Enable();
                PluginLog.Warning("ProcessInventoryTransactionDetour found!");
            }
            else
            {
                PluginLog.Error("ProcessInventoryTransactionDetour not found!");
            }

            // if (Plugin.SigScanner.TryScanText("4C 8B C1 48 85 D2 0F 84 ?? ?? ?? ?? 45 33 C9", out var shufflePtr)) // parent
            // {
            //     unsafe
            //     {
            //         ShuffleHook = Hook<ShuffleDelegate>.FromAddress(shufflePtr, ShuffleDetour);
            //         ShuffleHook.Enable();
            //         PluginLog.Warning("Shuffle found!");
            //     }
            // }
            // else
            // {
            //     PluginLog.Error("Shuffle not found!");
            // }

            // if (Plugin.SigScanner.TryScanText("E9 ?? ?? ?? ?? 41 B0 01 BA ?? ?? ?? ?? 49 8B C9", out var somePtr)) // parent
            // {
            //     unsafe
            //     {
            //         SomeHook = Hook<SomeDelegate>.FromAddress(somePtr, SomeDetour);
            //         SomeHook.Enable();
            //         PluginLog.Warning("Some found!");
            //     }
            // }
            // else
            // {
            //     PluginLog.Error("Some not found!");
            // }
            //
            // if (Plugin.SigScanner.TryScanText("48 83 EC 28 48 8B 05 ?? ?? ?? ?? 41 0F B6 50 ??", out var wowPtr)) // parent
            // {
            //     unsafe
            //     {
            //         WowHook = Hook<WowDelegate>.FromAddress(wowPtr, WowDetour);
            //         WowHook.Enable();
            //         PluginLog.Warning("Wow found!");
            //     }
            // }
            // else
            // {
            //     PluginLog.Error("Wow not found!");
            // }

            // if (Plugin.SigScanner.TryScanText("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 81 EC ?? ?? ?? ?? 41 0F B7 40 ?? 49 8B F8 48 8B F1 3D ?? ?? ?? ?? 0F 84", out var megaPtr)) // parent
            // {
            //     unsafe
            //     {
            //         MegaHook = Hook<MegaDelegate>.FromAddress(megaPtr, MegaDetour);
            //         MegaHook.Enable();
            //         PluginLog.Warning("Mega found!");
            //     }
            // }
            // else
            // {
            //     PluginLog.Error("Mega not found!");
            // }

            if (Plugin.SigScanner.TryScanText("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 8B D3 8B CE E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 8B D3 8B CE E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 8D 53 10", out var inventoryTransactionSuperPtr)) // parent
            {
                unsafe
                {
                    InventoryTransactionSuperHook = Hook<InventoryTransactionSuperDelegate>.FromAddress(inventoryTransactionSuperPtr, InventoryTransactionSuperDetour);
                    InventoryTransactionSuperHook.Enable();
                    PluginLog.Warning("InventoryTransactionSuper found!");
                }
            }
            else
            {
                PluginLog.Error("InventoryTransactionSuper not found!");
            }

            if (Plugin.SigScanner.TryScanText("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 8D 53 10 8B CE E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 8B D3", out var inventoryActionAckSuperPtr)) // parent
            {
                unsafe
                {
                    InventoryActionAckSuperHook = Hook<InventoryActionAckSuperDelegate>.FromAddress(inventoryActionAckSuperPtr, InventoryActionAckSuperDetour);
                    InventoryActionAckSuperHook.Enable();
                    PluginLog.Warning("InventoryActionAckSuper found!");
                }
            }
            else
            {
                PluginLog.Error("InventoryActionAckSuper not found!");
            }

            SKIP_DEBUG:

            // InventoryTransactionSuper
            // __int64 __fastcall sub_14076B1A0(unsigned int a1, __int64 a2)
            // E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 8B D3 8B CE E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 8B D3 8B CE E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 8D 53 10
            //
            // InventoryActionAckSuper
            // __int64 __fastcall sub_140769740(unsigned int a1, __int64 a2)
            // E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 8D 53 10 8B CE E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 8B D3

            ExcelSheet<Item>? itemsSheet = Plugin.DataManager.GetExcelSheet<Item>();
            List<Item> validItems = itemsSheet?.Where(item => item.ItemAction.Row > 0).ToList() ?? new List<Item>();
            _usableItems = validItems.ToDictionary(item => item.RowId);

            ExcelSheet<EventItem>? eventItemsSheet = Plugin.DataManager.GetExcelSheet<EventItem>();
            List<EventItem> validEventItems = eventItemsSheet?.Where(item => item.Action.Row > 0).ToList() ?? new List<EventItem>();
            _usableEventItems = validEventItems.ToDictionary(item => item.RowId);
        }

        static ushort[] ignore = {
            0x38D, // 909 ActorMove
            0x215, // 533 UpdatePositionHandler
            0x14C, // 332 EventFinish
            0x2A6, // 678 ? Condition-related
            0x1CB, // 459 StatusEffectList
            0x80 , // 128 ?? Maybe chat related..?
            0x8E , // 142 UpdateHpMpTp
            0x286, // 646 ActorControlSelf
            0x72 , // 114 ??? (ok to ignore...?)
            // 0xDA , // 218 ClientTrigger (ok to ignore...?)
            // 0x163, // 354 !! InventoryActionAck
            // 0x283, // 643 !! InventoryModifyHandler
            // 0x37A, // 890 !! UpdateInventorySlot
            // 0x162, // 354 !! InventoryActionAck
            0x2AE, // 686 ???
            0x3BD, // 957 ActorControlTarget
            /*0x72, 0x80, 0x3A0, 0x38D, 0x286, 0x227, 0x3BD, 0x215, 0x251, 0x28A, 0x140, 0x2F3, 0xDA, 0x127, 0x1A4,
            0xAF, 0x2A6, 0x2D2, 0x1CB, 0x17B, 0x14C*/ };

        private static Dictionary<ushort, string> important = new()
        {
            { 0x162, "InventoryActionAck" },         // 354
            { 0x283, "InventoryModifyHandler" },     // 643
            { 0x37A, "UpdateInventorySlot" },        // 890
            { 0x2DF, "InventoryTransaction" },       // 735
            { 0x16F, "InventoryTransactionFinish" }, // 367
        };

        private void GameNetworkOnNetworkMessage(IntPtr dataptr, ushort opcode, uint sourceactorid, uint targetactorid, NetworkMessageDirection direction)
        {
            if (ignore.Contains(opcode) && !important.ContainsKey(opcode))
            {
                return;
            }

            if (targetactorid != 0 && Plugin.ClientState.LocalPlayer != null && targetactorid != Plugin.ClientState.LocalPlayer!.ObjectId)
            {
                return;
            }

            if (important.TryGetValue(opcode, out string? value))
            {
                PluginLog.Warning($"[{value}] GameNetworkOnNetworkMessage(0x{dataptr:X}, 0x{opcode:X} ({opcode}), {sourceactorid}, {targetactorid}, {direction})");
            }
            else
            {
                PluginLog.Information($"GameNetworkOnNetworkMessage(0x{dataptr:X}, 0x{opcode:X} ({opcode}), {sourceactorid}, {targetactorid}, {direction})");
            }

            if (opcode == 0xDA)
            {
                var size = 50;
                byte[] managedArray = new byte[size];
                Marshal.Copy(dataptr, managedArray, 0, size);
                PluginLog.Information($"  {Convert.ToHexString(managedArray)}");
            }
        }

        private Hook<ProcessInventoryTransactionDelegate>? ProcessInventoryTransactionHook { get; set; }

        private uint ProcessInventoryTransactionDetour(IntPtr unk0, IntPtr unk1)
        {
            PluginLog.Warning("ProcessInventoryTransactionDetour!");
            return ProcessInventoryTransactionHook!.Original.Invoke(unk0, unk1);
        }

        private Hook<ShuffleDelegate>? ShuffleHook { get; set; }

        private unsafe void ShuffleDetour(ulong* unk0, IntPtr unk1)
        {
            PluginLog.Warning("ShuffleDetour!");
            ShuffleHook!.Original.Invoke(unk0, unk1);
        }

        private Hook<SomeDelegate>? SomeHook { get; set; }

        private unsafe void SomeDetour(IntPtr unk0, uint* unk1)
        {
            PluginLog.Warning("SomeDetour!");
            SomeHook!.Original.Invoke(unk0, unk1);
        }

        private Hook<WowDelegate>? WowHook { get; set; }

        private void WowDetour(IntPtr unk0, IntPtr unk1, IntPtr unk2)
        {
            PluginLog.Warning("WowDetour!");
            WowHook!.Original.Invoke(unk0, unk1, unk2);
        }
        
        private Hook<MegaDelegate>? MegaHook { get; set; }

        private void MegaDetour(IntPtr unk0, IntPtr unk1, IntPtr unk2)
        {
            PluginLog.Warning("MegaDetour!");
            MegaHook!.Original.Invoke(unk0, unk1, unk2);
        }
        
        private Hook<InventoryTransactionSuperDelegate>? InventoryTransactionSuperHook { get; set; }

        private uint InventoryTransactionSuperDetour(uint unk0, IntPtr unk1)
        {
            PluginLog.Warning("InventoryTransactionSuperDetour!");
            return InventoryTransactionSuperHook!.Original.Invoke(unk0, unk1);
        }
        
        private Hook<InventoryActionAckSuperDelegate>? InventoryActionAckSuperHook { get; set; }

        private uint InventoryActionAckSuperDetour(uint unk0, IntPtr unk1)
        {
            PluginLog.Warning("InventoryActionAckSuperDetour!");
            return InventoryActionAckSuperHook!.Original.Invoke(unk0, unk1);
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

            ProcessInventoryTransactionHook?.Disable();
            ShuffleHook?.Disable();
            SomeHook?.Disable();
            WowHook?.Disable();
            MegaHook?.Disable();
            InventoryTransactionSuperHook?.Disable();
            InventoryActionAckSuperHook?.Disable();
            Plugin.GameNetwork.NetworkMessage -= GameNetworkOnNetworkMessage;
            Instance = null!;
        }
        #endregion

        private IntPtr _useItemPtr = IntPtr.Zero;
        private Dictionary<uint, Item> _usableItems;
        private Dictionary<uint, EventItem> _usableEventItems;

        private Dictionary<(uint, bool), UsableItem> UsableItems = new();

        public unsafe void CalculateUsableItems()
        {
            InventoryManager* manager = InventoryManager.Instance();
            InventoryType[] inventoryTypes = new InventoryType[]
            {
                InventoryType.Inventory1,
                InventoryType.Inventory2,
                InventoryType.Inventory3,
                InventoryType.Inventory4,
                InventoryType.KeyItems
            };

            UsableItems.Clear();

            try
            {
                foreach (InventoryType inventoryType in inventoryTypes)
                {
                    InventoryContainer* container = manager->GetInventoryContainer(inventoryType);
                    if (container == null) continue;

                    for (int i = 0; i < container->Size; i++)
                    {
                        try
                        {
                            InventoryItem* item = container->GetInventorySlot(i);
                            if (item == null) continue;

                            if (item->Quantity == 0) continue;

                            bool hq = (item->Flags & InventoryItem.ItemFlags.HQ) != 0;
                            uint itemId = item->ItemID;
                            var key = (itemId, hq);

                            if (UsableItems.TryGetValue(key, out UsableItem? usableItem) && usableItem != null)
                            {
                                usableItem.Count += item->Quantity;
                            }
                            else
                            {
                                if (_usableItems.TryGetValue(itemId, out Item? itemData) && itemData != null)
                                {
                                    UsableItems.Add(key, new UsableItem(itemData, hq, item->Quantity));
                                }
                                else if (_usableEventItems.TryGetValue(itemId, out EventItem? eventItemData) && eventItemData != null)
                                {
                                    UsableItems.Add(key, new UsableItem(eventItemData, hq, item->Quantity));
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
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
            IntPtr agent = (IntPtr)agentModule->GetAgentByInternalID(10);

            UseItem usetItemDelegate = Marshal.GetDelegateForFunctionPointer<UseItem>(_useItemPtr);
            usetItemDelegate(agent, itemId, 999, 0, 0);
        }
    }

    public class UsableItem
    {
        public readonly string Name;
        public readonly uint ID;
        public readonly bool IsHQ;
        public readonly uint IconID;
        public uint Count;
        public readonly bool IsKey;

        public UsableItem(Item item, bool hq, uint count)
        {
            Name = item.Name;
            ID = item.RowId;
            IsHQ = hq;
            IconID = item.Icon;
            Count = count;
            IsKey = false;
        }

        public UsableItem(EventItem item, bool hq, uint count)
        {
            Name = item.Name;
            ID = item.RowId;
            IsHQ = hq;
            IconID = item.Icon;
            Count = count;
            IsKey = true;
        }

        public override string ToString()
        {
            return $"UsableItem: {ID}, {Name}, {IsHQ}, {IconID}, {Count}";
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Dalamud.Game.Network;
using Dalamud.Hooking;
using Dalamud.Logging;
using Newtonsoft.Json;

namespace TPie.Helpers;

public class ItemCacheHelper
{
    private LoadState _loadState = LoadState.Init;

    private Dictionary<string, ushort> _serverOpcodes = new();
    private Dictionary<string, ushort> _clientOpCodes = new();

    private delegate void SetKeyItem(IntPtr agent, ushort slotId, int itemId);
    private delegate void UnsetKeyItem(IntPtr agent, ushort slotId);
    private delegate char ModifyKeyItem(IntPtr agent);
    private Hook<SetKeyItem>? SetKeyItemHook { get; }
    private Hook<UnsetKeyItem>? UnsetKeyItemHook { get; }
    private Hook<ModifyKeyItem>? ModifyKeyItemHook { get; }

    private string[] opcodeNames = { "InventoryActionAck", "InventoryModifyHandler", "UpdateInventorySlot", "InventoryTransaction", "InventoryTransactionFinish" };
    private ushort[] opcodeValues = new ushort[5];

    #region singleton

    private ItemCacheHelper()
    {
        PluginLog.Information("Loading ItemCacheHelper");

        // Sigs valid as of 6.41

        if (Plugin.SigScanner.TryScanText("E8 ?? ?? ?? ?? 4C 8B 0D ?? ?? ?? ?? 41 B2 01", out var setKeyItemPtr))
        {
            SetKeyItemHook = Hook<SetKeyItem>.FromAddress(setKeyItemPtr, SetKeyItemDetour);
            SetKeyItemHook.Enable();
        }
        else
        {
            PluginLog.Error("Failed to hook SetKeyItem!");
            LoadFailure();

            return;
        }

        if (Plugin.SigScanner.TryScanText("E8 ?? ?? ?? ?? 0F B6 43 07", out var unsetKeyItemPtr))
        {
            UnsetKeyItemHook = Hook<UnsetKeyItem>.FromAddress(unsetKeyItemPtr, UnsetKeyItemDetour);
            UnsetKeyItemHook.Enable();
        }
        else
        {
            PluginLog.Error("Failed to hook UnsetKeyItem!");
            LoadFailure();

            return;
        }

        if (Plugin.SigScanner.TryScanText("E8 ?? ?? ?? ?? 84 C0 74 07 C6 87 ?? ?? ?? ?? ?? 83 BF", out var modifyKeyItemPtr))
        {
            ModifyKeyItemHook = Hook<ModifyKeyItem>.FromAddress(modifyKeyItemPtr, ModifyKeyItemDetour);
            ModifyKeyItemHook.Enable();
        }
        else
        {
            PluginLog.Error("Failed to hook ModifyKeyItemHook!");
            LoadFailure();

            return;
        }

        _loadState = LoadState.OpcodesLoading;

        LoadOpcodes();
    }

    public static void Initialize() { Instance = new ItemCacheHelper(); }

    public static ItemCacheHelper Instance { get; private set; } = null!;

    ~ItemCacheHelper() { Dispose(false); }

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

        _loadState = LoadState.Unloaded;
        SetKeyItemHook?.Disable();
        UnsetKeyItemHook?.Disable();
        ModifyKeyItemHook?.Disable();
        Plugin.GameNetwork.NetworkMessage -= GameNetworkOnNetworkMessage;

        Instance = null!;
    }

    #endregion

    enum LoadState
    {
        Init,
        OpcodesLoading,
        Finished,
        Error,
        Unloaded
    }

    private async void LoadOpcodes()
    {
        try
        {
            var client = new HttpClient();
            var content = await client.GetStringAsync("https://raw.githubusercontent.com/karashiiro/FFXIVOpcodes/master/opcodes.min.json");
            ExtractOpCode(content);
        }
        catch (Exception e)
        {
            PluginLog.Warning("Failed to load opcodes", e);
            LoadFailure();
        }

        for (var i = 0; i < opcodeNames.Length; i++)
        {
            var name = opcodeNames[i];

            if (_serverOpcodes.TryGetValue(name, out var serverValue))
            {
                opcodeValues[i] = serverValue;
            } else             if (_clientOpCodes.TryGetValue(name, out var clientValue))
            {
                opcodeValues[i] = clientValue;
            }
            else
            {
                PluginLog.Warning($"Unable to find opcode for {name}");
                LoadFailure();
                return;
            }
        }

        Plugin.GameNetwork.NetworkMessage += GameNetworkOnNetworkMessage;

        _loadState = LoadState.Finished;
        PluginLog.Information("ItemCacheHelper loaded");
    }

    private void GameNetworkOnNetworkMessage(IntPtr dataPtr, ushort opCode, uint sourceActorId, uint targetActorId, NetworkMessageDirection direction)
    {
        // var codes = opcodeValues.Select(s => s.ToString()).ToList();
        // PluginLog.Information($"[[[ {string.Join(",", codes)}");

        var index = Array.IndexOf(opcodeValues, opCode);

        if (index < 0)
            return;

        PluginLog.Information($"@@@ {opcodeNames[index]} ({opcodeValues[index]})");

        InvalidateInventoryCache();
    }

    private void LoadFailure()
    {
        _loadState = LoadState.Error;
        SetKeyItemHook?.Disable();
        UnsetKeyItemHook?.Disable();
    }

    private void SetKeyItemDetour(IntPtr agent, ushort slotId, int itemId)
    {
        PluginLog.Information($"= SetKeyItemDetour({slotId}, {itemId})");
        InvalidateInventoryCache();
        SetKeyItemHook!.Original.Invoke(agent, slotId, itemId);
    }

    private void UnsetKeyItemDetour(IntPtr agent, ushort slotId)
    {
        PluginLog.Information($"= UnsetKeyItemDetour({slotId})");
        InvalidateInventoryCache();
        UnsetKeyItemHook!.Original.Invoke(agent, slotId);
    }

    private char ModifyKeyItemDetour(IntPtr agent)
    {
        PluginLog.Information($"= ModifyKeyItemDetour()");
        InvalidateInventoryCache();
        return ModifyKeyItemHook!.Original.Invoke(agent);
    }


    private void InvalidateInventoryCache()
    {
        if (_loadState != LoadState.Finished)
        {
            return;
        }

        // throw new NotImplementedException();
    }

    private void ExtractOpCode(string body)
    {
        var regions = JsonConvert.DeserializeObject<List<OpcodeRegion>>(body);

        if (regions == null)
            throw new Exception("No regions found in opcode list");

        var region = regions.Find(r => r.Region == "Global");

        if (region?.Lists == null)
            throw new Exception("No global region found in opcode list");

        if (!region.Lists.TryGetValue("ServerZoneIpcType", out List<OpcodeList>? serverZoneIpcTypes))
            throw new Exception("No ServerZoneIpcType in opcode list");

        if (!region.Lists.TryGetValue("ClientZoneIpcType", out List<OpcodeList>? clientZoneIpcTypes))
            throw new Exception("No ServerZoneIpcType in opcode list");

        var newOpCodes = new Dictionary<string, ushort>();

        foreach (var opcode in serverZoneIpcTypes)
        {
            newOpCodes[opcode.Name] = opcode.Opcode;
        }

        var newClientOpCodes = new Dictionary<string, ushort>();

        foreach (var opcode in clientZoneIpcTypes)
        {
            newClientOpCodes[opcode.Name] = opcode.Opcode;
        }

        _serverOpcodes = newOpCodes;
        _clientOpCodes = newClientOpCodes;
    }
}

#pragma warning disable 8618
public class OpcodeRegion
{
    public string Version { get; set; } = null!;
    public string Region { get; set; }
    public Dictionary<string, List<OpcodeList>>? Lists { get; set; }
}

public class OpcodeList
{
    public string Name { get; set; } = null!;
    public ushort Opcode { get; set; }
}
#pragma warning restore 8618

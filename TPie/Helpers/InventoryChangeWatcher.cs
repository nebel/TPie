using System;
using System.Collections.Generic;
using System.Net.Http;
using Dalamud.Game.Network;
using Dalamud.Hooking;
using Dalamud.Logging;
using Newtonsoft.Json;

namespace TPie.Helpers;

public class InventoryChangeWatcher
{
    private LoadState _loadState = LoadState.Init;
    private ChangeState _changeState = ChangeState.Unknown;

    private Dictionary<string, ushort> _serverOpcodes = new();
    private Dictionary<string, ushort> _clientOpCodes = new();

    private delegate void SetKeyItem(IntPtr agent, ushort slotId, int itemId);
    private delegate void UnsetKeyItem(IntPtr agent, ushort slotId);
    private delegate char ModifyKeyItem(IntPtr agent);
    private Hook<SetKeyItem>? SetKeyItemHook { get; }
    private Hook<UnsetKeyItem>? UnsetKeyItemHook { get; }
    private Hook<ModifyKeyItem>? ModifyKeyItemHook { get; }

    private static readonly string[] OpcodeNames =
    {
        "InventoryActionAck",
        // "InventoryModifyHandler",
        // "UpdateInventorySlot",
        // "InventoryTransaction",
        "InventoryTransactionFinish",
    };
    private static readonly ushort[] OpcodeValues = new ushort[OpcodeNames.Length];

    #region singleton

    private InventoryChangeWatcher()
    {
        // Sigs valid as of 6.41

        if (Plugin.SigScanner.TryScanText("E8 ?? ?? ?? ?? 4C 8B 0D ?? ?? ?? ?? 41 B2 01", out var setKeyItemPtr))
        {
            SetKeyItemHook = Hook<SetKeyItem>.FromAddress(setKeyItemPtr, SetKeyItemDetour);
            SetKeyItemHook.Enable();
        }
        else
        {
            PluginLog.Warning("Failed to hook SetKeyItem!");
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
            PluginLog.Warning("Failed to hook UnsetKeyItem!");
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
            PluginLog.Warning("Failed to hook ModifyKeyItemHook!");
            LoadFailure();

            return;
        }

        _loadState = LoadState.OpcodesLoading;

        LoadOpcodes();
    }

    public static void Initialize() { Instance = new InventoryChangeWatcher(); }

    public static InventoryChangeWatcher Instance { get; private set; } = null!;

    ~InventoryChangeWatcher() { Dispose(false); }

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

    private enum LoadState
    {
        Init,
        OpcodesLoading,
        Finished,
        Error,
        Unloaded
    }

    private enum ChangeState
    {
        Unchanged,
        Changed,
        Unknown
    }

    private void InventoryChanged()
    {
        _changeState = ChangeState.Changed;
    }

    public bool CheckAndReset()
    {
        // Check the current change flag, returning whether an inventory scan should proceed (true) or not (false).
        // Assumes that the caller will proceed and therefore resets the change state if needed.
        switch (_changeState) {
            case ChangeState.Unchanged:
                return false;
            case ChangeState.Changed:
                _changeState = ChangeState.Unchanged;
                PluginLog.Debug("Inventory changed since last check, proceeding with rebuild");
                return true;
            case ChangeState.Unknown:
            default:
                return true;
        }
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

        for (var i = 0; i < OpcodeNames.Length; i++)
        {
            var name = OpcodeNames[i];

            if (_serverOpcodes.TryGetValue(name, out var serverValue))
            {
                OpcodeValues[i] = serverValue;
            } else             if (_clientOpCodes.TryGetValue(name, out var clientValue))
            {
                OpcodeValues[i] = clientValue;
            }
            else
            {
                PluginLog.Warning($"Unable to find opcode for {name}");
                LoadFailure();
                return;
            }
        }

        Plugin.GameNetwork.NetworkMessage += GameNetworkOnNetworkMessage;

        _changeState = ChangeState.Changed;
        _loadState = LoadState.Finished;
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

    private void LoadFailure()
    {
        PluginLog.Warning("Unable to initialize inventory change watcher. Inventory will be checked on each frame update.");

        _loadState = LoadState.Error;
        SetKeyItemHook?.Disable();
        UnsetKeyItemHook?.Disable();
    }

    // Handlers/Hooks

    private void GameNetworkOnNetworkMessage(IntPtr dataPtr, ushort opCode, uint sourceActorId, uint targetActorId, NetworkMessageDirection direction)
    {
        if (_loadState == LoadState.Finished)
        {
            var index = Array.IndexOf(OpcodeValues, opCode);
            if (index < 0)
                return;

            PluginLog.Debug($"@@@ {OpcodeNames[index]} ({OpcodeValues[index]}) :: {sourceActorId}, {targetActorId}, {direction}");
            InventoryChanged();
        }
    }

    private void SetKeyItemDetour(IntPtr agent, ushort slotId, int itemId)
    {
        if (_loadState == LoadState.Finished)
        {
            PluginLog.Debug($"= SetKeyItemDetour({slotId}, {itemId})");
            InventoryChanged();
        }

        SetKeyItemHook!.Original.Invoke(agent, slotId, itemId);
    }

    private void UnsetKeyItemDetour(IntPtr agent, ushort slotId)
    {
        if (_loadState == LoadState.Finished)
        {
            PluginLog.Debug($"= UnsetKeyItemDetour({slotId})");
            InventoryChanged();
        }

        UnsetKeyItemHook!.Original.Invoke(agent, slotId);
    }

    private char ModifyKeyItemDetour(IntPtr agent)
    {
        if (_loadState == LoadState.Finished)
        {
            PluginLog.Debug($"= ModifyKeyItemDetour()");
            InventoryChanged();
        }
        return ModifyKeyItemHook!.Original.Invoke(agent);
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

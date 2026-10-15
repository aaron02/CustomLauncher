using System.Diagnostics;
using System.Media;

namespace AscEmu.Launcher.Patching;

public sealed class RegionKeyMonitor : IDisposable
{
    const int RecordSize = 0x28;
    const int KeyOffset = 0x04;
    const int KeySize = 32;
    const int FlagOffset = 0x24;
    const int RecordCount = 12;
    const long MaximumRegionSize = 64L * 1024L * 1024L;

    readonly nint _processHandle;
    readonly int _processId;
    readonly bool _playSound;
    readonly CancellationTokenSource _cts = new();
    bool _playedPatchSound;

    public event Action<string>? StatusChanged;
    public event Action? Patched;

    public RegionKeyMonitor(nint processHandle, int processId, bool playSound)
    {
        _processHandle = processHandle;
        _processId = processId;
        _playSound = playSound;
    }

    public void Start() => _ = Task.Run(() => RunAsync(_cts.Token));

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }

    async Task RunAsync(CancellationToken token)
    {
        var memory = new ProcessMemory(_processHandle);
        nint tableBase = 0;
        var announcedWaiting = false;

        StatusChanged?.Invoke("Attached to WowB.exe; waiting for region-key table...");

        while (!token.IsCancellationRequested)
        {
            try
            {
                if (!IsProcessAlive())
                    return;

                if (tableBase != 0 && !ValidateTableAt(memory, tableBase))
                {
                    StatusChanged?.Invoke("Region-key table moved/rebuilt; rescanning...");
                    tableBase = 0;
                    announcedWaiting = false;
                }

                if (tableBase == 0)
                {
                    var matches = FindTables(memory);
                    if (matches.Count == 1)
                    {
                        tableBase = matches[0];
                        StatusChanged?.Invoke($"Found RegionGroup 1..12 table at 0x{tableBase.ToInt64():X}");
                        announcedWaiting = false;
                    }
                    else if (matches.Count > 1)
                    {
                        StatusChanged?.Invoke($"Refusing to patch: found {matches.Count} exact region-key tables");
                    }
                    else if (!announcedWaiting)
                    {
                        StatusChanged?.Invoke("Region keys are not built yet; listener is waiting...");
                        announcedWaiting = true;
                    }
                }

                if (tableBase != 0 && TableNeedsPatch(memory, tableBase))
                {
                    if (PatchTable(memory, tableBase))
                    {
                        StatusChanged?.Invoke("Patched all 12 Forever region public keys");
                        if (_playSound && !_playedPatchSound)
                        {
                            try { SystemSounds.Asterisk.Play(); } catch { }
                            _playedPatchSound = true;
                        }
                        Patched?.Invoke();
                    }
                    else
                    {
                        tableBase = 0;
                    }
                }

                await Task.Delay(250, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                tableBase = 0;
                await Task.Delay(500, token);
            }
        }
    }

    List<nint> FindTables(ProcessMemory memory)
    {
        var matches = new List<nint>();
        foreach (var region in memory.ReadableRegions())
        {
            if (!IsWritable(region.Protect) || region.Size < RecordCount * RecordSize || region.Size > MaximumRegionSize)
                continue;

            if (region.Size > int.MaxValue)
                continue;

            var bytes = new byte[(int)region.Size];
            if (!memory.Read(region.Base, bytes))
                continue;

            for (var offset = 0; offset + RecordCount * RecordSize <= bytes.Length; offset += 4)
            {
                if (!ValidateTableBytes(bytes.AsSpan(offset)))
                    continue;

                matches.Add(region.Base + offset);
                offset += RecordCount * RecordSize - 4;
            }
        }
        return matches;
    }

    static bool ValidateTableBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < RecordCount * RecordSize)
            return false;

        for (var index = 0; index < RecordCount; ++index)
        {
            var record = bytes.Slice(index * RecordSize, RecordSize);
            if (BitConverter.ToUInt32(record[..4]) != (uint)(index + 1))
                return false;
            if (record[FlagOffset] > 1)
                return false;
        }
        return true;
    }

    static bool ValidateTableAt(ProcessMemory memory, nint tableBase)
    {
        var bytes = new byte[RecordCount * RecordSize];
        return memory.Read(tableBase, bytes) && ValidateTableBytes(bytes);
    }

    static bool TableNeedsPatch(ProcessMemory memory, nint tableBase)
    {
        for (var index = 0; index < RecordCount; ++index)
        {
            var current = new byte[KeySize];
            var keyAddress = tableBase + index * RecordSize + KeyOffset;
            if (!memory.Read(keyAddress, current) || !current.SequenceEqual(ForeverPatchProfile.AscEmuEd25519PublicKey))
                return true;
        }
        return false;
    }

    static bool PatchTable(ProcessMemory memory, nint tableBase)
    {
        for (var index = 0; index < RecordCount; ++index)
        {
            var keyAddress = tableBase + index * RecordSize + KeyOffset;
            if (!memory.Write(keyAddress, ForeverPatchProfile.AscEmuEd25519PublicKey))
                return false;
        }
        return true;
    }

    static bool IsWritable(uint protection)
    {
        if ((protection & 0x100u) != 0)
            return false;
        return (protection & 0xFFu) is 0x04u or 0x08u or 0x40u or 0x80u;
    }

    bool IsProcessAlive()
    {
        try { return !Process.GetProcessById(_processId).HasExited; }
        catch { return false; }
    }
}

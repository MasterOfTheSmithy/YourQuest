#if UNITY_EDITOR_WIN
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

// note: Explicit R2 diagnostics query the current Unity process's DXGI budget; no device, reservation, eviction, cache or graphics setting is changed.
internal sealed class YQR2GpuBudgetObservation : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryInfo
    {
        public ulong budget, currentUsage, availableForReservation, currentReservation;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct AdapterDescription
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string description;
        public uint vendorId, deviceId, subsystemId, revision;
        public UIntPtr dedicatedVideoMemory, dedicatedSystemMemory, sharedSystemMemory;
        public uint luidLow;
        public int luidHigh;
        public uint flags;
    }

    private struct Reading
    {
        public long beforeTicks, afterTicks, utcTicks;
        public int unityFrame, phase, localResult, nonLocalResult;
        public MemoryInfo local, nonLocal;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int EnumerateAdapter(IntPtr factory, uint index, out IntPtr adapter);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int DescribeAdapter(IntPtr adapter, out AdapterDescription description);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int QueryMemory(IntPtr adapter, uint nodeIndex, uint segmentGroup, out MemoryInfo info);
    [DllImport("dxgi.dll", ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern int CreateDXGIFactory1(ref Guid interfaceId, out IntPtr factory);

    private IntPtr adapter;
    private readonly QueryMemory query;
    private readonly AdapterDescription description;
    private readonly Reading[] readings = new Reading[256];
    private int count;
    private int dropped;
    private long nextSampleTicks;

    private YQR2GpuBudgetObservation(IntPtr adapter, AdapterDescription description)
    {
        this.adapter = adapter;
        this.description = description;
        // note: Verified SDK COM slot 14 is IDXGIAdapter3.QueryVideoMemoryInfo; cache the read-only delegate once, not per sample.
        query = Bind<QueryMemory>(adapter, 14);
    }

    private static T Bind<T>(IntPtr owner, int slot) where T : Delegate
    {
        IntPtr table = Marshal.ReadIntPtr(owner);
        return Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(table, slot * IntPtr.Size));
    }

    internal static bool TryCreate(out YQR2GpuBudgetObservation observation, out string failure)
    {
        observation = null;
        failure = string.Empty;
        IntPtr factory = IntPtr.Zero;
        IntPtr selected = IntPtr.Zero;
        try
        {
            // note: Match exactly one hardware adapter to Unity's active device instead of assuming enumeration index zero or the Windows counter's adapter ordinal.
            Guid factoryId = new Guid("770aae78-f26f-4dba-a829-253c83d1b387");
            Marshal.ThrowExceptionForHR(CreateDXGIFactory1(ref factoryId, out factory));
            EnumerateAdapter enumerate = Bind<EnumerateAdapter>(factory, 12);
            AdapterDescription selectedDescription = default;
            for (uint index = 0; index < 16; index++)
            {
                int result = enumerate(factory, index, out IntPtr candidate);
                if (result == unchecked((int)0x887A0002)) break;
                Marshal.ThrowExceptionForHR(result);
                try
                {
                    Marshal.ThrowExceptionForHR(Bind<DescribeAdapter>(candidate, 10)(candidate, out AdapterDescription candidateDescription));
                    if (candidateDescription.vendorId != (uint)SystemInfo.graphicsDeviceVendorID ||
                        candidateDescription.deviceId != (uint)SystemInfo.graphicsDeviceID ||
                        !string.Equals(candidateDescription.description, SystemInfo.graphicsDeviceName, StringComparison.OrdinalIgnoreCase)) continue;
                    if (selected != IntPtr.Zero) throw new InvalidOperationException("Multiple DXGI adapters match the active Unity GPU");
                    Guid adapterId = new Guid("645967a4-1392-4310-a798-8053ce3e93fd");
                    Marshal.ThrowExceptionForHR(Marshal.QueryInterface(candidate, ref adapterId, out selected));
                    selectedDescription = candidateDescription;
                }
                finally { if (candidate != IntPtr.Zero) Marshal.Release(candidate); }
            }
            if (selected == IntPtr.Zero) throw new InvalidOperationException("No IDXGIAdapter3 matches the active Unity GPU");
            observation = new YQR2GpuBudgetObservation(selected, selectedDescription);
            selected = IntPtr.Zero;
            return true;
        }
        catch (Exception exception)
        {
            failure = exception.GetType().Name + ": " + exception.Message;
            return false;
        }
        finally
        {
            if (selected != IntPtr.Zero) Marshal.Release(selected);
            if (factory != IntPtr.Zero) Marshal.Release(factory);
        }
    }

    internal void Record(int unityFrame, bool coast, bool force = false)
    {
        if (adapter == IntPtr.Zero) return;
        long now = Stopwatch.GetTimestamp();
        if (!force && now < nextSampleTicks) return;
        nextSampleTicks = now + Stopwatch.Frequency / 4;
        if (count == readings.Length) { dropped++; return; }
        // note: Fixed-capacity, four-per-second readings carry HRESULT and before/after QPC bounds; a sample after a stall is not silently assigned to the preceding stalled frame.
        Reading reading = new Reading { unityFrame = unityFrame, phase = coast ? 1 : 0, utcTicks = DateTime.UtcNow.Ticks, beforeTicks = now };
        reading.localResult = query(adapter, 0, 0, out reading.local);
        reading.nonLocalResult = query(adapter, 0, 1, out reading.nonLocal);
        reading.afterTicks = Stopwatch.GetTimestamp();
        readings[count++] = reading;
    }

    internal void Write(string path)
    {
        // note: Formatting occurs after the measured window or in an explicit Edit-only probe; CurrentUsage versus Budget is distinct from Unity allocation estimates and does not alone prove eviction caused a frame.
        using (var writer = new StreamWriter(path, false, System.Text.Encoding.UTF8))
        {
            writer.WriteLine("# DXGI current Unity process budget utc=" + DateTime.UtcNow.ToString("O") + " assemblyMvid=" + typeof(YQR2GpuBudgetObservation).Assembly.ManifestModule.ModuleVersionId +
                " adapter=" + description.description + " vendor=" + description.vendorId + " device=" + description.deviceId +
                " luid=" + unchecked((uint)description.luidHigh).ToString("x8") + ":" + description.luidLow.ToString("x8") + " dedicatedVideoBytes=" + description.dedicatedVideoMemory.ToUInt64() +
                " qpcFrequency=" + Stopwatch.Frequency + " count=" + count + " dropped=" + dropped + "; read-only diagnostic, not clean certification or an eviction trace.");
            writer.WriteLine("unityFrame\tphase\tutcTicks\tqpcBefore\tqpcAfter\tlocalHresult\tlocalBudget\tlocalUsage\tlocalAvailableReservation\tlocalReservation\tnonLocalHresult\tnonLocalBudget\tnonLocalUsage\tnonLocalAvailableReservation\tnonLocalReservation");
            for (int index = 0; index < count; index++)
            {
                Reading r = readings[index];
                writer.WriteLine(r.unityFrame + "\t" + r.phase + "\t" + r.utcTicks + "\t" + r.beforeTicks + "\t" + r.afterTicks + "\t" + r.localResult + "\t" +
                    r.local.budget + "\t" + r.local.currentUsage + "\t" + r.local.availableForReservation + "\t" + r.local.currentReservation + "\t" + r.nonLocalResult + "\t" +
                    r.nonLocal.budget + "\t" + r.nonLocal.currentUsage + "\t" + r.nonLocal.availableForReservation + "\t" + r.nonLocal.currentReservation);
            }
        }
    }

    public void Dispose()
    {
        // note: Release only the observer's COM reference; this never releases Unity's graphics device, scene resources or reservations.
        if (adapter == IntPtr.Zero) return;
        Marshal.Release(adapter);
        adapter = IntPtr.Zero;
    }
}
#endif

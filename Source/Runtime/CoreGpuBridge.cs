using System;
using System.Linq;
using System.Reflection;

namespace LocalTts
{
    /// <summary>
    /// Optional soft integration with the in-process GPU-memory stats channels, bound entirely by
    /// reflection so Local TTS carries no compile-time dependency on either receiver. The mod's VRAM
    /// footprint is reported to BOTH known channels when present:
    /// <list type="bullet">
    ///   <item><description>RimSynapse Core (legacy) — the instance <c>GpuStats</c> object returned by
    ///     the static <c>RimSynapse.SynapseClient.Gpu</c> property. Core is being restructured and this
    ///     channel may go away.</description></item>
    ///   <item><description>The standalone "GPU Monitor for NVIDIA GPUs" mod — the static type
    ///     <c>NvidiaGpuMonitor.GpuMonitorApi</c>, the durable target as Core's channel is retired.</description></item>
    /// </list>
    /// Both expose the same method shape — <c>UpsertConsumer(string, string, float, bool)</c> and
    /// <c>RemoveConsumer(string)</c> — so the reporter feeds them identically. Each target is resolved
    /// independently by scanning the loaded assemblies; when a target is absent (or misbehaves) its
    /// calls are silent no-ops and can never break TTS synthesis.
    /// </summary>
    internal static class CoreGpuBridge
    {
        private static bool _resolved;

        // RimSynapse Core (legacy): UpsertConsumer/RemoveConsumer are INSTANCE methods on the object
        // returned by the static SynapseClient.Gpu property.
        private static PropertyInfo _coreGpuProp; // static SynapseClient.Gpu -> GpuStats
        private static MethodInfo _coreUpsert;    // GpuStats.UpsertConsumer(string, string, float, bool)
        private static MethodInfo _coreRemove;    // GpuStats.RemoveConsumer(string)

        // Standalone NVIDIA GPU Monitor: UpsertConsumer/RemoveConsumer are STATIC methods on the type
        // (invoked with a null target instance).
        private static MethodInfo _monitorUpsert; // GpuMonitorApi.UpsertConsumer(string, string, float, bool)
        private static MethodInfo _monitorRemove; // GpuMonitorApi.RemoveConsumer(string)

        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            // --- RimSynapse Core (legacy) ---
            try
            {
                Type client = null;
                foreach (var asm in assemblies)
                {
                    try { client = asm.GetType("RimSynapse.SynapseClient", false); }
                    catch { client = null; }
                    if (client != null) break;
                }
                if (client != null) // Core present
                {
                    _coreGpuProp = client.GetProperty("Gpu", BindingFlags.Public | BindingFlags.Static);
                    Type gpuType = _coreGpuProp?.PropertyType;
                    if (gpuType != null)
                    {
                        _coreUpsert = gpuType.GetMethod("UpsertConsumer",
                            new[] { typeof(string), typeof(string), typeof(float), typeof(bool) });
                        _coreRemove = gpuType.GetMethod("RemoveConsumer", new[] { typeof(string) });
                    }
                }
                // Core absent → handles stay null → every Core call no-ops.
            }
            catch
            {
                // Any resolution failure leaves the Core handles null → Core calls no-op.
            }

            // --- Standalone NVIDIA GPU Monitor ---
            try
            {
                Type api = null;
                foreach (var asm in assemblies)
                {
                    try { api = asm.GetType("NvidiaGpuMonitor.GpuMonitorApi", false); }
                    catch { api = null; }
                    if (api != null) break;
                }
                if (api != null) // Monitor present
                {
                    _monitorUpsert = api.GetMethod("UpsertConsumer",
                        new[] { typeof(string), typeof(string), typeof(float), typeof(bool) });
                    _monitorRemove = api.GetMethod("RemoveConsumer", new[] { typeof(string) });
                }
                // Monitor absent → handles stay null → every Monitor call no-ops.
            }
            catch
            {
                // Any resolution failure leaves the Monitor handles null → Monitor calls no-op.
            }
        }

        /// <summary>
        /// Report (or update) this mod's VRAM row on every present channel (Core and/or the NVIDIA GPU
        /// Monitor). Each target is a silent no-op when absent, and failures never propagate.
        /// </summary>
        public static void UpsertConsumer(string modId, string label, float vramMb, bool resident)
        {
            Resolve();

            // RimSynapse Core (legacy): instance method on SynapseClient.Gpu.
            try
            {
                object gpu = _coreGpuProp?.GetValue(null);
                if (gpu != null && _coreUpsert != null)
                    _coreUpsert.Invoke(gpu, new object[] { modId, label, vramMb, resident });
            }
            catch
            {
                // Core present but the channel misbehaved — never let it break synthesis.
            }

            // Standalone NVIDIA GPU Monitor: static method (null target instance).
            try
            {
                if (_monitorUpsert != null)
                    _monitorUpsert.Invoke(null, new object[] { modId, label, vramMb, resident });
            }
            catch
            {
                // Monitor present but the channel misbehaved — never let it break synthesis.
            }
        }

        /// <summary>
        /// Drop this mod's VRAM row from every present channel (Core and/or the NVIDIA GPU Monitor).
        /// Each target is a silent no-op when absent, and failures never propagate.
        /// </summary>
        public static void RemoveConsumer(string modId)
        {
            Resolve();

            // RimSynapse Core (legacy): instance method on SynapseClient.Gpu.
            try
            {
                object gpu = _coreGpuProp?.GetValue(null);
                if (gpu != null && _coreRemove != null)
                    _coreRemove.Invoke(gpu, new object[] { modId });
            }
            catch
            {
                // Nothing to clean up if the channel is gone.
            }

            // Standalone NVIDIA GPU Monitor: static method (null target instance).
            try
            {
                if (_monitorRemove != null)
                    _monitorRemove.Invoke(null, new object[] { modId });
            }
            catch
            {
                // Nothing to clean up if the channel is gone.
            }
        }
    }
}

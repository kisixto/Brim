using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Brim;

// CPU and memory per app. Processes with the same name (Chrome's many helpers, etc.) are added up into one
// entry. CPU is the share of all cores used since the previous sample, so the first sample has none.
internal sealed record AppUsage(string Name, int Pid, double Cpu, long Memory);

internal sealed class ProcessSampler
{
    private Dictionary<int, (TimeSpan Cpu, DateTime At)> _last = new();

    public bool HasCpu { get; private set; }

    public List<AppUsage> Sample()
    {
        var now = DateTime.UtcNow;
        var next = new Dictionary<int, (TimeSpan, DateTime)>();
        var apps = new Dictionary<string, (double Cpu, long Memory, int Pid)>(StringComparer.OrdinalIgnoreCase);

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                if (process.Id == 0) continue; // "Idle" isn't an app
                string name;
                long memory;
                try { name = process.ProcessName; memory = process.WorkingSet64; }
                catch { continue; } // exited mid-scan

                double cpu = 0;
                try
                {
                    var used = process.TotalProcessorTime; // access denied for some protected system processes
                    next[process.Id] = (used, now);
                    if (_last.TryGetValue(process.Id, out var prev))
                    {
                        double elapsedMs = (now - prev.At).TotalMilliseconds;
                        if (elapsedMs > 0) cpu = (used - prev.Cpu).TotalMilliseconds / (elapsedMs * Environment.ProcessorCount);
                    }
                }
                catch { }

                apps[name] = apps.TryGetValue(name, out var sum)
                    ? (sum.Cpu + cpu, sum.Memory + memory, sum.Pid)
                    : (cpu, memory, process.Id);
            }
        }

        HasCpu = _last.Count > 0;
        _last = next;
        return apps.Select(kv => new AppUsage(kv.Key, kv.Value.Pid, Math.Max(0, kv.Value.Cpu), kv.Value.Memory)).ToList();
    }
}

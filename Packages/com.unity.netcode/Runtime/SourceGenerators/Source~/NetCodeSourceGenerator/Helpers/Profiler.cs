using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Unity.NetCode.Generators
{
    /// <summary>
    /// Simple hierarchical profiler
    /// Used to track some performance of our code-generation suite
    /// </summary>
    public class Profiler
    {
        public class Auto : IDisposable
        {
            public Auto(string name)
            {
                Profiler.Begin(name);
            }
            public void Dispose()
            {
                Profiler.End();
            }
        }

        private class Marker
        {
            public int parent;
            public int id;
            public string name;
            public long overheadTicks;
            public long ticks;
            public int count;
            public int depth;
            public List<int> children = new List<int>();
        }
        public class Record
        {
            public long totalTime;
            public int count;
        }

        private List<Marker> timers = new List<Marker>();
        private int currentId;

        //This is necessary since the instance is static but it can be called by multiple threads.
        private static ThreadLocal<Profiler> _instance = new ThreadLocal<Profiler>(() =>
        {
            return new Profiler();
        });

        private static Profiler instance => _instance.Value;

        Profiler()
        {
            Init();
        }

        static public void Initialize()
        {
            instance.Init();
        }
        static public void Begin(string marker)
        {
            instance.Start(marker);
        }
        static public void End()
        {
            instance.Stop();
        }

        static public string PrintStats(bool fullTiming=false)
        {
            return instance.CollectStats(fullTiming);
        }

        int GetChildId(string name)
        {
            foreach (var childId in timers[currentId].children)
            {
                if (timers[childId].name == name)
                    return childId;
            }

            return -1;
        }

        private void Init()
        {
            timers.Clear();
            timers.Add(new Marker
            {
                parent = 0,
                id = 0,
                name = "Total",
                overheadTicks = 0,
                ticks = Stopwatch.GetTimestamp(),
                count = 0,
                depth = 0
            });
            currentId = 0;
        }

        private void Start(string name)
        {
            var t1 = Stopwatch.GetTimestamp();
            var childId = GetChildId(name);
            if(childId < 0)
            {
                var marker = new Marker
                {
                    name = name,
                    id = timers.Count,
                    parent = timers[currentId].id,
                    ticks = 0,
                    count = 0,
                    depth = timers[currentId].depth + 1
                };
                timers[currentId].children.Add(marker.id);
                timers.Add(marker);
                childId = marker.id;
            }
            var t2 = Stopwatch.GetTimestamp();
            ++timers[childId].count;
            timers[childId].ticks -= t2;
            timers[childId].overheadTicks += t2 - t1;
            currentId = childId;
        }

        private void Stop()
        {
            var marker = timers[currentId];
            marker.ticks += Stopwatch.GetTimestamp();
            currentId = marker.parent;
        }

        string CollectStats(bool fullTiming)
        {
            timers[0].ticks = Stopwatch.GetTimestamp() - timers[0].ticks;
            var builder = new System.Text.StringBuilder();
            builder.AppendLine("Timing:");
            //Timers is a tree stored in depth first order
            builder.Append($"{timers[0].name}: {(1000.0*(timers[0].ticks - timers[0].overheadTicks))/Stopwatch.Frequency} msec\n");
            if (fullTiming)
            {
                for (int i = 1; i < timers.Count; ++i)
                {
                    var node = timers[i];
                    var s = $"{node.name}: {(1000.0*node.ticks)/Stopwatch.Frequency} msec ({node.count}) [{(1000.0*node.overheadTicks)/Stopwatch.Frequency}]\n";
                    builder.Append(s.PadLeft(node.depth*2 + s.Length));
                }
            }
            timers[0].ticks = Stopwatch.GetTimestamp();
            return builder.ToString();
        }
    }
}

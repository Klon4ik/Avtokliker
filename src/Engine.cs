using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace AvtoClicker
{
    public interface IActionSink { bool Execute(AutomationAction action); }

    // Each action has its own deadline. A single dispatcher keeps move/down/up atomic.
    public sealed class Schedule
    {
        public readonly AutomationAction[] Actions;
        public readonly long[] Counts;
        private readonly long[] next;
        public Schedule(IEnumerable<AutomationAction> source)
        {
            List<AutomationAction> enabled = new List<AutomationAction>();
            foreach (AutomationAction action in source)
            {
                action.Validate();
                if (action.Enabled) enabled.Add(action.Copy());
            }
            Actions = enabled.ToArray();
            Counts = new long[Actions.Length];
            next = new long[Actions.Length];
            for (int i = 0; i < Actions.Length; i++) next[i] = Actions[i].DelayMs;
        }
        public bool IsComplete
        {
            get
            {
                for (int i = 0; i < Actions.Length; i++)
                    if (Actions[i].RepeatCount == 0 || Counts[i] < Actions[i].RepeatCount) return false;
                return true;
            }
        }
        public int Dispatch(long now, Func<long> clock, Func<bool> stopped, IActionSink sink)
        {
            long nearest = now + 20;
            for (int i = 0; i < Actions.Length; i++)
            {
                if (stopped()) return 1;
                AutomationAction action = Actions[i];
                if (action.RepeatCount != 0 && Counts[i] >= action.RepeatCount) continue;
                if (now >= next[i])
                {
                    if (sink.Execute(action)) Interlocked.Increment(ref Counts[i]);
                    // Skip missed slots; never replay a burst after sleep or a slow target.
                    long finished = clock();
                    next[i] += ((Math.Max(0, finished - next[i]) / action.IntervalMs) + 1) * action.IntervalMs;
                }
                nearest = Math.Min(nearest, next[i]);
            }
            return (int)Math.Max(1, Math.Min(20, nearest - clock()));
        }
    }

    public sealed class AutomationEngine : IDisposable
    {
        private readonly ManualResetEvent stop = new ManualResetEvent(false);
        private readonly IActionSink sink;
        private Thread thread;
        private Schedule schedule;
        private volatile bool running;
        private volatile string error;
        public AutomationEngine(IActionSink sink) { this.sink = sink; }
        public bool Running { get { return running; } }
        public string Error { get { return error; } }
        public long[] Counts
        {
            get
            {
                Schedule current = schedule;
                if (current == null) return new long[0];
                long[] result = new long[current.Counts.Length];
                for (int i = 0; i < result.Length; i++) result[i] = Interlocked.Read(ref current.Counts[i]);
                return result;
            }
        }
        public void Start(IEnumerable<AutomationAction> actions)
        {
            Stop();
            schedule = new Schedule(actions);
            if (schedule.Actions.Length == 0) throw new InvalidOperationException(L10n.T("Увімкніть хоча б одну дію."));
            stop.Reset();
            error = null;
            running = true;
            thread = new Thread(Run) { IsBackground = true, Name = L10n.T("Автоклікер: планувальник") };
            thread.Start();
        }
        private void Run()
        {
            try
            {
                Stopwatch clock = Stopwatch.StartNew();
                while (!stop.WaitOne(0) && !schedule.IsComplete)
                {
                    int wait = schedule.Dispatch(clock.ElapsedMilliseconds, () => clock.ElapsedMilliseconds,
                        () => stop.WaitOne(0), sink);
                    stop.WaitOne(wait);
                }
            }
            catch (Exception ex) { error = ex.Message; }
            finally { running = false; }
        }
        public void Stop()
        {
            stop.Set();
            if (thread != null && thread != Thread.CurrentThread) thread.Join();
            thread = null;
            running = false;
        }
        public void Dispose() { Stop(); stop.Dispose(); }
    }
}

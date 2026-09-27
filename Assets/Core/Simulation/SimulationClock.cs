namespace RustPlus.Core.Simulation
{
    /// <summary>
    /// Counts fixed simulation steps (50 Hz, matching <c>Time.fixedDeltaTime</c> in ProjectSettings).
    /// Ticks give logs, telemetry and saves an engine-independent timeline for attribution and replay.
    /// </summary>
    public sealed class SimulationClock
    {
        public const float FixedDeltaSeconds = 0.02f;

        /// <summary>The clock driven by the running game, or null outside play (tests, editor).</summary>
        public static SimulationClock Active { get; set; }

        /// <summary>Tick of the active clock, or -1 when no clock is running.</summary>
        public static long CurrentTick => Active?.Tick ?? -1;

        public long Tick { get; private set; }

        public SimulationClock(long startTick = 0)
        {
            Tick = startTick;
        }

        public void Advance()
        {
            Tick++;
        }

        public double ElapsedSeconds => Tick * (double)FixedDeltaSeconds;
    }
}

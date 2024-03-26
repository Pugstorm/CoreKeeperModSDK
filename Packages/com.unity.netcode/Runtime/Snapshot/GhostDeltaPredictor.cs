using Unity.Mathematics;

namespace Unity.NetCode
{
    /// <summary>
    /// <para>
    /// For internal use only. Used by the ghost component serializer to calculate (predict)
    /// the new value for a field, given the two previous baseline values.
    /// </para>
    /// <para>
    /// This value provides a good estimate for current value of a variable when changes are linear or otherwise predictable.
    /// I.e. Small deltas have good compression ratios.
    /// </para>
    /// </summary>
    public struct GhostDeltaPredictor
    {
        private int predictFrac;
        private int applyFrac;

        /// <summary>
        /// Construct the predictor using the last three recent baselines ticks. The ticks are used to calculate
        /// the relative weight that is applied to the baseline values.
        /// </summary>
        /// <param name="tick">the current server tick</param>
        /// <param name="baseline0_tick"></param>
        /// <param name="baseline1_tick"></param>
        /// <param name="baseline2_tick"></param>
        public GhostDeltaPredictor(NetworkTick tick, NetworkTick baseline0_tick, NetworkTick baseline1_tick, NetworkTick baseline2_tick)
        {
            predictFrac = 16 * baseline0_tick.TicksSince(baseline1_tick) / baseline1_tick.TicksSince(baseline2_tick);
            applyFrac = 16 * tick.TicksSince(baseline0_tick) / baseline0_tick.TicksSince(baseline1_tick);
        }

        /// <summary>
        /// Calculate the predicted value for the given integer, using the previous three baselines.
        /// </summary>
        /// <param name="baseline0"></param>
        /// <param name="baseline1"></param>
        /// <param name="baseline2"></param>
        /// <returns></returns>
        public int PredictInt(int baseline0, int baseline1, int baseline2)
        {
            int delta = baseline1 - baseline2;
            int predictBaseline = baseline1 + delta * predictFrac / 16;
            delta = baseline0 - baseline1;
            if (math.abs(baseline0 - predictBaseline) >= math.abs(delta))
                return baseline0;
            return baseline0 + delta * applyFrac / 16;
        }

        /// <summary>
        /// Calculate the predicted value for the given long, using the previous three baselines.
        /// </summary>
        /// <param name="baseline0"></param>
        /// <param name="baseline1"></param>
        /// <param name="baseline2"></param>
        /// <returns></returns>
        public long PredictLong(long baseline0, long baseline1, long baseline2)
        {
            long delta = baseline1 - baseline2;
            long predictBaseline = baseline1 + delta * predictFrac / 16;
            delta = baseline0 - baseline1;
            if (math.abs(baseline0 - predictBaseline) >= math.abs(delta))
                return baseline0;
            return baseline0 + delta * applyFrac / 16;
        }
    }
}

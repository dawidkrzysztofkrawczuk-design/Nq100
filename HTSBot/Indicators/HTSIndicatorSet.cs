using cAlgo.API;
using cAlgo.API.Indicators;

namespace HTSBot.Indicators
{
    /// <summary>
    /// Initialises and holds every EMA indicator instance required by the strategy:
    ///   • Execution-TF fast/slow bands for entry signal logic
    ///   • HTF fast/slow bands for multi-timeframe trend filter
    ///   • Trailing-TF fast band for the EMA-trailing-stop feature
    ///
    /// All indicator objects are managed by cTrader and updated automatically
    /// on each bar close for their respective timeframes.
    /// </summary>
    public class HTSIndicatorSet
    {
        // ── Execution-timeframe EMA bands (entry signals) ──────────────────
        /// <summary>EMA(high, fastLen) — upper edge of the fast band.</summary>
        public ExponentialMovingAverage FastHigh { get; }

        /// <summary>EMA(low, fastLen) — lower edge of the fast band.</summary>
        public ExponentialMovingAverage FastLow { get; }

        /// <summary>EMA(high, slowLen) — upper edge of the slow band.</summary>
        public ExponentialMovingAverage SlowHigh { get; }

        /// <summary>EMA(low, slowLen) — lower edge of the slow band.</summary>
        public ExponentialMovingAverage SlowLow { get; }

        // ── Higher-timeframe EMA bands (trend filter) ──────────────────────
        /// <summary>HTF EMA(high, fastLen) — upper edge of the HTF fast band.</summary>
        public ExponentialMovingAverage HtfFastHigh { get; }

        /// <summary>HTF EMA(low, fastLen) — lower edge of the HTF fast band.</summary>
        public ExponentialMovingAverage HtfFastLow { get; }

        /// <summary>HTF EMA(high, slowLen) — upper edge of the HTF slow band.</summary>
        public ExponentialMovingAverage HtfSlowHigh { get; }

        /// <summary>HTF EMA(low, slowLen) — lower edge of the HTF slow band.</summary>
        public ExponentialMovingAverage HtfSlowLow { get; }

        // ── Trailing-stop EMA bands (typically shorter period) ─────────────
        /// <summary>EMA(high, trailLen) — used as trailing SL anchor for Short.</summary>
        public ExponentialMovingAverage TrailHigh { get; }

        /// <summary>EMA(low, trailLen) — used as trailing SL anchor for Long.</summary>
        public ExponentialMovingAverage TrailLow { get; }

        /// <param name="robot">The host cBot — provides the Indicators factory.</param>
        /// <param name="execBars">1-minute (execution) bars series.</param>
        /// <param name="htfBars">Higher-timeframe bars series.</param>
        /// <param name="fastLen">Fast EMA period (default 66).</param>
        /// <param name="slowLen">Slow EMA period (default 288).</param>
        /// <param name="trailLen">Trailing-stop EMA period (default 33).</param>
        public HTSIndicatorSet(
            Robot robot,
            Bars execBars,
            Bars htfBars,
            int fastLen,
            int slowLen,
            int trailLen)
        {
            // Execution TF
            FastHigh  = robot.Indicators.ExponentialMovingAverage(execBars.HighPrices, fastLen);
            FastLow   = robot.Indicators.ExponentialMovingAverage(execBars.LowPrices,  fastLen);
            SlowHigh  = robot.Indicators.ExponentialMovingAverage(execBars.HighPrices, slowLen);
            SlowLow   = robot.Indicators.ExponentialMovingAverage(execBars.LowPrices,  slowLen);

            // HTF TF
            HtfFastHigh = robot.Indicators.ExponentialMovingAverage(htfBars.HighPrices, fastLen);
            HtfFastLow  = robot.Indicators.ExponentialMovingAverage(htfBars.LowPrices,  fastLen);
            HtfSlowHigh = robot.Indicators.ExponentialMovingAverage(htfBars.HighPrices, slowLen);
            HtfSlowLow  = robot.Indicators.ExponentialMovingAverage(htfBars.LowPrices,  slowLen);

            // Trailing-stop TF (execution bars, shorter period)
            TrailHigh = robot.Indicators.ExponentialMovingAverage(execBars.HighPrices, trailLen);
            TrailLow  = robot.Indicators.ExponentialMovingAverage(execBars.LowPrices,  trailLen);
        }
    }
}

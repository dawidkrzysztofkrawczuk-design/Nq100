using cAlgo.API;
using cAlgo.API.Indicators;

namespace cAlgo.Robots.HTS.Indicators
{
    /// <summary>
    /// Zbiór wskaźników EMA potrzebnych przez SignalEngine i ExecutionHandler:
    /// szybka/wolna wstęga (high &amp; low) na execution TF i HTF, plus EMA trailing stop.
    /// Klasa działa jak adapter na <see cref="cAlgo.API.IIndicatorsAccessor"/>.
    /// </summary>
    public sealed class HTSIndicatorSet
    {
        /// <summary>EMA(high, fastLen) – execution TF.</summary>
        public ExponentialMovingAverage FastHigh { get; }

        /// <summary>EMA(low, fastLen) – execution TF.</summary>
        public ExponentialMovingAverage FastLow { get; }

        /// <summary>EMA(high, slowLen) – execution TF.</summary>
        public ExponentialMovingAverage SlowHigh { get; }

        /// <summary>EMA(low, slowLen) – execution TF.</summary>
        public ExponentialMovingAverage SlowLow { get; }

        /// <summary>EMA(high, fastLen) – HTF (filtr trendu).</summary>
        public ExponentialMovingAverage HtfFastHigh { get; }

        /// <summary>EMA(low, fastLen) – HTF (filtr trendu).</summary>
        public ExponentialMovingAverage HtfFastLow { get; }

        /// <summary>EMA(high, slowLen) – HTF (filtr trendu).</summary>
        public ExponentialMovingAverage HtfSlowHigh { get; }

        /// <summary>EMA(low, slowLen) – HTF (filtr trendu).</summary>
        public ExponentialMovingAverage HtfSlowLow { get; }

        /// <summary>EMA(high, trailLen) – execution TF, używana w trailing stopie short.</summary>
        public ExponentialMovingAverage TrailHigh { get; }

        /// <summary>EMA(low, trailLen) – execution TF, używana w trailing stopie long.</summary>
        public ExponentialMovingAverage TrailLow { get; }

        /// <summary>Bary execution TF (zwykle wykres bota).</summary>
        public Bars ExecutionBars { get; }

        /// <summary>Bary HTF (filtr trendu).</summary>
        public Bars HtfBars { get; }

        /// <summary>
        /// Inicjalizuje pełny komplet wskaźników HTS RAW v2.6.
        /// </summary>
        /// <param name="indicators">Akcesor wskaźników z klasy Robot.</param>
        /// <param name="executionBars">Bars użyte do obliczeń na execution TF (zwykle <see cref="cAlgo.API.Robot.Bars"/>).</param>
        /// <param name="htfBars">Bars HTF zwrócone przez <see cref="cAlgo.API.MarketData.GetBars"/>.</param>
        /// <param name="fastEmaLength">Długość szybkiej EMA wstęgi.</param>
        /// <param name="slowEmaLength">Długość wolnej EMA wstęgi.</param>
        /// <param name="trailEmaLength">Długość EMA dla trailing stopu.</param>
        public HTSIndicatorSet(
            IIndicatorsAccessor indicators,
            Bars executionBars,
            Bars htfBars,
            int fastEmaLength,
            int slowEmaLength,
            int trailEmaLength)
        {
            ExecutionBars = executionBars;
            HtfBars = htfBars;

            FastHigh = indicators.ExponentialMovingAverage(executionBars.HighPrices, fastEmaLength);
            FastLow = indicators.ExponentialMovingAverage(executionBars.LowPrices, fastEmaLength);
            SlowHigh = indicators.ExponentialMovingAverage(executionBars.HighPrices, slowEmaLength);
            SlowLow = indicators.ExponentialMovingAverage(executionBars.LowPrices, slowEmaLength);

            HtfFastHigh = indicators.ExponentialMovingAverage(htfBars.HighPrices, fastEmaLength);
            HtfFastLow = indicators.ExponentialMovingAverage(htfBars.LowPrices, fastEmaLength);
            HtfSlowHigh = indicators.ExponentialMovingAverage(htfBars.HighPrices, slowEmaLength);
            HtfSlowLow = indicators.ExponentialMovingAverage(htfBars.LowPrices, slowEmaLength);

            TrailHigh = indicators.ExponentialMovingAverage(executionBars.HighPrices, trailEmaLength);
            TrailLow = indicators.ExponentialMovingAverage(executionBars.LowPrices, trailEmaLength);
        }

        /// <summary>
        /// Czy wszystkie EMA są już rozgrzane (wartości skończone) na podanym shifcie historycznym.
        /// Używane jako guard w SignalEngine i RiskManagerze.
        /// </summary>
        /// <param name="shift">0 = bieżący bar, 1 = ostatni zamknięty bar (typowo 1).</param>
        public bool IsWarmedUp(int shift = 1)
        {
            if (HtfBars.Count <= shift) return false;
            return IsFinite(FastHigh.Result.Last(shift))
                && IsFinite(FastLow.Result.Last(shift))
                && IsFinite(SlowHigh.Result.Last(shift))
                && IsFinite(SlowLow.Result.Last(shift))
                && IsFinite(HtfFastHigh.Result.Last(shift))
                && IsFinite(HtfFastLow.Result.Last(shift))
                && IsFinite(HtfSlowHigh.Result.Last(shift))
                && IsFinite(HtfSlowLow.Result.Last(shift));
        }

        private static bool IsFinite(double v)
            => !double.IsNaN(v) && !double.IsInfinity(v);
    }
}

using cAlgo.API;
using cAlgo.API.Indicators;

namespace cAlgo.Robots.HTS.Indicators
{
    /// <summary>
    /// Zbiór wskaźników EMA potrzebnych przez SignalEngine i ExecutionHandler:
    /// szybka/wolna wstęga (high &amp; low) na execution TF i HTF, plus EMA trailing stop.
    ///
    /// Wskaźniki są wstrzykiwane przez konstruktor jako gotowe instancje – tworzy je
    /// klasa <c>HTSBot</c> (Robot), w kontekście której <c>Indicators.ExponentialMovingAverage(...)</c>
    /// jest dostępne bezpośrednio. Dzięki temu ten plik nie zależy od konkretnego typu
    /// akcesora wskaźników (<c>IIndicatorsAccessor</c> vs <c>IndicatorsAccessor</c> – różny
    /// w zależności od wersji cAlgo API).
    /// </summary>
    public sealed class HTSIndicatorSet
    {
        public ExponentialMovingAverage FastHigh { get; }
        public ExponentialMovingAverage FastLow { get; }
        public ExponentialMovingAverage SlowHigh { get; }
        public ExponentialMovingAverage SlowLow { get; }

        public ExponentialMovingAverage HtfFastHigh { get; }
        public ExponentialMovingAverage HtfFastLow { get; }
        public ExponentialMovingAverage HtfSlowHigh { get; }
        public ExponentialMovingAverage HtfSlowLow { get; }

        public ExponentialMovingAverage TrailHigh { get; }
        public ExponentialMovingAverage TrailLow { get; }

        public Bars ExecutionBars { get; }
        public Bars HtfBars { get; }

        public HTSIndicatorSet(
            Bars executionBars,
            Bars htfBars,
            ExponentialMovingAverage fastHigh,
            ExponentialMovingAverage fastLow,
            ExponentialMovingAverage slowHigh,
            ExponentialMovingAverage slowLow,
            ExponentialMovingAverage htfFastHigh,
            ExponentialMovingAverage htfFastLow,
            ExponentialMovingAverage htfSlowHigh,
            ExponentialMovingAverage htfSlowLow,
            ExponentialMovingAverage trailHigh,
            ExponentialMovingAverage trailLow)
        {
            ExecutionBars = executionBars;
            HtfBars = htfBars;
            FastHigh = fastHigh;
            FastLow = fastLow;
            SlowHigh = slowHigh;
            SlowLow = slowLow;
            HtfFastHigh = htfFastHigh;
            HtfFastLow = htfFastLow;
            HtfSlowHigh = htfSlowHigh;
            HtfSlowLow = htfSlowLow;
            TrailHigh = trailHigh;
            TrailLow = trailLow;
        }

        /// <summary>
        /// Czy wszystkie EMA są już rozgrzane (wartości skończone) na podanym shifcie historycznym.
        /// </summary>
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

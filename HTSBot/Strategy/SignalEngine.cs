using System;
using cAlgo.API;
using cAlgo.Robots.HTS.Indicators;
using cAlgo.Robots.HTS.Models;

namespace cAlgo.Robots.HTS.Strategy
{
    /// <summary>
    /// Silnik sygnałów wskaźnika HTS RAW v2.6 – Precision Touch.
    /// Pracuje na <em>ostatnim ZAMKNIĘTYM</em> barze (Last(1)) – nie repaintuje.
    ///
    /// Zwraca <see cref="TradeSignal"/>:
    ///   • LongClassic / ShortClassic – sygnał kontynuacji trendu (zamknięcie po właściwej stronie Kijun).
    ///   • LongHook   / ShortHook    – sygnał na "haku" (zamknięcie po przeciwnej stronie Kijun).
    ///   • None – brak warunków HTS RAW v2.6.
    /// </summary>
    public sealed class SignalEngine
    {
        private readonly HTSIndicatorSet _ind;
        private readonly int _kijunLength;
        private readonly bool _enableClassic;
        private readonly bool _enableHook;

        /// <summary>
        /// Konstruktor SignalEngine.
        /// </summary>
        /// <param name="indicators">Skonfigurowany komplet wskaźników HTS.</param>
        /// <param name="kijunLength">Lookback dla Kijun-Sen (Highest+Lowest)/2.</param>
        /// <param name="enableClassicSignals">Czy emitować sygnały Classic.</param>
        /// <param name="enableHookSignals">Czy emitować sygnały Hook.</param>
        public SignalEngine(
            HTSIndicatorSet indicators,
            int kijunLength,
            bool enableClassicSignals,
            bool enableHookSignals)
        {
            if (indicators == null) throw new ArgumentNullException(nameof(indicators));
            if (kijunLength < 1) throw new ArgumentOutOfRangeException(nameof(kijunLength));

            _ind = indicators;
            _kijunLength = kijunLength;
            _enableClassic = enableClassicSignals;
            _enableHook = enableHookSignals;
        }

        /// <summary>
        /// Wylicz sygnał dla ostatniego ZAMKNIĘTEGO bara execution TF.
        /// Zwraca <see cref="TradeSignal.None"/>, gdy:
        ///   • wskaźniki nie są jeszcze rozgrzane,
        ///   • dane historii za krótkie do wyliczenia Kijun,
        ///   • żaden warunek HTS RAW v2.6 nie jest spełniony.
        /// </summary>
        public TradeSignal Evaluate()
        {
            var execBars = _ind.ExecutionBars;
            if (execBars == null || execBars.Count <= _kijunLength + 1)
                return TradeSignal.None;

            if (!_ind.IsWarmedUp(shift: 1))
                return TradeSignal.None;

            double fH = _ind.FastHigh.Result.Last(1);
            double fL = _ind.FastLow.Result.Last(1);

            // Spec sekcja 6.1: HTF czytamy z Last(1), żeby uniknąć lookahead bias na HTF.
            double htfFastHigh = _ind.HtfFastHigh.Result.Last(1);
            double htfFastLow = _ind.HtfFastLow.Result.Last(1);
            double htfSlowHigh = _ind.HtfSlowHigh.Result.Last(1);
            double htfSlowLow = _ind.HtfSlowLow.Result.Last(1);

            double barClose = execBars.ClosePrices.Last(1);
            double barHigh = execBars.HighPrices.Last(1);
            double barLow = execBars.LowPrices.Last(1);
            DateTime barTime = execBars.OpenTimes.Last(1);

            double kijun = ComputeKijun(execBars, _kijunLength);
            if (double.IsNaN(kijun))
                return TradeSignal.None;

            bool isBull = htfFastLow > htfSlowHigh;
            bool isBear = htfFastHigh < htfSlowLow;

            bool validTouchLong = barLow <= fH && barLow >= fL && barClose > fH;
            bool validTouchShort = barHigh >= fL && barHigh <= fH && barClose < fL;

            bool longClassic = isBull && barClose > kijun && validTouchLong;
            bool shortClassic = isBear && barClose < kijun && validTouchShort;
            bool longHook = isBull && barClose < kijun && validTouchLong;
            bool shortHook = isBear && barClose > kijun && validTouchShort;

            if (!_enableClassic) { longClassic = false; shortClassic = false; }
            if (!_enableHook) { longHook = false; shortHook = false; }

            SignalType type =
                longClassic ? SignalType.LongClassic :
                shortClassic ? SignalType.ShortClassic :
                longHook ? SignalType.LongHook :
                shortHook ? SignalType.ShortHook :
                SignalType.None;

            if (type == SignalType.None)
                return TradeSignal.None;

            // Logika HTS gwarantuje wzajemną wykluczalność: Classic vs Hook na tym samym barze
            // bazuje na pozycji close vs Kijun. Long vs Short bazuje na trendzie HTF.
            return new TradeSignal(type, barClose, fH, fL, kijun, barTime);
        }

        /// <summary>
        /// Kijun-Sen = (Highest(High, n) + Lowest(Low, n)) / 2 liczone na zamkniętych barach (Last(1)..Last(n)).
        /// Zwraca double.NaN, jeśli historia jest za krótka.
        /// </summary>
        private static double ComputeKijun(Bars bars, int length)
        {
            if (bars.Count <= length) return double.NaN;

            double highest = double.NegativeInfinity;
            double lowest = double.PositiveInfinity;

            for (int shift = 1; shift <= length; shift++)
            {
                double h = bars.HighPrices.Last(shift);
                double l = bars.LowPrices.Last(shift);
                if (h > highest) highest = h;
                if (l < lowest) lowest = l;
            }

            return (highest + lowest) / 2d;
        }
    }
}

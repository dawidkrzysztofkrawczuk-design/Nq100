using System;
using cAlgo.API;
using HTSBot.Indicators;
using HTSBot.Models;

namespace HTSBot.Strategy
{
    /// <summary>
    /// Replicates the HTS RAW v2.6 signal logic in C#.
    ///
    /// On every bar close the engine evaluates:
    ///   1. Multi-timeframe trend (isBull / isBear)
    ///   2. Precision-touch condition on the fast band
    ///   3. Kijun-Sen position to classify Classic vs Hook signals
    ///
    /// Pine Script equivalent:
    ///   fH = ema(high, fastLen)  |  fL = ema(low, fastLen)
    ///   sH = ema(high, slowLen)  |  sL = ema(low, slowLen)
    ///   kj = sma((highest(kLen) + lowest(kLen)) / 2, 1)
    ///   isBull = fL > sH  (+ HTF filter)
    ///   isBear = fH < sL  (+ HTF filter)
    ///   validTouchLong  = low &lt;= fH AND low &gt;= fL AND close &gt; fH
    ///   validTouchShort = high &gt;= fL AND high &lt;= fH AND close &lt; fL
    /// </summary>
    public class SignalEngine
    {
        private readonly HTSIndicatorSet _ind;
        private readonly Bars _execBars;
        private readonly Bars _htfBars;
        private readonly int _kijunLen;
        private readonly bool _enableMtf;
        private readonly SignalFilter _filter;

        /// <param name="indicators">Pre-initialised indicator set.</param>
        /// <param name="execBars">Execution-timeframe bars.</param>
        /// <param name="htfBars">Higher-timeframe bars.</param>
        /// <param name="kijunLen">Kijun-Sen lookback period.</param>
        /// <param name="enableMtf">Whether the HTF trend filter is active.</param>
        /// <param name="filter">Which signal types to emit.</param>
        public SignalEngine(
            HTSIndicatorSet indicators,
            Bars execBars,
            Bars htfBars,
            int kijunLen,
            bool enableMtf,
            SignalFilter filter)
        {
            _ind       = indicators;
            _execBars  = execBars;
            _htfBars   = htfBars;
            _kijunLen  = kijunLen;
            _enableMtf = enableMtf;
            _filter    = filter;
        }

        /// <summary>
        /// Evaluates the last completed bar and returns the current signal.
        /// Must be called from <c>OnBar()</c> so that <c>Last(1)</c> is the just-closed bar.
        /// </summary>
        public TradeSignal GetSignal()
        {
            // All Last(1) calls refer to the bar that just closed (called from OnBar).
            double fH    = _ind.FastHigh.Result.Last(1);
            double fL    = _ind.FastLow.Result.Last(1);
            double sH    = _ind.SlowHigh.Result.Last(1);
            double sL    = _ind.SlowLow.Result.Last(1);
            double kj    = CalculateKijun();

            double barHigh  = _execBars.HighPrices.Last(1);
            double barLow   = _execBars.LowPrices.Last(1);
            double barClose = _execBars.ClosePrices.Last(1);

            bool isBull = IsBullTrend(fL, sH);
            bool isBear = IsBearTrend(fH, sL);

            // ── Precision-touch conditions ──────────────────────────────────
            // LONG : wick enters the band from above (low inside band) + close above band top
            bool validLong  = barLow  <= fH && barLow  >= fL && barClose > fH;
            // SHORT: wick enters the band from below (high inside band) + close below band bottom
            bool validShort = barHigh >= fL && barHigh <= fH && barClose < fL;

            // ── Signal classification ────────────────────────────────────────
            bool longClassic  = isBull && barClose > kj && validLong;
            bool shortClassic = isBear && barClose < kj && validShort;
            bool longHook     = isBull && barClose < kj && validLong;
            bool shortHook    = isBear && barClose > kj && validShort;

            // Apply signal-type filter
            if (_filter == SignalFilter.HookOnly)
            {
                longClassic  = false;
                shortClassic = false;
            }
            else if (_filter == SignalFilter.ClassicOnly)
            {
                longHook  = false;
                shortHook = false;
            }

            if (longClassic)
                return Build(SignalType.LongClassic,  TradeDirection.Long,  barClose, fH, fL);
            if (shortClassic)
                return Build(SignalType.ShortClassic, TradeDirection.Short, barClose, fH, fL);
            if (longHook)
                return Build(SignalType.LongHook,     TradeDirection.Long,  barClose, fH, fL);
            if (shortHook)
                return Build(SignalType.ShortHook,    TradeDirection.Short, barClose, fH, fL);

            return TradeSignal.NoSignal();
        }

        // ── Private helpers ─────────────────────────────────────────────────

        /// <summary>
        /// Bullish trend: fast-low band is entirely above slow-high band.
        /// When MTF is enabled, this condition is evaluated on the HTF bars.
        /// </summary>
        private bool IsBullTrend(double execFastLow, double execSlowHigh)
        {
            if (!_enableMtf)
                return execFastLow > execSlowHigh;

            double htfFl = _ind.HtfFastLow.Result.Last(1);
            double htfSh = _ind.HtfSlowHigh.Result.Last(1);
            return htfFl > htfSh;
        }

        /// <summary>
        /// Bearish trend: fast-high band is entirely below slow-low band.
        /// When MTF is enabled, this condition is evaluated on the HTF bars.
        /// </summary>
        private bool IsBearTrend(double execFastHigh, double execSlowLow)
        {
            if (!_enableMtf)
                return execFastHigh < execSlowLow;

            double htfFh = _ind.HtfFastHigh.Result.Last(1);
            double htfSl = _ind.HtfSlowLow.Result.Last(1);
            return htfFh < htfSl;
        }

        /// <summary>
        /// Kijun-Sen: midpoint of (Highest High, Lowest Low) over the last
        /// <see cref="_kijunLen"/> completed bars.
        /// Equivalent to Pine Script: sma((highest(high, kLen) + lowest(low, kLen)) / 2, 1)
        /// </summary>
        private double CalculateKijun()
        {
            double highest = double.MinValue;
            double lowest  = double.MaxValue;

            // Last(1)…Last(kijunLen) covers the last kijunLen completed bars.
            int safeLen = Math.Min(_kijunLen, _execBars.Count - 1);
            for (int i = 1; i <= safeLen; i++)
            {
                double h = _execBars.HighPrices.Last(i);
                double l = _execBars.LowPrices.Last(i);
                if (h > highest) highest = h;
                if (l < lowest)  lowest  = l;
            }

            return (highest + lowest) / 2.0;
        }

        /// <summary>Convenience builder for a valid <see cref="TradeSignal"/>.</summary>
        private static TradeSignal Build(
            SignalType type,
            TradeDirection dir,
            double entryPrice,
            double fH,
            double fL)
        {
            return new TradeSignal
            {
                Type          = type,
                Direction     = dir,
                EntryPrice    = entryPrice,
                FastBandHigh  = fH,
                FastBandLow   = fL
            };
        }
    }
}

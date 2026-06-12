using System;
using cAlgo.API;
using HTSBot.Models;

namespace HTSBot.Risk
{
    /// <summary>
    /// Centralises all risk-related decisions:
    ///   • Trading-hours window check
    ///   • Daily loss-limit and profit-target enforcement
    ///   • Position sizing via % account risk
    ///   • Stop-loss and take-profit price calculation (dollar OR % of entry)
    ///
    /// Position-sizing formula:
    ///   Volume = (Balance × RiskPercent/100) / (SL_price_distance × PriceValuePerUnit)
    ///   where PriceValuePerUnit = Symbol.TickValue / Symbol.TickSize
    ///
    /// Dollar-SL interpretation:
    ///   A "dollar SL" of $X means: for the symbol's minimum volume step, the
    ///   SL is placed X dollars away in account currency.
    ///   SL_price_distance = X × TickSize / (VolumeInUnitsStep × TickValue)
    ///   This keeps the formula unit-consistent and avoids circular dependency.
    /// </summary>
    public class RiskManager
    {
        private readonly Robot _robot;
        private readonly DailyStats _dailyStats;

        // ── Configurable properties (set from HTSBot parameters) ────────────
        public double RiskPercent              { get; set; } = 1.0;
        public SLTPType StopLossType           { get; set; } = SLTPType.Percent;
        public double StopLossValue            { get; set; } = 0.5;
        public SLTPType TakeProfitType         { get; set; } = SLTPType.Percent;
        public double TakeProfitValue          { get; set; } = 1.0;
        public double DailyLossLimit           { get; set; } = 0;
        public double DailyProfitTargetDollar  { get; set; } = 0;
        public double DailyProfitTargetPercent { get; set; } = 0;
        public int TradingStartHour            { get; set; } = 8;
        public int TradingStartMinute          { get; set; } = 0;
        public int TradingEndHour              { get; set; } = 20;
        public int TradingEndMinute            { get; set; } = 0;

        public RiskManager(Robot robot, DailyStats dailyStats)
        {
            _robot      = robot;
            _dailyStats = dailyStats;
        }

        // ── Gate checks ──────────────────────────────────────────────────────

        /// <summary>
        /// Returns true if <paramref name="serverTime"/> (UTC) falls within the
        /// configured [Start, End) trading window.
        /// </summary>
        public bool IsWithinTradingHours(DateTime serverTime)
        {
            int nowMinutes   = serverTime.Hour * 60 + serverTime.Minute;
            int startMinutes = TradingStartHour * 60 + TradingStartMinute;
            int endMinutes   = TradingEndHour   * 60 + TradingEndMinute;

            if (startMinutes <= endMinutes)
                return nowMinutes >= startMinutes && nowMinutes < endMinutes;

            // Overnight session (e.g. 22:00 – 06:00)
            return nowMinutes >= startMinutes || nowMinutes < endMinutes;
        }

        /// <summary>
        /// Returns true when neither a daily loss limit nor a profit target has
        /// been breached. Sets <see cref="DailyStats.IsTradingHalted"/> on first breach.
        /// </summary>
        public bool IsTradingAllowed()
        {
            if (_dailyStats.IsTradingHalted) return false;

            double pnl     = _dailyStats.RealizedPnL;
            double balance = _robot.Account.Balance;

            if (DailyLossLimit > 0 && pnl <= -DailyLossLimit)
            {
                Halt($"Daily loss limit reached: {pnl:F2} ≤ -{DailyLossLimit:F2}");
                return false;
            }

            if (DailyProfitTargetDollar > 0 && pnl >= DailyProfitTargetDollar)
            {
                Halt($"Daily profit target (${DailyProfitTargetDollar:F2}) reached: {pnl:F2}");
                return false;
            }

            if (DailyProfitTargetPercent > 0)
            {
                double target = balance * (DailyProfitTargetPercent / 100.0);
                if (pnl >= target)
                {
                    Halt($"Daily profit target ({DailyProfitTargetPercent:F2}%) reached: {pnl:F2}");
                    return false;
                }
            }

            return true;
        }

        // ── Price calculations ───────────────────────────────────────────────

        /// <summary>
        /// Calculates the stop-loss absolute price for a new position.
        /// </summary>
        public double CalculateStopLossPrice(TradeDirection direction, double entryPrice)
        {
            double dist = StopLossDistance(entryPrice);
            return direction == TradeDirection.Long
                ? entryPrice - dist
                : entryPrice + dist;
        }

        /// <summary>
        /// Calculates the take-profit absolute price for a new position.
        /// </summary>
        public double CalculateTakeProfitPrice(TradeDirection direction, double entryPrice)
        {
            double dist = TakeProfitDistance(entryPrice);
            return direction == TradeDirection.Long
                ? entryPrice + dist
                : entryPrice - dist;
        }

        /// <summary>
        /// Calculates normalised position volume in units using the % risk model.
        /// </summary>
        /// <param name="entryPrice">Expected fill price.</param>
        /// <param name="stopLossPrice">Stop-loss price (absolute).</param>
        public double CalculateVolume(double entryPrice, double stopLossPrice)
        {
            double riskAmount = _robot.Account.Balance * (RiskPercent / 100.0);
            double slDist     = Math.Abs(entryPrice - stopLossPrice);

            if (slDist < double.Epsilon)
            {
                _robot.Print("[RiskManager] SL distance is effectively zero – using minimum volume.");
                return _robot.Symbol.VolumeInUnitsMin;
            }

            // Monetary value of a 1-price-unit move for 1 unit of volume
            double priceValuePerUnit = _robot.Symbol.TickValue / _robot.Symbol.TickSize;
            double rawVolume = riskAmount / (slDist * priceValuePerUnit);

            return NormalizeVolume(rawVolume);
        }

        /// <summary>Accumulates closed P&amp;L for daily-limit tracking.</summary>
        public void UpdateDailyPnL(double closedPositionPnL)
        {
            _dailyStats.RealizedPnL += closedPositionPnL;
        }

        // ── Private helpers ──────────────────────────────────────────────────

        private double StopLossDistance(double entryPrice)
        {
            if (StopLossType == SLTPType.Dollar)
            {
                // Dollar SL: price distance for which 1 VolumeStep unit loses StopLossValue $
                double minVol            = _robot.Symbol.VolumeInUnitsStep;
                double priceValuePerUnit = _robot.Symbol.TickValue / _robot.Symbol.TickSize;
                return StopLossValue / (minVol * priceValuePerUnit);
            }
            // Percent SL: distance = entry × (SL% / 100)
            return entryPrice * (StopLossValue / 100.0);
        }

        private double TakeProfitDistance(double entryPrice)
        {
            if (TakeProfitType == SLTPType.Dollar)
            {
                double minVol            = _robot.Symbol.VolumeInUnitsStep;
                double priceValuePerUnit = _robot.Symbol.TickValue / _robot.Symbol.TickSize;
                return TakeProfitValue / (minVol * priceValuePerUnit);
            }
            return entryPrice * (TakeProfitValue / 100.0);
        }

        private double NormalizeVolume(double raw)
        {
            double step = _robot.Symbol.VolumeInUnitsStep;
            double min  = _robot.Symbol.VolumeInUnitsMin;
            double max  = _robot.Symbol.VolumeInUnitsMax;

            double normalised = Math.Floor(raw / step) * step;   // round DOWN to avoid over-risking
            return Math.Max(min, Math.Min(max, normalised));
        }

        private void Halt(string reason)
        {
            _dailyStats.IsTradingHalted = true;
            _robot.Print($"[RiskManager] Trading halted — {reason}");
        }
    }
}

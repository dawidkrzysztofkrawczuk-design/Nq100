using System;

namespace HTSBot.Models
{
    /// <summary>
    /// Tracks intraday P&amp;L and halt flags.
    /// Call <see cref="Reset"/> at the start of each new trading day.
    /// </summary>
    public class DailyStats
    {
        /// <summary>The UTC date this stats object covers.</summary>
        public DateTime Date { get; private set; }

        /// <summary>
        /// Cumulative realised P&amp;L for the day in account currency.
        /// Updated by <see cref="RiskManager"/> every time a position closes.
        /// </summary>
        public double RealizedPnL { get; set; }

        /// <summary>
        /// When true, the RiskManager will refuse to open any new positions.
        /// Set automatically when a daily limit is breached.
        /// </summary>
        public bool IsTradingHalted { get; set; }

        public DailyStats()
        {
            Date = DateTime.UtcNow.Date;
        }

        /// <summary>Resets all counters for a new trading day.</summary>
        public void Reset()
        {
            Date = DateTime.UtcNow.Date;
            RealizedPnL = 0;
            IsTradingHalted = false;
        }

        /// <summary>Returns true if <paramref name="currentTime"/> belongs to a different UTC day.</summary>
        public bool IsNewDay(DateTime currentTime) => currentTime.Date != Date;
    }
}

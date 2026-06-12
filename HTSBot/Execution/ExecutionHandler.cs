using System.Linq;
using cAlgo.API;
using HTSBot.Indicators;
using HTSBot.Models;
using HTSBot.Risk;

namespace HTSBot.Execution
{
    /// <summary>
    /// Handles all order-management operations:
    ///   • Opening positions with calculated SL/TP
    ///   • Updating the EMA-based trailing stop
    ///   • Emergency close-all on bot shutdown
    ///
    /// Trailing-stop logic (Pine Script reference):
    ///   LONG  → trail SL to TrailLow  (EMA of lows, typically period 33)
    ///   SHORT → trail SL to TrailHigh (EMA of highs, typically period 33)
    ///   SL may only move in the favourable direction (up for LONG, down for SHORT).
    /// </summary>
    public class ExecutionHandler
    {
        private readonly Robot _robot;
        private readonly RiskManager _riskMgr;
        private readonly string _label;
        private readonly bool _trailingEnabled;
        private readonly HTSIndicatorSet _ind;

        /// <param name="robot">The host cBot instance.</param>
        /// <param name="riskManager">Configured risk manager.</param>
        /// <param name="label">Unique label to tag all bot-managed positions.</param>
        /// <param name="trailingEnabled">Whether EMA trailing stop is active.</param>
        /// <param name="indicators">Indicator set (for trailing EMA values).</param>
        public ExecutionHandler(
            Robot robot,
            RiskManager riskManager,
            string label,
            bool trailingEnabled,
            HTSIndicatorSet indicators)
        {
            _robot           = robot;
            _riskMgr         = riskManager;
            _label           = label;
            _trailingEnabled = trailingEnabled;
            _ind             = indicators;
        }

        // ── Order placement ──────────────────────────────────────────────────

        /// <summary>
        /// Opens a market order for the given <paramref name="signal"/>.
        /// SL and TP are calculated by the <see cref="RiskManager"/> and submitted
        /// as pip distances so cTrader enforces them at the broker level.
        /// </summary>
        public void OpenPosition(TradeSignal signal)
        {
            var tradeType = signal.Direction == TradeDirection.Long
                ? TradeType.Buy
                : TradeType.Sell;

            // Use the live Ask/Bid as reference because ExecuteMarketOrder fills
            // at the current market price, not at the signal bar's close.
            double refPrice = tradeType == TradeType.Buy
                ? _robot.Symbol.Ask
                : _robot.Symbol.Bid;

            double slPrice = _riskMgr.CalculateStopLossPrice(signal.Direction, refPrice);
            double tpPrice = _riskMgr.CalculateTakeProfitPrice(signal.Direction, refPrice);
            double volume  = _riskMgr.CalculateVolume(refPrice, slPrice);

            double slPips = PriceToPips(refPrice, slPrice);
            double tpPips = PriceToPips(refPrice, tpPrice);

            string comment = $"Signal:{signal.Type}|Ref:{refPrice:F5}";

            _robot.Print(
                $"[ExecutionHandler] Opening {tradeType} | Vol:{volume} " +
                $"| SL:{slPrice:F5} ({slPips:F1}p) | TP:{tpPrice:F5} ({tpPips:F1}p)");

            TradeResult result = _robot.ExecuteMarketOrder(
                tradeType,
                _robot.Symbol.Name,
                volume,
                _label,
                slPips,
                tpPips,
                comment);

            if (!result.IsSuccessful)
            {
                _robot.Print(
                    $"[ExecutionHandler] Order FAILED — " +
                    $"Error: {result.Error} | {result.ErrorDescription}");
            }
        }

        // ── Trailing stop ────────────────────────────────────────────────────

        /// <summary>
        /// Moves the stop-loss of every bot-managed open position to the
        /// current trailing EMA band value, but only in the favourable direction.
        /// Should be called in OnBar() (EMA changes only on bar close) or OnTick()
        /// for intra-bar responsiveness.
        /// </summary>
        public void UpdateTrailingStop()
        {
            if (!_trailingEnabled) return;

            foreach (var position in _robot.Positions.Where(p => p.Label == _label))
            {
                double newSl;

                if (position.TradeType == TradeType.Buy)
                {
                    newSl = _ind.TrailLow.Result.Last(1);

                    // Only move SL upward (never worsen the stop)
                    if (position.StopLoss.HasValue && newSl <= position.StopLoss.Value)
                        continue;
                }
                else
                {
                    newSl = _ind.TrailHigh.Result.Last(1);

                    // Only move SL downward
                    if (position.StopLoss.HasValue && newSl >= position.StopLoss.Value)
                        continue;
                }

                TradeResult result = _robot.ModifyPosition(position, newSl, position.TakeProfit);

                if (!result.IsSuccessful)
                {
                    _robot.Print(
                        $"[ExecutionHandler] Failed to update trailing SL for " +
                        $"position #{position.Id} — {result.ErrorDescription}");
                }
                else
                {
                    _robot.Print(
                        $"[ExecutionHandler] Trailing SL updated → {newSl:F5} " +
                        $"(position #{position.Id})");
                }
            }
        }

        // ── Emergency helpers ────────────────────────────────────────────────

        /// <summary>
        /// Closes all positions tagged with this bot's label.
        /// Intended for OnStop() or circuit-breaker scenarios.
        /// </summary>
        public void CloseAllPositions(string reason = "")
        {
            foreach (var position in _robot.Positions.Where(p => p.Label == _label).ToList())
            {
                _robot.Print(
                    $"[ExecutionHandler] Closing position #{position.Id}" +
                    (string.IsNullOrEmpty(reason) ? "" : $" — Reason: {reason}"));

                _robot.ClosePosition(position);
            }
        }

        // ── Utility ──────────────────────────────────────────────────────────

        /// <summary>Converts a price distance to pips using the symbol's pip size.</summary>
        private double PriceToPips(double refPrice, double targetPrice)
        {
            return System.Math.Abs(refPrice - targetPrice) / _robot.Symbol.PipSize;
        }
    }
}

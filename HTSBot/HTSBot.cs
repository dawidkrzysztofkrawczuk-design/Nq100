using System.Linq;
using cAlgo.API;
using HTSBot.Execution;
using HTSBot.Indicators;
using HTSBot.Models;
using HTSBot.Risk;
using HTSBot.Strategy;

namespace HTSBot
{
    /// <summary>
    /// HTS RAW v2.6 – cTrader cBot
    ///
    /// Architecture layers
    /// ───────────────────
    ///   HTSBot (Orchestrator)
    ///     ├── HTSIndicatorSet  (Data / Indicator layer)
    ///     ├── SignalEngine     (Strategy layer)
    ///     ├── RiskManager      (Risk layer)
    ///     └── ExecutionHandler (Execution layer)
    ///
    /// Bar lifecycle
    /// ─────────────
    ///   OnBar()  — called at the close of each 1-minute bar
    ///              Evaluates signals, checks risk gates, opens positions.
    ///   OnTick() — called on every price tick
    ///              Updates EMA trailing stop if enabled.
    ///   OnStop() — closes any open positions tagged with the bot label.
    /// </summary>
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class HTSBot : Robot
    {
        // ════════════════════════════════════════════════════════════════════
        // PARAMETERS — EMA Settings
        // ════════════════════════════════════════════════════════════════════

        [Parameter("Fast EMA Period", Group = "EMA Settings",
            DefaultValue = 66, MinValue = 2, MaxValue = 1000)]
        public int FastEmaLength { get; set; }

        [Parameter("Slow EMA Period", Group = "EMA Settings",
            DefaultValue = 288, MinValue = 2, MaxValue = 2000)]
        public int SlowEmaLength { get; set; }

        [Parameter("Kijun-Sen Period", Group = "EMA Settings",
            DefaultValue = 26, MinValue = 2, MaxValue = 200)]
        public int KijunLength { get; set; }

        // ════════════════════════════════════════════════════════════════════
        // PARAMETERS — Trend Filter (MTF)
        // ════════════════════════════════════════════════════════════════════

        [Parameter("Enable MTF Trend Filter", Group = "Trend Filter",
            DefaultValue = true)]
        public bool EnableMTF { get; set; }

        [Parameter("HTF Timeframe", Group = "Trend Filter",
            DefaultValue = "Minute5")]
        public TimeFrame HTFTimeframe { get; set; }

        // ════════════════════════════════════════════════════════════════════
        // PARAMETERS — Signal Filter
        // ════════════════════════════════════════════════════════════════════

        [Parameter("Signal Filter", Group = "Signal Settings",
            DefaultValue = SignalFilter.Both)]
        public SignalFilter AllowedSignals { get; set; }

        // ════════════════════════════════════════════════════════════════════
        // PARAMETERS — Position Sizing & Stop-Loss / Take-Profit
        // ════════════════════════════════════════════════════════════════════

        [Parameter("Risk % per Trade", Group = "Risk Management",
            DefaultValue = 1.0, MinValue = 0.01, MaxValue = 100.0)]
        public double RiskPercent { get; set; }

        [Parameter("Stop Loss Type", Group = "Risk Management",
            DefaultValue = SLTPType.Percent)]
        public SLTPType StopLossType { get; set; }

        [Parameter("Stop Loss Value  ($ or %)", Group = "Risk Management",
            DefaultValue = 0.5, MinValue = 0.001)]
        public double StopLossValue { get; set; }

        [Parameter("Take Profit Type", Group = "Risk Management",
            DefaultValue = SLTPType.Percent)]
        public SLTPType TakeProfitType { get; set; }

        [Parameter("Take Profit Value  ($ or %)", Group = "Risk Management",
            DefaultValue = 1.0, MinValue = 0.001)]
        public double TakeProfitValue { get; set; }

        // ════════════════════════════════════════════════════════════════════
        // PARAMETERS — Trailing Stop
        // ════════════════════════════════════════════════════════════════════

        [Parameter("Enable Trailing Stop (EMA)", Group = "Trailing Stop",
            DefaultValue = false)]
        public bool EnableTrailingStop { get; set; }

        [Parameter("Trailing Stop EMA Period", Group = "Trailing Stop",
            DefaultValue = 33, MinValue = 2, MaxValue = 500)]
        public int TrailEmaLength { get; set; }

        // ════════════════════════════════════════════════════════════════════
        // PARAMETERS — Trading Hours (UTC)
        // ════════════════════════════════════════════════════════════════════

        [Parameter("Trading Start Hour (UTC)", Group = "Trading Hours",
            DefaultValue = 8, MinValue = 0, MaxValue = 23)]
        public int TradingStartHour { get; set; }

        [Parameter("Trading Start Minute (UTC)", Group = "Trading Hours",
            DefaultValue = 0, MinValue = 0, MaxValue = 59)]
        public int TradingStartMinute { get; set; }

        [Parameter("Trading End Hour (UTC)", Group = "Trading Hours",
            DefaultValue = 20, MinValue = 0, MaxValue = 23)]
        public int TradingEndHour { get; set; }

        [Parameter("Trading End Minute (UTC)", Group = "Trading Hours",
            DefaultValue = 0, MinValue = 0, MaxValue = 59)]
        public int TradingEndMinute { get; set; }

        // ════════════════════════════════════════════════════════════════════
        // PARAMETERS — Daily Limits
        // ════════════════════════════════════════════════════════════════════

        [Parameter("Daily Loss Limit ($)  [0 = disabled]", Group = "Daily Limits",
            DefaultValue = 0, MinValue = 0)]
        public double DailyLossLimit { get; set; }

        [Parameter("Daily Profit Target ($)  [0 = disabled]", Group = "Daily Limits",
            DefaultValue = 0, MinValue = 0)]
        public double DailyProfitTargetDollar { get; set; }

        [Parameter("Daily Profit Target (%)  [0 = disabled]", Group = "Daily Limits",
            DefaultValue = 0, MinValue = 0, MaxValue = 100)]
        public double DailyProfitTargetPercent { get; set; }

        // ════════════════════════════════════════════════════════════════════
        // PRIVATE FIELDS
        // ════════════════════════════════════════════════════════════════════

        private const string BotLabel = "HTS_BOT";

        private HTSIndicatorSet  _indicators;
        private SignalEngine     _signalEngine;
        private RiskManager      _riskManager;
        private ExecutionHandler _executionHandler;
        private DailyStats       _dailyStats;
        private Bars             _htfBars;

        // ════════════════════════════════════════════════════════════════════
        // LIFECYCLE
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Wires up all subsystems. Called once when the bot is started.
        /// </summary>
        protected override void OnStart()
        {
            Print($"[HTSBot] Starting — Symbol: {Symbol.Name} | TF: {TimeFrame} | HTF: {HTFTimeframe}");

            _dailyStats = new DailyStats();

            // Load higher-timeframe bars for trend filter
            _htfBars = MarketData.GetBars(HTFTimeframe);

            // Indicators (execution TF + HTF + trailing)
            _indicators = new HTSIndicatorSet(
                this, Bars, _htfBars,
                FastEmaLength, SlowEmaLength, TrailEmaLength);

            // Strategy layer
            _signalEngine = new SignalEngine(
                _indicators, Bars, _htfBars,
                KijunLength, EnableMTF, AllowedSignals);

            // Risk layer
            _riskManager = new RiskManager(this, _dailyStats)
            {
                RiskPercent              = RiskPercent,
                StopLossType             = StopLossType,
                StopLossValue            = StopLossValue,
                TakeProfitType           = TakeProfitType,
                TakeProfitValue          = TakeProfitValue,
                DailyLossLimit           = DailyLossLimit,
                DailyProfitTargetDollar  = DailyProfitTargetDollar,
                DailyProfitTargetPercent = DailyProfitTargetPercent,
                TradingStartHour         = TradingStartHour,
                TradingStartMinute       = TradingStartMinute,
                TradingEndHour           = TradingEndHour,
                TradingEndMinute         = TradingEndMinute
            };

            // Execution layer
            _executionHandler = new ExecutionHandler(
                this, _riskManager, BotLabel, EnableTrailingStop, _indicators);

            // Track closed positions for daily P&L
            Positions.Closed += OnPositionClosed;

            Print("[HTSBot] Initialisation complete.");
        }

        /// <summary>
        /// Main trading loop. Runs at the close of every 1-minute bar.
        /// Order of checks: new day → trading hours → daily limits → signal → execute.
        /// </summary>
        protected override void OnBar()
        {
            // ── Daily reset ──────────────────────────────────────────────────
            if (_dailyStats.IsNewDay(Server.Time))
            {
                Print($"[HTSBot] New day — previous P&L: {_dailyStats.RealizedPnL:F2}. Resetting stats.");
                _dailyStats.Reset();
            }

            // ── Trading-hours gate ───────────────────────────────────────────
            if (!_riskManager.IsWithinTradingHours(Server.Time))
                return;

            // ── Daily risk gate ──────────────────────────────────────────────
            if (!_riskManager.IsTradingAllowed())
                return;

            // ── Update trailing stop for any open position ───────────────────
            bool hasOpenPosition = Positions.Any(p => p.Label == BotLabel);
            if (hasOpenPosition)
            {
                _executionHandler.UpdateTrailingStop();
                return;   // One position at a time — wait for it to close.
            }

            // ── Signal evaluation ────────────────────────────────────────────
            TradeSignal signal = _signalEngine.GetSignal();
            if (!signal.IsValid)
                return;

            Print($"[HTSBot] Signal → {signal.Type} | Bar close: {signal.EntryPrice:F5} " +
                  $"| fH: {signal.FastBandHigh:F5} | fL: {signal.FastBandLow:F5}");

            // ── Order execution ──────────────────────────────────────────────
            _executionHandler.OpenPosition(signal);
        }

        /// <summary>
        /// Tick-level handler — updates the EMA trailing stop between bar closes
        /// for tighter exit precision when enabled.
        /// </summary>
        protected override void OnTick()
        {
            if (!EnableTrailingStop) return;
            if (!Positions.Any(p => p.Label == BotLabel)) return;

            _executionHandler.UpdateTrailingStop();
        }

        /// <summary>
        /// Graceful shutdown — logs final stats.
        /// Positions are intentionally left open so the user controls the exit.
        /// Set <c>closeOnStop = true</c> to change this behaviour.
        /// </summary>
        protected override void OnStop()
        {
            Print($"[HTSBot] Stopped — Daily P&L: {_dailyStats.RealizedPnL:F2}");
            Positions.Closed -= OnPositionClosed;
        }

        // ════════════════════════════════════════════════════════════════════
        // EVENT HANDLERS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Updates daily P&amp;L when one of our positions closes.
        /// Uses GrossProfit (before commission) to mirror typical P&amp;L displays.
        /// </summary>
        private void OnPositionClosed(PositionClosedEventArgs args)
        {
            if (args.Position.Label != BotLabel) return;

            double pnl = args.Position.GrossProfit;
            _riskManager.UpdateDailyPnL(pnl);

            Print($"[HTSBot] Position #{args.Position.Id} closed " +
                  $"| P&L: {pnl:F2} | Daily total: {_dailyStats.RealizedPnL:F2}");
        }
    }
}

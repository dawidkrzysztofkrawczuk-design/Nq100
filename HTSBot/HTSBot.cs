using System;
using cAlgo.API;
using cAlgo.Robots.HTS.Indicators;
using cAlgo.Robots.HTS.Models;
using cAlgo.Robots.HTS.Strategy;

namespace cAlgo.Robots
{
    /// <summary>
    /// HTS RAW v2.6 – Precision Touch cBot (cTrader / cAlgo API).
    ///
    /// <para>Iteracja 1 (ten commit): szkielet projektu + detekcja sygnałów.</para>
    /// <para>
    /// W tej iteracji bot:
    ///   • Inicjalizuje kompletny zestaw EMA (execution TF + HTF) i Kijun-Sen.
    ///   • Na każdym OnBar uruchamia <see cref="SignalEngine.Evaluate"/> na ostatnim ZAMKNIĘTYM barze.
    ///   • Loguje wykryty sygnał (LongClassic / LongHook / ShortClassic / ShortHook) przez <c>Print</c>.
    ///   • NIE otwiera ani nie modyfikuje pozycji – moduły Risk / Execution / UI / Export
    ///     dochodzą w iteracjach 2–5 zgodnie z planem budowy v2.6.
    /// </para>
    ///
    /// <para>
    /// Wszystkie parametry są już zadeklarowane (sekcja 4 specyfikacji), aby kontrakt UI
    /// w cTrader Automate IDE był stabilny przy kolejnych iteracjach – te niewykorzystane
    /// w iter. 1 są oznaczone w komentarzu numerem iteracji, w której zostaną podłączone.
    /// </para>
    /// </summary>
    [Robot(AccessRights = AccessRights.FullAccess)]
    public class HTSBot : Robot
    {
        // === [EMA Settings] ===
        [Parameter("Fast EMA Length", Group = "EMA Settings", DefaultValue = 66, MinValue = 1)]
        public int FastEmaLength { get; set; }

        [Parameter("Slow EMA Length", Group = "EMA Settings", DefaultValue = 288, MinValue = 1)]
        public int SlowEmaLength { get; set; }

        [Parameter("Kijun Length", Group = "EMA Settings", DefaultValue = 26, MinValue = 1)]
        public int KijunLength { get; set; }

        [Parameter("Trail EMA Length", Group = "EMA Settings", DefaultValue = 33, MinValue = 1)]
        public int TrailEmaLength { get; set; }

        // === [Timeframes] ===
        [Parameter("HTF Timeframe", Group = "Timeframes", DefaultValue = "Minute5")]
        public TimeFrame HTFTimeframe { get; set; }

        // === [Signal Settings] ===
        [Parameter("Enable Classic Signals", Group = "Signal Settings", DefaultValue = true)]
        public bool EnableClassicSignals { get; set; }

        [Parameter("Enable Hook Signals", Group = "Signal Settings", DefaultValue = true)]
        public bool EnableHookSignals { get; set; }

        [Parameter("Pyramiding Mode", Group = "Signal Settings", DefaultValue = PyramidingMode.Disabled)]
        public PyramidingMode PyramidingMode { get; set; }

        // === [Risk Management] === (iteracja 2)
        [Parameter("Risk %", Group = "Risk Management", DefaultValue = 1.0, MinValue = 0.01)]
        public double RiskPercent { get; set; }

        [Parameter("Stop Loss Type", Group = "Risk Management", DefaultValue = SLTPType.Percent)]
        public SLTPType StopLossType { get; set; }

        [Parameter("Stop Loss Value", Group = "Risk Management", DefaultValue = 0.5, MinValue = 0.0)]
        public double StopLossValue { get; set; }

        [Parameter("Take Profit Type", Group = "Risk Management", DefaultValue = TakeProfitMode.Percent)]
        public TakeProfitMode TakeProfitType { get; set; }

        [Parameter("Take Profit Value", Group = "Risk Management", DefaultValue = 1.0, MinValue = 0.0)]
        public double TakeProfitValue { get; set; }

        [Parameter("Risk:Reward Ratio", Group = "Risk Management", DefaultValue = 2.0, MinValue = 0.0)]
        public double RiskRewardRatio { get; set; }

        // === [Partial Close] === (iteracja 3)
        [Parameter("Enable Partial Close", Group = "Partial Close", DefaultValue = false)]
        public bool EnablePartialClose { get; set; }

        [Parameter("Partial Close %", Group = "Partial Close", DefaultValue = 50.0, MinValue = 1.0, MaxValue = 99.0)]
        public double PartialClosePercent { get; set; }

        [Parameter("Partial Close TP Type", Group = "Partial Close", DefaultValue = SLTPType.Percent)]
        public SLTPType PartialCloseTpType { get; set; }

        [Parameter("Partial Close TP Value", Group = "Partial Close", DefaultValue = 0.5, MinValue = 0.0)]
        public double PartialCloseTpValue { get; set; }

        // === [Trailing Stop] === (iteracja 3)
        [Parameter("Enable Trailing Stop", Group = "Trailing Stop", DefaultValue = false)]
        public bool EnableTrailingStop { get; set; }

        // === [Daily Limits] === (iteracja 4)
        [Parameter("Daily Loss Limit ($)", Group = "Daily Limits", DefaultValue = 0.0, MinValue = 0.0)]
        public double DailyLossLimit { get; set; }

        [Parameter("Daily Profit Target ($)", Group = "Daily Limits", DefaultValue = 0.0, MinValue = 0.0)]
        public double DailyProfitTargetDollar { get; set; }

        [Parameter("Daily Profit Target (%)", Group = "Daily Limits", DefaultValue = 0.0, MinValue = 0.0)]
        public double DailyProfitTargetPercent { get; set; }

        [Parameter("Close Positions On Daily Limit", Group = "Daily Limits", DefaultValue = true)]
        public bool ClosePositionsOnDailyLimit { get; set; }

        // === [Trading Hours] === (iteracja 4) – 7 dni × 5 pól
        [Parameter("Monday Enabled", Group = "Trading Hours - Monday", DefaultValue = true)]
        public bool MondayEnabled { get; set; }
        [Parameter("Monday Start Hour", Group = "Trading Hours - Monday", DefaultValue = 8, MinValue = 0, MaxValue = 23)]
        public int MondayStartHour { get; set; }
        [Parameter("Monday Start Minute", Group = "Trading Hours - Monday", DefaultValue = 0, MinValue = 0, MaxValue = 59)]
        public int MondayStartMinute { get; set; }
        [Parameter("Monday End Hour", Group = "Trading Hours - Monday", DefaultValue = 20, MinValue = 0, MaxValue = 23)]
        public int MondayEndHour { get; set; }
        [Parameter("Monday End Minute", Group = "Trading Hours - Monday", DefaultValue = 0, MinValue = 0, MaxValue = 59)]
        public int MondayEndMinute { get; set; }

        [Parameter("Tuesday Enabled", Group = "Trading Hours - Tuesday", DefaultValue = true)]
        public bool TuesdayEnabled { get; set; }
        [Parameter("Tuesday Start Hour", Group = "Trading Hours - Tuesday", DefaultValue = 8, MinValue = 0, MaxValue = 23)]
        public int TuesdayStartHour { get; set; }
        [Parameter("Tuesday Start Minute", Group = "Trading Hours - Tuesday", DefaultValue = 0, MinValue = 0, MaxValue = 59)]
        public int TuesdayStartMinute { get; set; }
        [Parameter("Tuesday End Hour", Group = "Trading Hours - Tuesday", DefaultValue = 20, MinValue = 0, MaxValue = 23)]
        public int TuesdayEndHour { get; set; }
        [Parameter("Tuesday End Minute", Group = "Trading Hours - Tuesday", DefaultValue = 0, MinValue = 0, MaxValue = 59)]
        public int TuesdayEndMinute { get; set; }

        [Parameter("Wednesday Enabled", Group = "Trading Hours - Wednesday", DefaultValue = true)]
        public bool WednesdayEnabled { get; set; }
        [Parameter("Wednesday Start Hour", Group = "Trading Hours - Wednesday", DefaultValue = 8, MinValue = 0, MaxValue = 23)]
        public int WednesdayStartHour { get; set; }
        [Parameter("Wednesday Start Minute", Group = "Trading Hours - Wednesday", DefaultValue = 0, MinValue = 0, MaxValue = 59)]
        public int WednesdayStartMinute { get; set; }
        [Parameter("Wednesday End Hour", Group = "Trading Hours - Wednesday", DefaultValue = 20, MinValue = 0, MaxValue = 23)]
        public int WednesdayEndHour { get; set; }
        [Parameter("Wednesday End Minute", Group = "Trading Hours - Wednesday", DefaultValue = 0, MinValue = 0, MaxValue = 59)]
        public int WednesdayEndMinute { get; set; }

        [Parameter("Thursday Enabled", Group = "Trading Hours - Thursday", DefaultValue = true)]
        public bool ThursdayEnabled { get; set; }
        [Parameter("Thursday Start Hour", Group = "Trading Hours - Thursday", DefaultValue = 8, MinValue = 0, MaxValue = 23)]
        public int ThursdayStartHour { get; set; }
        [Parameter("Thursday Start Minute", Group = "Trading Hours - Thursday", DefaultValue = 0, MinValue = 0, MaxValue = 59)]
        public int ThursdayStartMinute { get; set; }
        [Parameter("Thursday End Hour", Group = "Trading Hours - Thursday", DefaultValue = 20, MinValue = 0, MaxValue = 23)]
        public int ThursdayEndHour { get; set; }
        [Parameter("Thursday End Minute", Group = "Trading Hours - Thursday", DefaultValue = 0, MinValue = 0, MaxValue = 59)]
        public int ThursdayEndMinute { get; set; }

        [Parameter("Friday Enabled", Group = "Trading Hours - Friday", DefaultValue = true)]
        public bool FridayEnabled { get; set; }
        [Parameter("Friday Start Hour", Group = "Trading Hours - Friday", DefaultValue = 8, MinValue = 0, MaxValue = 23)]
        public int FridayStartHour { get; set; }
        [Parameter("Friday Start Minute", Group = "Trading Hours - Friday", DefaultValue = 0, MinValue = 0, MaxValue = 59)]
        public int FridayStartMinute { get; set; }
        [Parameter("Friday End Hour", Group = "Trading Hours - Friday", DefaultValue = 20, MinValue = 0, MaxValue = 23)]
        public int FridayEndHour { get; set; }
        [Parameter("Friday End Minute", Group = "Trading Hours - Friday", DefaultValue = 0, MinValue = 0, MaxValue = 59)]
        public int FridayEndMinute { get; set; }

        [Parameter("Saturday Enabled", Group = "Trading Hours - Saturday", DefaultValue = false)]
        public bool SaturdayEnabled { get; set; }
        [Parameter("Saturday Start Hour", Group = "Trading Hours - Saturday", DefaultValue = 0, MinValue = 0, MaxValue = 23)]
        public int SaturdayStartHour { get; set; }
        [Parameter("Saturday Start Minute", Group = "Trading Hours - Saturday", DefaultValue = 0, MinValue = 0, MaxValue = 59)]
        public int SaturdayStartMinute { get; set; }
        [Parameter("Saturday End Hour", Group = "Trading Hours - Saturday", DefaultValue = 0, MinValue = 0, MaxValue = 23)]
        public int SaturdayEndHour { get; set; }
        [Parameter("Saturday End Minute", Group = "Trading Hours - Saturday", DefaultValue = 0, MinValue = 0, MaxValue = 59)]
        public int SaturdayEndMinute { get; set; }

        [Parameter("Sunday Enabled", Group = "Trading Hours - Sunday", DefaultValue = false)]
        public bool SundayEnabled { get; set; }
        [Parameter("Sunday Start Hour", Group = "Trading Hours - Sunday", DefaultValue = 0, MinValue = 0, MaxValue = 23)]
        public int SundayStartHour { get; set; }
        [Parameter("Sunday Start Minute", Group = "Trading Hours - Sunday", DefaultValue = 0, MinValue = 0, MaxValue = 59)]
        public int SundayStartMinute { get; set; }
        [Parameter("Sunday End Hour", Group = "Trading Hours - Sunday", DefaultValue = 0, MinValue = 0, MaxValue = 23)]
        public int SundayEndHour { get; set; }
        [Parameter("Sunday End Minute", Group = "Trading Hours - Sunday", DefaultValue = 0, MinValue = 0, MaxValue = 59)]
        public int SundayEndMinute { get; set; }

        // === [After Hours] === (iteracja 4)
        [Parameter("Close Positions After Hours", Group = "After Hours", DefaultValue = false)]
        public bool ClosePositionsAfterHours { get; set; }

        // === [Display] === (iteracja 5)
        [Parameter("Show Chart Panel", Group = "Display", DefaultValue = true)]
        public bool ShowChartPanel { get; set; }

        // === [Export] === (iteracja 5)
        [Parameter("Enable CSV Export", Group = "Export", DefaultValue = false)]
        public bool EnableCsvExport { get; set; }

        [Parameter("CSV File Path", Group = "Export", DefaultValue = "HTSBot_trades.csv")]
        public string CsvFilePath { get; set; } = "HTSBot_trades.csv";

        // ==================== INTERNALS ====================

        /// <summary>Etykieta używana do oznaczania własnych pozycji.</summary>
        public const string PositionLabel = "HTS_BOT";

        private HTSIndicatorSet? _indicators;
        private SignalEngine? _signalEngine;
        private TradingSchedule? _schedule;
        private DailyStats? _dailyStats;

        /// <summary>UTC timestamp ostatnio przetworzonego sygnału (deduplikacja per bar).</summary>
        private DateTime _lastSignalBarUtc = DateTime.MinValue;

        // ==================== LIFECYCLE ====================

        protected override void OnStart()
        {
            try
            {
                ValidateParameters();

                Bars htfBars = MarketData.GetBars(HTFTimeframe);

                _indicators = new HTSIndicatorSet(
                    executionBars: Bars,
                    htfBars: htfBars,
                    fastHigh: Indicators.ExponentialMovingAverage(Bars.HighPrices, FastEmaLength),
                    fastLow: Indicators.ExponentialMovingAverage(Bars.LowPrices, FastEmaLength),
                    slowHigh: Indicators.ExponentialMovingAverage(Bars.HighPrices, SlowEmaLength),
                    slowLow: Indicators.ExponentialMovingAverage(Bars.LowPrices, SlowEmaLength),
                    htfFastHigh: Indicators.ExponentialMovingAverage(htfBars.HighPrices, FastEmaLength),
                    htfFastLow: Indicators.ExponentialMovingAverage(htfBars.LowPrices, FastEmaLength),
                    htfSlowHigh: Indicators.ExponentialMovingAverage(htfBars.HighPrices, SlowEmaLength),
                    htfSlowLow: Indicators.ExponentialMovingAverage(htfBars.LowPrices, SlowEmaLength),
                    trailHigh: Indicators.ExponentialMovingAverage(Bars.HighPrices, TrailEmaLength),
                    trailLow: Indicators.ExponentialMovingAverage(Bars.LowPrices, TrailEmaLength));

                _signalEngine = new SignalEngine(
                    _indicators,
                    KijunLength,
                    EnableClassicSignals,
                    EnableHookSignals);

                _schedule = BuildSchedule();
                _dailyStats = new DailyStats(Server.Time.ToUniversalTime());

                Print("[HTSBot] Iteracja 1: signal-detection-only. " +
                      $"Symbol={Symbol.Name} ExecTF={Bars.TimeFrame} HTF={HTFTimeframe} " +
                      $"FastEMA={FastEmaLength} SlowEMA={SlowEmaLength} Kijun={KijunLength}");

                Print($"[HTSBot] Schedule today: {_schedule.FormatTodayWindow(Server.Time.ToUniversalTime())} " +
                      $"(Mon={_schedule.Monday} Tue={_schedule.Tuesday} Wed={_schedule.Wednesday} " +
                      $"Thu={_schedule.Thursday} Fri={_schedule.Friday} Sat={_schedule.Saturday} Sun={_schedule.Sunday})");
            }
            catch (Exception ex)
            {
                Print($"[HTSBot] OnStart FAILED: {ex.GetType().Name}: {ex.Message}");
                Stop();
            }
        }

        protected override void OnBar()
        {
            if (_signalEngine == null || _dailyStats == null) return;

            try
            {
                DateTime utcNow = Server.Time.ToUniversalTime();

                if (_dailyStats.ResetIfNewDay(utcNow))
                    Print($"[HTSBot] Nowy dzień UTC ({_dailyStats.DayUtc:yyyy-MM-dd}) – reset DailyStats.");

                TradeSignal signal = _signalEngine.Evaluate();
                if (!signal.IsValid) return;

                if (signal.BarTimeUtc <= _lastSignalBarUtc) return;
                _lastSignalBarUtc = signal.BarTimeUtc;

                Print($"[HTSBot][SIGNAL] {signal.SignalType} {Symbol.Name} " +
                      $"bar={signal.BarTimeUtc:yyyy-MM-dd HH:mm} " +
                      $"close={signal.ReferenceClose} fH={signal.FastHigh} fL={signal.FastLow} kj={signal.Kijun}");
            }
            catch (Exception ex)
            {
                Print($"[HTSBot] OnBar error: {ex.GetType().Name}: {ex.Message}");
            }
        }

        protected override void OnTick()
        {
            // Iteracja 1: brak logiki tickowej. Trailing/partial close dochodzą w iter. 3.
        }

        protected override void OnStop()
        {
            Print("[HTSBot] Stop.");
        }

        // ==================== HELPERS ====================

        /// <summary>Spina parametry [Trading Hours - *] w obiekt <see cref="TradingSchedule"/>.</summary>
        private TradingSchedule BuildSchedule()
        {
            return new TradingSchedule(
                monday: new DaySchedule(MondayEnabled, MondayStartHour, MondayStartMinute, MondayEndHour, MondayEndMinute),
                tuesday: new DaySchedule(TuesdayEnabled, TuesdayStartHour, TuesdayStartMinute, TuesdayEndHour, TuesdayEndMinute),
                wednesday: new DaySchedule(WednesdayEnabled, WednesdayStartHour, WednesdayStartMinute, WednesdayEndHour, WednesdayEndMinute),
                thursday: new DaySchedule(ThursdayEnabled, ThursdayStartHour, ThursdayStartMinute, ThursdayEndHour, ThursdayEndMinute),
                friday: new DaySchedule(FridayEnabled, FridayStartHour, FridayStartMinute, FridayEndHour, FridayEndMinute),
                saturday: new DaySchedule(SaturdayEnabled, SaturdayStartHour, SaturdayStartMinute, SaturdayEndHour, SaturdayEndMinute),
                sunday: new DaySchedule(SundayEnabled, SundayStartHour, SundayStartMinute, SundayEndHour, SundayEndMinute));
        }

        /// <summary>Sanity check parametrów wejściowych – fail fast w OnStart.</summary>
        private void ValidateParameters()
        {
            if (FastEmaLength >= SlowEmaLength)
                throw new ArgumentException(
                    $"FastEmaLength ({FastEmaLength}) musi być MNIEJSZY od SlowEmaLength ({SlowEmaLength}).");

            if (RiskPercent <= 0d || RiskPercent > 100d)
                throw new ArgumentException($"RiskPercent musi być w (0, 100], jest {RiskPercent}.");

            if (PartialClosePercent <= 0d || PartialClosePercent >= 100d)
                throw new ArgumentException(
                    $"PartialClosePercent musi być w (0, 100), jest {PartialClosePercent}.");
        }
    }
}

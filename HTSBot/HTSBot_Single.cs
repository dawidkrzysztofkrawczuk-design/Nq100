// =====================================================================================
//  HTSBot — HTS RAW v2.6 Precision Touch (cTrader cBot, single-file build)
//  Wersja: Iteracja 1 (szkielet + detekcja sygnałów, brak otwierania pozycji).
//
//  INSTRUKCJA INSTALACJI (3 kroki):
//    1) cTrader → Automate → Robots → "+" → "New Robot" → nazwij "HTSBot".
//    2) W edytorze USUŃ całą zawartość pliku, który cTrader stworzył domyślnie,
//       i WKLEJ tutaj cały ten plik (Ctrl+A → Ctrl+V).
//    3) Build (Ctrl+B). Powinno się skompilować "Build succeeded".
//       Następnie przeciągnij HTSBot na wykres → ustaw parametry → Play.
//
//  Wszystkie klasy pomocnicze (Models, Indicators, Strategy) są w tym jednym pliku
//  pod wspólną przestrzenią nazw "cAlgo.Robots". W cTrader Automate na liście robotów
//  pojawi się TYLKO jedna pozycja: HTSBot.
// =====================================================================================

using System;
using cAlgo.API;
using cAlgo.API.Indicators;

namespace cAlgo.Robots
{
    // ============================== ENUMS (Models/Enums.cs) ==============================

    /// <summary>Klasyfikacja sygnału generowanego przez HTS RAW v2.6 SignalEngine.</summary>
    public enum SignalType
    {
        None,
        LongClassic,
        LongHook,
        ShortClassic,
        ShortHook
    }

    /// <summary>Kierunek pozycji.</summary>
    public enum TradeDirection
    {
        Long,
        Short
    }

    /// <summary>Typ wartości dla Stop Loss / Take Profit (w dolarach lub w procentach od ceny wejścia).</summary>
    public enum SLTPType
    {
        Dollar,
        Percent
    }

    /// <summary>Tryb wyznaczania Take Profit.</summary>
    public enum TakeProfitMode
    {
        Percent,
        Dollar,
        RiskReward,
        None,
        TrailingOnly
    }

    /// <summary>Tryb pyramidingu – co zrobić gdy pojawia się nowy sygnał, a pozycja już jest otwarta.</summary>
    public enum PyramidingMode
    {
        Disabled,
        AddToWinner
    }

    // ============================== TradeSignal ==============================

    /// <summary>Niemutowalny model sygnału tradingowego wygenerowanego przez SignalEngine.</summary>
    public sealed class TradeSignal
    {
        public SignalType SignalType { get; }
        public TradeDirection Direction { get; }
        public double ReferenceClose { get; }
        public double FastHigh { get; }
        public double FastLow { get; }
        public double Kijun { get; }
        public DateTime BarTimeUtc { get; }

        public TradeSignal(
            SignalType signalType,
            double referenceClose,
            double fastHigh,
            double fastLow,
            double kijun,
            DateTime barTimeUtc)
        {
            SignalType = signalType;
            Direction = (signalType == SignalType.LongClassic || signalType == SignalType.LongHook)
                ? TradeDirection.Long
                : TradeDirection.Short;
            ReferenceClose = referenceClose;
            FastHigh = fastHigh;
            FastLow = fastLow;
            Kijun = kijun;
            BarTimeUtc = barTimeUtc;
        }

        public static TradeSignal None { get; } = new TradeSignal(
            SignalType.None, 0d, 0d, 0d, 0d, DateTime.MinValue);

        public bool IsValid => SignalType != SignalType.None;

        public override string ToString()
            => $"TradeSignal[{SignalType} @ {BarTimeUtc:yyyy-MM-dd HH:mm} close={ReferenceClose:F5} kj={Kijun:F5}]";
    }

    // ============================== DailyStats ==============================

    /// <summary>Agregator statystyk dziennych: zrealizowany P&amp;L, liczba transakcji, flaga halt.</summary>
    public sealed class DailyStats
    {
        public DateTime DayUtc { get; private set; }
        public double RealizedPnL { get; private set; }
        public int ClosedTrades { get; private set; }
        public bool TradingHalted { get; private set; }
        public string HaltReason { get; private set; } = string.Empty;

        public DailyStats(DateTime dayUtc)
        {
            DayUtc = dayUtc.Date;
        }

        public void RegisterClosedTrade(double grossProfit)
        {
            RealizedPnL += grossProfit;
            ClosedTrades += 1;
        }

        public void Halt(string reason)
        {
            TradingHalted = true;
            HaltReason = reason ?? string.Empty;
        }

        public bool ResetIfNewDay(DateTime utcNow)
        {
            var today = utcNow.Date;
            if (today == DayUtc) return false;

            DayUtc = today;
            RealizedPnL = 0d;
            ClosedTrades = 0;
            TradingHalted = false;
            HaltReason = string.Empty;
            return true;
        }
    }

    // ============================== TradingSchedule ==============================

    /// <summary>Konfiguracja godzin handlu dla pojedynczego dnia tygodnia (UTC).</summary>
    public sealed class DaySchedule
    {
        public bool Enabled { get; set; }
        public int StartHour { get; set; }
        public int StartMinute { get; set; }
        public int EndHour { get; set; }
        public int EndMinute { get; set; }

        public DaySchedule(bool enabled, int startHour, int startMinute, int endHour, int endMinute)
        {
            Enabled = enabled;
            StartHour = Clamp(startHour, 0, 23);
            StartMinute = Clamp(startMinute, 0, 59);
            EndHour = Clamp(endHour, 0, 23);
            EndMinute = Clamp(endMinute, 0, 59);
        }

        private static int Clamp(int value, int min, int max)
            => value < min ? min : (value > max ? max : value);

        /// <summary>True, jeśli czas UTC mieści się w sesji. Obsługuje sesje przez północ.</summary>
        public bool ContainsUtc(DateTime utcTime)
        {
            if (!Enabled) return false;
            int nowMin = utcTime.Hour * 60 + utcTime.Minute;
            int startMin = StartHour * 60 + StartMinute;
            int endMin = EndHour * 60 + EndMinute;

            if (startMin <= endMin)
                return nowMin >= startMin && nowMin < endMin;

            return nowMin >= startMin || nowMin < endMin;
        }

        public override string ToString()
            => Enabled
                ? $"{StartHour:D2}:{StartMinute:D2}-{EndHour:D2}:{EndMinute:D2} UTC"
                : "off";
    }

    /// <summary>Pełny harmonogram handlu na 7 dni tygodnia.</summary>
    public sealed class TradingSchedule
    {
        public DaySchedule Monday { get; set; }
        public DaySchedule Tuesday { get; set; }
        public DaySchedule Wednesday { get; set; }
        public DaySchedule Thursday { get; set; }
        public DaySchedule Friday { get; set; }
        public DaySchedule Saturday { get; set; }
        public DaySchedule Sunday { get; set; }

        public TradingSchedule(
            DaySchedule monday,
            DaySchedule tuesday,
            DaySchedule wednesday,
            DaySchedule thursday,
            DaySchedule friday,
            DaySchedule saturday,
            DaySchedule sunday)
        {
            Monday = monday;
            Tuesday = tuesday;
            Wednesday = wednesday;
            Thursday = thursday;
            Friday = friday;
            Saturday = saturday;
            Sunday = sunday;
        }

        public DaySchedule For(DayOfWeek day)
        {
            switch (day)
            {
                case DayOfWeek.Monday: return Monday;
                case DayOfWeek.Tuesday: return Tuesday;
                case DayOfWeek.Wednesday: return Wednesday;
                case DayOfWeek.Thursday: return Thursday;
                case DayOfWeek.Friday: return Friday;
                case DayOfWeek.Saturday: return Saturday;
                case DayOfWeek.Sunday: return Sunday;
                default: throw new ArgumentOutOfRangeException(nameof(day));
            }
        }

        public bool IsWithinSchedule(DateTime utcTime)
            => For(utcTime.DayOfWeek).ContainsUtc(utcTime);

        public string FormatTodayWindow(DateTime utcTime)
            => $"{utcTime.DayOfWeek}: {For(utcTime.DayOfWeek)}";
    }

    // ============================== HTSIndicatorSet ==============================

    /// <summary>Zbiór EMA potrzebnych przez SignalEngine i ExecutionHandler:
    /// szybka/wolna wstęga (high &amp; low) na execution TF i HTF, plus EMA trailing stop.</summary>
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

    // ============================== SignalEngine ==============================

    /// <summary>Silnik sygnałów wskaźnika HTS RAW v2.6 – Precision Touch.
    /// Pracuje na ostatnim ZAMKNIĘTYM barze (Last(1)) – nie repaintuje.</summary>
    public sealed class SignalEngine
    {
        private readonly HTSIndicatorSet _ind;
        private readonly int _kijunLength;
        private readonly bool _enableClassic;
        private readonly bool _enableHook;

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

        public TradeSignal Evaluate()
        {
            var execBars = _ind.ExecutionBars;
            if (execBars == null || execBars.Count <= _kijunLength + 1)
                return TradeSignal.None;

            if (!_ind.IsWarmedUp(shift: 1))
                return TradeSignal.None;

            double fH = _ind.FastHigh.Result.Last(1);
            double fL = _ind.FastLow.Result.Last(1);

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

            return new TradeSignal(type, barClose, fH, fL, kijun, barTime);
        }

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

    // ============================== HTSBot (Robot orchestrator) ==============================

    /// <summary>
    /// HTS RAW v2.6 – Precision Touch cBot.
    /// Iteracja 1: detekcja sygnałów (logowanie do Print). Brak otwierania pozycji.
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
        public PyramidingMode PyramidingModeParam { get; set; }

        // === [Risk Management] === (iter. 2)
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

        // === [Partial Close] === (iter. 3)
        [Parameter("Enable Partial Close", Group = "Partial Close", DefaultValue = false)]
        public bool EnablePartialClose { get; set; }

        [Parameter("Partial Close %", Group = "Partial Close", DefaultValue = 50.0, MinValue = 1.0, MaxValue = 99.0)]
        public double PartialClosePercent { get; set; }

        [Parameter("Partial Close TP Type", Group = "Partial Close", DefaultValue = SLTPType.Percent)]
        public SLTPType PartialCloseTpType { get; set; }

        [Parameter("Partial Close TP Value", Group = "Partial Close", DefaultValue = 0.5, MinValue = 0.0)]
        public double PartialCloseTpValue { get; set; }

        // === [Trailing Stop] === (iter. 3)
        [Parameter("Enable Trailing Stop", Group = "Trailing Stop", DefaultValue = false)]
        public bool EnableTrailingStop { get; set; }

        // === [Daily Limits] === (iter. 4)
        [Parameter("Daily Loss Limit ($)", Group = "Daily Limits", DefaultValue = 0.0, MinValue = 0.0)]
        public double DailyLossLimit { get; set; }

        [Parameter("Daily Profit Target ($)", Group = "Daily Limits", DefaultValue = 0.0, MinValue = 0.0)]
        public double DailyProfitTargetDollar { get; set; }

        [Parameter("Daily Profit Target (%)", Group = "Daily Limits", DefaultValue = 0.0, MinValue = 0.0)]
        public double DailyProfitTargetPercent { get; set; }

        [Parameter("Close Positions On Daily Limit", Group = "Daily Limits", DefaultValue = true)]
        public bool ClosePositionsOnDailyLimit { get; set; }

        // === [Trading Hours - Mon..Sun] === (iter. 4)
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

        // === [After Hours] === (iter. 4)
        [Parameter("Close Positions After Hours", Group = "After Hours", DefaultValue = false)]
        public bool ClosePositionsAfterHours { get; set; }

        // === [Display] === (iter. 5)
        [Parameter("Show Chart Panel", Group = "Display", DefaultValue = true)]
        public bool ShowChartPanel { get; set; }

        // === [Export] === (iter. 5)
        [Parameter("Enable CSV Export", Group = "Export", DefaultValue = false)]
        public bool EnableCsvExport { get; set; }

        [Parameter("CSV File Path", Group = "Export", DefaultValue = "HTSBot_trades.csv")]
        public string CsvFilePath { get; set; } = "HTSBot_trades.csv";

        // ==================== INTERNALS ====================

        public const string PositionLabel = "HTS_BOT";

        private HTSIndicatorSet _indicators;
        private SignalEngine _signalEngine;
        private TradingSchedule _schedule;
        private DailyStats _dailyStats;
        private DateTime _lastSignalBarUtc = DateTime.MinValue;

        // ==================== LIFECYCLE ====================

        protected override void OnStart()
        {
            try
            {
                ValidateParameters();

                Bars htfBars = MarketData.GetBars(HTFTimeframe);

                _indicators = new HTSIndicatorSet(
                    Indicators,
                    Bars,
                    htfBars,
                    FastEmaLength,
                    SlowEmaLength,
                    TrailEmaLength);

                _signalEngine = new SignalEngine(
                    _indicators,
                    KijunLength,
                    EnableClassicSignals,
                    EnableHookSignals);

                _schedule = BuildSchedule();
                _dailyStats = new DailyStats(Server.Time.ToUniversalTime());

                Print("[HTSBot] Iteracja 1: signal-detection-only. " +
                      "Symbol=" + Symbol.Name + " ExecTF=" + Bars.TimeFrame + " HTF=" + HTFTimeframe +
                      " FastEMA=" + FastEmaLength + " SlowEMA=" + SlowEmaLength + " Kijun=" + KijunLength);

                Print("[HTSBot] Schedule today: " + _schedule.FormatTodayWindow(Server.Time.ToUniversalTime()) +
                      " (Mon=" + _schedule.Monday + " Tue=" + _schedule.Tuesday + " Wed=" + _schedule.Wednesday +
                      " Thu=" + _schedule.Thursday + " Fri=" + _schedule.Friday +
                      " Sat=" + _schedule.Saturday + " Sun=" + _schedule.Sunday + ")");
            }
            catch (Exception ex)
            {
                Print("[HTSBot] OnStart FAILED: " + ex.GetType().Name + ": " + ex.Message);
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
                    Print("[HTSBot] Nowy dzien UTC (" + _dailyStats.DayUtc.ToString("yyyy-MM-dd") + ") – reset DailyStats.");

                TradeSignal signal = _signalEngine.Evaluate();
                if (!signal.IsValid) return;

                if (signal.BarTimeUtc <= _lastSignalBarUtc) return;
                _lastSignalBarUtc = signal.BarTimeUtc;

                Print("[HTSBot][SIGNAL] " + signal.SignalType + " " + Symbol.Name +
                      " bar=" + signal.BarTimeUtc.ToString("yyyy-MM-dd HH:mm") +
                      " close=" + signal.ReferenceClose +
                      " fH=" + signal.FastHigh + " fL=" + signal.FastLow + " kj=" + signal.Kijun);
            }
            catch (Exception ex)
            {
                Print("[HTSBot] OnBar error: " + ex.GetType().Name + ": " + ex.Message);
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

        private void ValidateParameters()
        {
            if (FastEmaLength >= SlowEmaLength)
                throw new ArgumentException(
                    "FastEmaLength (" + FastEmaLength + ") musi byc MNIEJSZY od SlowEmaLength (" + SlowEmaLength + ").");

            if (RiskPercent <= 0d || RiskPercent > 100d)
                throw new ArgumentException("RiskPercent musi byc w (0, 100], jest " + RiskPercent + ".");

            if (PartialClosePercent <= 0d || PartialClosePercent >= 100d)
                throw new ArgumentException("PartialClosePercent musi byc w (0, 100), jest " + PartialClosePercent + ".");
        }
    }
}

using System;

namespace cAlgo.Robots.HTS.Models
{
    /// <summary>
    /// Niemutowalny model sygnału tradingowego wygenerowanego przez SignalEngine.
    /// Niesie wszystkie informacje potrzebne RiskManagerowi i ExecutionHandlerowi
    /// do zbudowania zlecenia bez ponownego odczytu wskaźników.
    /// </summary>
    public sealed class TradeSignal
    {
        /// <summary>Typ sygnału HTS RAW v2.6.</summary>
        public SignalType SignalType { get; }

        /// <summary>Kierunek tradu wynikający z typu sygnału.</summary>
        public TradeDirection Direction { get; }

        /// <summary>Cena zamknięcia bara, na którym powstał sygnał (referencja).</summary>
        public double ReferenceClose { get; }

        /// <summary>Górna krawędź szybkiej wstęgi (EMA(high, fastLen)) dla bara sygnału.</summary>
        public double FastHigh { get; }

        /// <summary>Dolna krawędź szybkiej wstęgi (EMA(low, fastLen)) dla bara sygnału.</summary>
        public double FastLow { get; }

        /// <summary>Wartość Kijun-Sen na barze sygnału.</summary>
        public double Kijun { get; }

        /// <summary>UTC timestamp bara, na którym powstał sygnał.</summary>
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

        /// <summary>Sentinel – brak sygnału.</summary>
        public static TradeSignal None { get; } = new TradeSignal(
            SignalType.None, 0d, 0d, 0d, 0d, DateTime.MinValue);

        /// <summary>Czy obiekt reprezentuje realny sygnał (różny od None).</summary>
        public bool IsValid => SignalType != SignalType.None;

        public override string ToString()
            => $"TradeSignal[{SignalType} @ {BarTimeUtc:yyyy-MM-dd HH:mm} close={ReferenceClose:F5} kj={Kijun:F5}]";
    }
}

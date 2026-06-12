namespace HTSBot.Models
{
    /// <summary>
    /// Immutable snapshot of a single trading signal produced by the SignalEngine.
    /// Carries all price context needed by the ExecutionHandler.
    /// </summary>
    public class TradeSignal
    {
        /// <summary>Signal classification (None means no valid signal).</summary>
        public SignalType Type { get; init; } = SignalType.None;

        /// <summary>Long or Short.</summary>
        public TradeDirection Direction { get; init; }

        /// <summary>Close price of the bar that generated the signal.</summary>
        public double EntryPrice { get; init; }

        /// <summary>Fast EMA of high prices at signal bar (upper band edge).</summary>
        public double FastBandHigh { get; init; }

        /// <summary>Fast EMA of low prices at signal bar (lower band edge).</summary>
        public double FastBandLow { get; init; }

        /// <summary>Returns true when this signal should be acted upon.</summary>
        public bool IsValid => Type != SignalType.None;

        /// <summary>Creates a sentinel "no signal" instance.</summary>
        public static TradeSignal NoSignal() => new() { Type = SignalType.None };
    }
}

namespace HTSBot.Models
{
    /// <summary>
    /// Identifies the type and direction of a trading signal.
    /// Classic = entry with Kijun confirmation; Hook = entry against Kijun.
    /// </summary>
    public enum SignalType
    {
        None,
        LongClassic,
        LongHook,
        ShortClassic,
        ShortHook
    }

    /// <summary>Trade direction for an open position.</summary>
    public enum TradeDirection
    {
        Long,
        Short
    }

    /// <summary>
    /// How a stop-loss or take-profit value is interpreted.
    /// Dollar = absolute account-currency amount; Percent = % of entry price.
    /// </summary>
    public enum SLTPType
    {
        Dollar,
        Percent
    }

    /// <summary>Which signal types the bot is allowed to trade.</summary>
    public enum SignalFilter
    {
        Both,
        ClassicOnly,
        HookOnly
    }
}

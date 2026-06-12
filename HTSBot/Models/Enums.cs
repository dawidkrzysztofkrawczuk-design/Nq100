namespace cAlgo.Robots.HTS.Models
{
    /// <summary>
    /// Klasyfikacja sygnału generowanego przez HTS RAW v2.6 SignalEngine.
    /// </summary>
    public enum SignalType
    {
        /// <summary>Brak sygnału na bieżącym barze.</summary>
        None,

        /// <summary>Long Classic – HTF byczy + zamknięcie nad Kijun + Precision Touch.</summary>
        LongClassic,

        /// <summary>Long Hook – HTF byczy + zamknięcie pod Kijun + Precision Touch (kontra-Kijun).</summary>
        LongHook,

        /// <summary>Short Classic – HTF niedźwiedzi + zamknięcie pod Kijun + Precision Touch.</summary>
        ShortClassic,

        /// <summary>Short Hook – HTF niedźwiedzi + zamknięcie nad Kijun + Precision Touch (kontra-Kijun).</summary>
        ShortHook
    }

    /// <summary>
    /// Kierunek pozycji.
    /// </summary>
    public enum TradeDirection
    {
        Long,
        Short
    }

    /// <summary>
    /// Typ wartości dla Stop Loss / Take Profit (w dolarach lub w procentach od ceny wejścia).
    /// </summary>
    public enum SLTPType
    {
        Dollar,
        Percent
    }

    /// <summary>
    /// Tryb wyznaczania Take Profit.
    /// </summary>
    public enum TakeProfitMode
    {
        /// <summary>Stały % od ceny wejścia.</summary>
        Percent,

        /// <summary>Stała kwota $ ryzyka/zysku przeliczona na dystans cenowy.</summary>
        Dollar,

        /// <summary>R:R ratio – TP = SL distance × ratio.</summary>
        RiskReward,

        /// <summary>Brak stałego TP – tylko SL i/lub trailing.</summary>
        None,

        /// <summary>Brak stałego TP – pozycję zamyka wyłącznie trailing stop.</summary>
        TrailingOnly
    }

    /// <summary>
    /// Tryb pyramidingu – co zrobić gdy pojawia się nowy sygnał, a pozycja już jest otwarta.
    /// </summary>
    public enum PyramidingMode
    {
        /// <summary>Ignoruj nowy sygnał (max 1 pozycja naraz).</summary>
        Disabled,

        /// <summary>Powiększ wygrywającą pozycję, jeśli sygnał zgodny z kierunkiem.</summary>
        AddToWinner
    }
}

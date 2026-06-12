using System;

namespace cAlgo.Robots.HTS.Models
{
    /// <summary>
    /// Agregator statystyk dziennych: zrealizowany P&L, liczba transakcji, flaga halt.
    /// Resetowany na granicy dnia UTC w orkiestratorze (HTSBot.OnBar).
    /// </summary>
    public sealed class DailyStats
    {
        /// <summary>Data UTC, dla której statystyki są aktualne (bez czasu).</summary>
        public DateTime DayUtc { get; private set; }

        /// <summary>Zrealizowany P&L w walucie konta.</summary>
        public double RealizedPnL { get; private set; }

        /// <summary>Liczba zamkniętych transakcji w bieżącym dniu.</summary>
        public int ClosedTrades { get; private set; }

        /// <summary>Czy bot jest zatrzymany na dziś (np. po hicie limitu straty/zysku).</summary>
        public bool TradingHalted { get; private set; }

        /// <summary>Powód zatrzymania (do wyświetlenia w panelu i logach).</summary>
        public string HaltReason { get; private set; } = string.Empty;

        public DailyStats(DateTime dayUtc)
        {
            DayUtc = dayUtc.Date;
        }

        /// <summary>
        /// Zarejestruj zamkniętą transakcję – aktualizuje P&L i licznik.
        /// </summary>
        public void RegisterClosedTrade(double grossProfit)
        {
            RealizedPnL += grossProfit;
            ClosedTrades += 1;
        }

        /// <summary>Zatrzymaj handel na dziś z podanym powodem (idempotentne).</summary>
        public void Halt(string reason)
        {
            TradingHalted = true;
            HaltReason = reason ?? string.Empty;
        }

        /// <summary>
        /// Zresetuj statystyki dla nowego dnia UTC. Zwraca true, gdy faktycznie nastąpił reset.
        /// </summary>
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
}

using System;

namespace cAlgo.Robots.HTS.Models
{
    /// <summary>
    /// Konfiguracja godzin handlu dla pojedynczego dnia tygodnia.
    /// Wszystkie wartości wyrażone w UTC.
    /// </summary>
    public sealed class DaySchedule
    {
        /// <summary>Czy handel w tym dniu jest dozwolony.</summary>
        public bool Enabled { get; set; }

        /// <summary>Godzina rozpoczęcia sesji (0–23 UTC).</summary>
        public int StartHour { get; set; }

        /// <summary>Minuta rozpoczęcia sesji (0–59).</summary>
        public int StartMinute { get; set; }

        /// <summary>Godzina zakończenia sesji (0–23 UTC).</summary>
        public int EndHour { get; set; }

        /// <summary>Minuta zakończenia sesji (0–59).</summary>
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

        /// <summary>
        /// Zwraca true, jeśli podany czas UTC mieści się w sesji tego dnia.
        /// Obsługuje sesje przez północ (StartMin > EndMin).
        /// </summary>
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
                ? $"{StartHour:D2}:{StartMinute:D2}–{EndHour:D2}:{EndMinute:D2} UTC"
                : "off";
    }

    /// <summary>
    /// Pełny harmonogram handlu na 7 dni tygodnia.
    /// Używany przez RiskManager do decyzji o wpuszczeniu nowych zleceń
    /// i ewentualnym zamknięciu pozycji po godzinach.
    /// </summary>
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

        /// <summary>Pobierz harmonogram dla wskazanego dnia tygodnia.</summary>
        public DaySchedule For(DayOfWeek day) => day switch
        {
            DayOfWeek.Monday => Monday,
            DayOfWeek.Tuesday => Tuesday,
            DayOfWeek.Wednesday => Wednesday,
            DayOfWeek.Thursday => Thursday,
            DayOfWeek.Friday => Friday,
            DayOfWeek.Saturday => Saturday,
            DayOfWeek.Sunday => Sunday,
            _ => throw new ArgumentOutOfRangeException(nameof(day))
        };

        /// <summary>
        /// Czy podany czas UTC mieści się w dozwolonej sesji handlowej dla swojego dnia tygodnia.
        /// </summary>
        public bool IsWithinSchedule(DateTime utcTime)
            => For(utcTime.DayOfWeek).ContainsUtc(utcTime);

        /// <summary>
        /// Sformatuj okno handlu dla bieżącego dnia (do panelu na wykresie).
        /// </summary>
        public string FormatTodayWindow(DateTime utcTime)
            => $"{utcTime.DayOfWeek}: {For(utcTime.DayOfWeek)}";
    }
}

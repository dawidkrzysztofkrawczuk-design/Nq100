# PLAN BUDOWY — HTS RAW v2.6 cTrader cBot
> Dokument specyfikacyjny dla developera / modelu AI.
> Platforma: **cTrader** (cAlgo API, C#).
> Strategia bazowa: wskaźnik **HTS RAW v2.6 – Precision Touch**.
> Wszystkie wymagania potwierdzone przez właściciela projektu.

---

## 1. ZATWIERDZONA SPECYFIKACJA (20 pytań)

| # | Temat | Decyzja |
|---|---|---|
| 1 | Instrument | Wszystkie (Forex, Indeksy, Surowce, Krypto) |
| 2 | Interwał wejść (execution TF) | Wybieralny w ustawieniach (M1/M5/M15/inne) |
| 3 | Interwał trendu (HTF) | Wybieralny w ustawieniach |
| 4 | Sygnały | Classic + Hook — każdy można osobno włączyć/wyłączyć |
| 5 | Max pozycji jednocześnie | 1 pozycja naraz |
| 6 | Nowy sygnał gdy pozycja otwarta | Wybór: ignoruj LUB powiększ (pyramiding) |
| 7 | Stop Loss — typ | Wybór: stały $ LUB stały % |
| 8 | Trailing Stop — start | Tylko gdy ręcznie włączony w ustawieniach |
| 9 | Trailing Stop — EMA | Konfigurowalna długość EMA |
| 10 | Take Profit — typ | Wybór: stały %, stały $, R:R ratio, brak TP, trailing only |
| 11 | Partial Close | Konfigurowalne: ile % zamknąć + przy jakim TP |
| 12 | Wielkość pozycji | % kapitału na trade (dynamiczne position sizing) |
| 13 | Dzienny limit straty | Zamknij wszystkie pozycje + zatrzymaj bota na resztę dnia |
| 14 | Dzienny cel zysku | Zamknij wszystkie pozycje + zatrzymaj bota na resztę dnia |
| 15 | Godziny handlu | Dni tygodnia + godziny osobno na każdy dzień (Pon–Nd) |
| 16 | Pozycja po godzinach | Wybór: zostaw otwartą LUB zamknij natychmiast |
| 17 | Powiadomienia | Brak |
| 18 | Panel na wykresie | Tak — dzienny P&L, status bota, liczba transakcji |
| 19 | Backtest | Musi działać + eksport wyników do CSV |
| 20 | Format pliku | Pliki .cs do cTrader IDE |

---

## 2. TECHNOLOGIA

| Element | Wybór |
|---|---|
| Platforma | cTrader (Spotware) |
| Język | C# (.NET 6) |
| API | cAlgo API (`using cAlgo.API;`) |
| Klasa bazowa bota | `Robot` |
| Parametry | Atrybut `[Parameter]` |
| Wskaźniki | `Robot.Indicators.ExponentialMovingAverage(source, period)` |
| Wieloczasowość | `Robot.MarketData.GetBars(TimeFrame)` |
| Panel na wykresie | `Chart.DrawStaticText(...)` |
| Eksport CSV | `System.IO.File.AppendAllText(...)` |

---

## 3. STRUKTURA KATALOGÓW I PLIKÓW

```
HTSBot/
├── HTSBot.csproj
├── HTSBot.cs                       ← [Robot] Orchestrator
├── Models/
│   ├── Enums.cs                    ← typy wyliczeniowe
│   ├── TradeSignal.cs              ← model sygnału
│   ├── DailyStats.cs               ← statystyki dnia
│   └── TradingSchedule.cs          ← harmonogram godzin (per dzień tygodnia)
├── Indicators/
│   └── HTSIndicatorSet.cs          ← inicjalizacja EMA
├── Strategy/
│   └── SignalEngine.cs             ← logika HTS v2.6
├── Risk/
│   └── RiskManager.cs              ← ryzyko, SL/TP, partial close, limity
├── Execution/
│   └── ExecutionHandler.cs         ← zlecenia, trailing, zamykanie
├── UI/
│   └── ChartPanel.cs               ← panel informacyjny na wykresie
└── Export/
    └── CsvExporter.cs              ← eksport transakcji do CSV
```

---

## 4. WSZYSTKIE PARAMETRY BOTA (z grupami)

### [EMA Settings]
| Parametr | Typ | Domyślnie | Opis |
|---|---|---|---|
| FastEmaLength | int | 66 | Okres szybkiej wstęgi EMA |
| SlowEmaLength | int | 288 | Okres wolnej wstęgi EMA |
| KijunLength | int | 26 | Lookback dla Kijun-Sen |
| TrailEmaLength | int | 33 | Okres EMA dla trailing stop |

### [Timeframes]
| Parametr | Typ | Domyślnie | Opis |
|---|---|---|---|
| HTFTimeframe | TimeFrame | Minute5 | Interwał filtra trendu |

> Uwaga: execution TF jest ustawiany przez użytkownika podczas przypinania bota do wykresu — cTrader automatycznie używa TF wykresu jako `Bars`.

### [Signal Settings]
| Parametr | Typ | Domyślnie | Opis |
|---|---|---|---|
| EnableClassicSignals | bool | true | Włącz sygnały Classic (z Kijun) |
| EnableHookSignals | bool | true | Włącz sygnały Hook (bez Kijun) |
| PyramidingMode | PyramidingMode | Disabled | Disabled / AddToWinner |

### [Risk Management]
| Parametr | Typ | Domyślnie | Opis |
|---|---|---|---|
| RiskPercent | double | 1.0 | % kapitału na trade |
| StopLossType | SLTPType | Percent | Dollar / Percent |
| StopLossValue | double | 0.5 | Wartość SL w $ lub % |
| TakeProfitType | TakeProfitMode | Percent | Percent / Dollar / RiskReward / None / TrailingOnly |
| TakeProfitValue | double | 1.0 | Wartość TP |
| RiskRewardRatio | double | 2.0 | Używany gdy TakeProfitType = RiskReward |

### [Partial Close]
| Parametr | Typ | Domyślnie | Opis |
|---|---|---|---|
| EnablePartialClose | bool | false | Włącz częściowe zamykanie |
| PartialClosePercent | double | 50.0 | % pozycji do zamknięcia (0–100) |
| PartialCloseTpType | SLTPType | Percent | Typ TP1 dla partial close |
| PartialCloseTpValue | double | 0.5 | Wartość TP1 dla partial close |

### [Trailing Stop]
| Parametr | Typ | Domyślnie | Opis |
|---|---|---|---|
| EnableTrailingStop | bool | false | Włącz EMA trailing stop |

### [Daily Limits]
| Parametr | Typ | Domyślnie | Opis |
|---|---|---|---|
| DailyLossLimit | double | 0 | Max strata dnia w $ (0=wyłączone) |
| DailyProfitTargetDollar | double | 0 | Cel zysku w $ (0=wyłączone) |
| DailyProfitTargetPercent | double | 0 | Cel zysku w % kapitału (0=wyłączone) |
| ClosePositionsOnDailyLimit | bool | true | Zamknij pozycje gdy limit hit (zawsze true per spec) |

### [Trading Hours — poniedziałek do niedzieli]
Dla każdego dnia tygodnia (7×):

| Parametr | Typ | Domyślnie | Opis |
|---|---|---|---|
| MondayEnabled | bool | true | Handel w poniedziałek |
| MondayStartHour | int | 8 | Godzina start (UTC) |
| MondayStartMinute | int | 0 | Minuta start |
| MondayEndHour | int | 20 | Godzina koniec (UTC) |
| MondayEndMinute | int | 0 | Minuta koniec |
| ... (analogicznie Tue–Sun) | | | |

### [After Hours]
| Parametr | Typ | Domyślnie | Opis |
|---|---|---|---|
| ClosePositionsAfterHours | bool | false | Zamknij pozycje gdy koniec godzin |

### [Display]
| Parametr | Typ | Domyślnie | Opis |
|---|---|---|---|
| ShowChartPanel | bool | true | Wyświetl panel na wykresie |

### [Export]
| Parametr | Typ | Domyślnie | Opis |
|---|---|---|---|
| EnableCsvExport | bool | false | Eksportuj transakcje do CSV |
| CsvFilePath | string | "HTSBot_trades.csv" | Ścieżka pliku CSV |

---

## 5. TYPY WYLICZENIOWE

```csharp
public enum SignalType
{
    None, LongClassic, LongHook, ShortClassic, ShortHook
}

public enum TradeDirection { Long, Short }

public enum SLTPType { Dollar, Percent }

public enum TakeProfitMode
{
    Percent,       // stały % od wejścia
    Dollar,        // stały $ od wejścia
    RiskReward,    // R:R ratio × SL distance
    None,          // brak TP (tylko SL/trailing)
    TrailingOnly   // brak stałego TP, trailing zamknie pozycję
}

public enum PyramidingMode { Disabled, AddToWinner }
```

---

## 6. LOGIKA SYGNAŁÓW (HTS RAW v2.6 → C#)

### 6.1 Definicje wskaźników

```
fH = EMA(high, fastLen)   → ind.FastHigh.Result.Last(1)
fL = EMA(low,  fastLen)   → ind.FastLow.Result.Last(1)
sH = EMA(high, slowLen)   → ind.SlowHigh.Result.Last(1)
sL = EMA(low,  slowLen)   → ind.SlowLow.Result.Last(1)

kj = (Highest(high, kLen) + Lowest(low, kLen)) / 2
   → pętla Last(1)..Last(kLen) przez bars.HighPrices i bars.LowPrices

HTF trend:
  htfFastHigh = ind.HtfFastHigh.Result.Last(1)
  htfFastLow  = ind.HtfFastLow.Result.Last(1)
  htfSlowHigh = ind.HtfSlowHigh.Result.Last(1)
  htfSlowLow  = ind.HtfSlowLow.Result.Last(1)
```

### 6.2 Warunki trendu

```
isBull = htfFastLow  > htfSlowHigh    // HTF: szybka wstęga całkowicie nad wolną
isBear = htfFastHigh < htfSlowLow     // HTF: szybka wstęga całkowicie pod wolną
```

### 6.3 Warunek precyzyjnego dotyku (Precision Touch)

```
LONG:
  barLow  <= fH          // knot wchodzi w wstęgę od góry
  barLow  >= fL          // ale nie przebija wstęgi na wylot
  barClose > fH          // zamknięcie POWYŻEJ górnej krawędzi wstęgi

SHORT:
  barHigh >= fL          // knot wchodzi w wstęgę od dołu
  barHigh <= fH          // ale nie przebija wstęgi na wylot
  barClose < fL          // zamknięcie PONIŻEJ dolnej krawędzi wstęgi
```

### 6.4 Klasyfikacja sygnałów

```
longClassic  = isBull AND barClose > kj AND validTouchLong
shortClassic = isBear AND barClose < kj AND validTouchShort
longHook     = isBull AND barClose < kj AND validTouchLong
shortHook    = isBear AND barClose > kj AND validTouchShort
```

### 6.5 Filtrowanie

```
if !EnableClassicSignals → longClassic = shortClassic = false
if !EnableHookSignals    → longHook    = shortHook    = false
```

---

## 7. POSITION SIZING I SL/TP

### 7.1 Obliczenie SL (ceny)

```
Tryb Percent:
  slDist = entryPrice × (StopLossValue / 100)

Tryb Dollar:
  slDist = StopLossValue × TickSize / (VolumeInUnitsStep × TickValue)

slPrice (Long)  = entryPrice - slDist
slPrice (Short) = entryPrice + slDist
```

### 7.2 Obliczenie TP (ceny)

```
Tryb Percent:    tpDist = entryPrice × (TakeProfitValue / 100)
Tryb Dollar:     tpDist = TakeProfitValue × TickSize / (VolumeInUnitsStep × TickValue)
Tryb RiskReward: tpDist = slDist × RiskRewardRatio
Tryb None/TrailingOnly: tpPrice = null

tpPrice (Long)  = entryPrice + tpDist
tpPrice (Short) = entryPrice - tpDist
```

### 7.3 Wielkość pozycji

```
riskAmount       = Account.Balance × (RiskPercent / 100)
priceValuePerUnit = TickValue / TickSize
volume           = riskAmount / (slDist × priceValuePerUnit)
volume           = floor(volume / VolumeInUnitsStep) × VolumeInUnitsStep
volume           = clamp(volume, VolumeInUnitsMin, VolumeInUnitsMax)
```

### 7.4 Partial Close

```
if EnablePartialClose AND !position.PartialCloseDone:
  tp1Dist  = oblicz jak wyżej dla PartialCloseTpType/Value
  tp1Price = entryPrice ± tp1Dist

  if (Long  AND currentPrice >= tp1Price) OR
     (Short AND currentPrice <= tp1Price):
    volumeToClose = position.VolumeInUnits × (PartialClosePercent / 100)
    ClosePosition(position, volumeToClose)
    position.PartialCloseDone = true
    przesuń SL na breakeven (entryPrice)
```

---

## 8. TRAILING STOP

```
Wywołanie: OnTick() lub OnBar() gdy EnableTrailingStop = true

LONG:
  newSL = ind.TrailLow.Result.Last(1)    // EMA(low, TrailEmaLength)
  if newSL > position.StopLoss:          // tylko w górę
    ModifyPosition(position, newSL, position.TakeProfit)

SHORT:
  newSL = ind.TrailHigh.Result.Last(1)   // EMA(high, TrailEmaLength)
  if newSL < position.StopLoss:          // tylko w dół
    ModifyPosition(position, newSL, position.TakeProfit)
```

> `ModifyPosition` przyjmuje **absolutne ceny**, nie pipy.

---

## 9. GODZINY HANDLU (per dzień tygodnia)

```
Dla bieżącego dnia UTC (DayOfWeek):
  1. Sprawdź czy dzień jest włączony (MondayEnabled, TuesdayEnabled, ...)
  2. Oblicz: nowMin = hour*60+min, startMin, endMin
  3. if startMin <= endMin:
       return nowMin >= startMin AND nowMin < endMin
     else (sesja przez północ):
       return nowMin >= startMin OR nowMin < endMin

Po godzinach:
  if ClosePositionsAfterHours AND !IsWithinTradingHours:
    ExecutionHandler.CloseAllPositions("After hours")
```

---

## 10. LIMITY DZIENNE

```
OnPositionClosed:
  dailyStats.RealizedPnL += position.GrossProfit
  CheckDailyLimits()

CheckDailyLimits():
  if DailyLossLimit > 0 AND RealizedPnL <= -DailyLossLimit:
    HALT + CloseAllPositions("Daily loss limit")

  if DailyProfitTargetDollar > 0 AND RealizedPnL >= DailyProfitTargetDollar:
    HALT + CloseAllPositions("Daily profit target $")

  if DailyProfitTargetPercent > 0:
    target = Balance × (DailyProfitTargetPercent / 100)
    if RealizedPnL >= target:
      HALT + CloseAllPositions("Daily profit target %")
```

> Kluczowa różnica od poprzedniej wersji: **zamknij istniejące pozycje** (nie tylko zatrzymaj nowe).

---

## 11. PANEL NA WYKRESIE (ChartPanel)

Wyświetlany w lewym górnym rogu wykresu przez `Chart.DrawStaticText`.
Aktualizowany w `OnBar()` i `OnTick()`.

```
┌─────────────────────────────┐
│ HTS RAW v2.6 Bot            │
│ Status: AKTYWNY / ZATRZYMANY│
│ Dzienny P&L: +$125.50       │
│ Transakcje dziś: 3          │
│ Otwarta pozycja: Long M1    │
│ Godziny: 08:00 – 20:00 UTC  │
└─────────────────────────────┘
```

---

## 12. EKSPORT CSV (CsvExporter)

Plik tworzony/dołączany przy każdym zamknięciu pozycji.

### Nagłówek CSV:
```
Date,Time,Symbol,Direction,SignalType,Volume,EntryPrice,ExitPrice,StopLoss,TakeProfit,PnL,DailyPnL
```

### Logika:
```
OnPositionClosed:
  if EnableCsvExport:
    wiersz = Format(position, signalType, dailyPnL)
    File.AppendAllText(CsvFilePath, wiersz)
```

---

## 13. FLOW DIAGRAMU OnBar()

```
[Bar zamknięty]
      │
      ▼
[Nowy dzień UTC?] ──TAK──► Reset DailyStats
      │
      ▼
[Czy dzień tygodnia włączony?] ──NIE──► return
      │
      ▼
[Czy w godzinach handlu?] ──NIE──► [ClosePositionsAfterHours?] → zamknij/zostaw → return
      │
      ▼
[IsTradingHalted?] ──TAK──► return
      │
      ▼
[Otwarta pozycja HTS_BOT?]
      │
    TAK─────────────────────────────────────────────┐
      │ NIE                                          ▼
      ▼                               [UpdateTrailingStop()]
[GetSignal()]                         [CheckPartialClose()]
      │                                              │
  Brak──► return                              return
      │
  Sygnał
      │
      ▼
[CalculateSL / CalculateTP / CalculateVolume]
      │
      ▼
[ExecuteMarketOrder]
      │
      ▼
[Loguj + UpdateCsvExport + UpdatePanel]
```

---

## 14. MODUŁ `TradingSchedule`

Przechowuje harmonogram godzin dla każdego dnia tygodnia.

```csharp
public class DaySchedule
{
    public bool   Enabled     { get; set; }
    public int    StartHour   { get; set; }
    public int    StartMinute { get; set; }
    public int    EndHour     { get; set; }
    public int    EndMinute   { get; set; }
}

public class TradingSchedule
{
    public DaySchedule Monday    { get; set; }
    public DaySchedule Tuesday   { get; set; }
    public DaySchedule Wednesday { get; set; }
    public DaySchedule Thursday  { get; set; }
    public DaySchedule Friday    { get; set; }
    public DaySchedule Saturday  { get; set; }
    public DaySchedule Sunday    { get; set; }

    public bool IsWithinSchedule(DateTime utcTime) { ... }
}
```

---

## 15. ZASADY PISANIA KODU

1. **C# .NET 6**, `Nullable enable`, pełne Type Hints
2. **Docstringi** `/// <summary>` na każdej klasie i publicznej metodzie
3. **Logowanie** przez `robot.Print("[Klasa] komunikat")`
4. **Fail-safe**: sprawdzaj `TradeResult.IsSuccessful` po każdym zleceniu
5. **Jedna pozycja naraz** — sprawdzaj `Positions.Any(p => p.Label == "HTS_BOT")`
6. **Volume zaokrąglaj w DÓŁ** (`Math.Floor`) — nigdy nie ryzykuj więcej niż zakładane %
7. **Trailing tylko w korzystnym kierunku** — SL dla Long tylko rośnie, dla Short tylko maleje
8. **Żadnych hardkodowanych wartości** — wszystko przez `[Parameter]`
9. **Partial close** śledź flagą na pozycji (komentarz lub słownik)
10. **CSV** — zapis przez `System.IO.File.AppendAllText`, obsłuż `IOException`

---

## 16. KOLEJNOŚĆ BUDOWANIA (moduł po module)

```
Iteracja 1: Szkielet + sygnały
  → Models/ + Indicators/ + Strategy/SignalEngine.cs
  → Test: czy sygnały pojawiają się w backtest na M1?

Iteracja 2: Ryzyko + wykonanie
  → Risk/RiskManager.cs + Execution/ExecutionHandler.cs
  → Test: czy pozycje otwierają się z prawidłowym SL/TP?

Iteracja 3: Trailing + Partial Close
  → Rozbudowa ExecutionHandler + RiskManager
  → Test: czy SL przesuwa się prawidłowo?

Iteracja 4: Godziny + Limity dzienne
  → Models/TradingSchedule.cs + rozbudowa RiskManager
  → Test: czy bot zatrzymuje się i zamyka pozycje?

Iteracja 5: Panel + CSV
  → UI/ChartPanel.cs + Export/CsvExporter.cs
  → Test: czy panel wyświetla aktualne dane?

Iteracja 6: Backtest + kalibracja
  → Pełny test na historycznych danych
  → Weryfikacja position sizing na różnych instrumentach
```

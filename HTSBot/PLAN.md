# PLAN BUDOWY — HTS RAW v2.6 cTrader cBot
> Dokument specyfikacyjny dla developera / modelu AI.  
> Platform: **cTrader** (cAlgo API, C#).  
> Strategia bazowa: wskaźnik **HTS RAW v2.6 – Precision Touch**.

---

## 1. CEL PROJEKTU

Zbudować w pełni autonomicznego bota dla platformy **cTrader**, który:
- wchodzi w pozycje na **wykresie 1-minutowym** (M1)
- filtruje trend z **wyższego interwału** (domyślnie M5, wybieralny)
- implementuje **precyzyjny warunek dotyku wstęg EMA** (Precision Touch)
- zarządza ryzykiem, Stop Lossem, Take Profitem i Trailing Stopem
- szanuje godziny handlu i dzienne limity strat/zysku

---

## 2. TECHNOLOGIA

| Element | Wybór |
|---|---|
| Platforma | cTrader (Spotware) |
| Język | C# (.NET 6) |
| API | cAlgo API (`using cAlgo.API;`) |
| Klasa bazowa bota | `Robot` (z `cAlgo.API`) |
| Parametry | Atrybut `[Parameter]` w klasie głównej |
| Wskaźniki | `Robot.Indicators.ExponentialMovingAverage(source, periods)` |
| Wieloczasowość | `Robot.MarketData.GetBars(TimeFrame)` |

---

## 3. STRUKTURA KATALOGÓW

```
HTSBot/
├── HTSBot.csproj
├── HTSBot.cs                    ← główna klasa [Robot] (Orchestrator)
├── Models/
│   ├── Enums.cs                 ← typy wyliczeniowe
│   ├── TradeSignal.cs           ← model sygnału
│   └── DailyStats.cs            ← statystyki dnia
├── Indicators/
│   └── HTSIndicatorSet.cs       ← inicjalizacja wszystkich EMA
├── Strategy/
│   └── SignalEngine.cs          ← logika sygnałów (HTS v2.6)
├── Risk/
│   └── RiskManager.cs           ← ryzyko, SL/TP, godziny, limity
└── Execution/
    └── ExecutionHandler.cs      ← otwieranie zleceń, trailing stop
```

> **Zasada:** każdy plik = jedna klasa = jedna odpowiedzialność (SOLID).  
> `HTSBot.cs` jest TYLKO orkiestratorem — nie zawiera żadnej logiki biznesowej.

---

## 4. OPIS KAŻDEGO MODUŁU

### 4.1 `Models/Enums.cs`

```csharp
public enum SignalType   { None, LongClassic, LongHook, ShortClassic, ShortHook }
public enum TradeDirection { Long, Short }
public enum SLTPType     { Dollar, Percent }
public enum SignalFilter  { Both, ClassicOnly, HookOnly }
```

---

### 4.2 `Models/TradeSignal.cs`

Niezmienny (immutable) snapshot sygnału:

| Właściwość | Typ | Opis |
|---|---|---|
| `Type` | `SignalType` | Typ sygnału |
| `Direction` | `TradeDirection` | Long / Short |
| `EntryPrice` | `double` | Close baru sygnałowego |
| `FastBandHigh` | `double` | Górna krawędź szybkiej wstęgi (fH) |
| `FastBandLow` | `double` | Dolna krawędź szybkiej wstęgi (fL) |
| `IsValid` | `bool` | `Type != None` |

Metoda statyczna: `TradeSignal.NoSignal()` → zwraca sentinel bez sygnału.

---

### 4.3 `Models/DailyStats.cs`

| Właściwość | Typ | Opis |
|---|---|---|
| `Date` | `DateTime` | Data UTC bieżącego dnia |
| `RealizedPnL` | `double` | Skumulowany P&L zamkniętych pozycji |
| `IsTradingHalted` | `bool` | Flaga zatrzymania na dany dzień |

Metody:
- `Reset()` — zeruje P&L i flagę, ustawia nową datę
- `IsNewDay(DateTime)` — porównuje datę UTC

---

### 4.4 `Indicators/HTSIndicatorSet.cs`

Inicjalizuje w konstruktorze **10 wskaźników EMA** przez `robot.Indicators.ExponentialMovingAverage(source, period)`:

| Nazwa pola | Źródło | Okres | Cel |
|---|---|---|---|
| `FastHigh` | exec high | fastLen | Górna krawędź szybkiej wstęgi |
| `FastLow` | exec low | fastLen | Dolna krawędź szybkiej wstęgi |
| `SlowHigh` | exec high | slowLen | Górna krawędź wolnej wstęgi |
| `SlowLow` | exec low | slowLen | Dolna krawędź wolnej wstęgi |
| `HtfFastHigh` | HTF high | fastLen | HTF trend filter |
| `HtfFastLow` | HTF low | fastLen | HTF trend filter |
| `HtfSlowHigh` | HTF high | slowLen | HTF trend filter |
| `HtfSlowLow` | HTF low | slowLen | HTF trend filter |
| `TrailHigh` | exec high | trailLen | Trailing SL dla Short |
| `TrailLow` | exec low | trailLen | Trailing SL dla Long |

Konstruktor przyjmuje: `(Robot robot, Bars execBars, Bars htfBars, int fastLen, int slowLen, int trailLen)`

---

### 4.5 `Strategy/SignalEngine.cs`

**Tłumaczenie Pine Script → C#:**

```
Pine Script                         C# (Last(1) = last closed bar)
───────────────────────────────────────────────────────────────────
fH = ema(high, eFastLen)         →  ind.FastHigh.Result.Last(1)
fL = ema(low,  eFastLen)         →  ind.FastLow.Result.Last(1)
sH = ema(high, eSlowLen)         →  ind.SlowHigh.Result.Last(1)
sL = ema(low,  eSlowLen)         →  ind.SlowLow.Result.Last(1)

kj = (highest(high,kLen)
     +lowest(low,kLen)) / 2      →  CalculateKijun() — pętla Last(1..kLen)

isBull = fL > sH                 →  IsBullTrend()
  + MTF: htfFastLow > htfSlowHigh

isBear = fH < sL                 →  IsBearTrend()
  + MTF: htfFastHigh < htfSlowLow

validTouchLong  = low<=fH        →  barLow <= fH
               AND low>=fL       →  AND barLow >= fL
               AND close>fH      →  AND barClose > fH

validTouchShort = high>=fL       →  barHigh >= fL
               AND high<=fH      →  AND barHigh <= fH
               AND close<fL      →  AND barClose < fL

longClassic  = isBull AND close>kj AND validTouchLong
shortClassic = isBear AND close<kj AND validTouchShort
longHook     = isBull AND close<kj AND validTouchLong
shortHook    = isBear AND close>kj AND validTouchShort
```

**Priorytet zwracania** (jeśli kilka prawd jednocześnie): Classic > Hook.

**Metoda publiczna:** `TradeSignal GetSignal()` — wywołać z `OnBar()`.

---

### 4.6 `Risk/RiskManager.cs`

#### A. Sprawdzenie godzin handlu
```
startMinutes = StartHour * 60 + StartMinute
endMinutes   = EndHour   * 60 + EndMinute
nowMinutes   = serverTime.Hour * 60 + serverTime.Minute

if startMinutes <= endMinutes:
    return nowMinutes >= startMinutes AND nowMinutes < endMinutes
else (sesja przez północ):
    return nowMinutes >= startMinutes OR nowMinutes < endMinutes
```

#### B. Sprawdzenie limitów dziennych
```
if DailyLossLimit > 0 AND RealizedPnL <= -DailyLossLimit  → HALT
if DailyProfitTargetDollar > 0 AND RealizedPnL >= DailyProfitTargetDollar → HALT
if DailyProfitTargetPercent > 0:
    target = Balance * (DailyProfitTargetPercent / 100)
    if RealizedPnL >= target → HALT
```

#### C. Obliczenie ceny Stop Lossa

```
Tryb Dollar:
  SL_dist = StopLossValue × TickSize / (VolumeInUnitsStep × TickValue)
  // Interpretacja: dla minimalnego stepа wolumenu, SL oddala się o StopLossValue $

Tryb Percent:
  SL_dist = EntryPrice × (StopLossValue / 100)

Cena SL dla Long  = EntryPrice - SL_dist
Cena SL dla Short = EntryPrice + SL_dist
```

#### D. Obliczenie ceny Take Profita
Identycznie jak SL, ale dodajemy/odejmujemy w przeciwnym kierunku.

#### E. Wielkość pozycji (Position Sizing)
```
RiskAmount = Balance × (RiskPercent / 100)
PriceValuePerUnit = TickValue / TickSize
Volume = RiskAmount / (SL_dist × PriceValuePerUnit)

Normalizacja:
  Volume = floor(Volume / VolumeInUnitsStep) × VolumeInUnitsStep
  Volume = clamp(Volume, VolumeInUnitsMin, VolumeInUnitsMax)
```

> **Zaokrąglenie w DÓŁ** (`floor`) — nigdy nie ryzykować więcej niż zakładane %.

---

### 4.7 `Execution/ExecutionHandler.cs`

#### A. Otwieranie pozycji (`OpenPosition`)
```
refPrice  = Ask (dla Buy) / Bid (dla Sell)   // cena rynkowa, nie close baru
slPrice   = riskManager.CalculateStopLossPrice(direction, refPrice)
tpPrice   = riskManager.CalculateTakeProfitPrice(direction, refPrice)
volume    = riskManager.CalculateVolume(refPrice, slPrice)
slPips    = Abs(refPrice - slPrice) / Symbol.PipSize
tpPips    = Abs(refPrice - tpPrice) / Symbol.PipSize

ExecuteMarketOrder(tradeType, Symbol.Name, volume, BotLabel, slPips, tpPips, comment)
```

> SL/TP przekazywane jako **pipy** (liczba dodatnia = odległość od ceny wejścia).
> Broker egzekwuje je na poziomie zlecenia.

#### B. Trailing Stop (`UpdateTrailingStop`)
```
Dla każdej pozycji z etykietą BotLabel:

  LONG:
    newSL = ind.TrailLow.Result.Last(1)   // EMA(low, trailLen)
    if position.StopLoss != null AND newSL <= currentSL → pomiń (nie cofaj SL)
    else → ModifyPosition(position, newSL, position.TakeProfit)

  SHORT:
    newSL = ind.TrailHigh.Result.Last(1)  // EMA(high, trailLen)
    if position.StopLoss != null AND newSL >= currentSL → pomiń
    else → ModifyPosition(position, newSL, position.TakeProfit)
```

`ModifyPosition` przyjmuje **absolutne ceny** (nie pipy).

#### C. Zamykanie awaryjne (`CloseAllPositions`)
Zamknij wszystkie pozycje z etykietą BotLabel. Wywołać na żądanie lub w `OnStop()`.

---

### 4.8 `HTSBot.cs` — Orkiestrator

#### Wszystkie parametry (z grupami i wartościami domyślnymi):

```
[EMA Settings]
  FastEmaLength       int     66
  SlowEmaLength       int     288
  KijunLength         int     26

[Trend Filter]
  EnableMTF           bool    true
  HTFTimeframe        TimeFrame  Minute5

[Signal Settings]
  AllowedSignals      SignalFilter  Both

[Risk Management]
  RiskPercent         double  1.0   (min 0.01, max 100)
  StopLossType        SLTPType  Percent
  StopLossValue       double  0.5   (min 0.001)
  TakeProfitType      SLTPType  Percent
  TakeProfitValue     double  1.0   (min 0.001)

[Trailing Stop]
  EnableTrailingStop  bool    false
  TrailEmaLength      int     33    (min 2, max 500)

[Trading Hours]
  TradingStartHour    int     8     (0-23)
  TradingStartMinute  int     0     (0-59)
  TradingEndHour      int     20    (0-23)
  TradingEndMinute    int     0     (0-59)

[Daily Limits]
  DailyLossLimit           double  0  (0=disabled)
  DailyProfitTargetDollar  double  0  (0=disabled)
  DailyProfitTargetPercent double  0  (0=disabled)
```

#### Metody lifecycle:

**`OnStart()`**
```
1. Utwórz DailyStats
2. Załaduj _htfBars = MarketData.GetBars(HTFTimeframe)
3. Utwórz HTSIndicatorSet(this, Bars, _htfBars, FastEmaLength, SlowEmaLength, TrailEmaLength)
4. Utwórz SignalEngine(indicators, Bars, _htfBars, KijunLength, EnableMTF, AllowedSignals)
5. Utwórz RiskManager(this, dailyStats) → przypisz wszystkie parametry ryzyka
6. Utwórz ExecutionHandler(this, riskManager, "HTS_BOT", EnableTrailingStop, indicators)
7. Subskrybuj Positions.Closed += OnPositionClosed
```

**`OnBar()`** (wywoływana przy zamknięciu każdego baru M1)
```
1. if dailyStats.IsNewDay(Server.Time) → dailyStats.Reset()
2. if !riskManager.IsWithinTradingHours(Server.Time) → return
3. if !riskManager.IsTradingAllowed() → return
4. if Positions.Any(p => p.Label == "HTS_BOT"):
     executionHandler.UpdateTrailingStop()
     return  ← tylko 1 pozycja naraz
5. signal = signalEngine.GetSignal()
6. if !signal.IsValid → return
7. executionHandler.OpenPosition(signal)
```

**`OnTick()`**
```
if !EnableTrailingStop → return
if !Positions.Any(p => p.Label == "HTS_BOT") → return
executionHandler.UpdateTrailingStop()
```

**`OnStop()`**
```
Zaloguj: "Stopped, Daily P&L: {dailyStats.RealizedPnL}"
Odsubskrybuj Positions.Closed
```

**`OnPositionClosed(PositionClosedEventArgs args)`**
```
if args.Position.Label != "HTS_BOT" → return
riskManager.UpdateDailyPnL(args.Position.GrossProfit)
Zaloguj P&L i daily total
```

---

## 5. SCHEMAT PRZEPŁYWU (FLOWCHART)

```
[Nowy bar M1 zamknięty]
         │
         ▼
[Czy nowy dzień UTC?] ──TAK──► Reset DailyStats
         │
         ▼
[Czy w godzinach handlu?] ──NIE──► return
         │
         ▼
[Czy nie przekroczono limitu dziennego?] ──NIE──► return (HALT)
         │
         ▼
[Czy jest otwarta pozycja HTS_BOT?] ──TAK──► UpdateTrailingStop() → return
         │ NIE
         ▼
[SignalEngine.GetSignal()]
         │
    ┌────┴────┐
  Brak      Signal
 sygnału   ┌──────────────────────┐
    │       │ • Oblicz SL/TP ceny  │
  return   │ • Oblicz Volume       │
           │ • ExecuteMarketOrder  │
           └──────────────────────┘

[OnTick — równolegle]
  if EnableTrailingStop → UpdateTrailingStop()

[OnPositionClosed]
  UpdateDailyPnL(GrossProfit)
```

---

## 6. KLUCZOWE ZASADY PISANIA KODU

1. **Typowanie** — C# z `Nullable enable`, wszystkie właściwości z typami
2. **Docstringi** — każda klasa i publiczna metoda ma `/// <summary>`
3. **Logowanie** — każde istotne zdarzenie przez `robot.Print()` z prefiksem `[NazwaKlasy]`
4. **Fail-safe** — sprawdzaj `TradeResult.IsSuccessful` po każdym `ExecuteMarketOrder` i `ModifyPosition`
5. **Jedna pozycja** — bot nie otwiera nowej pozycji dopóki poprzednia nie zostanie zamknięta
6. **Brak hardkodowanych kluczy** — wszystko przez parametry `[Parameter]` cTradera
7. **Volume round DOWN** — zaokrąglaj `Math.Floor`, nie `Math.Round`
8. **Trailing tylko w korzystnym kierunku** — SL dla Long może tylko rosnąć, dla Short tylko maleć

---

## 7. INSTALACJA W CTRADER

1. Otwórz cTrader → **Automate** → **New cBot**
2. Odtwórz strukturę folderów: `Models/`, `Indicators/`, `Strategy/`, `Risk/`, `Execution/`
3. Wklej każdy plik `.cs` do odpowiedniego folderu
4. Kliknij **Build** — cTrader kompiluje wszystko automatycznie
5. Dołącz bota do wykresu **M1** i ustaw parametry w panelu

---

## 8. CO JESZCZE MOŻNA DODAĆ (BACKLOG)

- [ ] Maksymalna liczba pozycji dziennie
- [ ] Filtr dni tygodnia (np. brak handlu w piątek po 20:00)
- [ ] News filter (blokada X minut przed/po newsach)
- [ ] Breakeven (przesuń SL na BE po osiągnięciu X% zysku)
- [ ] Powiadomienia Telegram/e-mail po otwarciu/zamknięciu pozycji
- [ ] Panel informacyjny na wykresie (Chart.DrawText)
- [ ] Backtesting raport eksport CSV

---

## 9. WERYFIKACJA ZROZUMIENIA STRATEGII

| Pytanie | Odpowiedź |
|---|---|
| Rynek | Forex/CFD na cTrader (np. EURUSD, NAS100, XAGUSD) |
| API | cTrader cAlgo (C#) |
| Timeframe wejść | M1 (1 minuta) |
| Timeframe trendu | M5 domyślnie (wybieralny) |
| Strategia | HTS RAW v2.6 — EMA bands precision touch |
| Wejście Long | Bar dotyka dolnej części szybkiej wstęgi od góry + zamknięcie powyżej górnej krawędzi |
| Wejście Short | Bar dotyka górnej części szybkiej wstęgi od dołu + zamknięcie poniżej dolnej krawędzi |
| Classic vs Hook | Classic = z potwierdzeniem Kijun; Hook = przeciwko Kijun |
| Stop Loss | Poniżej szybkiej wstęgi (trailing) lub stały % / $ od wejścia |
| Trailing SL | Podąża za EMA(low/high, 33) — tylko w korzystnym kierunku |
| Take Profit | % lub $ od ceny wejścia |
| Ryzyko | % kapitału na trade — dynamiczne pozycjonowanie |
| Język | C# (nie Python — cTrader wymaga C#) |

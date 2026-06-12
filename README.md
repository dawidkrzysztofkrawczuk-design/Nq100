# HTS RAW v2.6 — cTrader cBot

Produkcyjny cBot do platformy **cTrader** (cAlgo API, C# / .NET 6) implementujący strategię
**HTS RAW v2.6 — Precision Touch**: Classic + Hook signals na wstędze EMA + filtr trendu HTF
+ Kijun-Sen, ze zarządzaniem ryzykiem, partial close, EMA trailing stop, dziennymi limitami,
godzinami handlu (per dzień tygodnia), panelem na wykresie i eksportem CSV.

> Repozytorium budowane jest **przyrostowo**, moduł po module, według planu
> w sekcji 16 specyfikacji. Aktualnie ukończona iteracja: **Iteracja 1 – Szkielet + Sygnały**.

## Status iteracji

| Iter. | Zakres | Status |
|------:|---|:---:|
| 1 | Szkielet projektu, wszystkie modele, wskaźniki HTS (EMA + HTF + Kijun), `SignalEngine`, orkiestrator z detekcją sygnałów (logowanie do `Print`). | ✅ |
| 2 | `RiskManager` (position sizing, SL/TP) + `ExecutionHandler` (otwieranie pozycji, fail-safe) | ⏳ |
| 3 | EMA trailing stop + partial close | ⏳ |
| 4 | Godziny handlu (7 dni) + dzienne limity (loss / profit $/%) | ⏳ |
| 5 | `ChartPanel` (P&L, status, transakcje) + `CsvExporter` | ⏳ |
| 6 | Backtest end-to-end + kalibracja parametrów | ⏳ |

## Struktura projektu

```
HTSBot/
├── HTSBot.csproj
├── HTSBot.cs                       ← [Robot] Orchestrator
├── Models/
│   ├── Enums.cs
│   ├── TradeSignal.cs
│   ├── DailyStats.cs
│   └── TradingSchedule.cs
├── Indicators/
│   └── HTSIndicatorSet.cs          ← EMA fast/slow + HTF + TrailEMA
├── Strategy/
│   └── SignalEngine.cs             ← logika HTS RAW v2.6
├── Risk/                            (iteracja 2)
├── Execution/                       (iteracja 2)
├── UI/                              (iteracja 5)
└── Export/                          (iteracja 5)
```

## Wymagania

- cTrader Desktop (5.x lub nowszy) z modułem **Automate**
- .NET 6 SDK (do edycji w Visual Studio / Rider, opcjonalne — cTrader IDE buduje sam)

## Instalacja w cTrader IDE

1. Otwórz cTrader → **Automate** → **Robots** → ikona **+** → **New Robot**.
2. Nazwij projekt **HTSBot**.
3. W panelu projektu, dla każdego pliku z katalogu `HTSBot/`:
   - Kliknij PPM → **Add file** lub **Add folder** zachowując strukturę katalogów.
   - Skopiuj zawartość każdego `.cs` z tego repo do nowo utworzonego pliku.
4. Kliknij **Build** (Ctrl+B). Komunikat „Build succeeded” oznacza poprawną kompilację.
5. Przeciągnij **HTSBot** z listy robotów na wykres → ustaw parametry → **Play**.

> Plik `HTSBot.csproj` jest pomocniczy do pracy poza cTrader IDE (np. w Riderze /
> Visual Studio / CI). cTrader Automate ma własny system buildów oparty o `cAlgo.API`.

## Konfiguracja parametrów (główne grupy)

- **EMA Settings** — długości fast/slow EMA, Kijun, Trail EMA.
- **Timeframes** — interwał HTF (filtr trendu). Execution TF = TF wykresu, do którego dołączamy bota.
- **Signal Settings** — przełączniki Classic/Hook + tryb pyramiding.
- **Risk Management** — % kapitału, typ i wartość SL, typ i wartość TP, R:R ratio.
- **Partial Close** — TP1 + % wolumenu do zamknięcia (przesuwa SL na BE).
- **Trailing Stop** — włącznik EMA trailing.
- **Daily Limits** — limit straty $, cele zysku $/%, automatyczne zamknięcie pozycji przy hicie.
- **Trading Hours - Mon..Sun** — osobny harmonogram dla każdego dnia tygodnia (UTC).
- **After Hours** — czy zamykać pozycje gdy sesja się skończyła.
- **Display / Export** — panel + CSV.

## Bezpieczeństwo

Kod realizuje zasady bezpiecznego deweloperu botów:
- Brak hardkodowanych kluczy/sekretów — bot używa wyłącznie API cTradera (kontekst sesji platformy).
- Wszystkie operacje I/O i tradingowe owinięte w `try/catch` z logiem do `Print`.
- Walidacja parametrów w `OnStart` z zatrzymaniem bota (`Stop()`) przy błędnej konfiguracji.
- Pozycje otwierane wyłącznie z etykietą `HTS_BOT` — bot nie dotyka cudzych zleceń.
- (Iter. 2+) `TradeResult.IsSuccessful` sprawdzane po każdym zleceniu, otwarte zlecenia
  anulowane fail-safe przy błędzie.

## Testowanie

W cTrader Automate → wybierz **HTSBot** → **Backtesting**. Wymagane do akceptacji iteracji:

- **Iteracja 1**: w logu (Print) pojawiają się sygnały `LongClassic / LongHook / ShortClassic / ShortHook`
  na barach spełniających warunki HTS RAW v2.6. Brak otwierania pozycji.
- **Iteracja 6**: pełny test historyczny + eksport CSV + weryfikacja position sizing
  na min. 3 instrumentach (Forex, indeks, krypto).

## Licencja

Projekt prywatny — właściciel decyduje o licencji.

# HTS RAW v2.6 — cTrader cBot

Bot do platformy **cTrader** (cAlgo API, C# / .NET 6) — strategia **HTS RAW v2.6 — Precision Touch**.
Status: **Iteracja 1 z 6** (szkielet projektu + detekcja sygnałów; zarządzanie ryzykiem,
trailing, dzienne limity, panel i CSV w kolejnych iteracjach).

---

## ⚡ Najszybszy start (3 minuty) — wersja JEDNOPLIKOWA

To jest **jeden** bot, który ma być uruchomiony jako **jeden** robot w cTrader.
Najprostszy sposób:

1. Otwórz cTrader → **Automate** → **Robots** → kliknij **+** (New Robot) → wpisz nazwę **`HTSBot`**.
2. cTrader otworzy edytor z wygenerowanym domyślnym plikiem `.cs`. **Zaznacz całą jego zawartość (Ctrl+A) i USUŃ.**
3. Otwórz plik [`HTSBot/HTSBot_Single.cs`](HTSBot/HTSBot_Single.cs) z tego repo, **skopiuj WSZYSTKO (Ctrl+A → Ctrl+C)**.
4. **Wklej (Ctrl+V)** do edytora cTrader IDE.
5. Kliknij **Build** (Ctrl+B). Powinno pojawić się „Build succeeded".
6. Przeciągnij **HTSBot** z listy robotów na wykres → ustaw parametry → kliknij **Play**.

To wszystko. Plik `HTSBot_Single.cs` zawiera w środku **wszystkie** klasy
(modele, wskaźniki, silnik sygnałów, orchestrator) — w cTrader na liście robotów
zobaczysz **tylko jeden** wpis: **HTSBot**.

> ⚠️ **Nie używaj wersji jednoplikowej i wieloplikowej jednocześnie.** Jeśli wkleisz
> `HTSBot_Single.cs`, **nie dodawaj** plików z katalogów `Models/`, `Indicators/`,
> `Strategy/` — kompilator zgłosiłby błąd „duplikat klasy HTSBot".

### Co bot robi po uruchomieniu (Iteracja 1)

- Inicjalizuje wszystkie EMA + filtr trendu HTF + Kijun-Sen.
- Na każdym **zamkniętym** barze sprawdza warunki HTS RAW v2.6.
- Loguje wykryty sygnał w panelu **Log** cTradera, np.:
  ```
  [HTSBot][SIGNAL] LongClassic EURUSD bar=2026-06-12 14:30 close=1.07823 fH=... fL=... kj=...
  ```
- **Nie otwiera pozycji.** Otwieranie zleceń + SL/TP + position sizing dochodzi w **Iteracji 2**.

---

## 🛠️ Wersja wieloplikowa (dla devów / Visual Studio / Rider)

Jeśli pracujesz w **Visual Studio** lub **Rider** poza cTrader IDE, użyj wersji
modułowej z katalogów:

```
HTSBot/
├── HTSBot.csproj
├── HTSBot.cs                       ← [Robot] Orchestrator
├── Models/{Enums,TradeSignal,DailyStats,TradingSchedule}.cs
├── Indicators/HTSIndicatorSet.cs
└── Strategy/SignalEngine.cs
```

Każda klasa siedzi w osobnym pliku zgodnie z zasadą SOLID (sekcja 3 specyfikacji).
**To są te SAME klasy** co w `HTSBot_Single.cs` — tylko rozłożone na pliki.

Wybierz **jedną z dwóch ścieżek**:

| Ścieżka | Kiedy wybrać | Co skopiować do cTrader |
|---|---|---|
| 🟢 **Jednoplikowa** (zalecana) | Pracujesz **wyłącznie** w cTrader Automate IDE | Tylko `HTSBot/HTSBot_Single.cs` |
| 🔵 Wieloplikowa | Edytujesz w VS/Rider, używasz CI, chcesz „czysty" SOLID | `HTSBot.cs` + wszystkie pliki z `Models/`, `Indicators/`, `Strategy/` |

---

## Status iteracji

| Iter. | Zakres | Status |
|------:|---|:---:|
| 1 | Szkielet projektu, modele, wskaźniki HTS, `SignalEngine`, orkiestrator z detekcją sygnałów | ✅ |
| 2 | `RiskManager` (position sizing, SL/TP) + `ExecutionHandler` (otwieranie pozycji, fail-safe) | ⏳ |
| 3 | EMA trailing stop + partial close | ⏳ |
| 4 | Godziny handlu (7 dni) + dzienne limity (loss / profit $/%) | ⏳ |
| 5 | `ChartPanel` (P&L, status, transakcje) + `CsvExporter` | ⏳ |
| 6 | Backtest end-to-end + kalibracja parametrów | ⏳ |

---

## Konfiguracja parametrów (główne grupy)

- **EMA Settings** — długości fast/slow EMA, Kijun, Trail EMA.
- **Timeframes** — interwał HTF (filtr trendu). Execution TF = TF wykresu, do którego dołączysz bota.
- **Signal Settings** — przełączniki Classic/Hook + tryb pyramiding.
- **Risk Management** — % kapitału, typ i wartość SL, typ i wartość TP, R:R ratio. *(aktywne od iter. 2)*
- **Partial Close** — TP1 + % wolumenu do zamknięcia. *(iter. 3)*
- **Trailing Stop** — włącznik EMA trailing. *(iter. 3)*
- **Daily Limits** — limit straty $, cele zysku $/%, auto-zamknięcie pozycji. *(iter. 4)*
- **Trading Hours - Mon..Sun** — osobny harmonogram dla każdego dnia tygodnia (UTC). *(iter. 4)*
- **After Hours** — czy zamykać pozycje po godzinach. *(iter. 4)*
- **Display / Export** — panel + CSV. *(iter. 5)*

---

## Bezpieczeństwo

- Brak hardkodowanych kluczy — bot używa wyłącznie API cTradera (kontekst sesji platformy).
- Wszystkie operacje I/O i tradingowe owinięte w `try/catch` z logiem do `Print`.
- Walidacja parametrów w `OnStart` z zatrzymaniem bota (`Stop()`) przy błędnej konfiguracji.
- Pozycje otwierane wyłącznie z etykietą `HTS_BOT` — bot nie dotyka cudzych zleceń.
- (Iter. 2+) `TradeResult.IsSuccessful` sprawdzane po każdym zleceniu, fail-safe na błędach sieci.

## Test acceptance dla iter. 1

W cTrader Automate → wybierz **HTSBot** → **Backtesting** → uruchom na dowolnym
Forex M1 z HTF=M5. W zakładce **Log** powinny pojawić się wpisy
`[HTSBot][SIGNAL] LongClassic / LongHook / ShortClassic / ShortHook`. Brak otwierania pozycji.

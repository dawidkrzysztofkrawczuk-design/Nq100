# HTS RAW v2.6 cTrader Bot

A production-grade cTrader cBot that implements the **HTS RAW v2.6 – Precision Touch** strategy in C# using the cAlgo API.

## Architecture

```
HTSBot/
├── HTSBot.cs                   ← Orchestrator (Robot entry point + all [Parameter] declarations)
├── Models/
│   ├── Enums.cs                ← SignalType, TradeDirection, SLTPType, SignalFilter
│   ├── TradeSignal.cs          ← Immutable signal snapshot
│   └── DailyStats.cs           ← Intraday P&L tracker
├── Indicators/
│   └── HTSIndicatorSet.cs      ← All EMA band instances (exec TF + HTF + trailing)
├── Strategy/
│   └── SignalEngine.cs         ← HTS signal logic (trend + precision touch + Kijun)
├── Risk/
│   └── RiskManager.cs          ← Position sizing, SL/TP prices, daily limits, trading hours
└── Execution/
    └── ExecutionHandler.cs     ← Market orders, EMA trailing stop, emergency close
```

## Strategy Logic (HTS RAW v2.6)

| Element | Pine Script | C# equivalent |
|---|---|---|
| Fast band | `ema(high/low, 66)` | `HTSIndicatorSet.FastHigh / FastLow` |
| Slow band | `ema(high/low, 288)` | `HTSIndicatorSet.SlowHigh / SlowLow` |
| Kijun-Sen | `(highest(26) + lowest(26)) / 2` | `SignalEngine.CalculateKijun()` |
| Bull trend | `fL > sH` (+ HTF) | `SignalEngine.IsBullTrend()` |
| Bear trend | `fH < sL` (+ HTF) | `SignalEngine.IsBearTrend()` |
| Precision touch Long | `low ∈ [fL, fH] AND close > fH` | `validLong` |
| Precision touch Short | `high ∈ [fL, fH] AND close < fL` | `validShort` |
| Classic signal | Kijun confirmation | `longClassic / shortClassic` |
| Hook signal | Against Kijun | `longHook / shortHook` |

## Parameters

### EMA Settings
| Parameter | Default | Description |
|---|---|---|
| Fast EMA Period | 66 | Fast band EMA length |
| Slow EMA Period | 288 | Slow band EMA length |
| Kijun-Sen Period | 26 | Highest/Lowest lookback |

### Trend Filter
| Parameter | Default | Description |
|---|---|---|
| Enable MTF Trend Filter | true | Use higher-timeframe trend |
| HTF Timeframe | Minute5 | Higher-timeframe for trend |

### Signal Settings
| Parameter | Default | Options |
|---|---|---|
| Signal Filter | Both | Both / ClassicOnly / HookOnly |

### Risk Management
| Parameter | Default | Description |
|---|---|---|
| Risk % per Trade | 1.0 | % of account balance risked |
| Stop Loss Type | Percent | Dollar or Percent |
| Stop Loss Value | 0.5 | If % → 0.5% of entry; if $ → $ per min volume step |
| Take Profit Type | Percent | Dollar or Percent |
| Take Profit Value | 1.0 | Same interpretation as SL |

### Trailing Stop
| Parameter | Default | Description |
|---|---|---|
| Enable Trailing Stop | false | EMA-based trailing SL |
| Trailing Stop EMA Period | 33 | LONG: trail to EMA(low,33); SHORT: trail to EMA(high,33) |

### Trading Hours (UTC)
| Parameter | Default |
|---|---|
| Trading Start Hour | 8 |
| Trading Start Minute | 0 |
| Trading End Hour | 20 |
| Trading End Minute | 0 |

### Daily Limits
| Parameter | Default | Description |
|---|---|---|
| Daily Loss Limit ($) | 0 | 0 = disabled. Bot stops when daily loss ≥ this value |
| Daily Profit Target ($) | 0 | 0 = disabled. Bot stops when daily profit ≥ this value |
| Daily Profit Target (%) | 0 | 0 = disabled. % of account balance |

## Position Sizing Formula

```
Risk Amount  = Account Balance × (Risk % / 100)
SL Distance  = EntryPrice × (SL% / 100)          [Percent mode]
             = SL$ × TickSize / (VolumeStep × TickValue)  [Dollar mode]
Volume       = Risk Amount / (SL Distance × TickValue / TickSize)
```

Volume is rounded DOWN to the nearest `VolumeInUnitsStep` and clamped to `[VolumeInUnitsMin, VolumeInUnitsMax]`.

## Dollar SL Interpretation

A "Dollar SL" of **$X** means: for the symbol's minimum volume step (e.g. 1000 units on EURUSD),
the SL is placed X dollars away in the account currency. Position size is then calculated so the
total risk equals `Account Balance × Risk%`.

**Example** — EURUSD, $10 000 account, 1% risk, Dollar SL = $10:
- Min vol step = 1 000 units, TickValue = 0.00001, TickSize = 0.00001
- SL price distance = 10 × 0.00001 / (1000 × 0.00001) = **0.010** (100 pips)
- Volume = (10 000 × 0.01) / (0.010 × 1.0) = **100 000 units** (1 std lot)
- Actual loss at SL = 100 000 × 0.010 × 1.0 = **$100** ✓ (= 1% of $10 000)

## Installation in cTrader

### Option A — cTrader cAlgo IDE (recommended)
1. Open cTrader → Automate → New → cBot
2. In the cAlgo editor, create matching folders: `Models/`, `Indicators/`, `Strategy/`, `Risk/`, `Execution/`
3. Copy each `.cs` file into the corresponding folder
4. Click **Build** — cTrader compiles and links all files automatically
5. The bot appears under **My Robots** ready to attach to any chart

### Option B — Import compiled DLL
1. Set `CTRADER_PATH` to your cTrader installation directory
2. Run `dotnet build -c Release`
3. Drop the output `.dll` into cTrader's `Robots` directory
4. Restart cTrader

## Running in Backtesting
- Attach the bot to a **M1** (1-minute) chart
- Enable "Use tick data" for highest accuracy
- Configure parameters in the backtester parameter panel
- Backtesting respects all risk rules, trading hours, and daily limits

## Risk Warnings
- This is algorithmic trading software. Past performance of a strategy does not guarantee future results.
- Always test thoroughly in backtesting and on a demo account before live trading.
- Never risk more than you can afford to lose.

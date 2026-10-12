# Changelog

## Unreleased

- The main window shows the active data folder and `AppState.json` path. Click that line to open the folder in Explorer. The File menu can open an AppState.json file, choose a data folder, open the folder, or reset to `Documents\ExitPlan`. The choice is saved in `%LocalAppData%\ExitPlan\data-location.json` (not in the install folder), so a Velopack update keeps it. If the saved folder is missing, ExitPlan uses the default folder and says so. Choosing a folder that is missing data files lists them and can create them from the built-in defaults without overwriting existing files.
- Windows install and in-app updates use [Velopack](https://velopack.io). `.\publish.ps1` produces `ExitPlan-win-Setup.exe`, a full nupkg, and `releases.win.json`. Packages are published to the public repository https://github.com/pwscho/ExitPlan. In-app checks and `vpk download` do not use a token.
- Scenarios and edited tax, RMD, and market files are stored in `Documents\ExitPlan` so an update does not erase them. The first launch copies existing copies from an older install when those files are not already there.

## 2.3.0

- **Increase baseline with inflation** guardrail option (PR #30).
- Social Security taxation thresholds fixed at statutory amounts, capital gains in provisional income, IRMAA included in the Investment Return, Account Withdrawals, and Percent Withdrawals columns (PR #29).

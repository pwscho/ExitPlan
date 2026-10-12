# Changelog

## Unreleased

- Windows install and in-app updates use [Velopack](https://velopack.io). `.\publish.ps1` produces `ExitPlan-win-Setup.exe`, a full nupkg, and `releases.win.json`. Packages are published to the public repository https://github.com/pwscho/ExitPlan. In-app checks and `vpk download` do not use a token.
- Scenarios and edited tax, RMD, and market files are stored in `Documents\ExitPlan` so an update does not erase them. The first launch copies existing copies from an older install when those files are not already there.

## 2.3.0

- **Increase baseline with inflation** guardrail option (PR #30).
- Social Security taxation thresholds fixed at statutory amounts, capital gains in provisional income, IRMAA included in the Investment Return, Account Withdrawals, and Percent Withdrawals columns (PR #29).

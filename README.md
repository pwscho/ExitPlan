# ExitPlan

ExitPlan is a household retirement planning model for Windows. You enter accounts, income, spending, and Social Security, and it projects year by year: balances, withdrawals, taxes, and how long the money lasts.

Current version: **2.3.0**. Release notes: [CHANGELOG.md](CHANGELOG.md).

## Download

Installers are published on GitHub Releases:

**https://github.com/pwscho/ExitPlan/releases/latest**

Download **`ExitPlan-win-Setup.exe`**. You do not need Visual Studio or the .NET Desktop Runtime. The installer is per-user and does not ask for an administrator password.

When a newer release is on GitHub, ExitPlan asks `ExitPlan X.Y.Z is available. Update now?` You can also use **Check for updates** in the left sidebar. Nothing is downloaded unless you choose **Yes**.

Windows SmartScreen may say **“Windows protected your PC”** until the file has a reputation. Choose **More info**, then **Run anyway**. Releases are intended to be Authenticode-signed on a local PC (the certificate is not stored in this repository or in CI). The Release page lists the SHA256 checksum of `ExitPlan-win-Setup.exe`. Compare it with the file you downloaded if you want to confirm they match.

## A model, not advice

ExitPlan is a planning model. It is not financial, tax, or Social Security advice, and it is not a guarantee of future results. Tax and Medicare tables go stale. Full retirement age modeling is simplified.

Each time ExitPlan opens, a short disclaimer is shown first. Continue to use the program. Closing that window exits.

Your scenarios stay on your PC, in `Documents\ExitPlan\Data\AppState.json` (tax, RMD, and market files you edit are in that same `Documents\ExitPlan` folder). ExitPlan does not send usage data. An update replaces the program files and leaves that folder alone.

A license will be chosen before a wider release.

## Using ExitPlan

A **scenario** is a complete set of assumptions: accounts, income, spending, Social Security, tax rules, and simulation settings. You may create multiple scenarios to compare strategies.

Each year of the simulation produces a flat **ResultRow**: year, account balances, contributions, withdrawals, RMDs, taxes, Social Security, investment returns, and ending balances.

Work through the tabs from left to right.

### Scenario

High-level parameters: start year, inflation, spending need, and spending growth plan (Fixed, Historical, or Guardrails). Guardrails compares the portfolio to a baseline (the Initial Portfolio Value) and cuts or raises spending growth when the portfolio crosses the lower or upper trigger. If that baseline is left at $0, ExitPlan fills it from starting account balances when you run so Guardrails is not identical to Fixed. **Increase baseline with inflation** (off by default) grows that baseline each year with the same inflation as spending, so later years compare the portfolio to an inflation-adjusted target. Left off, the triggers keep using the original portfolio value. **Increase floor with inflation** is separate: it grows the minimum spending floor, not the portfolio baseline.

### Accounts

Accounts are categorized as Tax Deferred, Tax Free, and Taxable. Enter starting balances, expected returns, spending bucket, and withdrawal priority. Spending uses the lowest bucket first, then priority. Tax and IRMAA skip Tax Free when another account can pay.

### Social Security

Enter details for each household member (Primary and Spouse): current age, benefit start age, benefit at full retirement age, and COLA. Claiming is limited to ages 62–70. The full-retirement-age adjustment is a simplified yearly factor, not the Social Security Administration’s month-by-month formula.

### Income

Add non-Social-Security income and mark whether it is taxable as ordinary income.

### Withdrawals

Special withdrawals (for example, one-time expenses). Inflation does not apply to these.

### Roth Conversions

Convert funds from Traditional to Roth. Converted amounts are ordinary income. If a matching Tax Free account of the same asset class is missing, ExitPlan creates one.

### Results

Year-by-year balances, withdrawals, RMDs, taxes, Social Security, investment returns, ending balances, and guardrail adjustments (shown in base spending). **Monte Carlo Settings** next to **Generate Report** opens a dialog for simulation count, seed, and **Randomize**. **Randomize** picks a new integer; reports reuse the saved seed until you change it. The values still save with the scenario.

Click **Run Simulation** for a one-pass fixed simulation. The Results tab and Owner/Tax Bracket Summary update immediately. **Generate Report** also runs historical and Monte Carlo paths using the scenario's Monte Carlo seed and simulation count. The same seed and count produce the same market paths; **Randomize** is the only way to pick a new seed.

Success means the last year's **EndingBalance** is greater than zero.

Key fields include StartingBalance, InvestmentReturn, Withdrawal, TaxesDue, RMD, MAGI (uses taxable Social Security, not gross benefits), IrmaaPaid (Medicare IRMAA surcharge cash paid in year N+2), and EndingBalance.

Tax years 2026 and later use **Tax2026.json** (Rev. Proc. 2025-32 plus CMS 2026 Part B+D IRMAA). Years 2025 and earlier use **Tax2025.json**. Leftover standard deduction can shelter capital gains. IRMAA cliffs and surcharges inflate with tax-bracket inflation.

The startup disclaimer is the fuller notice. A shorter line stays on the main window (bottom of the left sidebar) and, shorter still, in the footer of a generated PDF summary.

## Developer

ExitPlan is a Visual Studio solution with three live projects:

- **ExitPlan.Core** — the simulation engine (`net8.0`)
- **ExitPlan.UI** — the Windows desktop interface (WPF, `net8.0-windows`, startup project)
- **ExitPlan.Core.Tests** — xUnit tests for engine behavior

A historical 1.0 snapshot/stub remains in the `ExitPlan/` folder and is not the live product.

### Open in Visual Studio

Copy the files to your machine and open **ExitPlan.sln**.

1. Build the solution to restore NuGet packages and compile the code.
2. Set **ExitPlan.UI** as the startup project.
3. Run the application.

`dotnet test` on **ExitPlan.Core.Tests** should pass. The WPF UI cannot be *run* on Linux. A win-x64 folder publish and a Velopack `ExitPlan-win-Setup.exe` can still be produced there (`EnableWindowsTargeting`, `vpk pack --runtime win-x64`). That is expected.

### Windows installer

Preferred path for other people: GitHub Releases, as in **[DISTRIBUTE.md](DISTRIBUTE.md)** (bump the version, `git tag vX.Y.Z` on `master`, or **Actions → Windows installer → Run workflow**). That produces `ExitPlan-win-Setup.exe` plus the Velopack nupkg and `releases.win.json` on `windows-latest`. The publisher Authenticode-signs locally and publishes the SHA256 on the Release. CI does not hold a signing certificate.

Local build on a PC with the .NET 8 SDK:

```powershell
.\publish.ps1
```

The installer lands in `artifacts\`. Detailed local steps: **[docs/DEPLOY.md](docs/DEPLOY.md)**. This is packaging and update delivery; the simulation engine is unchanged. Releases and in-app updates use the public repository https://github.com/pwscho/ExitPlan. Source stays in this private repository (`ExitPlan-dev` after the rename). That is covered in DISTRIBUTE.md.

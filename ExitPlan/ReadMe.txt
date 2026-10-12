ExitPlan
A lightweight, high‑accuracy retirement simulation engine built for clarity, reproducibility, and long‑term maintainability.

Features
Year‑by‑year financial simulation with deterministic and Monte Carlo modes

Flat ResultRow output for fast reporting and charting

Account‑level balance tracking with contributions, withdrawals, and RMD logic

Modular tax engine with year‑specific rule sets

Clean separation of UI and core logic (MVVM‑friendly)

Append‑friendly historical market data structures

Project Structure
Code
ExitPlan/
 ├── ExitPlan.Core/     # Simulation engine and domain models
 └── ExitPlan.UI/       # WPF front‑end
Getting Started
Build
bash
git clone https://github.com/pwscho/ExitPlan.git
cd ExitPlan
dotnet build
Run (WPF UI)
bash
cd ExitPlan.UI
dotnet run
How It Works
The engine produces a flat list of ResultRow objects:

Code
Year | AccountId | Category | Value | HouseholdId | ScenarioId
This structure keeps memory usage low and makes charting, exporting, and scenario comparison straightforward.

Development Philosophy
Transparent, explicit logic

Reproducible results

Scenario isolation

Minimal allocations and clean data flow

Easy annual updates to tax and market data

License
MIT License — see LICENSE.
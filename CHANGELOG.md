# Changelog

## Unreleased

- Delete Scenario, on the File menu and the sidebar button, asks `Are you sure you want to delete the scenario "<name>"? This cannot be undone.` Yes and No are the choices, and No is selected. The scenario is deleted only when you choose Yes.
- The menu bar is File, Config, and Help. File has Add Scenario, Copy Scenario, and Delete Scenario (the same commands as the sidebar buttons; Copy and Delete stay disabled when no scenario is selected) and Close, shown with Alt+F4, which closes the window the same way as the title-bar button. Config, between File and Help, has Tax Settings, RMD Settings, and Market Data (those sidebar buttons are gone) and then the data-folder commands. Help is unchanged (Check for updates, About ExitPlan).
- Help → About ExitPlan shows the icon and name, the version, publisher, copyright, the PolyForm Noncommercial license (with a link and a Show LICENSE.md button when that file ships), project and releases links, the update channel and status, the short simulation disclaimer, and the active data folder and `AppState.json` paths as selectable text with Copy. Check for updates is on the Help menu. The version is the Velopack installed version when this copy was installed, otherwise the assembly informational version with the `+git` suffix removed.
- The data folder and `AppState.json` path are no longer shown at the bottom of the main window. The Config menu can open an AppState.json file, choose a data folder, open the folder, or reset to `Documents\ExitPlan`. The choice is saved in `%LocalAppData%\ExitPlan\data-location.json` (not in the install folder), so a Velopack update keeps it. If the saved folder is missing, ExitPlan uses the default folder and says so. Choosing a folder that is missing data files lists them and can create them from the built-in defaults without overwriting existing files.
- Assembly, file, and informational versions follow `<Version>` in the csproj (2.4.2). The explicit `<InformationalVersion>2.3</InformationalVersion>` was left behind after later bumps, and the SDK appends `+` plus the git commit, so Windows showed ProductVersion `2.3+<hash>`. `<FileVersion>` had also stayed at 2.4.0.0. There is no `Directory.Build.props`, and `publish.ps1` and the installer workflow do not pass `-p:Version`. `LICENSE.md` is copied into the published folder.
- Windows install and in-app updates use [Velopack](https://velopack.io). `.\publish.ps1` produces `ExitPlan-win-Setup.exe`, a full nupkg, and `releases.win.json`. Packages are published to the public repository https://github.com/pwscho/ExitPlan. In-app checks and `vpk download` do not use a token.
- Scenarios and edited tax, RMD, and market files are stored in `Documents\ExitPlan` so an update does not erase them. The first launch copies existing copies from an older install when those files are not already there.

## 2.3.0

- **Increase baseline with inflation** guardrail option (PR #30).
- Social Security taxation thresholds fixed at statutory amounts, capital gains in provisional income, IRMAA included in the Investment Return, Account Withdrawals, and Percent Withdrawals columns (PR #29).


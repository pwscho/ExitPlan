# Deploy ExitPlan for Windows

This is the **local** recipe. To ship a public download, bump the version, tag `vX.Y.Z` on `master` (or run **Actions → Windows installer**), and send **https://github.com/pwscho/ExitPlan/releases/latest**. See **[DISTRIBUTE.md](../DISTRIBUTE.md)**.

The installer is [Velopack](https://velopack.io). It is a self-contained setup program, so someone can run ExitPlan **without** installing the .NET Desktop Runtime or Visual Studio. The same packages are what the app downloads later when the user chooses **Check for updates**.

You need the **.NET 8 SDK**. `.\publish.ps1` installs the Velopack CLI (`vpk` 1.2.161, the same version as the Velopack NuGet package) if it is not already installed. The UI is WPF (`ExitPlan.UI`, `net8.0-windows`, product version **2.3.0**). GitHub Actions `windows-latest` is the usual way to pack a release; this page is the same script run by hand.

Linux can `dotnet publish` the win-x64 folder because the UI project sets `EnableWindowsTargeting`, and `.\publish.ps1` packs the Windows installer from Linux as well (`vpk [win] pack --runtime win-x64 --channel win`). You still cannot *run* the WPF app on Linux.

## What you will produce

| Output | Where it lands |
| --- | --- |
| Published app folder | `artifacts\win-x64\` |
| Installer | `artifacts\ExitPlan-win-Setup.exe` |
| Full package | `artifacts\ExitPlan-2.3.0-full.nupkg` |
| Delta package | `artifacts\ExitPlan-<version>-delta.nupkg` only when an older Velopack package is already in `artifacts\` |
| Update feed | `artifacts\releases.win.json` |

`ExitPlan-win-Setup.exe` is a per-user installer. It does not ask for an administrator password. It installs to **`%LocalAppData%\ExitPlan`** and adds Start Menu and desktop shortcuts. Uninstall from Settings → Apps. An update replaces only the `current` folder inside that directory.

**Folder publish, not single-file.** ExitPlan uses WPF plus LiveCharts/SkiaSharp and AvalonEdit (native DLLs). Shipped defaults (`TaxData`, `RMDData`, `MarketData`, `Data`) are in the install folder so the first launch can copy them. The files the user edits are not left there. See [User data](#user-data).

---

## 1. From the repo root, run one command

1. Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) if `dotnet --version` does not work.
2. Open **PowerShell**.
3. `cd` to the folder that contains `ExitPlan.sln` (the repo root).
4. Run:

```powershell
.\publish.ps1
```

That publishes a self-contained `win-x64` folder and then runs `vpk pack` (pack id `ExitPlan`, version from `ExitPlan.UI.csproj`, icon `ExitPlan.UI\Assets\ExitPlan.ico`, main exe `ExitPlan.UI.exe`).

The script fails if these files are missing from the publish folder:

- `ExitPlan.UI.exe`
- `Data\AppState.json`
- `TaxData\Tax2025.json`
- `TaxData\Tax2026.json`
- `RMDData\RMD2025.json`
- `MarketData\MarketData.json`

You do **not** need Visual Studio. You do **not** give the user a .NET Runtime installer.

The script then runs `vpk download github` against the public releases repo https://github.com/pwscho/ExitPlan so the new package can include a delta. That download does not use a token. If it fails (offline, or no earlier Velopack release), packing still produces `ExitPlan-win-Setup.exe` and the full nupkg.

In-app update checks use the same public URL and do not use a token. Source stays in the private repository. See [DISTRIBUTE.md](../DISTRIBUTE.md).

---

## 2. Where the installer and packages land

```text
artifacts\ExitPlan-win-Setup.exe
artifacts\ExitPlan-2.3.0-full.nupkg
artifacts\releases.win.json
```

The `2.3.0` part is `<Version>` in `ExitPlan.UI.csproj`. `artifacts\` is gitignored. For other people, the Windows installer workflow attaches these files to a GitHub Release. Share https://github.com/pwscho/ExitPlan/releases/latest.

Recipients run **`ExitPlan-win-Setup.exe`**. They do not run the nupkg by hand. The nupkg and `releases.win.json` are what already-installed copies download.

---

## 3. How to test on a clean machine / VM

Use a Windows 10/11 PC or VM that does **not** have Visual Studio or the .NET Desktop Runtime.

1. Copy `ExitPlan-win-Setup.exe` onto that machine.
2. Run it. There is no wizard and no UAC prompt. When it finishes, ExitPlan starts.
3. Confirm the app opens, a scenario loads, and **Run Simulation** works **offline**.
4. Close ExitPlan. In File Explorer, confirm `%UserProfile%\Documents\ExitPlan` contains `Data\AppState.json`, `TaxData`, `RMDData`, and `MarketData`.
5. Open **Settings → Apps** and uninstall **ExitPlan**. `%LocalAppData%\ExitPlan` and the shortcuts should go away. `Documents\ExitPlan` should still be there.

Running `artifacts\win-x64\ExitPlan.UI.exe` directly is not a Velopack install. **Check for updates** will say that updates apply after Setup.exe. The first launch still copies user data into Documents.

To try an update locally, pack a higher version into a second folder and point a test build at it, or use a real GitHub Release. The app never downloads or restarts for an update unless the user chooses **Yes**.

---

## 4. Code signing (follow-up, not part of this recipe)

`.\publish.ps1` does not sign. Windows **Authenticode** is applied afterward, on the PC that holds the certificate. This repo does not include a certificate, and CI must not gain one.

SmartScreen can still show “Windows protected your PC” until a signed file builds a reputation. Testers use **More info** → **Run anyway**.

Sign locally, then publish the SHA256 on the GitHub Release. Full steps: **[DISTRIBUTE.md](../DISTRIBUTE.md)**.

In-app updates install the binaries inside the nupkg, not `ExitPlan-win-Setup.exe`. Signing only the setup program does not sign the copy that an update installs. For a signed update, sign `artifacts\win-x64\ExitPlan.UI.exe` **before** `vpk pack`, then sign `artifacts\ExitPlan-win-Setup.exe`, and upload those packages. `signtool` comes with the Windows SDK. `/a` picks a certificate already in the local store.

```powershell
signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /a .\artifacts\win-x64\ExitPlan.UI.exe
.\publish.ps1
signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /a .\artifacts\ExitPlan-win-Setup.exe
Get-FileHash .\artifacts\ExitPlan-win-Setup.exe -Algorithm SHA256
```

Use your certificate vendor’s timestamp server if it is not DigiCert’s. Do not commit a `.pfx`.

---

## User data

Velopack replaces `%LocalAppData%\ExitPlan\current` on every update, and uninstall removes `%LocalAppData%\ExitPlan`. Files saved beside `ExitPlan.UI.exe` would be lost.

ExitPlan keeps user-editable files in **`Documents\ExitPlan`** (`%UserProfile%\Documents\ExitPlan`):

| Folder | What it is |
| --- | --- |
| `Data\AppState.json` | Scenarios |
| `TaxData\Tax{YYYY}.json` | Shipped tax tables and any year you save |
| `RMDData\RMD{YYYY}.json` | Shipped RMD tables and any year you save |
| `MarketData\MarketData.json` | Market history |
| `Logs\exitplan.log` | Startup and update log |

On every launch the app copies a file from the install (or from an older Inno Setup folder) **only if that file is not already in Documents**. It never overwrites a file you already have. A later release that adds `Tax2027.json` will copy that new file. A `Tax2026.json` you edited stays as you left it. To restore a shipped default, delete that one file in Documents and start ExitPlan again.

Older Inno installs stored those files next to the program, usually `%LocalAppData%\Programs\ExitPlan` or `C:\Program Files\ExitPlan`. The first launch of this version copies them across when the Documents copy is missing. If you installed Inno to some other folder, copy `Data`, `TaxData`, `RMDData`, and `MarketData` into `Documents\ExitPlan` yourself before the first launch (or start once, then replace the copied defaults with your copies).

Developers can point at another folder with the `EXITPLAN_DATA_DIR` environment variable. A normal `dotnet run` still uses Documents, so the sample `AppState.json` in the repo is only the seed for a Documents folder that does not have one yet. When `EXITPLAN_DATA_DIR` is set it overrides a saved folder for that run.

The File menu has **Open AppState.json**, **Choose Data Folder**, **Open Data Folder**, and **Reset to Default Folder**. The main window shows the active folder and the AppState.json path; clicking that line opens the folder in Explorer. The choice is stored in `%LocalAppData%\ExitPlan\data-location.json`, next to the Velopack install and not inside `current`, so an update does not erase it. Uninstall still removes `%LocalAppData%\ExitPlan`. If the saved folder is missing, ExitPlan uses `Documents\ExitPlan` and says so. Choosing a folder that lacks data files lists what is missing and, only if you agree, copies the built-in Tax, RMD, and market files plus a sample AppState.json. Existing files are not overwritten.

---

## How to bump the version

The product version on master is **2.3** (`<Version>2.3.0</Version>` in `ExitPlan.UI.csproj` and `ExitPlan.Core`, `ProductInfo.Version` in `ExitPlan.Core`). **Do not reuse tag `v2.3.0`.** That tag already has an Inno Setup installer. The first Velopack release needs a new version.

| What you are shipping | Edit this |
| --- | --- |
| Installer feed version and the nupkg name | `ExitPlan.UI.csproj` `<Version>` and the same `<Version>` in `ExitPlan.Core`. Tag `v` plus that version (`v2.4.0`). |
| Window title “ExitPlan 2.3” | `ExitPlan.Core\ProductInfo.cs` |

Velopack versions are three-part semver (`2.4.0`), which matches `<Version>`. The update prompt shows that number. The app does not update itself unless the user chooses **Yes**.

---

## CI note

`.github/workflows/windows-installer.yml` on `windows-latest`:

1. Runs tests (on `ubuntu-latest`) and `python3 installer/verify-packaging.py`.
2. Installs `vpk` 1.2.161.
3. Runs `vpk download github` for a delta from the previous release. A failure here is ignored (there is no earlier Velopack package yet).
4. Runs `publish.ps1 -SkipDownload` (`dotnet publish`, then `vpk pack`).
5. On a `v*` tag, or **workflow_dispatch** with “Create a GitHub Release”, runs `vpk upload github` so `ExitPlan-win-Setup.exe`, the nupkg, and `releases.win.json` are attached to that Release. Pull requests upload an Actions artifact only.

`vpk download` reads the public releases repo and does not use a token. `vpk upload` creates the Release on https://github.com/pwscho/ExitPlan. Creating a release is a write, so that step uses the Actions secret `EXITPLAN_RELEASE_TOKEN` (a token that can publish releases on the public repo). The private source repository’s `GITHUB_TOKEN` cannot write there. The token is not baked into the app.

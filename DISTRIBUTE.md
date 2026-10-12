# Distribute ExitPlan

Public download after a release is published:

**https://github.com/pwscho/ExitPlan/releases/latest**

Send people that link. They want **`ExitPlan-win-Setup.exe`**. They do **not** need Visual Studio, the .NET SDK, or the .NET Desktop Runtime.

The installer version is `ExitPlan.UI.csproj` `<Version>` (today **2.3.0**). Do not invent a second scheme. Tag `v2.4.0` for product 2.4.0. **`v2.3.0` is already published** as an Inno Setup file, so the first Velopack release has to be a newer version. This change does not bump the version.

Wait until any feature PRs you want in the build are merged to `master`, then cut the release from `master`.

## Cut a release (preferred)

On a machine with git, after the version bump is on `master`:

```bash
git checkout master
git pull origin master
git tag v2.4.0
git push origin v2.4.0
```

Use the real version in the tag. `v` plus `<Version>` is required (`v2.4.0` for `<Version>2.4.0</Version>`).

That starts **Windows installer** on `windows-latest`. The job runs `ExitPlan.Core.Tests`, publishes a self-contained `win-x64` folder, runs `vpk download github` (delta against the previous Velopack release, if there is one), runs `vpk pack`, and runs `vpk upload github` to attach the packages to the GitHub Release for that tag.

Then share: https://github.com/pwscho/ExitPlan/releases/latest

People who already installed with `ExitPlan-win-Setup.exe` open ExitPlan and either accept the startup prompt or use **Check for updates**. The prompt is `ExitPlan X.Y.Z is available. Update now?` Nothing downloads until they choose **Yes**.

## Manual release (no local tag)

1. GitHub → **Actions** → **Windows installer** → **Run workflow**.
2. Choose the `master` branch (or another commit you intend to ship).
3. Leave the tag empty to use `v` + the csproj version.
4. Leave **Create a GitHub Release** checked.

Uncheck the release option if you only want the workflow artifact (Setup.exe, nupkgs, `releases.win.json`).

## Local one-liner

On a PC with the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0):

```powershell
.\publish.ps1
```

Output: `artifacts\ExitPlan-win-Setup.exe`, `artifacts\ExitPlan-<version>-full.nupkg`, and `artifacts\releases.win.json`.

The script installs `vpk` 1.2.161 if needed and downloads the previous public release (no token) so a delta can be built. Full local steps: [docs/DEPLOY.md](docs/DEPLOY.md).

Uploading that folder yourself is `vpk upload github` (what CI runs). You rarely need to type it. CI already does, with `--publish` and `--tag vX.Y.Z`.

## What recipients get

- One `ExitPlan-win-Setup.exe` (Velopack installer, not a single-file `dotnet` bundle).
- Per-user install in `%LocalAppData%\ExitPlan` (no admin). Start Menu shortcut, desktop shortcut, Settings → Apps uninstall.
- Their scenarios and edited tax, RMD, and market files in **`Documents\ExitPlan`**, which an update does not replace.
- Later versions from **Check for updates**, or from the prompt the app shows on startup when a newer release exists. The user has to choose **Yes**.

## Public releases and the private source repo

Source stays in the private repository (`pwscho/ExitPlan`, renamed to `pwscho/ExitPlan-dev`). Installers and in-app updates come from the public repository **https://github.com/pwscho/ExitPlan**.

- **The installed app** calls that public repo with `GithubSource` and no access token. Offline checks are logged and startup continues. Unauthenticated GitHub allows 60 release checks per hour per IP, which is enough for this app.
- **CI** `vpk download github` reads the same public URL and does not use a token.
- **CI** `vpk upload github` also targets that public URL. GitHub will not create a Release anonymously, and the private source repo’s `GITHUB_TOKEN` cannot write to the public repo. The upload step uses the Actions secret `EXITPLAN_RELEASE_TOKEN` (permission to publish releases on `pwscho/ExitPlan` only). Do not put that token in the app, in `Documents\ExitPlan`, or in this repository.

A failed check is written to `Documents\ExitPlan\Logs\exitplan.log`. Running from Visual Studio is not an installed copy; **Check for updates** says so instead of pretending an update exists.

## SmartScreen (expected)

First-run Windows often shows **“Windows protected your PC”** until the signed file has a reputation. Tell people: **More info** → **Run anyway**. Browsers may also flag an uncommon download.

## Sign locally and publish SHA256

CI uploads **unsigned** packages. This workflow does not Authenticode-sign, and the repository has no signing certificate or password. Do not commit a `.pfx` or put one in GitHub Actions.

Updates install the files inside the nupkg. Sign `artifacts\win-x64\ExitPlan.UI.exe` before `.\publish.ps1` if the updated app should be signed, then sign `ExitPlan-win-Setup.exe`, then replace the Release assets with those signed files. On the Windows PC that already has the code-signing certificate installed:

```powershell
signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /a .\artifacts\win-x64\ExitPlan.UI.exe
.\publish.ps1
signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /a .\artifacts\ExitPlan-win-Setup.exe
Get-FileHash .\artifacts\ExitPlan-win-Setup.exe -Algorithm SHA256
```

Use the timestamp server that came with the certificate if it is not DigiCert’s. `/n "subject"` selects one certificate when several are installed.

On the GitHub Release, replace the unsigned `ExitPlan-win-Setup.exe` and the nupkg with the signed files (or re-run `vpk upload github` with a token). Edit the Release notes and replace `SHA256: (add after signing)` with the hash from `Get-FileHash`.

Do not share the Release link until the attached installer is the one you intend to ship and the notes show that SHA256. Recipients can compare it:

```powershell
Get-FileHash .\ExitPlan-win-Setup.exe -Algorithm SHA256
```

## Do not ship

- Signing certificates, passwords, or a GitHub token inside the app
- Personal `AppState.json` with real account balances (the repo sample under `ExitPlan.UI/Data/AppState.json` is the seed the installer packs; the user’s copy lives in Documents)

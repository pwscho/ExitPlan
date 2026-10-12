#!/usr/bin/env python3
"""Sanity-check the Windows Velopack packaging files. Does not compile WPF or run vpk."""

from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def fail(message: str) -> None:
    print(f"FAIL: {message}", file=sys.stderr)
    raise SystemExit(1)


def require_version(text: str, path: Path) -> str:
    match = re.search(r"<Version>([^<]+)</Version>", text)
    if not match:
        fail(f"{path.relative_to(ROOT)} has no <Version>")
    return match.group(1).strip()


def main() -> None:
    ui_csproj_path = ROOT / "ExitPlan.UI" / "ExitPlan.UI.csproj"
    core_csproj_path = ROOT / "ExitPlan.Core" / "ExitPlan.Core.csproj"
    ui_csproj = ui_csproj_path.read_text(encoding="utf-8")
    core_csproj = core_csproj_path.read_text(encoding="utf-8")
    publish = (ROOT / "publish.ps1").read_text(encoding="utf-8")
    deploy = (ROOT / "docs" / "DEPLOY.md").read_text(encoding="utf-8")
    readme = (ROOT / "README.md").read_text(encoding="utf-8")
    distribute = (ROOT / "DISTRIBUTE.md").read_text(encoding="utf-8")
    workflow_path = ROOT / ".github" / "workflows" / "windows-installer.yml"
    if not workflow_path.is_file():
        fail("missing .github/workflows/windows-installer.yml")
    workflow = workflow_path.read_text(encoding="utf-8")
    program = (ROOT / "ExitPlan.UI" / "Program.cs").read_text(encoding="utf-8")
    updater = (ROOT / "ExitPlan.UI" / "Updates" / "UpdateChecker.cs").read_text(encoding="utf-8")
    migrator = (ROOT / "ExitPlan.Core" / "UserDataMigrator.cs").read_text(encoding="utf-8")

    retired_iss = ROOT / "installer" / "ExitPlan.iss"
    if retired_iss.exists():
        fail("installer/ExitPlan.iss is retired; Velopack replaced Inno Setup")

    version = require_version(ui_csproj, ui_csproj_path)
    core_version = require_version(core_csproj, core_csproj_path)
    if core_version != version:
        fail(
            f"ExitPlan.Core <Version> {core_version} does not match "
            f"ExitPlan.UI <Version> {version}"
        )

    tf_match = re.search(r"<TargetFramework>([^<]+)</TargetFramework>", ui_csproj)
    if not tf_match or tf_match.group(1).strip() != "net8.0-windows":
        fail("ExitPlan.UI TargetFramework is not net8.0-windows")

    if "Velopack" not in ui_csproj or "1.2.161" not in ui_csproj:
        fail("ExitPlan.UI.csproj must reference Velopack 1.2.161")
    if "StartupObject" not in ui_csproj or "ExitPlan.UI.Program" not in ui_csproj:
        fail("ExitPlan.UI.csproj must start at ExitPlan.UI.Program")
    icon = ROOT / "ExitPlan.UI" / "Assets" / "ExitPlan.ico"
    if not icon.is_file():
        fail("missing ExitPlan.UI/Assets/ExitPlan.ico")

    required_files = [
        ROOT / "ExitPlan.UI" / "Data" / "AppState.json",
        ROOT / "ExitPlan.Core" / "TaxData" / "Tax2025.json",
        ROOT / "ExitPlan.Core" / "TaxData" / "Tax2026.json",
        ROOT / "ExitPlan.Core" / "RMDData" / "RMD2025.json",
        ROOT / "ExitPlan.Core" / "MarketData" / "MarketData.json",
    ]
    for path in required_files:
        if not path.is_file():
            fail(f"Missing content file: {path.relative_to(ROOT)}")

    for needle in (
        "VelopackApp.Build()",
        "SetAutoApplyOnStartup(false)",
        ".Run()",
        "UserDataMigrator.MigrateToDefault()",
    ):
        if needle not in program:
            fail(f"Program.cs missing {needle!r}")

    for needle in (
        "https://github.com/pwscho/ExitPlan",
        "GithubSource",
        "UpdateManager",
        "CheckForUpdatesAsync",
        "DownloadUpdatesAsync",
        "ApplyUpdatesAndRestart",
        "ExitPlan {version} is available. Update now?",
        "accessToken: null",
        "NotInstalledException",
    ):
        if needle not in updater:
            fail(f"UpdateChecker.cs missing {needle!r}")

    if "EXITPLAN_GITHUB_TOKEN" in updater or "github-token.txt" in updater:
        fail("UpdateChecker.cs must not read a GitHub token; releases are public")

    for needle in (
        "Documents",
        "EXITPLAN_DATA_DIR",
        "Programs",
        "MigrateToDefault",
    ):
        if needle not in migrator:
            fail(f"UserDataMigrator.cs missing {needle!r}")

    paths = (ROOT / "ExitPlan.Core" / "AppDataPaths.cs").read_text(encoding="utf-8")
    for needle in ("AppState.json", "TaxData", "RMDData", "MarketData"):
        if needle not in paths:
            fail(f"AppDataPaths.cs missing {needle!r}")

    for needle in (
        "--self-contained",
        "win-x64",
        "PublishSingleFile=false",
        "TaxData\\Tax2025.json",
        "RMDData\\RMD2025.json",
        "MarketData\\MarketData.json",
        "Data\\AppState.json",
        "vpk pack",
        "--packId",
        "ExitPlan",
        "--mainExe",
        "ExitPlan.UI.exe",
        "--icon",
        "ExitPlan.ico",
        "--channel",
        "--outputDir",
        "vpk download github",
        "https://github.com/pwscho/ExitPlan",
        "--noPortable",
        "releases.win.json",
        "1.2.161",
    ):
        if needle not in publish:
            fail(f"publish.ps1 missing {needle!r}")

    if "ISCC" in publish or "jrsoftware" in publish.lower():
        fail("publish.ps1 still references Inno Setup")
    if "EXITPLAN_GITHUB_TOKEN" in publish or "--token" in publish:
        fail("publish.ps1 must not send a token; vpk download reads the public releases repo")

    for heading in (
        "## 1. From the repo root, run one command",
        "## 2. Where the installer and packages land",
        "## 3. How to test on a clean machine / VM",
        "## 4. Code signing (follow-up, not part of this recipe)",
        "## User data",
        "## How to bump the version",
        "https://velopack.io",
        "Authenticode",
        "SmartScreen",
        "https://github.com/pwscho/ExitPlan/releases/latest",
        "DISTRIBUTE.md",
        ".github/workflows/windows-installer.yml",
        "Documents\\ExitPlan",
        "Check for updates",
    ):
        if heading not in deploy:
            fail(f"docs/DEPLOY.md missing {heading!r}")

    if "jrsoftware.org" in deploy or "ISCC" in deploy or "Install Inno Setup" in deploy:
        fail("docs/DEPLOY.md still documents Inno Setup as the installer")
    if "github-token.txt" in deploy or "EXITPLAN_GITHUB_TOKEN" in deploy:
        fail("docs/DEPLOY.md still documents a read token for the public releases repo")

    if "docs/DEPLOY.md" not in readme:
        fail("README.md does not link to docs/DEPLOY.md")
    if "DISTRIBUTE.md" not in readme:
        fail("README.md does not link to DISTRIBUTE.md")
    if "https://github.com/pwscho/ExitPlan/releases/latest" not in readme:
        fail("README.md missing public download URL")
    if "Documents\\ExitPlan" not in readme and "Documents\\\\ExitPlan" not in readme:
        fail("README.md does not document Documents\\ExitPlan")
    if "Check for updates" not in readme:
        fail("README.md missing Check for updates")
    if "ExitPlan-win-Setup.exe" not in readme:
        fail("README.md missing ExitPlan-win-Setup.exe")

    for needle in (
        "https://github.com/pwscho/ExitPlan/releases/latest",
        "git tag v",
        "Run workflow",
        "windows-latest",
        "ExitPlan-win-Setup.exe",
        "SmartScreen",
        "Authenticode",
        "publish.ps1",
        "vpk upload github",
        "Documents\\ExitPlan",
        "EXITPLAN_RELEASE_TOKEN",
        "ExitPlan-dev",
        "Check for updates",
    ):
        if needle not in distribute:
            fail(f"DISTRIBUTE.md missing {needle!r}")

    if "github-token.txt" in distribute or "EXITPLAN_GITHUB_TOKEN" in distribute:
        fail("DISTRIBUTE.md still documents a read token for the public releases repo")

    if "jrsoftware" in distribute.lower() or "innosetup" in distribute.lower() or "publish.ps1 -Compile" in distribute:
        fail("DISTRIBUTE.md still documents Inno Setup as the installer")

    for needle in (
        "windows-latest",
        "workflow_dispatch",
        "v*",
        "publish.ps1 -SkipDownload",
        "vpk download github",
        "vpk upload github",
        "--channel win",
        "vpk pack",
        "ExitPlan.Core.Tests",
        "win-x64",
        "contents: read",
        "EXITPLAN_RELEASE_TOKEN",
        "https://github.com/pwscho/ExitPlan",
        "1.2.161",
        "releases.win.json",
    ):
        if needle not in workflow:
            fail(f"windows-installer.yml missing {needle!r}")

    if "contents: write" in workflow or "secrets.GITHUB_TOKEN" in workflow or "github.token" in workflow:
        fail("workflow must not use the source repo token or contents: write")

    if "innosetup" in workflow.lower() or "softprops/action-gh-release" in workflow or "ISCC" in workflow:
        fail("workflow still uses Inno Setup or the old release action")
    if "pfx" in workflow.lower() or "signtool" in workflow.lower():
        fail("workflow must not code-sign or reference certificates")

    main_window = (ROOT / "ExitPlan.UI" / "MainWindow.xaml").read_text(encoding="utf-8")
    if "Check for updates" not in main_window:
        fail("MainWindow.xaml missing Check for updates")

    print("OK packaging checks")
    print(f"  UI project:     ExitPlan.UI ({tf_match.group(1).strip()})")
    print(f"  Version:        {version}")
    print(f"  Publish script: publish.ps1")
    print(f"  Pack:           vpk (Velopack { '1.2.161' })")
    print(f"  Workflow:       .github/workflows/windows-installer.yml")
    print(f"  Distribute:     DISTRIBUTE.md")
    print(f"  Deploy guide:   docs/DEPLOY.md")
    print("  Runtime data:   Tax2025, Tax2026, RMD2025, MarketData, AppState")
    print("  User data:      Documents\\ExitPlan")


if __name__ == "__main__":
    main()

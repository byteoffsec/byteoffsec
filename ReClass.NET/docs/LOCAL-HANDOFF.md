# Local hand-off prompt

Paste the block below into a new Claude Code session that runs on your Windows machine, inside your
clone of this repository (after `git checkout claude/reclass-modernize-node-fixes-ggtwei`).

---

You are working in my local Windows clone of https://github.com/byteoffsec/byteoffsec on the branch
`claude/reclass-modernize-node-fixes-ggtwei`. Do everything locally, nothing in the cloud.

## Context

`ReClass.NET/` holds "ReClass.NET ByteOffSec Edition", a fork of ReClass.NET (upstream master `a02fcb9`,
which is v1.2 plus later fixes). A previous session built it on Linux: it compiles for x86 and x64 with
`dotnet build`, 1384 unit tests pass under Mono, and the app was only ever run under Mono on a virtual
display. Nothing has been executed on Windows yet. Your job is to verify, fix and finish it on Windows.

Read first: `ReClass.NET/README.md`, `ReClass.NET/UPSTREAM.md`, `git log --stat main..HEAD`.

What exists (file pointers):

1. Build: SDK-style projects (`net472`, platforms x86/x64, x64 defines `RECLASSNET64`),
   `ReClass.NET/Directory.Build.props`, `.github/workflows/build.yml`, `ReClass.NET/Makefile`.
2. Offset-preserving node layout (upstream issues #88 and #196): `ReClass.NET/ReClass.NET/Nodes/BaseContainerNode.cs`
   (`UpdateLayout`, `ReconcileSizeChanges`, `ConsumeSuccessors`, `ReplaceNodeRange`, `GetNodesConsumedByResize`,
   `GetNodesConsumedByReplacingRanges`), `Nodes/BaseNode.cs` (`LayoutSize`, `IsAttached`,
   `ParticipatesInSizeCompensation`), `Nodes/ContainerLayoutPolicy.cs`, `Forms/MainForm.Functions.cs`
   (`ReplaceSelectedNodesWithType` with the confirmation dialog), `Memory/NodeDissector.cs`,
   `Controls/MemoryViewControl.cs` (`PruneSelection`), legacy importers in `DataExchange/ReClass/`.
   Setting: `Settings.PreserveNodeOffsetsOnResize`. Tests: `ReClass.NET_Tests/Nodes/*`.
3. Theme system: `ReClass.NET/ReClass.NET/UI/Theme/*` (`ThemeManager`, `ThemePalette`, `ThemedToolStripRenderer`,
   `NativeTheming`, `NodeColorPresets`, `ThemedGlyphs`), hooked through `Forms/IconForm.cs` and
   `UI/GlobalWindowManager.cs`; `Settings.Theme` (Dark is the default); Appearance group in `Forms/SettingsForm*`.
   Tests: `ReClass.NET_Tests/UI/*`.
4. Branding: `Constants.cs`, `Properties/AssemblyInfo.cs`, `Forms/AboutForm.cs`.

Rules: keep the public API of the `ReClassNET.*` namespaces source compatible (plugins compile against it);
never change the `.rcnet` file format; tabs and Allman braces (see `ReClass.NET/.editorconfig`); small
commits with clear messages; never rewrite history on the branch; never push to `main` unless I say so.

## Plan (do the steps in order, stop and report after each one)

Step 0, prerequisites: Visual Studio 2022 with the ".NET desktop development" and "Desktop development
with C++" workloads (MSVC v143 and a Windows 10 SDK), .NET SDK 8.0, git. Tell me what is missing.

Step 1, build (Developer PowerShell for VS 2022, in `ReClass.NET/`):

    msbuild NativeCore\Windows\NativeCore.vcxproj /p:Configuration=Release /p:Platform=x64   "/p:SolutionDir=$PWD\"
    msbuild NativeCore\Windows\NativeCore.vcxproj /p:Configuration=Release /p:Platform=Win32 "/p:SolutionDir=$PWD\"
    dotnet build ReClass.NET\ReClass.NET.csproj -c Release -p:Platform=x64
    dotnet build ReClass.NET\ReClass.NET.csproj -c Release -p:Platform=x86
    dotnet build ReClass.NET_Launcher\ReClass.NET_Launcher.csproj -c Release
    Copy-Item Dependencies\x64\* bin\Release\x64\ ; Copy-Item Dependencies\x86\* bin\Release\x86\
    New-Item -ItemType Directory bin\Release\x64\Plugins, bin\Release\x86\Plugins -Force

Also confirm `ReClass.NET.sln` opens and builds in Visual Studio and that the WinForms designer opens
`Forms\SettingsForm.cs` and `Forms\MainForm.cs` without errors. Fix build or designer problems.

Step 2, tests:

    dotnet test ReClass.NET_Tests\ReClass.NET_Tests.csproj -c Release -p:Platform=x64
    dotnet test ReClass.NET_Tests\ReClass.NET_Tests.csproj -c Release -p:Platform=x86

Expected on each: 1384 passed, 2 skipped, 0 failed. Investigate any failure (x86 is the first run on a
real 32-bit runtime; pointer-size assumptions are the likely culprit).

Step 3, verify the Windows-only theme paths by running `bin\Release\x64\ReClass.NET.exe` (dark theme is
the default) and fix what is wrong in `UI/Theme/*`:
- dark title bar (DwmSetWindowAttribute in `NativeTheming`), on every window incl. dialogs;
- scroll bars: project tree, memory view, scanner grids (DataGridView child scroll bars), log, code view;
- combo boxes (flat frame overlay `ComboBoxOverlay`): scanner Scan Type / Value Type, settings Theme combo,
  process browser; enabled, disabled, focused, dropped down;
- tab control strip (`TabControlOverlay`), group box borders, check boxes / radio buttons, buttons,
  NumericUpDown, progress bar, tool tips, status bar, menu bar, context menus, tool bar glyphs;
- Code Generator window: syntax colours must survive (the box is excluded from generic theming);
- live switch Dark -> Light -> Dark in Settings > General > Appearance; Light theme must look like a
  clean flat version of the original;
- DPI 125 % and 150 % if you can;
- a plugin window (install the sample plugin from https://github.com/ReClassNET/ReClass.NET-SamplePlugin).

Step 4, verify the node fix by hand: create a class, name the node at 0x0C `health` and at 0x10 `ammo`;
change the Hex32 at 0x00 to Vector3 (health/ammo must keep their offsets); change the Hex32 at 0x08 to
Vector3 (a dialog must list health and ammo, answer No); shift-select two Hex32 rows and pick Vector2
(exactly that range is refilled); edit a text length and an array count (nodes below stay put); add
a Function node followed by named fields and attach to a process (functions are exempt: the fields
below shift, nothing is deleted); toggle "Keep following node offsets on resize" off and confirm the
old shifting behaviour returns; save, reload, compare. Import an old `.reclass` file if you have one.

Step 5, use it for real for a while on a process you normally reverse: pointers, class instances,
arrays, unions, vtables, dissect, copy/paste between instances, memory scanner, debugger. Any exception
or visual glitch: fix it, add a test when the logic is testable.

Step 6, package: copy `bin\Release\ReClass.NET_Launcher.exe` plus the `x64\` and `x86\` folders into a
new folder next to `C:\ReverseEngineering\Tools\ReClass.NET-v1.2` and move my plugins into `x64\Plugins`.

Step 7, when I am happy: merge the branch into `main` (I will tell you how), optionally tag `v1.3.0`;
the GitHub Actions workflow then publishes a release zip (or build it locally).

## Known limitations from the reviews (not bugs to fix unless they bite)

- Nodes sized by the tool at runtime (functions, arrays of functions, instances of classes containing
  them) are exempt from the offset guarantee and still shift the nodes below them.
- A referenced class that was never filled has an unknown size; its first fill shifts instead of consumes.
- `ThemeManager.Exclude` restores draw modes but not colours already applied; call it before a form is shown.
- NumericUpDown spin buttons and the stock ProgressBar stay light on Windows in dark mode.
- Legacy `.reclass` / `.reclassqt` import now keeps acyclic class instances (upstream dropped them).

If the cloud CI run https://github.com/byteoffsec/byteoffsec/actions/runs/37387691819 failed, read its
logs before anything else; fixes to `.github/workflows/build.yml` are fine.

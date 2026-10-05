# Upstream relationship

This directory is a maintained fork of [ReClassNET/ReClass.NET](https://github.com/ReClassNET/ReClass.NET)
by KN4CK3R (MIT licensed, see `LICENSE`).

| | |
|---|---|
| Upstream repository | https://github.com/ReClassNET/ReClass.NET |
| Base commit | `a02fcb9bd669c8f81facd3ee9ad57cdcbf2cc0e1` (master, 2023-07-03, "Merge pull request #260 from descear/master") |
| Upstream release it corresponds to | v1.2 plus the fixes merged on master afterwards |
| Imported as | flat copy (no upstream history), `.git` excluded |

## Pulling in upstream changes

The tree was imported as a plain copy, so upstream commits are applied as patches:

```bash
git remote add upstream https://github.com/ReClassNET/ReClass.NET.git
git fetch upstream
# everything upstream did since the base commit, re-rooted into this folder
git diff a02fcb9bd669c8f81facd3ee9ad57cdcbf2cc0e1 upstream/master | git apply --3way --directory=ReClass.NET
```

Resolve conflicts (most likely in the files listed below), rebuild, run the tests, and update the
base commit in the table above.

## Files that intentionally differ from upstream

- `*.csproj`, `Directory.Build.props`, `Makefile`, `ReClass.NET.sln`: SDK-style projects that build
  with `dotnet build` on every OS; the legacy MSBuild projects and the Mono/Docker makefiles were removed.
- `ReClass.NET/Constants.cs`, `Properties/AssemblyInfo.cs`, `Forms/AboutForm.cs`: edition branding.
- `ReClass.NET/Nodes/*`, `Memory/NodeDissector.cs`, `Forms/MainForm.Functions.cs`: offset-preserving node
  editing (upstream issues #88 and #196).
- `ReClass.NET/UI/Theme/*`, `Forms/*`, `Controls/*`, `Settings.cs`, `Util/SettingsSerializer.cs`:
  theme system (light / dark) and the flat modern look.
- `ReClass.NET_Tests/Nodes/*`, `ReClass.NET_Tests/UI/*`: tests for the above.

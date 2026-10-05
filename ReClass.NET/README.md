# ReClass.NET - ByteOffSec Edition

[![Build](https://github.com/byteoffsec/byteoffsec/actions/workflows/build.yml/badge.svg)](https://github.com/byteoffsec/byteoffsec/actions/workflows/build.yml)

A maintained custom build of [ReClass.NET](https://github.com/ReClassNET/ReClass.NET), the structure
reverse engineering tool for Windows (x86 / x64) and Linux, with a modern dark UI and fixes for the
long-standing node editing problems of the original.

> Based on upstream ReClass.NET master (`a02fcb9`, which is v1.2 plus later fixes) by KN4CK3R,
> MIT licensed. See [UPSTREAM.md](UPSTREAM.md) for how the fork tracks upstream.

## What is different

### Node editing no longer breaks the offsets below the edited node

In the original, changing a node to a *bigger* type (Hex32 to Vector3, Hex16 to Int32, Hex8 to Int16,
Hex32 to Int64 / Pointer / class instance, ...) pushed every node below it down by the size difference,
so all the fields you had already named further down a long class pointed to the wrong offsets, and
since the project file only stores the node list, the damage was saved with the project
(upstream issues [#88](https://github.com/ReClassNET/ReClass.NET/issues/88) and
[#196](https://github.com/ReClassNET/ReClass.NET/issues/196)). The same happened when editing a text
length, an array count, or when a referenced class changed size.

In this edition a class keeps the offsets of all following nodes stable whenever a node changes size:

- a node that grows consumes the bytes of the nodes after it (a partially covered node is replaced by
  padding for its remainder, so the next untouched node keeps its exact offset),
- a node that shrinks is padded with hex bytes,
- if a growth would swallow a field you already defined (anything that is not plain hex padding), the
  type change asks for confirmation first,
- selecting several contiguous nodes and picking a type refills exactly that byte range with as many
  instances of the type as fit,
- inserting bytes, adding bytes and deleting nodes behave as before (those are intentional layout changes),
- loading a project reproduces the saved layout byte for byte.

The legacy behaviour can be restored in *Settings > General* ("Keep offsets of following nodes when a
node changes size").

### Dark mode and a modern flat UI

- Light and Dark themes (Dark is the default), switchable live in *Settings > General > Appearance*.
- Every window, menu, tool bar, status bar, grid, tree and dialog follows the theme; on Windows 10/11
  the title bar and scroll bars switch too.
- Matching light / dark colour presets for the memory view (type, name, value, address, comment, ...)
  with one-click reset buttons in *Settings > Colors*. Your own colour customisations are never
  overwritten automatically.
- Flat tool bars, menus and buttons without gradients and 3D bevels.

### Modern build

- SDK-style projects: `dotnet build` works on Windows, Linux and macOS for the managed code, and the
  solution still opens in Visual Studio 2022.
- GitHub Actions builds the native core, both platforms, the launcher, runs the test suite on Windows
  and Linux (Mono) and publishes ready-to-run artifacts; tags starting with `v` create a release.
- The node layout logic is covered by unit tests.

Everything else (nodes, plugins, memory scanner, debugger, code generator, project format, plugin API)
is unchanged and compatible with the original: existing `.rcnet` projects, plugins built against
ReClass.NET 1.2 and the `settings.xml` of the original build keep working.

## Download / install

1. Take the `ReClass.NET-ByteOffSec-windows` artifact of the latest
   [build](https://github.com/byteoffsec/byteoffsec/actions/workflows/build.yml) (or the zip of a
   release).
2. Extract it anywhere (for example next to your existing `ReClass.NET-v1.2` folder) and start
   `ReClass.NET_Launcher.exe`, or `x64\ReClass.NET.exe` / `x86\ReClass.NET.exe` directly.
3. Plugins go into the `x64\Plugins` / `x86\Plugins` folder as before.

## Building

### Windows

- Visual Studio 2022 with the ".NET desktop development" and "Desktop development with C++" workloads:
  open `ReClass.NET.sln`, pick `x64` or `x86`, build.
- Command line:

  ```powershell
  msbuild NativeCore\Windows\NativeCore.vcxproj /p:Configuration=Release /p:Platform=x64 "/p:SolutionDir=$PWD\"
  dotnet build ReClass.NET\ReClass.NET.csproj -c Release -p:Platform=x64
  dotnet build ReClass.NET_Launcher\ReClass.NET_Launcher.csproj -c Release
  dotnet test  ReClass.NET_Tests\ReClass.NET_Tests.csproj -c Release -p:Platform=x64
  ```

  Output: `bin\Release\x64\` (plus `bin\Release\ReClass.NET_Launcher.exe`). Copy `Dependencies\x64\*`
  next to the executable for symbol support.

### Linux

```bash
sudo apt install dotnet-sdk-8.0 mono-complete libgdiplus g++ make
make            # managed projects, x86 + x64 + launcher
make native     # NativeCore.so (x64)
make test       # unit tests (xunit console runner under Mono)
make dist       # runnable tree in build/Release/x64
mono build/Release/x64/ReClass.NET.exe
```

## Credits

- [KN4CK3R](https://github.com/KN4CK3R) and the ReClass.NET contributors for the original tool.
- The theme and node layout work of this edition by [ByteOffSec](https://github.com/byteoffsec).

## License

MIT, same as upstream. See [LICENSE](LICENSE).

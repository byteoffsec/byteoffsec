# ReClass.NET ByteOffSec Edition
#
# The managed projects are SDK-style and build with the .NET SDK (8.0+) on any OS.
# The native core needs MSVC on Windows (NativeCore/Windows/NativeCore.vcxproj, built by the
# Visual Studio solution or the CI workflow) and g++ on Linux (NativeCore/Unix/Makefile).
# Running the result on Linux additionally needs Mono (mono-complete) and libgdiplus.

.PHONY: all debug release native native-clean test dist clean

CONFIGURATION ?= Release
DOTNET ?= dotnet
MONO ?= mono
XUNIT_CONSOLE ?= xunit-console/tools/net472/xunit.console.exe

all: release

debug:
	$(MAKE) release CONFIGURATION=Debug

release:
	$(DOTNET) build ReClass.NET/ReClass.NET.csproj -c $(CONFIGURATION) -p:Platform=x86 -nologo
	$(DOTNET) build ReClass.NET/ReClass.NET.csproj -c $(CONFIGURATION) -p:Platform=x64 -nologo
	$(DOTNET) build ReClass.NET_Launcher/ReClass.NET_Launcher.csproj -c $(CONFIGURATION) -nologo

native:
	$(MAKE) -C NativeCore/Unix before_release out_release64

native-clean:
	$(MAKE) -C NativeCore/Unix clean

# Builds the test project and runs it. `dotnet test` cannot host .NET Framework tests on Linux,
# so the xunit console runner is executed under Mono instead.
test:
	$(DOTNET) build ReClass.NET_Tests/ReClass.NET_Tests.csproj -c $(CONFIGURATION) -p:Platform=x64 -nologo
	test -f $(XUNIT_CONSOLE) || ( \
		curl -sSL -o xunit.runner.console.nupkg https://api.nuget.org/v3-flatcontainer/xunit.runner.console/2.9.2/xunit.runner.console.2.9.2.nupkg && \
		unzip -q -o xunit.runner.console.nupkg -d xunit-console && rm -f xunit.runner.console.nupkg )
	$(MONO) $(XUNIT_CONSOLE) bin/Tests/$(CONFIGURATION)/x64/ReClass.NET_Tests.dll -nologo -noshadow

# Assembles a runnable Linux x64 tree in build/.
dist: release native
	mkdir -p build/$(CONFIGURATION)/x64/Plugins
	cp -r bin/$(CONFIGURATION)/x64/* build/$(CONFIGURATION)/x64/
	cp NativeCore/Unix/build/release/x64/NativeCore.so build/$(CONFIGURATION)/x64/

clean: native-clean
	rm -rf bin obj build ReClass.NET/obj ReClass.NET_Launcher/obj ReClass.NET_Tests/obj xunit-console

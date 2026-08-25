# Icod.Processes

`Icod.Processes` is a cross-platform .NET library for safe child-process
execution and neutral process-control primitives. It provides reusable process
mechanisms without tying callers to a command suite such as CoreUtils or
ProcPs.

The library is the standalone successor to the process infrastructure that was
originally incubated under `Icod.CommandFramework.Processes`.

## Features

- exact argument-vector child launching without shell quoting;
- explicit inherited, empty, and modified child environments;
- executable lookup using explicit working-directory and environment snapshots;
- asynchronous standard-input, standard-output, and standard-error forwarding;
- captured output with concurrent draining of both output streams;
- monotonic execution timeouts through `Icod.Timing`;
- cancellation policies for the child, process tree, or leave-running behavior;
- process identities with optional PID-reuse protection;
- process, process-group, session, and POSIX priority-selector targets;
- arbitrary-process liveness observation and asynchronous waiting;
- portable signal parsing and translation, including Linux real-time signals;
- signal delivery with controlled platform substitutions;
- Linux signal disposition and blocked-mask observations;
- POSIX queued signal delivery for individual processes;
- POSIX nice-value operations and Windows priority-class substitutions;
- POSIX launch-time signal disposition/mask policy; and
- atomic POSIX child process-group creation when the native launch path is used.

## Requirements

The initial `1.0.0` release targets .NET 10.0. The implementation uses process
launch capabilities provided by the .NET 10 runtime and intentionally does not
add compatibility shims for older target frameworks.

The only runtime package dependency is `Icod.Timing` 1.0.0.

## Installation

```text
Install-Package Icod.Processes -Version 1.0.0
```

or:

```text
dotnet add package Icod.Processes --version 1.0.0
```

## Example

```csharp
using Icod.Processes;

var options = new ProcessRunOptions( "dotnet" ) {
	CaptureStandardOutput = true,
	ResolveExecutable = true,
	ReturnLaunchFailureResult = true,
	Timeout = TimeSpan.FromSeconds( 10 )
};
options.Arguments.Add( "--version" );

ProcessResult result = await ProcessRunner.RunAsync( options );
Console.WriteLine( result.StandardOutput );
```

A larger runnable example is available under `samples/Icod.Processes.Sample`.

## Design boundary

`Icod.Processes` owns general process execution and control mechanisms. It does
not own ProcPs-specific process enumeration, `/proc` field parsing, selection
grammar, metrics, personalities, or command presentation. Those remain in
`Icod.ProcPs.Shared`.

Likewise, command-line parsing, diagnostics, and other command-hosting concerns
remain outside this package.

## Building

On Windows:

```text
build.cmd
```

On Unix-like hosts:

```text
./build.sh
```

Both scripts support `clean`, `restore`, `build`, `test`, and `pack`. With no
argument they run the complete sequence.

CI builds and tests on Windows, Ubuntu, and macOS. Publishing from `main` packs
and publishes the NuGet package after all three platform jobs succeed.

## Author

Timothy J. Bruce <uniblab@hotmail.com>

Copyright (c) 2026 Timothy J. Bruce.

## License

Licensed under the GNU Lesser General Public License v3.0 or later
(`LGPL-3.0-or-later`). See `LICENSE` for the complete license text.

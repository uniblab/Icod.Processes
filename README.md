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

The current `1.1.0` release targets .NET 10.0. The implementation uses process
launch capabilities provided by the .NET 10 runtime and intentionally does not
add compatibility shims for older target frameworks.

The only runtime package dependency is `Icod.Timing` 1.0.0.

## Installation

```text
Install-Package Icod.Processes -Version 1.1.0
```

or:

```text
dotnet add package Icod.Processes --version 1.1.0
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

## Platform capabilities

`Icod.Processes` exposes neutral contracts, but not every operating system has
the same native process-control facilities. Providers report unsupported
operations explicitly rather than fabricating Unix semantics.

| Capability | Windows | Linux | macOS |
| --- | --- | --- | --- |
| Child execution, streams, environment, and working directory | Yes | Yes | Yes |
| Process identity, PID-reuse observation, liveness, and waiting | Yes | Yes | Yes |
| New process group at child launch | Yes | Yes | Yes |
| Custom native `argv[0]` | Unsupported | Yes | Yes |
| Native child file-descriptor duplication | Unsupported | Yes | Yes |
| Process-group target control | Unsupported | Yes | Yes |
| Signal delivery | Termination substitution | Native | Native |
| Signal disposition observation | Unsupported | Yes | Unsupported |
| Blocked-signal observation | Unsupported | Yes | Unsupported |
| Queued signal values | Unsupported | Yes | Unsupported |
| Priority operations | Priority-class approximation | Native nice values | Native nice values |

Applications should inspect provider capabilities and operation results where a
feature can vary by host.

## Migrating from Icod.CommandFramework.Processes

Code that currently consumes the process layer from `Icod.CommandFramework`
can migrate without taking a dependency on ProcPs or CoreUtils.

Replace the package dependency with:

```xml
<PackageReference Include="Icod.Processes" Version="1.1.0" />
```

and replace:

```csharp
using Icod.CommandFramework.Processes;
```

with:

```csharp
using Icod.Processes;
```

The standalone package owns the neutral process execution and control contracts.
ProcPs-specific enumeration, `/proc` parsing, metrics, matching, personalities,
and presentation remain outside this library.

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

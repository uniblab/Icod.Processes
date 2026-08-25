# Contributing to Icod.Processes

Thank you for contributing to `Icod.Processes`. The library provides neutral,
cross-platform process execution and process-control primitives. Changes should
preserve process-safety guarantees, explicit platform behavior, and the package
boundary between general process mechanisms and suite-specific policy.

## Supported toolchain

- Target framework: `net10.0`.
- Language version: C# 13.
- Nullable reference types and implicit global usings remain enabled.
- Supported CI runners are `windows-latest`, `ubuntu-latest`, and `macos-latest`.
- Repository text files use UTF-8 with LF line endings.
- Public, protected, and internal types and members should have substantive XML
  documentation; use `<inheritdoc/>` where appropriate.

Do not change the target framework, language version, configuration policy, or
repository line-ending convention as part of an unrelated contribution.

## Architecture

`Icod.Processes` owns neutral process mechanisms such as:

- child-process launching and stream orchestration;
- executable lookup and child environments;
- process identity, PID-reuse protection, liveness, and waiting;
- process, process-group, and session targets;
- signal parsing, delivery, and observable signal state;
- portable priority operations; and
- cancellation, timeout, and termination models.

Do not add ProcPs-specific enumeration, `/proc` report fields, process-selection
grammars, metrics, personalities, or presentation policy here. Do not add
command-line parsing or command-hosting infrastructure to this package.

The package may depend on lower-level neutral libraries such as `Icod.Timing`;
it must not depend on `Icod.CommandFramework`, `Icod.CoreUtils`, or
`Icod.ProcPs`.

## C# style

Follow `.editorconfig` and the surrounding source. In particular:

- use tabs for C# indentation;
- use 1TBS braces and always brace `if`, `else`, loops, and similar statements;
- use PascalCase for types and members and camelCase for locals and parameters;
- validate public, protected, and internal method parameters at entry;
- keep nullable flow explicit instead of suppressing warnings casually;
- propagate `CancellationToken` through asynchronous work; and
- avoid unrelated formatting churn.

Platform-specific behavior must return or throw a controlled result consistent
with the existing contract. Do not silently claim support or fabricate Unix
semantics on hosts that cannot provide them.

## Tests

Add or update tests for behavior that changes. Important process-library cases
include:

- exact argument vectors and environment construction;
- working directories and executable lookup;
- redirected and captured streams;
- cancellation, timeout, and deterministic cleanup;
- PID reuse and liveness races;
- process-group and session targets;
- signal translation and delivery;
- priority behavior;
- platform capability gating; and
- native POSIX launch behavior where applicable.

Tests must not write to standard output or standard error except when they are
explicitly communicating with a child process. Keep temporary resources uniquely
named and delete only resources owned by the test.

## Build and validation

From the repository root:

```text
dotnet clean Icod.Processes.sln -c Debug
dotnet restore Icod.Processes.sln
dotnet build Icod.Processes.sln -c Debug --no-restore
dotnet test Icod.Processes.sln -c Debug --no-build
```

Before merge or release, also validate Release:

```text
dotnet clean Icod.Processes.sln -c Release
dotnet restore Icod.Processes.sln
dotnet build Icod.Processes.sln -c Release --no-restore
dotnet test Icod.Processes.sln -c Release --no-build
```

`build.cmd` and `build.sh` may be used for the standard local sequence. Pull
requests run the Staging configuration across the three CI operating systems;
pushes to `main` run Release and publish only after the Release matrix succeeds.

## Pull requests and commits

Keep changes focused. A pull request should identify:

- the process contract being changed;
- important platform-specific behavior;
- added or changed tests;
- the build/test commands and platforms used; and
- any intentionally unsupported or deferred behavior.

Use concise imperative commit subjects. Discuss cross-package ownership changes
before introducing a new shared abstraction.

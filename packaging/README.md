# Icod.Processes build and distribution tooling

`Icod.Processes` follows the canonical Icod C#/.NET lifecycle while preserving its .NET 10 process-control package contract.

| Lifecycle | Configuration | Entry point |
| --- | --- | --- |
| local `build.cmd` / `build.sh` | `Debug` | `packaging/Invoke-Build.ps1` |
| pull request | `Staging` | `.github/workflows/pull-request.yaml` |
| push to `main` | `Release` | `.github/workflows/main.yaml` |
| manual diagnostic | selected | `.github/workflows/distribution-validation.yaml` |
| `v*` tag contained in `main` | `Release` | `.github/workflows/release.yaml` |

The package verifier requires the exact generated `Icod.Processes` `.nupkg` and matching `.snupkg`. It checks package identity/version, README, LICENSE, icon, the `net10.0` DLL/XML payload, the `Icod.Timing` 1.0.0 dependency, and the portable-PDB signature of the symbol payload.

`DebugType`, `DebugSymbols`, and signing policy are shared across configurations in the project files. Debug, Staging, and Release all use portable debug information.

Ordinary pushes to `main` validate but never publish. Tagged releases publish the same exact validated package to NuGet.org and GitHub Packages in parallel, then create the GitHub Release.

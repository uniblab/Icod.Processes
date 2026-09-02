param(
    [Parameter(Mandatory = $true)][string]$SourceDirectory,
    [Parameter(Mandatory = $true)][string]$DestinationDirectory,
    [Parameter(Mandatory = $true)][string]$ExpectedVersion
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Import-Module (Join-Path $PSScriptRoot 'RepositoryTools.psm1') -Force

foreach ($variableName in @('SourceDirectory', 'DestinationDirectory')) {
    $value = Get-Variable -Name $variableName -ValueOnly
    if (-not [System.IO.Path]::IsPathRooted($value)) {
        $value = Join-Path $repositoryRoot $value
    }
    Set-Variable -Name $variableName -Value ([System.IO.Path]::GetFullPath($value))
}

if (-not (Test-Path -LiteralPath $SourceDirectory -PathType Container)) {
    throw "Source package directory '$SourceDirectory' does not exist."
}
if (Test-Path -LiteralPath $DestinationDirectory) {
    Remove-Item -LiteralPath $DestinationDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $DestinationDirectory -Force | Out-Null

$packages = @(
    Get-ChildItem -LiteralPath $SourceDirectory -Filter 'Icod.Processes.*.nupkg' -File |
        Where-Object { -not $_.Name.EndsWith('.symbols.nupkg', [System.StringComparison]::OrdinalIgnoreCase) }
)
if (1 -ne $packages.Count) {
    throw "Expected exactly one Icod.Processes package candidate; found $($packages.Count)."
}

$metadata = Get-PackageMetadata -PackagePath $packages[0].FullName
if ($metadata.Version -ne $ExpectedVersion) {
    throw "Package version '$($metadata.Version)' does not match release version '$ExpectedVersion'."
}

$symbolPath = Join-Path $SourceDirectory "Icod.Processes.$ExpectedVersion.snupkg"
if (-not (Test-Path -LiteralPath $symbolPath -PathType Leaf)) {
    throw "Matching symbol package '$symbolPath' does not exist."
}

Copy-Item -LiteralPath $packages[0].FullName -Destination $DestinationDirectory
Copy-Item -LiteralPath $symbolPath -Destination $DestinationDirectory
Write-Host "Selected Icod.Processes $ExpectedVersion release package and symbols."

param(
    [Parameter(Mandatory = $true)][string]$ArtifactDirectory,
    [ValidateSet('Debug', 'Staging', 'Release')][string]$Configuration = 'Release',
    [string]$ExpectedVersion = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Import-Module (Join-Path $PSScriptRoot 'RepositoryTools.psm1') -Force

if (-not [System.IO.Path]::IsPathRooted($ArtifactDirectory)) {
    $ArtifactDirectory = Join-Path $repositoryRoot $ArtifactDirectory
}
$ArtifactDirectory = [System.IO.Path]::GetFullPath($ArtifactDirectory)
if (-not (Test-Path -LiteralPath $ArtifactDirectory -PathType Container)) {
    throw "Artifact directory '$ArtifactDirectory' does not exist."
}

$packages = @(
    Get-ChildItem -LiteralPath $ArtifactDirectory -Filter 'Icod.Processes.*.nupkg' -File |
        Where-Object { -not $_.Name.EndsWith('.symbols.nupkg', [System.StringComparison]::OrdinalIgnoreCase) } |
        Sort-Object Name
)
if (1 -ne $packages.Count) {
    throw "Expected exactly one Icod.Processes .nupkg; found $($packages.Count)."
}

$package = $packages[0]
$metadata = Get-PackageMetadata -PackagePath $package.FullName
if ('Icod.Processes' -ne $metadata.Id) {
    throw "Unexpected package ID '$($metadata.Id)'."
}
if (-not [string]::IsNullOrWhiteSpace($ExpectedVersion) -and $ExpectedVersion -ne $metadata.Version) {
    throw "Package version '$($metadata.Version)' does not match expected '$ExpectedVersion'."
}

$symbols = @(
    Get-ChildItem -LiteralPath $ArtifactDirectory -Filter "Icod.Processes.$($metadata.Version).snupkg" -File
)
if (1 -ne $symbols.Count) {
    throw "Expected matching Icod.Processes symbol package; found $($symbols.Count)."
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($package.FullName)
try {
    $entries = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\\', '/') })
    foreach ($required in @(
        'README.md',
        'LICENSE',
        'icon.png',
        'lib/net10.0/Icod.Processes.dll',
        'lib/net10.0/Icod.Processes.xml'
    )) {
        if ($required -notin $entries) {
            throw "Package '$($package.Name)' is missing '$required'."
        }
    }

    $nuspecEntry = $archive.Entries |
        Where-Object { $_.FullName.EndsWith('.nuspec', [System.StringComparison]::OrdinalIgnoreCase) } |
        Select-Object -First 1
    $reader = [System.IO.StreamReader]::new($nuspecEntry.Open())
    try {
        [xml]$nuspec = $reader.ReadToEnd()
    } finally {
        $reader.Dispose()
    }
    $timingDependency = $nuspec.SelectSingleNode(
        "//*[local-name()='dependency' and @id='Icod.Timing']"
    )
    if ($null -eq $timingDependency) {
        throw "Package '$($package.Name)' does not declare its Icod.Timing dependency."
    }
    if ('1.0.0' -ne $timingDependency.version) {
        throw "Icod.Timing dependency version '$($timingDependency.version)' does not match 1.0.0."
    }
} finally {
    $archive.Dispose()
}

$symbolArchive = [System.IO.Compression.ZipFile]::OpenRead($symbols[0].FullName)
try {
    $pdbEntry = $symbolArchive.Entries |
        Where-Object {
            $_.FullName.Replace('\\', '/') -in @(
                'lib/net10.0/Icod.Processes.pdb',
                'Icod.Processes.pdb'
            )
        } |
        Select-Object -First 1
    if ($null -eq $pdbEntry) {
        throw "Symbol package '$($symbols[0].Name)' does not contain Icod.Processes.pdb."
    }

    $stream = $pdbEntry.Open()
    try {
        $signature = New-Object byte[] 4
        if (4 -ne $stream.Read($signature, 0, 4)) {
            throw "Symbol PDB '$($pdbEntry.FullName)' is too short."
        }
        if (
            0x42 -ne $signature[0] -or
            0x53 -ne $signature[1] -or
            0x4A -ne $signature[2] -or
            0x42 -ne $signature[3]
        ) {
            throw "Symbol PDB '$($pdbEntry.FullName)' is not a portable PDB."
        }
    } finally {
        $stream.Dispose()
    }
} finally {
    $symbolArchive.Dispose()
}

Write-Host "Exact Icod.Processes package verification completed successfully for $($metadata.Version) ($Configuration)."

[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ReportDirectory,
    [string]$BackendRoot = (Join-Path $PSScriptRoot '..\..')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$backend = [IO.Path]::GetFullPath($BackendRoot)
$project = Join-Path $backend 'src\CureFlow.Api\CureFlow.Api.csproj'
if (!(Test-Path -LiteralPath $project -PathType Leaf)) { throw 'Backend API project was not found.' }
if (![IO.Path]::IsPathFullyQualified($ReportDirectory)) { throw 'An explicit absolute report directory is required.' }
$destination = [IO.Path]::GetFullPath($ReportDirectory)

# Audit data retrieval failure is an error; suppressing or ignoring an unavailable
# advisory source must never produce a clean report. This does not run tests.
& dotnet restore $project --source https://api.nuget.org/v3/index.json `
    -p:NuGetAudit=true -p:NuGetAuditMode=all -p:NuGetAuditLevel=low -warnaserror:NU1900
if ($LASTEXITCODE -ne 0) { throw 'Restore/advisory metadata retrieval failed.' }

function Read-PackageReport([string[]]$Options) {
    $json = & dotnet list $project package --include-transitive --format json --no-restore @Options
    if ($LASTEXITCODE -ne 0) { throw 'Dependency inventory/advisory scan failed.' }
    $parsed = ($json -join [Environment]::NewLine) | ConvertFrom-Json
    if ($parsed.version -ne 1 -or !$parsed.projects -or $parsed.projects.Count -ne 1) {
        throw 'Unexpected package report; do not treat missing inventory as clean.'
    }
    return $parsed
}

$inventory = Read-PackageReport @()
$advisories = Read-PackageReport @('--vulnerable', '--source', 'https://api.nuget.org/v3/index.json')
$findings = @()
foreach ($entry in $advisories.projects) {
    if (!$entry.PSObject.Properties['frameworks']) { continue }
    foreach ($framework in $entry.frameworks) {
        foreach ($kind in @('topLevelPackages', 'transitivePackages')) {
            if (!$framework.PSObject.Properties[$kind]) { continue }
            foreach ($package in $framework.$kind) {
                foreach ($advisory in $package.vulnerabilities) {
                    $findings += [ordered]@{ Package = $package.id; Version = $package.resolvedVersion;
                        Severity = $advisory.severity; Advisory = $advisory.advisoryurl; DependencyKind = $kind }
                }
            }
        }
    }
}

$report = [ordered]@{
    SchemaVersion = 1
    RecordedAtUtc = [DateTime]::UtcNow.ToString('O')
    AdvisorySource = 'https://api.nuget.org/v3/index.json'
    Inventory = $inventory
    Findings = $findings
    ContainerScan = 'Not executed; independently scan the pinned release image before acceptance.'
    ReleaseAcceptance = 'Not established; accountable triage, regression/rollback and environment evidence are required.'
}
[IO.Directory]::CreateDirectory($destination) | Out-Null
$artifact = Join-Path $destination ('dependency-audit-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffffffZ') + '.json')
$bytes = [Text.Encoding]::UTF8.GetBytes(($report | ConvertTo-Json -Depth 30))
$file = [IO.FileStream]::new($artifact, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
try { $file.Write($bytes, 0, $bytes.Length); $file.Flush($true) } finally { $file.Dispose() }
Write-Output ('Dependency audit recorded: {0}; findings: {1}; container/release acceptance: pending.' -f $artifact, $findings.Count)
if ($findings.Count -gt 0) { exit 1 }

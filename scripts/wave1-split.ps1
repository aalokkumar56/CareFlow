# Wave 1: split bundled Application/Domain files (one type per file)
$ErrorActionPreference = 'Stop'
$root = 'D:\Projects\Sarvik\Care-Flow\Cure-Flow\dotnet-backend\src'

function Write-TypeFile($dir, $typeName, $content) {
    $path = Join-Path $dir "$typeName.cs"
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    Set-Content -Path $path -Value $content -Encoding UTF8
}

# --- Enums ---
$enumDir = Join-Path $root 'CureFlow.Domain\Enums'
$enumFile = Join-Path $enumDir 'Enums.cs'
if (Test-Path $enumFile) {
    $text = Get-Content $enumFile -Raw
    $matches = [regex]::Matches($text, '(?ms)(///.*?\r?\n)*public enum (\w+)\s*\{[^}]+\}')
    foreach ($m in $matches) {
        $name = $m.Groups[2].Value
        $body = "namespace CureFlow.Domain.Enums;`r`n`r`n" + $m.Value.Trim()
        Write-TypeFile $enumDir $name $body
    }
    Remove-Item $enumFile -Force
}

# --- IBusinessServices interfaces ---
$bizDir = Join-Path $root 'CureFlow.Application\Interfaces\BusinessServices'
$bizSrc = Join-Path $root 'CureFlow.Application\Interfaces\IBusinessServices.cs'
if (Test-Path $bizSrc) {
    $header = @"
using CureFlow.Application.Common;
using CureFlow.Application.DTOs;

namespace CureFlow.Application.Interfaces;

"@
    $text = Get-Content $bizSrc -Raw
    $parts = [regex]::Split($text, '(?=\r?\npublic interface )')
    foreach ($part in $parts) {
        if ($part -notmatch 'public interface (\w+)') { continue }
        $name = $Matches[1]
        $body = $header.TrimEnd() + "`r`n`r`n" + $part.Trim()
        Write-TypeFile $bizDir $name $body
    }
    Remove-Item $bizSrc -Force
}

# --- IServices interfaces ---
$svcDir = Join-Path $root 'CureFlow.Application\Interfaces\Services'
$svcSrc = Join-Path $root 'CureFlow.Application\Interfaces\IServices.cs'
if (Test-Path $svcSrc) {
    $header = @"
using CureFlow.Application.DTOs;
using CureFlow.Application.Common;
using CureFlow.Domain.Enums;

namespace CureFlow.Application.Interfaces;

"@
    $text = Get-Content $svcSrc -Raw
    $parts = [regex]::Split($text, '(?=\r?\npublic interface )')
    foreach ($part in $parts) {
        if ($part -notmatch 'public interface (\w+)') { continue }
        $name = $Matches[1]
        $body = $header.TrimEnd() + "`r`n`r`n" + $part.Trim()
        Write-TypeFile $svcDir $name $body
    }
    Remove-Item $svcSrc -Force
}

# --- DTO splitter helper ---
function Split-DtoFile($srcPath, $targetSubDir) {
    if (-not (Test-Path $srcPath)) { return 0 }
    $dir = Join-Path (Join-Path $root 'CureFlow.Application\DTOs') $targetSubDir
    $text = Get-Content $srcPath -Raw
    $usingBlock = ''
    if ($text -match '(?ms)^(using[^;]+;[\r\n]+)+') { $usingBlock = $Matches[0].Trim() + "`r`n`r`n" }
    $namespace = 'namespace CureFlow.Application.DTOs;'
    $count = 0
    $records = [regex]::Matches($text, '(?ms)(///.*?\r?\n)*public (?:record|class) (\w+)')
    foreach ($m in $records) {
        $name = $m.Groups[2].Value
        $start = $m.Index
        $next = $records | Where-Object { $_.Index -gt $start } | Select-Object -First 1
        $end = if ($next) { $next.Index } else { $text.Length }
        $typeBody = $text.Substring($start, $end - $start).Trim()
        $body = $usingBlock + $namespace + "`r`n`r`n" + $typeBody
        Write-TypeFile $dir $name $body
        $count++
    }
    Remove-Item $srcPath -Force
    return $count
}

$dtoMap = @{
    'CoreDtos.cs' = @('Auth', 'Patient', 'Appointment', 'Patient')  # handled below custom
}
# Custom CoreDtos split by section comments
$corePath = Join-Path $root 'CureFlow.Application\DTOs\CoreDtos.cs'
if (Test-Path $corePath) {
    $text = Get-Content $corePath -Raw
    $usingBlock = "using CureFlow.Domain.Entities;`r`nusing CureFlow.Domain.Enums;`r`n`r`n"
    $ns = "namespace CureFlow.Application.DTOs;`r`n`r`n"
    $authTypes = 'LoginRequest','RegisterTenantRequest','AuthResponse','UserDto','TenantDto','CreateUserRequest'
    $patientTypes = 'CreatePatientRequest','UpdatePatientRequest','PatientSummaryDto','PatientDetailDto','LifestyleProfileDto'
    $apptTypes = 'CreateAppointmentRequest','UpdateAppointmentRequest','AppointmentDto'
    $maps = @{
        'Auth' = $authTypes
        'Patient' = $patientTypes
        'Appointment' = $apptTypes
    }
    $records = [regex]::Matches($text, '(?ms)(///.*?\r?\n)*public record (\w+)')
    foreach ($m in $records) {
        $name = $m.Groups[2].Value
        $folder = 'Common'
        foreach ($k in $maps.Keys) {
            if ($maps[$k] -contains $name) { $folder = $k; break }
        }
        $start = $m.Index
        $idx = [array]::IndexOf(($records | ForEach-Object Index), $start)
        $end = if ($idx -lt $records.Count - 1) { $records[$idx + 1].Index } else { $text.Length }
        $typeBody = $text.Substring($start, $end - $start).Trim()
        $dir = Join-Path (Join-Path $root 'CureFlow.Application\DTOs') $folder
        Write-TypeFile $dir $name ($usingBlock + $ns + $typeBody)
    }
    Remove-Item $corePath -Force
}

Split-DtoFile (Join-Path $root 'CureFlow.Application\DTOs\EhrDtos.cs') 'Ehr' | Out-Null
Split-DtoFile (Join-Path $root 'CureFlow.Application\DTOs\WhatsAppDtos.cs') 'WhatsApp' | Out-Null
Split-DtoFile (Join-Path $root 'CureFlow.Application\DTOs\StaffDtos.cs') 'Staff' | Out-Null
Split-DtoFile (Join-Path $root 'CureFlow.Application\DTOs\VisitDtos.cs') 'Visit' | Out-Null
Split-DtoFile (Join-Path $root 'CureFlow.Application\DTOs\UserManagementDtos.cs') 'UserManagement' | Out-Null

# --- ApiResponse common types ---
$apiPath = Join-Path $root 'CureFlow.Application\Common\ApiResponse.cs'
if (Test-Path $apiPath) {
    $text = Get-Content $apiPath -Raw
    $types = [regex]::Matches($text, '(?ms)(///.*?\r?\n)*public class (\w+)')
    foreach ($m in $types) {
        $name = $m.Groups[2].Value
        $start = $m.Index
        $next = $types | Where-Object { $_.Index -gt $start } | Select-Object -First 1
        $end = if ($next) { $next.Index } else { $text.Length }
        $typeBody = $text.Substring($start, $end - $start).Trim()
        $body = "namespace CureFlow.Application.Common;`r`n`r`n" + $typeBody
        Write-TypeFile (Join-Path $root 'CureFlow.Application\Common') $name $body
    }
    Remove-Item $apiPath -Force
}

# --- Domain WhatsApp entities ---
$waPath = Join-Path $root 'CureFlow.Domain\Entities\WhatsAppEntities.cs'
if (Test-Path $waPath) {
    $text = Get-Content $waPath -Raw
    $using = "using CureFlow.Domain.Common;`r`n`r`nnamespace CureFlow.Domain.Entities;`r`n`r`n"
    $classes = [regex]::Matches($text, '(?ms)public class (\w+)')
    foreach ($m in $classes) {
        $name = $m.Groups[1].Value
        $start = $m.Index
        $next = $classes | Where-Object { $_.Index -gt $start } | Select-Object -First 1
        $end = if ($next) { $next.Index } else { $text.Length }
        $typeBody = $text.Substring($start, $end - $start).Trim()
        Write-TypeFile (Join-Path $root 'CureFlow.Domain\Entities\WhatsApp') $name ($using + $typeBody)
    }
    Remove-Item $waPath -Force
}

# --- Domain Referrals bundle ---
$refPath = Join-Path $root 'CureFlow.Domain\Entities\Referrals.cs'
if (Test-Path $refPath) {
    $text = Get-Content $refPath -Raw
    $using = "using CureFlow.Domain.Common;`r`nusing CureFlow.Domain.Enums;`r`n`r`nnamespace CureFlow.Domain.Entities;`r`n`r`n"
    $classes = [regex]::Matches($text, '(?ms)public class (\w+)')
    foreach ($m in $classes) {
        $name = $m.Groups[1].Value
        $folder = switch ($name) {
            'ReferringDoctor' { 'Referral' }
            'Referral' { 'Referral' }
            { $_ -like 'Campaign*' } { 'Campaign' }
            default { '' }
        }
        $start = $m.Index
        $next = $classes | Where-Object { $_.Index -gt $start } | Select-Object -First 1
        $end = if ($next) { $next.Index } else { $text.Length }
        $typeBody = $text.Substring($start, $end - $start).Trim()
        $dir = Join-Path $root "CureFlow.Domain\Entities\$folder"
        Write-TypeFile $dir $name ($using + $typeBody)
    }
    Remove-Item $refPath -Force
}

Write-Host 'Wave1 split complete.'

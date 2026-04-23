param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$Summary,

    [switch]$SkipTests,
    [switch]$SkipDocker,
    [switch]$NoPush,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Fail {
    param([string]$Message)
    Write-Host ""
    Write-Host "[ERROR] $Message" -ForegroundColor Red
    exit 1
}

function Require-Command {
    param([string]$Name)
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        Fail "No s'ha trobat '$Name' al PATH."
    }
}

function Get-CurrentBranch {
    $branch = git rev-parse --abbrev-ref HEAD
    return $branch.Trim()
}

function Update-Readme {
    param(
        [string]$ReadmePath,
        [string]$NewVersion,
        [string]$EntrySummary
    )

    if (-not (Test-Path $ReadmePath)) {
        Fail "No s'ha trobat README.md a $ReadmePath"
    }

    $content = Get-Content $ReadmePath -Raw -Encoding UTF8

    $versionPattern = '(\#\s*.+?\s+·\s+v)(\d+\.\d+\.\d+)'
    if ($content -notmatch $versionPattern) {
        Fail "No s'ha pogut localitzar la versió a la capçalera del README."
    }

    $content = [regex]::Replace($content, $versionPattern, "`${1}$NewVersion", 1)

    $today = Get-Date -Format "yyyy-MM-dd"
    $newEntry = @"
## v$NewVersion
- $EntrySummary
- Release generada el $today

"@

    $changelogPattern = '(?ms)(##\s+Changelog\s*\r?\n)'
    if ([regex]::IsMatch($content, $changelogPattern)) {
        $content = [regex]::Replace($content, $changelogPattern, "`${1}`r`n$newEntry", 1)
    } else {
        $content = $content.TrimEnd() + "`r`n`r`n## Changelog`r`n`r`n$newEntry"
    }

    Set-Content -Path $ReadmePath -Value $content -Encoding UTF8
}

try {
    $repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
    Set-Location $repoRoot

    Write-Host ""
    Write-Host "=====================================================" -ForegroundColor DarkCyan
    Write-Host " AutoCo - Release local (Windows -> GitHub -> LXC)" -ForegroundColor DarkCyan
    Write-Host " Repositori: $repoRoot" -ForegroundColor DarkCyan
    Write-Host "=====================================================" -ForegroundColor DarkCyan

    if ($Version -notmatch '^\d+\.\d+\.\d+$') {
        Fail "La versió ha de seguir el format X.Y.Z"
    }

    Require-Command git
    Require-Command dotnet

    Write-Step "Comprovant estat del repositori git"
    if (-not (Test-Path (Join-Path $repoRoot ".git"))) {
        Fail "El directori actual no és un repositori git."
    }

    $status = git status --porcelain
    if ($status) {
        Fail "El repositori té canvis pendents. Fes commit o neteja l'estat abans de fer release."
    }

    $branch = Get-CurrentBranch
    Write-Host "Branca actual: $branch"

    Write-Step "Executant validacions locals"
    $checkScript = Join-Path $PSScriptRoot "check.ps1"
    if (-not (Test-Path $checkScript)) {
        Fail "No s'ha trobat check.ps1 al mateix directori que release.ps1"
    }

    $checkArgs = @()
    if ($SkipTests) { $checkArgs += "-SkipTests" }
    if ($SkipDocker) { $checkArgs += "-SkipDocker" }

    & $checkScript @checkArgs
    if ($LASTEXITCODE -ne 0) {
        Fail "La validació local ha fallat."
    }

    Write-Step "Actualitzant README.md"
    $readmePath = Join-Path $repoRoot "README.md"
    Update-Readme -ReadmePath $readmePath -NewVersion $Version -EntrySummary $Summary

    Write-Step "Mostrant resum de canvis"
    git diff -- README.md

    if ($DryRun) {
        Write-Host ""
        Write-Host "[DRY-RUN] No s'executarà cap commit ni push." -ForegroundColor Yellow
        Write-Host "[INFO] Després del push, el desplegament real es fa a la LXC executant deploy/server-update.sh" -ForegroundColor Yellow
        exit 0
    }

    Write-Step "Preparant commit"
    git add README.md
    git commit -m "release: v$Version - $Summary"

    if (-not $NoPush) {
        Write-Step "Fent push a GitHub"
        git push
    } else {
        Write-Host ""
        Write-Host "[INFO] Push omès per paràmetre -NoPush." -ForegroundColor Yellow
    }

    Write-Host ""
    Write-Host "Release completada correctament." -ForegroundColor Green
    Write-Host "Següent pas: entrar a la LXC i executar deploy/server-update.sh" -ForegroundColor Green
    exit 0
}
catch {
    Fail $_.Exception.Message
}

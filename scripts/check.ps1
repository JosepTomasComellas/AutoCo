param(
    [switch]$SkipTests,
    [switch]$SkipDocker
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

try {
    $repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
    Set-Location $repoRoot

    Write-Host ""
    Write-Host "==============================================" -ForegroundColor DarkCyan
    Write-Host " AutoCo - Validació local (Windows/PowerShell)" -ForegroundColor DarkCyan
    Write-Host " Repositori: $repoRoot" -ForegroundColor DarkCyan
    Write-Host "==============================================" -ForegroundColor DarkCyan

    $solution = Get-ChildItem -Path $repoRoot -Filter *.sln -File -ErrorAction SilentlyContinue | Select-Object -First 1

    if (-not $solution) {
        Write-Host ""
        Write-Host "[WARN] No s'ha trobat cap fitxer .sln al directori arrel." -ForegroundColor Yellow
        Write-Host "       Es provarà igualment a compilar els projectes detectats." -ForegroundColor Yellow
    }

    Write-Step "Comprovant eines disponibles"
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        Fail "No s'ha trobat 'dotnet' al PATH."
    }

    Write-Step "Restauració de paquets"
    if ($solution) {
        dotnet restore $solution.FullName
    } else {
        dotnet restore
    }

    Write-Step "Compilació en mode Release"
    if ($solution) {
        dotnet build $solution.FullName -c Release --no-restore
    } else {
        dotnet build -c Release --no-restore
    }

    if (-not $SkipTests) {
        $testProjects = Get-ChildItem -Path $repoRoot -Recurse -Filter *.csproj -File |
            Where-Object { $_.Name -match 'Test|Tests' }

        if ($testProjects) {
            Write-Step "Execució de tests"
            foreach ($project in $testProjects) {
                Write-Host "Executant tests a: $($project.FullName)"
                dotnet test $project.FullName -c Release --no-build
            }
        } else {
            Write-Host ""
            Write-Host "[INFO] No s'han trobat projectes de test." -ForegroundColor Yellow
        }
    } else {
        Write-Host ""
        Write-Host "[INFO] Tests omesos per paràmetre -SkipTests." -ForegroundColor Yellow
    }

    if (-not $SkipDocker) {
        $composeFiles = @(
            Join-Path $repoRoot "docker-compose.yml",
            Join-Path $repoRoot "compose.yml",
            Join-Path $repoRoot "docker-compose.yaml",
            Join-Path $repoRoot "compose.yaml"
        ) | Where-Object { Test-Path $_ }

        if ($composeFiles.Count -gt 0) {
            if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
                Fail "S'ha trobat fitxer docker compose però 'docker' no és al PATH."
            }

            $composeFile = $composeFiles[0]
            Write-Step "Validació de docker compose"
            docker compose -f $composeFile config | Out-Null
        } else {
            Write-Host ""
            Write-Host "[INFO] No s'ha trobat cap fitxer docker compose." -ForegroundColor Yellow
        }
    } else {
        Write-Host ""
        Write-Host "[INFO] Validació Docker omesa per paràmetre -SkipDocker." -ForegroundColor Yellow
    }

    Write-Host ""
    Write-Host "Validació completada correctament." -ForegroundColor Green
    exit 0
}
catch {
    Fail $_.Exception.Message
}

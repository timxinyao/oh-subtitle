$ErrorActionPreference = 'Stop'

$projectRoot = (Resolve-Path -LiteralPath $PSScriptRoot).Path
$projectFile = Join-Path $projectRoot 'src\OhSubtitle.csproj'
$binRoot = Join-Path $projectRoot 'src\bin'
$objRoot = Join-Path $projectRoot 'src\obj'
$publishDir = Join-Path $projectRoot 'src\bin\Release\net6.0-windows\win-x64\publish'
$publishExe = Join-Path $publishDir 'OhSubtitle.exe'

if (-not (Test-Path -LiteralPath $projectFile)) {
    throw "Project file not found: $projectFile"
}

# 只处理当前项目 bin 目录内的 OhSubtitle.exe，不误杀其他位置的同名程序。
$knownExecutablePaths = @(
    if (Test-Path -LiteralPath $binRoot) {
        Get-ChildItem -LiteralPath $binRoot -Recurse -File -Filter 'OhSubtitle.exe' |
            ForEach-Object { $_.FullName }
    }
)

$runningProcesses = @(Get-CimInstance Win32_Process -Filter "Name = 'OhSubtitle.exe'" | Where-Object {
    if (-not $_.ExecutablePath) {
        return $false
    }

    $executablePath = [IO.Path]::GetFullPath($_.ExecutablePath)
    return $knownExecutablePaths -contains $executablePath
})

foreach ($candidate in $runningProcesses) {
    $process = Get-Process -Id $candidate.ProcessId -ErrorAction SilentlyContinue
    if (-not $process) {
        continue
    }

    Write-Output "Closing OhSubtitle process $($candidate.ProcessId): $($candidate.ExecutablePath)"
    $requestedClose = $process.CloseMainWindow()
    if ($requestedClose) {
        $process.WaitForExit(3000) | Out-Null
    }

    if (-not $process.HasExited) {
        Write-Output "The process did not close gracefully; stopping process $($candidate.ProcessId)."
        Stop-Process -Id $candidate.ProcessId -Force
        $process.WaitForExit(3000) | Out-Null
    }
}

# 删除本项目生成目录，发布后只重新生成最终自包含目录。
foreach ($generatedDirectory in @($binRoot, $objRoot)) {
    if (Test-Path -LiteralPath $generatedDirectory) {
        $resolvedDirectory = (Resolve-Path -LiteralPath $generatedDirectory).Path
        if ($resolvedDirectory -ne $generatedDirectory) {
            throw "Unexpected cleanup target: $resolvedDirectory"
        }

        Remove-Item -LiteralPath $generatedDirectory -Recurse -Force
    }
}

$dotnet = $null
$bundledDotnet = 'C:\Users\yaoxin\.cache\codex-runtimes\dotnet-sdk-6.0\dotnet.exe'
if (Test-Path -LiteralPath $bundledDotnet) {
    $dotnet = $bundledDotnet
}
else {
    $dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($dotnetCommand) {
        $sdkList = & $dotnetCommand.Source --list-sdks 2>$null
        if ($LASTEXITCODE -eq 0 -and $sdkList) {
            $dotnet = $dotnetCommand.Source
        }
    }
}

if (-not $dotnet) {
    throw 'dotnet SDK was not found. Install .NET 6 SDK or add dotnet.exe to PATH.'
}

& $dotnet publish $projectFile -c Release -r win-x64 --self-contained true -o $publishDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$publishedExecutables = @(Get-ChildItem -LiteralPath $binRoot -Recurse -File -Filter 'OhSubtitle.exe')
if ($publishedExecutables.Count -ne 1 -or $publishedExecutables[0].FullName -ne $publishExe) {
    $paths = ($publishedExecutables | ForEach-Object { $_.FullName }) -join [Environment]::NewLine
    throw "Expected exactly one published executable at $publishExe, found:`n$paths"
}

Write-Output "Published self-contained executable: $publishExe"

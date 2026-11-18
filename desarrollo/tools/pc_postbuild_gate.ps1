# Puerta automatica post-build de PC (PLAN_PC §33). Ejecuta el .exe unos segundos con su propio -logFile y FALLA (exit 1)
# si el Player.log contiene errores del pipeline URP (Blitter, "shader is null", NullReferenceException) o el exe no arranca.
# Uso: powershell -File pc_postbuild_gate.ps1 -BuildDir <carpeta con TwinSightX500.exe o su padre> [-Seconds 25]
param([Parameter(Mandatory=$true)][string]$BuildDir, [int]$Seconds = 25)
$exe = Get-ChildItem -Path $BuildDir -Filter TwinSightX500.exe -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $exe) { Write-Output "GATE FALLA: no hay TwinSightX500.exe en $BuildDir"; exit 1 }
$log = Join-Path $env:TEMP ("twinsight_gate_" + [guid]::NewGuid().ToString("N") + ".log")
$p = Start-Process -FilePath $exe.FullName -ArgumentList @("-logFile", $log, "-screen-fullscreen", "0", "-screen-width", "1280", "-screen-height", "720", "-popupwindow") -PassThru
Start-Sleep -Seconds $Seconds
if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force } 
Start-Sleep -Milliseconds 500
if (-not (Test-Path $log)) { Write-Output "GATE FALLA: no se genero Player.log"; exit 1 }
$bad = Select-String -Path $log -Pattern 'Blitter','shader is null','Cannot create required material','NullReferenceException','LocalKeyword with a null Shader' | Select-Object -First 8
if ($bad) { Write-Output "GATE FALLA: errores de render en $log"; $bad | ForEach-Object { Write-Output ("  " + $_.Line) }; exit 1 }
Write-Output "GATE OK: $($exe.FullName) ($log sin errores de Blitter/shader/NullReference)"
exit 0

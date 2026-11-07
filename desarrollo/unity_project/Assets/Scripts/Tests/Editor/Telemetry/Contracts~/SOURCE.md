# Copia del contrato de telemetría (WP-01) para los tests EditMode

- Origen: repo web `E:\twinsight-web-twin`, commit `fb5671bf1730b9f7eaf359a43f51841dab0add24`
  (`feat: contrato de telemetria versionado (WP-01)`), `protocolVersion` 1.0.0.
- Copiado: `contracts/telemetry.schema.json`, `contracts/labels.json`, `contracts/fixtures/**`, `contracts/golden/**`
  y `tests/fixtures/mini.twf` (en `golden/mini.twf`, lo cita `golden/replay.json`).
- `labels.json` también vive en `Assets/Scripts/Telemetry/Resources/TelemetryLabels.json` (lo usa el juego); un test exige que
  las dos copias sean idénticas.
- La carpeta acaba en `~`: Unity no la importa (sin `.meta`, no entra en ninguna build). Los tests la leen con `System.IO`.
- El TWF completo (`data/x500v2_flight.twf`, 1,2 MB) NO se copia: los tests de fotogramas de la repetición lo buscan en
  `Assets/StreamingAssets/Telemetry/x500v2_flight.twf` (donde lo pondrá WP-09), luego en `%TWINSIGHT_WEB_REPO%/data/` y en
  `E:/twinsight-web-twin/data/`; si no está, esos casos quedan como *Inconclusive*.

## Refrescar (cuando cambie el contrato)

PowerShell, desde `desarrollo/unity_project`:

```powershell
$web = "E:\twinsight-web-twin"; $dst = "Assets\Scripts\Tests\Editor\Telemetry\Contracts~"
Remove-Item -Recurse -Force "$dst\fixtures", "$dst\golden"
Copy-Item -Recurse "$web\contracts\fixtures", "$web\contracts\golden" $dst
Copy-Item "$web\contracts\labels.json", "$web\contracts\telemetry.schema.json" $dst
Copy-Item "$web\tests\fixtures\mini.twf" "$dst\golden\mini.twf"
Copy-Item "$web\contracts\labels.json" "Assets\Scripts\Telemetry\Resources\TelemetryLabels.json"
git -C $web log -1 --format=%H -- contracts   # anotar el hash aquí arriba
```

Después: subir `TelemetryProtocol.Version` si cambió `protocolVersion` y pasar los tests (Window > General > Test Runner > EditMode,
filtro `WebGL.Telemetry`).

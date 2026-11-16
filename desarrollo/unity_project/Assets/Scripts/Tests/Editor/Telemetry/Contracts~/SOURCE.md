# Copia del contrato de telemetría (WP-01) para los tests EditMode

- Origen: repo web `E:\twinsight-web-twin`, commit `ab3e5a6a706513c8c95f1400aebc44b6130c24a7` (contrato WP-01 de `fb5671b` + `classification.replay.local` y dt exacto en `golden/quadx.json` [8e47e15] + `classification.sim` y `replay.localDetail` [96de458] + `golden/history.json` de las graficas de Analisis [69822e7] + `golden/flightsim.json` del simulador de vuelo [ab3e5a6]; en 96de458 solo cambia `labels.json` y en 69822e7 solo se anade `golden/history.json`)
  (`feat: contrato de telemetria versionado (WP-01)`), `protocolVersion` 1.0.0.
- Copiado: `contracts/telemetry.schema.json`, `contracts/labels.json`, `contracts/fixtures/**`, `contracts/golden/**`
  y `tests/fixtures/mini.twf` (en `golden/mini.twf`, lo cita `golden/replay.json`).
- `labels.json` también vive en `Assets/Scripts/Telemetry/Resources/TelemetryLabels.json` (lo usa el juego); un test exige que
  las dos copias sean idénticas.
- La carpeta acaba en `~`: Unity no la importa (sin `.meta`, no entra en ninguna build). Los tests la leen con `System.IO`.
- El TWF completo (`data/x500v2_flight.twf`, 1,2 MB) NO se copia: los tests de fotogramas de la repetición lo buscan en
  `Assets/StreamingAssets/Telemetry/x500v2_flight.twf` (WP-09: ya está ahí; sha256 `883de9a2…594433e`), luego en `%TWINSIGHT_WEB_REPO%/data/` y en
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

## Lote 1a (2026-11-16): geocerca, viento y fallas del simulador

- Origen: repo web, rama `fable/lote1` (worktree `E:\twinsight-web-twin-fable`), commit `1311a20bdb5b9917cb2dbf980acfa77795e0031e` (el contrato de los goldens es el de `47a905b`, ultimo commit que toca `contracts/`).
- Copiados de `contracts/golden/`: `geofence.json`, `wind.json`, `motorfail.json` (nuevos) y `flightsim.json` (sustituido: `hoverThrottle` 0,62 calibrado con el vuelo real y los casos `motor_off` / `restore` de `replies` ahora aceptados).
- Los demas goldens de esta carpeta no cambian (igual salvo fin de linea). `labels.json` y `telemetry.schema.json` NO se actualizan aqui (la web cambio `source.replay` y anadio rotulos de sesiones propias; eso es de otro paquete).
- Los cuatro estan ignorados por `*~`: se anaden con `git add -f`.

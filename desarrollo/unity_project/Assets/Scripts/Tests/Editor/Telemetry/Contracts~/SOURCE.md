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

## Lote 1b (2026-11-16): mapa, comprobaciones previas y salud de la bateria

- Origen: repo web `E:\twinsight-web-twin`, rama `web/telemetria-control`, commit `a8664fa98ee9592a50e8a45d65dcd290782d194b` (el ultimo commit que toca estos tres goldens es `6a4f68bf53731541d09e2514cc1566bd7d3d4e92`).
- Copiados de `contracts/golden/`: `minimap.json`, `preflight.json` y `battery.json` (nuevos). Los demas goldens no cambian.
- `battery.json` `real_flight` usa el TWF completo (`x500v2_flight.twf`), que se busca como en `history.json`; si no esta, ese caso queda *Inconclusive*.
- `minimap.json` guarda los cuaterniones y los rumbos redondeados a 9 decimales: el test de `yaw` admite 1e-8 por ese redondeo de la entrada y el de `to_screen` toma los rumbos exactos de la lista del generador (`tools/contracts/golden_minimap.ts`).
- Ignorados por `*~`: se anaden con `git add -f`.

## Lote 1 herramienta 5 (2026-11-17): circuito como datos y records

- Origen: repo web `E:\twinsight-web-twin`, rama `web/telemetria-control` (ultimo commit que toca `contracts/courses/basic.json` y `contracts/golden/course.json`: `80330e3d2976a8fab625c3fbb830d4eb338a6dcd`).
- Copiado `contracts/courses/basic.json` a `courses/basic.json` (aqui) y a `Assets/StreamingAssets/Telemetry/courses/basic.json` (lo carga `CourseLayout.Default()`; si falta, usa la copia en C# `CourseLayout.Builtin()`).
- `golden/course.json` ya estaba copiado (identico al de la web); ahora lo usan `CourseGoldenTests` (trazado, volumen, aros, zonas, guiones, vueltas en polilinea) y `CourseRecordsTests` (puntuacion, records, almacenamiento y fantasma).
- Ignorados por `*~`: se anaden con `git add -f`.

## Contrato 1.1 (2026-11-17)
- Origen: repo web, rama `fable/contrato-1-1` (worktree `E:\twinsight-web-fable-c11`), commit `e086d776fcc5a6cea6bd6d7e454aa27c1a47acc6` («contrato 1.1: seq/ts opcionales, battery_off»; aun no fusionado en `web/telemetria-control`).
- Copiados: `telemetry.schema.json` (protocolVersion 1.1.0), `fixtures/**` (nuevos: `client.command.battery_off`, `client.command.stamped`, `client.manual.stamped`, `ack.sitl.stamped`, `ack.sitl.stale`, e invalidos `battery_off_with_motor`, `fractional_seq`, `inject_with_seq`, `negative_seq`, `hello.bad_version`; `hello.sitl` lleva `protocolVersion`).
- `labels.json` (aqui y en `Resources/TelemetryLabels.json`): solo cambia `protocolVersion` a 1.1.0; el resto de su texto NO se tomo de esa rama (otras ediciones de la web).
- No se copio `golden/stamp.json`: el emisor y el filtro de sellos (`src/shared/stamp.ts`) son del relay y del cliente web; el escritorio solo lee y escribe `seq`/`ts` y envia `battery_off` si se le pide.

# Tareas para GLM 5.3 (web chat) — Fix fasteners X500 v2

## Contexto (2026-08-24)

**Causa raíz encontrada y corregida en código**: `HolybroFastenerCatalogBuilder.cs` calculaba el cuadrante (FL/FR/BL/BR) con `root.forward`/`root.right` en espacio mundo. Con la rotación del root `Quaternion.Euler(-90, 90, 0)`, `root.forward` apunta exactamente a `Vector3.up`; tras `ProjectOnPlane(offset, Vector3.up)` el `frontDot` era siempre 0 → **todos los tornillos quedaban marcados "F"** (nunca B). Evidencia en `holybro_fastener_instances.json` antes del fix: 78 tornillos de brazo solo en FL/FR (45+33, cero BL/BR) y 17 de motor solo en FL/FR (10+7).

**Parche aplicado** (ya en disco, falta regenerar en Unity):
- `ResolveQuadrantSuffixFromWorld` ahora deriva los ejes front/right desde las posiciones reales de los anclajes `x500v2_arm_FL/FR/BL/BR` (`TryDeriveWorldAxisDirectionsFromArmAnchors`), inmune a la rotación del root. Fallback a espacio local vía `InverseTransformPoint` (X=right, Z=forward).
- `ResolveClosestNutParent` (lock_nut_m3) ahora usa los anclajes canónicos por distancia en vez de `root.Find` por nombre.
- **Fix motores (SetupImportedDroneThermalTest.cs)**: nuevo pase `RedistributeQuadrantInstancesByNearestArm` tras `ReparentAuxiliaryChildren`. Los 4 meshes `DJ-2216-KV880*` no traen cuadrante en el nombre y terminaban agrupados bajo un único anclaje (por eso aislar un motor mostraba los 4). Ahora cada mesh se reparentea al anclaje `x500v2_motor_<FL/FR/BL/BR>` según el brazo más cercano, y se elimina el cubo `_runtime_proxy` del anclaje si existía. El companion system (`BuildCanonicalPartScopeIds` → `TryGetArmAssemblyCompanionIds`) ya incluye motor y hélice al aislar un brazo, así que con los anclajes correctos cada motor queda integrado a su brazo automáticamente.

**Hipótesis off-by-one del usuario: DESCARTADA como causa actual.** El resolver actual no lee números de sufijo (`_001`) en absoluto — solo familia + posición 3D. La vieja tabla manual por índices (`ResolveManualParentCanonicalPartId`) fue eliminada. Ese off-by-one sí pudo causar los errores menores originales, pero ya no existe código que dependa de él. **No hace falta renombrar piezas en Blender.**

---

## TAREA 1 (crítica) — Regenerar catálogo y verificar distribución

Pegar en GLM 5.3 max web chat:

```
Contexto: proyecto Unity en E:\WebGL_tesis\desarrollo\unity_project. Se corrigió HolybroFastenerCatalogBuilder.cs para que los cuadrantes FL/FR/BL/BR se deriven de los anclajes de los 4 brazos (antes todo salía "F" por un bug de espacio-mundo con la rotación Euler(-90,90,0) del root).

Instrucciones:
1. Abre el proyecto en Unity Editor y espera recompilación (no debe haber errores en Assets/Editor/HolybroFastenerCatalogBuilder.cs).
2. Ejecuta el menú: Tools > Cleanup > 🧹 FULL CLEAN & REBUILD DRONE (Zero Duplicates).
3. Al terminar, abre Assets/Resources/holybro_fastener_instances.json y verifica la distribución de "parentCanonicalPartId". DEBE contener entradas x500v2_arm_BL, x500v2_arm_BR, x500v2_motor_BL, x500v2_motor_BR con conteos aproximadamente simétricos entre cuadrantes (cada brazo del X500 v2 lleva la misma tornillería; los cap_screw_m3x6 de motor deberían repartirse 2 por cuadrante).
4. Si sigue habiendo 0 tornillos BL/BR: los anclajes ExplodablePart de los brazos no existen con esos IDs canónicos; revisa con qué Data.id quedaron los 4 brazos en la jerarquía (x500v2_Drone) y repórtalo.
5. En Play Mode, prueba Isolate en: ARM TUBE FRONT LEFT, un brazo trasero, y un motor trasero. Verifica que no aparezcan tornillos flotantes de otros cuadrantes ni falten tornillos locales.
6. Verificación de motores: en la consola del rebuild debe aparecer "Motores reasignados por cuadrante: N" (si N=0 y no hay warning, los motores ya estaban bien; si hay warning de anchors faltantes, reportarlo). Luego en Play Mode: al seleccionar UN motor debe aislarse SOLO ese motor con sus 2-4 tornillos M3x6 (los otros 3 motores deben ocultarse), y al aislar un BRAZO debe verse el brazo + SU motor + SU hélice + su tornillería (companion system).
```

## TAREA 1b — Si los ESC tienen el mismo síntoma que tenían los motores

Los ESC (`x500v2_esc_FL/FR/BL/BR`) son también piezas por cuadrante con nombre FBX compartido. Si al seleccionar un ESC se aíslan los 4:

```
En SetupImportedDroneThermalTest.cs, justo debajo de la llamada RedistributeQuadrantInstancesByNearestArm(..., "x500v2_motor_", "dj-2216") agrega una llamada idéntica con ("x500v2_esc_", "<token-del-nombre-FBX-del-ESC>"). Para hallar el token: busca en E:\WebGL_tesis\desarrollo\docs\investigacion\Holybro\x500v2_blender_synced_parts.json la entrada del ESC (parte de electrónica de velocidad, probablemente "DianTiao" o similar) y usa su blenderName normalizado en minúsculas con guiones. Luego rebuild y verifica aislando un ESC.
```

## TAREA 2 (si algo sigue mal) — Ajustar umbrales de familias ambiguas

```
Contexto: tras el fix de cuadrantes, las familias cap_screw_m25x6 y cap_screw_m25x12 usan umbrales de distancia radial al centro (0.085 m y 0.10 m) para decidir entre bottom_plate/rails_battery y arm_<QUAD>. Si al aislar la bottom plate o los rieles aparecen tornillos de brazo (o viceversa), esos umbrales están mal calibrados.

Instrucciones:
1. En holybro_fastener_instances.json, extrae todas las instancias de esas dos familias con su localPosition.
2. Calcula la distancia radial (plano horizontal) de cada una al centro del dron.
3. Deben formar dos grupos claramente separados (cerca del centro vs zona de brazos). Ajusta los umbrales en ResolveSpatialParentCanonicalPartId al punto medio entre grupos.
```

## TAREA 3 (opcional) — Ground truth desde Blender como verificación cruzada

Blender 4.3 está en `C:\Program Files\Blender Foundation\Blender 4.3\blender.exe`. No hay MCP de Blender conectado a la sesión de ZCode, pero se puede correr headless:

```
Escríbeme un script Python para Blender 4.3 que:
1. Recorra todos los objetos del blend (el dron final con tornillos instanciados).
2. Clasifique como fastener si el nombre contiene (case-insensitive): GB70, ZSLM, NILONGZHU, LM_M3, CHEN, PAN, FALAN.
3. Para cada fastener calcule su posición mundial y encuentre la malla "pieza madre" más cercana (excluyendo otros fasteners) por distancia de bounding box.
4. Exporte E:/WebGL_tesis/blender_files/fastener_ground_truth.json con: nombre fastener, posición, nearest_parent_name, distancia.
Luego dame el comando para ejecutarlo headless:
"C:\Program Files\Blender Foundation\Blender 4.3\blender.exe" --background "<blend_final>" --python script.py
```

Después comparar ese JSON contra `holybro_fastener_instances.json` regenerado (Tarea 1): cada `nearest_parent_name` mapeado a ID canónico debe coincidir con `parentCanonicalPartId` en ≥95% de los casos.

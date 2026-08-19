# Órdenes corregidas al agente — Build v6 con control estructural

## Importante: el reporte anterior no es verificable todavía

El agente afirmó haber creado v6, pero su script dice explícitamente que abrió **`informe_final_definitivo_v5.tex`** y desde ahí generó v6. Antes de aceptar el resultado, debe entregar:

1. El contenido completo del main final que realmente compiló.
2. El PDF final generado.
3. La salida literal de `grep`/PowerShell para demostrar los valores.
4. La salida literal del conteo de entornos `figure` y `table` por capítulo.

No se aceptan afirmaciones del tipo "100%" sin esos cuatro artefactos.

## Corrección técnica a las órdenes v6 anteriores

No uses `\apptocmd{\subsection}{\FloatBarrier}{}{}` ni `\usepackage[subsection]{placeins}` como único mecanismo. Eso solo pone una barrera al encontrar una subsección; si una subsección contiene muchas figuras, la cola sigue creciendo antes de llegar a la siguiente barrera. Además, **no uses `\FloatBarrier` después de cada figura**, porque puede producir páginas casi vacías.

La estrategia correcta para este documento es un control explícito por **bloques narrativos de figuras**, no una barrera automática global.

## Tarea 1 — Verificar el main real

Ejecuta antes de modificar:

```powershell
Select-String -Path informe_final_definitivo.tex -Pattern 'usepackage\{flafter\}|usepackage\[subsection\]\{placeins\}|apptocmd.*FloatBarrier|setcounter\{topnumber\}|setcounter\{bottomnumber\}|setcounter\{totalnumber\}|textfloatsep|intextsep|floatsep|vspace\{-'
```

El main final debe tener:

```latex
\usepackage{flafter}
\usepackage{placeins}
\setcounter{topnumber}{4}
\setcounter{bottomnumber}{2}
\setcounter{totalnumber}{6}
\setlength{\textfloatsep}{0pt}
\setlength{\intextsep}{0pt}
\setlength{\floatsep}{0pt}
```

`\apanote` no debe contener ningún `\vspace`, positivo ni negativo, excepto ninguno: debe comenzar con `\par` y terminar con `\endgroup`.

## Tarea 2 — No forzar `[tp]` para todos indiscriminadamente

El usuario pidió que las figuras y tablas estén en la misma página donde se mencionan por primera vez o en la siguiente, no que todas se acumulen en páginas de flotantes. Mantén `[htbp]` en figuras/tablas normales. `flafter` evita que aparezcan antes de su mención. Usa `[tp]` únicamente para figuras grandes que claramente no caben junto al texto.

Verifica que no exista `[H]`.

## Tarea 3 — Bajar la densidad de flotantes con barreras colocadas manualmente

En los capítulos 01–08, identifica bloques donde se acumulan varios flotantes seguidos. Inserta `\FloatBarrier` únicamente:

- antes de un nuevo título de Nivel 1;
- antes de una subsección que comienza después de un bloque de varias figuras/tablas;
- después de un bloque de 2–4 figuras/tablas consecutivas, si todavía hay flotantes pendientes.

No insertes barrera antes/después de cada flotante.

En el capítulo 4 (51 figuras + 11 tablas), revisa especialmente los bloques de figuras del onboarding, UI, arquitectura y shaders. La regla práctica es no permitir más de 3–4 flotantes pendientes sin una barrera.

## Tarea 4 — Recortar márgenes internos de imágenes

La separación imagen → nota sigue siendo distinta cuando la imagen contiene blanco interno. La diferencia de Figura 3/7 frente a Tabla 1 no se corrige con `\apanote` ni con separadores: es el `MediaBox`/lienzo de los PDF o los márgenes transparentes de los PNG.

Para cada asset usado por las figuras con hueco grande:

1. Haz copia de seguridad.
2. Para PDF vectorial, usa `pdfcrop --margins '0 0 0 0' input.pdf output-crop.pdf` o el recorte equivalente que elimine el blanco real. No uses `\vspace` negativo.
3. Para PNG, recorta el lienzo transparente/blanco externo sin cambiar la escala visual deseada.
4. Sustituye únicamente la ruta del asset en el capítulo afectado.

Reporta el nombre exacto de cada asset recortado y sus dimensiones antes/después.

## Tarea 5 — Verificación real del PDF

Compila 3 veces y ejecuta:

```powershell
Select-String -Path informe_final_definitivo.log -Pattern 'too many unprocessed floats|Float too large|Overfull \\hbox|Warning: Reference|undefined'
```

Después cuenta entornos en los fuentes:

```python
import glob, re
for f in sorted(glob.glob('chapters/*.tex')):
    s=open(f,encoding='utf8').read()
    print(f, len(re.findall(r'\\begin\{figure\}',s)), len(re.findall(r'\\begin\{table\}',s)))
```

El agente debe reportar esos números, no sustituirlos por una cifra inventada.

## Tarea 6 — Verificación visual dirigida

Renderiza y revisa al menos:

- Figura 1, Figura 2, Figura 3, Figura 4 y Figura 7.
- Tabla 1.
- Tres figuras del capítulo 4 y tres del capítulo 5.
- Figura H1 y una tabla de cada apéndice que contenga tablas.

Para cada caso registra:

| Caso | página | mención | rótulo | imagen/tabla→nota | nota→texto | solapamiento |
|---|---:|---|---:|---:|---:|---|

Usa unidades consistentes. No mezcles `pitch`, `blanco` y distancia entre cajas como si fueran la misma medida. La medición automática por bandas de texto no puede distinguir siempre imagen, caption, nota y párrafo; debe complementarse con inspección de coordenadas PDF y revisión visual.

## Criterio de aceptación

- 0 solapamientos reales.
- 0 advertencias `too many unprocessed floats`/`Float too large`.
- 0 referencias indefinidas.
- Ninguna página en blanco espuria.
- Ninguna figura aparece antes de su primera mención.
- Las imágenes con blanco interno se recortan como assets, no con hacks de espaciado.
- El PDF final y el main final deben tener exactamente el mismo nombre base y fecha de compilación.

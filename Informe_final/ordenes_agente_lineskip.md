# Órdenes al agente — Espaciado flotante-texto: el colapso de `\lineskip`

## 1. El análisis cuantitativo exacto (configuración actual)

| Parámetro | Valor | Origen |
|---|---|---|
| `\baselineskip` base de la clase a 12pt | 14.5pt | article 12pt |
| Pitch efectivo con `\setstretch{2}` | **29pt** (14.5 × 2) | `\baselinestretch` se aplica al seleccionar fuente |
| Blanco interlineal normal entre líneas de texto | **≈18.5pt** (29 − 8.5 altura − 2 profundidad) | geometría Times 12pt |
| `\lineskip` por defecto | **1pt** | LaTeX kernel |
| `\lineskiplimit` | 0pt | LaTeX kernel |
| `\textfloatsep` / `\intextsep` / `\floatsep` actuales | 0pt | ya aplicado |
| Hueco ANTES del flotante (texto → rótulo "Figura N") | **≈1pt** | colapso a `\lineskip` (ver abajo) |
| Hueco DESPUÉS de la nota (nota → texto) | **18.5pt** | pegamento interlineal normal (no colapsa) |

## 2. La causa raíz (mecanismo exacto)

Cuando TeX apila una caja alta (el flotante completo: rótulo + imagen + nota, ~300pt de alto) tras una línea de texto, el pegamento interlineal se calcula como `\baselineskip − prevdepth − altura_de_la_caja`. Como la caja del flotante es altísima, ese cálculo da un número muy negativo, y al ser menor que `\lineskiplimit` (0pt), TeX **descarta el pegamento interlineal e inserta `\lineskip` (1pt) en su lugar**. Por eso el rótulo queda pegado al texto anterior ("casi overlap"): el hueco es `\intextsep` (0) + `\lineskip` (1pt) = 1pt.

Después de la nota NO hay colapso: la caja siguiente es una línea de texto normal (altura 8.5pt) y la profundidad de la caja del flotante es pequeña (~2pt), así que el pegamento interlineal queda en 29 − 2 − 8.5 = **18.5pt**, exactamente el blanco interlineal normal. Por eso la asimetría: antes ≈ 1pt, después ≈ 18.5pt.

**Por qué NO se arregla con los separadores:** `\intextsep`/`\textfloatsep` son simétricos (afectan antes y después por igual). Subirlos arregla el "antes" y rompe el "después". La palanca correcta es `\lineskip`, porque solo dispara donde ocurre el colapso (cajas altas = flotantes), nunca en texto normal.

## 3. El fix (3 líneas, en el main que realmente compilas)

En el preámbulo, justo DESPUÉS del bloque de separadores de flotantes, agrega:

```latex
% Hueco antes de un flotante = blanco interlineal normal (18.5pt).
% \lineskip solo se usa cuando el pegamento interlineal colapsa (cajas
% altas, es decir, los flotantes); el texto normal nunca colapsa.
% Valor absoluto en pt: NUNCA múltiplos de \baselineskip en el preambulo
% (ahi ese registro vale 14.5pt, no 29pt).
\setlength{\lineskip}{18.5pt}
% Proteccion: las filas de tablas con celdas multilinea tambien colapsan
% a \lineskip y deben seguir compactas (1pt, como hasta ahora):
\AtBeginEnvironment{tabular}{\setlength{\lineskip}{1pt}}
\AtBeginEnvironment{longtable}{\setlength{\lineskip}{1pt}}
```

No edites ningún capítulo. No toques `\intextsep` ni `\textfloatsep` (quedan en 0pt).

**Resultado esperado:** hueco antes del rótulo = 18.5pt (idéntico al blanco entre líneas de texto); hueco después de la nota = 18.5pt (ya lo era). Uniforme. Los flotantes al tope de página no se ven afectados (al inicio de una página no hay caja previa, así que `\lineskip` no se inserta: el rótulo sigue pegado al margen superior, como debe ser).

## 4. Verificación con medición objetiva (no "se ve bien")

Guarda este script como `measure_gaps.py` y úsalo sobre las páginas renderizadas:

```python
# Uso: pdftoppm -png -r 150 -f 35 -l 35 informe_final_definitivo.pdf pagina
#      python measure_gaps.py pagina-035.png
import sys
from PIL import Image
import numpy as np

PT = 72.0 / 150.0  # puntos por pixel a 150 DPI
a = np.array(Image.open(sys.argv[1]).convert('L'))
rows = (a < 120).sum(axis=1) > 2
bands, in_b, s = [], False, 0
for r, v in enumerate(rows):
    if v and not in_b: s, in_b = r, True
    elif not v and in_b: bands.append([s, r - 1]); in_b = False
if in_b: bands.append([s, len(rows) - 1])
merged = []
for b in bands:
    if merged and b[0] - merged[-1][1] <= 5: merged[-1][1] = b[1]
    else: merged.append(b)
prev = None
for i, (t, b) in enumerate(merged):
    if prev:
        print(f'banda {i:2d}: top={t:4d}  pitch={((t - prev[0]) * PT):5.1f}pt  blanco={((t - prev[1]) * PT):5.1f}pt')
    prev = (t, b)
print('Referencia: pitch texto 2.0 = 29pt; blanco interlineal normal = 18.5pt')
```

Protocolo:
1. Compila 3 veces con `pdflatex -interaction=nonstopmode informe_final_definitivo.tex`.
2. Renderiza una página con flotante a mitad de texto (ej. la página de la Figura 2 o la Tabla 1) a 150 DPI con `pdftoppm`.
3. Ejecuta el script. Identifica la banda del rótulo ("Figura 2"/"Tabla 1") y verifica:
   - [ ] El **blanco** entre la línea de texto previa y el rótulo ≈ 18.5pt (±1pt).
   - [ ] El **blanco** entre la última línea de la nota y el texto siguiente ≈ 18.5pt (±1pt).
   - [ ] El **pitch** entre líneas de texto normales ≈ 29pt.
   - [ ] Las filas de las tablas NO muestran huecos nuevos (el reset de `\lineskip` en tabular/longtable las protege).
4. Si el hueco posterior a la nota mide sistemáticamente corto por un delta Δ, repórtalo con el número medido (no lo "ajustes a ojo"): se compensa con un valor fijo en pt, nunca con múltiplos de `\baselineskip`.
5. Búsqueda de `??` en el PDF: 0. `Overfull` en el log: reporta.

**Reporta al usuario:** la salida numérica del script (antes y después del rótulo, y después de la nota) y la captura de una página con flotante a mitad de texto.

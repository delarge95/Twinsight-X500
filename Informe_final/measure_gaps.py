# Uso: pdftoppm -png -r 150 -f 20 -l 20 informe_final_definitivo.pdf pagina
#      python measure_gaps.py pagina-020.png
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

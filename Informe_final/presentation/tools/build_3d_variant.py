"""Genera index_final_3d.html: el mismo deck con la Fase B (3D sincronizado en las slides 9 y 12).

El deck principal (index_final.html) no cambia. Uso, desde Informe_final/presentation:
    python tools/build_3d_variant.py
Volver a ejecutarlo cada vez que cambie index_final.html.
"""
import os

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
src = open(os.path.join(ROOT, 'index_final.html'), encoding='utf8', newline='').read()
anchor = '<script src="assets/model/preshow_glb_datauri.js"></script>'
assert src.count(anchor) == 1, 'no se encontró el punto de inserción'
inject = anchor + '\r\n<script src="assets/model/screw_parts_datauri.js"></script>\r\n<script src="assets/js/phaseb.js"></script>'
out = src.replace(anchor, inject)
out = out.replace('<title>TwinSight X500 — Defensa de Tesis</title>', '<title>TwinSight X500 — Defensa de Tesis (3D)</title>')
open(os.path.join(ROOT, 'index_final_3d.html'), 'w', encoding='utf8', newline='').write(out)
print('index_final_3d.html generado')

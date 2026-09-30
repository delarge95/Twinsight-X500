"""Empaqueta los íconos reales de la app (docs/Build/UI_SVG_Exports, exportados por
SvgPainterExporter desde ProceduralIcons) en assets/js/app_icons.js para la variante 3D.
El ícono de Analyze se exportó vacío (sus barras son animadas); se reconstruye con la
misma geometría de ProceduralAnalyzeIcon.cs (barras en reposo 0,3 · 0,6 · 0,4).

Uso, desde Informe_final/presentation:  python tools/build_app_icons.py
"""
import json, os, re
HERE = os.path.dirname(os.path.abspath(__file__)); ROOT = os.path.dirname(HERE)
SRC = os.path.join(ROOT, '..', '..', 'docs', 'Build', 'UI_SVG_Exports')
NAMES = {'inspect': 'Inspect', 'studio': 'Studio', 'home': 'Home', 'reset': 'Reset', 'close': 'Close', 'pins': 'Pins',
         'isolate': 'Isolate', 'power': 'Power', 'cut': 'Cut', 'explode': 'Explode', 'filter': 'Filter', 'shaders': 'Shaders'}
out = {}
for key, n in NAMES.items():
    s = open(os.path.join(SRC, f'Procedural{n}Icon_01_Rest.svg'), encoding='utf-8-sig').read()
    s = re.sub(r'#(CCCCCC|FFFFFF|cccccc|ffffff)', 'currentColor', s)
    s = s.replace('stroke-width="1.5"', 'stroke-width="1.6" vector-effect="non-scaling-stroke"')
    s = re.sub(r'<svg width="256" height="256"', '<svg', s)
    out[key] = ' '.join(s.split())
# Analyze: línea base + 3 barras (ProceduralAnalyzeIcon.DrawIconPath a 256 px)
bot, left, right, bw, hmax = 220.16, 35.84, 220.16, 36.864, 184.32
bars = ''.join(f'<rect x="{left + i * 2 * bw:.2f}" y="{bot - h * hmax:.2f}" width="{bw:.2f}" height="{h * hmax:.2f}" fill="currentColor"/>' for i, h in enumerate((0.3, 0.6, 0.4)))
out['analyze'] = f'<svg viewBox="0 0 256 256" xmlns="http://www.w3.org/2000/svg"><path d="M {left} {bot} L {right} {bot}" stroke="currentColor" stroke-width="1.6" vector-effect="non-scaling-stroke" fill="none" stroke-linecap="round"/>{bars}</svg>'
with open(os.path.join(ROOT, 'assets', 'js', 'app_icons.js'), 'w', encoding='utf8') as f:
    f.write('/* Íconos de la app (UI_SVG_Exports). Generado por tools/build_app_icons.py. */\nwindow.__APP_ICONS = ' + json.dumps(out, ensure_ascii=False) + ';\n')
print(len(out), 'íconos')

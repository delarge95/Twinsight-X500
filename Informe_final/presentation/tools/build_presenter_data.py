"""Genera presenter_data.js a partir de PRESENTATION_SCRIPT.md y SPEAKER_CARDS.md.

La vista del presentador (presenter.html) se abre con doble clic (file://), donde el
navegador no permite leer archivos .md con fetch. Por eso el guion se embebe en un .js.

Uso (desde Informe_final/presentation):
    python tools/build_presenter_data.py
Volver a ejecutarlo cada vez que cambie el guion o las tarjetas.
"""
import html
import json
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)


def md_inline(s):
    s = html.escape(s, quote=False)
    s = re.sub(r'\*\*(.+?)\*\*', r'<b>\1</b>', s)
    s = re.sub(r'(?<![\w*])\*(?!\s)(.+?)(?<!\s)\*(?![\w*])', r'<i>\1</i>', s)
    s = re.sub(r'`([^`]+)`', r'<code>\1</code>', s)
    return s


def cues(s):
    """[pausa], [mirar jurado], [señalar …] → marcas de dirección escénica."""
    return re.sub(r'\[(?!click )([^\]]+)\]', r'<span class="cue">\1</span>', s)


def to_sec(m):
    a, b = m.split(':')
    return int(a) * 60 + int(b)


def parse_script(path):
    src = open(path, encoding='utf8').read()
    body = src[src.index('## 6.'):src.index('## 7.')]
    parts = re.split(r'\n### SLIDE (\d+) — ([^\n]+)\n', body)[1:]
    slides = []
    for i in range(0, len(parts), 3):
        n, title, text = int(parts[i]), parts[i + 1].strip(), parts[i + 2]
        text = text.split('\n---')[0]
        tm = re.search(r'\*\*Tiempo:\*\* (\d+:\d+) – (\d+:\d+) · \*\*Pasos:\*\* ([^\n]+)', text)
        steps = tm.group(3).strip()
        steps = int(steps) if steps.isdigit() else 0
        screen = re.search(r'\*\*En pantalla:\*\* ([^\n]+)', text)
        buckets = {'oral': [], 'transition': [], 'contingency': []}
        procedure, avoid, mode = [], '', 'oral'
        for line in text.split('\n'):
            ls = line.strip()
            if ls.startswith('**Transición:**'):
                mode = 'transition'; continue
            if ls.startswith('**Contingencia'):
                mode = 'contingency'; continue
            if ls.startswith('**Procedimiento:**'):
                mode = 'procedure'; continue
            if ls.startswith('**Guion oral:**'):
                mode = 'oral'; continue
            if '**No decir:**' in ls:
                avoid = ls.split('**No decir:**', 1)[1].strip()
                pre = ls.split('**No decir:**', 1)[0].strip()
                if pre:
                    avoid = pre + ' ' + avoid
                continue
            if mode == 'procedure' and re.match(r'\d+\. ', ls):
                procedure.append(md_inline(re.sub(r'^\d+\. ', '', ls)))
                continue
            if ls.startswith('>') and mode in buckets:
                buckets[mode].append(ls[1:].strip())
        oral = '\n'.join(buckets['oral'])
        paras = [p.strip() for p in re.split(r'\n\s*\n|\n(?=\S)', oral) if p.strip()]
        oral = '\n\n'.join(paras)
        # segmentos por clic: 0 = antes del primer clic
        pieces = re.split(r'\[click (\d+)(?: — ([^\]]*))?\]', oral)
        segs = [{'k': 0, 'label': '', 'html': cues(md_inline(pieces[0])).strip()}]
        for j in range(1, len(pieces), 3):
            segs.append({'k': int(pieces[j]), 'label': (pieces[j + 1] or '').strip(),
                         'html': cues(md_inline(pieces[j + 2])).strip()})
        segs = [s for s in segs if s['html'] or s['k']]
        slides.append({
            'n': n, 'title': title, 'start': to_sec(tm.group(1)), 'end': to_sec(tm.group(2)), 'steps': steps,
            'screen': md_inline(screen.group(1)) if screen else '',
            'segments': segs,
            'transition': cues(md_inline(' '.join(buckets['transition']))),
            'contingency': md_inline(' '.join(buckets['contingency'])),
            'procedure': procedure,
            'avoid': md_inline(avoid),
        })
    return slides


def parse_cards(path):
    src = open(path, encoding='utf8').read()
    key, backups = {}, []
    for m in re.finditer(r'^\| \*\*(\d+)\*\* \| ([^|]*)\| ([^|]*)\| ([^|]*)\| ([^|]*)\| ([^|]*)\|$', src, re.M):
        key[int(m.group(1))] = {'msg': md_inline(m.group(4).strip().strip('"')), 'screen': md_inline(m.group(5).strip()),
                                'avoid': md_inline(m.group(6).strip())}
    sec = src.split('## Backups útiles', 1)
    if len(sec) > 1:
        for m in re.finditer(r'^\| ([^|]+) \| (B[\d]+[^|]*) \|$', sec[1].split('\n---')[0], re.M):
            backups.append({'topic': md_inline(m.group(1).strip()), 'go': m.group(2).strip()})
    return key, backups


def main():
    slides = parse_script(os.path.join(ROOT, 'PRESENTATION_SCRIPT.md'))
    key, backups = parse_cards(os.path.join(ROOT, 'SPEAKER_CARDS.md'))
    for s in slides:
        s['key'] = key.get(s['n'], {}).get('msg', '')
    data = {'slides': slides, 'backups': backups, 'preshow': key.get(0, {}),
            'total': slides[-1]['end'] if slides else 0}
    out = os.path.join(ROOT, 'presenter_data.js')
    with open(out, 'w', encoding='utf8') as f:
        f.write('/* Generado por tools/build_presenter_data.py a partir de PRESENTATION_SCRIPT.md y SPEAKER_CARDS.md. No editar a mano. */\n')
        f.write('window.__TW_SCRIPT = ' + json.dumps(data, ensure_ascii=False, indent=1) + ';\n')
    print(f'{len(slides)} slides, {sum(len(s["segments"]) for s in slides)} segmentos, total {data["total"]} s, {len(backups)} backups -> {out}')


if __name__ == '__main__':
    main()

import fitz, sys, re, os

# Configuración de codificación para consola de Windows
sys.stdout.reconfigure(encoding='utf-8')

PDF_PATH = 'informe_final_definitivo.pdf'

if not os.path.exists(PDF_PATH):
    print(f"Error: No se encontró el archivo {PDF_PATH}")
    sys.exit(1)

doc = fitz.open(PDF_PATH)

print("=" * 80)
print("  AUDITORÍA TIPOGRÁFICA AUTOMATIZADA - NORMAS APA 7 (UNAD)")
print(f"  Documento: {PDF_PATH} ({len(doc)} páginas)")
print("=" * 80)
print("  Criterio: Interlineado doble uniforme 2.0 (Pitch nominal = 28.89 pt)")
print("  Tolerancia válida: 25.0 pt <= Salto <= 32.0 pt")
print("=" * 80 + "\n")

TARGET_PITCH = 28.89
MIN_VALID_PITCH = 25.0
MAX_VALID_PITCH = 32.5

def extract_visual_lines(page):
    """
    Agrupa las palabras de la página en líneas visuales basadas en su línea base (y1),
    descartando encabezados de página (número de página) y pies de página.
    """
    words = page.get_text('words')
    if not words:
        return []
    
    # Descartar número de página superior (y1 < 55) y margen inferior excesivo (y1 > 745)
    body_words = [w for w in words if w[3] >= 55 and w[1] <= 745]
    if not body_words:
        return []
    
    # Agrupar palabras cuya línea base (y1) difiera en menos de 3.5 pt
    sorted_words = sorted(body_words, key=lambda w: (w[1], w[0]))
    lines = []
    
    for w in sorted_words:
        matched = False
        for l in lines:
            if abs(w[3] - l['y1']) < 3.5:
                l['words'].append(w)
                l['y1'] = max(l['y1'], w[3])
                l['y0'] = min(l['y0'], w[1])
                l['x0'] = min(l['x0'], w[0])
                l['x1'] = max(l['x1'], w[2])
                matched = True
                break
        if not matched:
            lines.append({
                'y0': w[1], 'y1': w[3], 'x0': w[0], 'x1': w[2],
                'words': [w]
            })
            
    # Ordenar líneas estrictamente de arriba a abajo por su línea base
    lines.sort(key=lambda l: l['y1'])
    for l in lines:
        l['words'].sort(key=lambda w: w[0])
        l['text'] = ' '.join(w[4] for w in l['words']).strip()
        
    return lines

# Categorías de auditoría
floats_after_extended = []
floats_after_short = []
floats_before_extended = []
floats_before_short = []
vease_gaps_extended = []
headings_before_anomalies = []
stacked_floats = []

total_floats_audited = 0
total_vease_audited = 0

# Analizar cuerpo del documento (Página 15 en adelante para excluir preliminares e índices)
START_PAGE = 15

for pidx in range(START_PAGE - 1, len(doc)):
    pno = pidx + 1
    page = doc[pidx]
    lines = extract_visual_lines(page)
    
    if not lines:
        continue
        
    # --- 1. AUDITORÍA DE FLOTANTES (FIGURAS Y TABLAS) ---
    for i, l in enumerate(lines):
        m = re.match(r'^(Figura|Tabla)\s+(\d+)\b', l['text'])
        # Validar que es un título de flotante (no una mención dentro de párrafo largo)
        if m and (l['x0'] < 100 or len(l['text']) < 65 or l['text'].startswith('Figura') or l['text'].startswith('Tabla')):
            total_floats_audited += 1
            float_name = m.group(0)
            is_top = (l['y1'] < 120.0)
            
            # A. Espacio antes de flotante intercalado
            if not is_top and i > 0:
                prev_line = lines[i-1]
                gap_before = l['y1'] - prev_line['y1']
                prev_txt = prev_line['text'][:45]
                if gap_before > MAX_VALID_PITCH:
                    floats_before_extended.append((pno, float_name, gap_before, prev_txt))
                elif gap_before < MIN_VALID_PITCH:
                    floats_before_short.append((pno, float_name, gap_before, prev_txt))
                    
            # B. Localizar bloque de Nota. y medir espacio posterior
            note_start_idx = -1
            for j in range(i+1, min(len(lines), i+25)):
                if 'Nota.' in lines[j]['text'] or lines[j]['text'].startswith('Nota'):
                    note_start_idx = j
                    break
                    
            if note_start_idx != -1:
                # Seguir todas las líneas continuas de la nota
                note_end_idx = note_start_idx
                for k in range(note_start_idx + 1, len(lines)):
                    line_gap = lines[k]['y1'] - lines[k-1]['y1']
                    # Una línea de nota está separada por <= 31 pt y no es un nuevo título/flotante
                    if line_gap <= 31.5 and not re.match(r'^(Figura|Tabla|\d+\.\d+|\s*Nota\.)', lines[k]['text']):
                        note_end_idx = k
                        if 'Elaboración propia' in lines[k]['text'] or 'propia.' in lines[k]['text']:
                            break
                    else:
                        break
                        
                last_note_line = lines[note_end_idx]
                if note_end_idx + 1 < len(lines):
                    next_line = lines[note_end_idx + 1]
                    gap_after = next_line['y1'] - last_note_line['y1']
                    nxt_txt = next_line['text'][:45]
                    last_txt = last_note_line['text'][-30:]
                    
                    # Detectar si el siguiente elemento es otro flotante apilado
                    if re.match(r'^(Figura|Tabla)\s+(\d+)\b', next_line['text']):
                        stacked_floats.append((pno, float_name, gap_after, next_line['text'][:35]))
                    else:
                        if gap_after > MAX_VALID_PITCH:
                            floats_after_extended.append((pno, float_name, is_top, gap_after, last_txt, nxt_txt))
                        elif gap_after < MIN_VALID_PITCH:
                            floats_after_short.append((pno, float_name, is_top, gap_after, last_txt, nxt_txt))

    # --- 2. AUDITORÍA DE PÁRRAFOS CON 'VÉASE LA FIGURA/TABLA' ---
    for i, l in enumerate(lines):
        if re.search(r'\(véase la (Figura|Tabla)\s+\d+\)\.|\(véanse las? (Figuras|Tablas)[^)]+\)\.', l['text']):
            total_vease_audited += 1
            if i + 1 < len(lines):
                next_l = lines[i+1]
                # Si la siguiente línea no es el inicio de una figura/tabla intercalada
                if not re.match(r'^(Figura|Tabla)\s+(\d+)\b', next_l['text']):
                    gap = next_l['y1'] - l['y1']
                    if gap > MAX_VALID_PITCH or gap < MIN_VALID_PITCH:
                        vease_gaps_extended.append((pno, gap, l['text'][-45:], next_l['text'][:45]))

print(f"Total flotantes analizados (Págs {START_PAGE}-{len(doc)}): {total_floats_audited}")
print(f"Total menciones 'véase' analizadas: {total_vease_audited}\n")

# --- REPORTE DE RESULTADOS ---

print("=" * 80)
print(f"1. FLOTANTES CON ESPACIO POSTERIOR EXTENSO (> {MAX_VALID_PITCH} pt)")
print("=" * 80)
if floats_after_extended:
    for pno, name, is_top, gap, txt_end, txt_next in floats_after_extended:
        pos = "TOPE" if is_top else "INTERCALADA"
        print(f"  Pág {pno:3d} | {name:10s} ({pos:11s}) | Salto: {gap:5.2f} pt | Fin nota: ...{txt_end!r} -> Sig: {txt_next!r}")
else:
    print("  [OK] Ninguno detectado.")

print("\n" + "=" * 80)
print(f"2. FLOTANTES CON ESPACIO POSTERIOR CORTO (< {MIN_VALID_PITCH} pt - Colapso a espacio sencillo)")
print("=" * 80)
if floats_after_short:
    for pno, name, is_top, gap, txt_end, txt_next in floats_after_short:
        pos = "TOPE" if is_top else "INTERCALADA"
        print(f"  Pág {pno:3d} | {name:10s} ({pos:11s}) | Salto: {gap:5.2f} pt | Fin nota: ...{txt_end!r} -> Sig: {txt_next!r}")
else:
    print("  [OK] Ninguno detectado.")

print("\n" + "=" * 80)
print(f"3. FLOTANTES INTERCALADOS CON ESPACIO PREVIO ANÓMALO (Fuera de {MIN_VALID_PITCH}-{MAX_VALID_PITCH} pt)")
print("=" * 80)
if floats_before_extended or floats_before_short:
    for pno, name, gap, prev_txt in floats_before_extended:
        print(f"  Pág {pno:3d} | {name:10s} (EXTENSO) | Salto: {gap:5.2f} pt | Texto prev: {prev_txt!r}")
    for pno, name, gap, prev_txt in floats_before_short:
        print(f"  Pág {pno:3d} | {name:10s} (CORTO)   | Salto: {gap:5.2f} pt | Texto prev: {prev_txt!r}")
else:
    print("  [OK] Ninguno detectado.")

print("\n" + "=" * 80)
print(f"4. PÁRRAFOS 'VÉASE...' CON SALTO POSTERIOR ANÓMALO (Fuera de {MIN_VALID_PITCH}-{MAX_VALID_PITCH} pt)")
print("=" * 80)
if vease_gaps_extended:
    for pno, gap, txt_prev, txt_next in vease_gaps_extended:
        print(f"  Pág {pno:3d} | Salto: {gap:5.2f} pt | Final: ...{txt_prev!r} -> Sig: {txt_next!r}")
else:
    print("  [OK] Ninguno detectado.")

print("\n" + "=" * 80)
print("5. FLOTANTES APILADOS CONSECUTIVOS (Dos figuras/tablas en la misma página)")
print("=" * 80)
if stacked_floats:
    for pno, name, gap, next_float in stacked_floats:
        print(f"  Pág {pno:3d} | {name:10s} -> {next_float:25s} | Separación: {gap:5.2f} pt")
else:
    print("  [OK] Ninguno detectado.")

print("\n" + "=" * 80)
total_anomalies = len(floats_after_extended) + len(floats_after_short) + len(floats_before_extended) + len(floats_before_short) + len(vease_gaps_extended)
print(f"RESUMEN FINAL: {total_anomalies} anomalías detectadas en total.")
print("=" * 80)

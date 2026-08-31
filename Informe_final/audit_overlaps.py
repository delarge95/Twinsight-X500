import fitz
import sys

sys.stdout.reconfigure(encoding='utf-8')

doc = fitz.open('informe_final_definitivo.pdf')

print('================================================================================')
print('  AUDITORÍA EXHAUSTIVA DE SUPERPOSICIÓN DE TEXTO EN TODO EL DOCUMENTO')
print(f'  Total páginas analizadas: {len(doc)}')
print('================================================================================\n')

total_overlaps = 0
total_near_collisions = 0
total_float_notes_checked = 0

for pno, page in enumerate(doc):
    blocks = page.get_text('dict')['blocks']
    lines = []
    for b in blocks:
        if 'lines' in b:
            for l in b['lines']:
                txt = ''.join(s['text'] for s in l['spans']).strip()
                if txt:
                    lines.append({
                        'bbox': l['bbox'],
                        'y0': l['bbox'][1],
                        'y1': l['bbox'][3],
                        'x0': l['bbox'][0],
                        'x1': l['bbox'][2],
                        'text': txt
                    })
    
    # Sort lines vertically by y0
    lines.sort(key=lambda item: (round(item['y0'], 1), item['x0']))
    
    # 1. Check all consecutive lines for vertical overlap
    for i in range(len(lines)):
        for j in range(i + 1, min(len(lines), i + 6)): # check nearby lines
            l1, l2 = lines[i], lines[j]
            
            # Check horizontal overlap (they share x span)
            x_overlap = min(l1['x1'], l2['x1']) - max(l1['x0'], l2['x0'])
            if x_overlap > 25.0: # overlapping in x by at least 25pt
                vert_diff = l2['y0'] - l1['y1']
                
                # If vert_diff < -0.5, l2 starts above l1's bottom line -> OVERLAP
                if vert_diff < -0.5:
                    total_overlaps += 1
                    print(f'🚨 SUPERPOSICIÓN DETECTADA en Pág {pno+1:3d}:')
                    print(f'   Línea 1 (y=[{l1["y0"]:.2f}, {l1["y1"]:.2f}]): "{l1["text"][:65]}"')
                    print(f'   Línea 2 (y=[{l2["y0"]:.2f}, {l2["y1"]:.2f}]): "{l2["text"][:65]}"')
                    print(f'   Solapamiento vertical: {-vert_diff:.2f} pt\n')
                elif vert_diff < 5.0 and any(k in l1['text'] for k in ['Nota.', 'Elaboración', 'propia.']):
                    total_near_collisions += 1
                    print(f'⚠️ DISTANCIA MUY REDUCIDA (<5pt) tras nota en Pág {pno+1:3d}:')
                    print(f'   Nota (y=[{l1["y0"]:.2f}, {l1["y1"]:.2f}]): "{l1["text"][:65]}"')
                    print(f'   Sig  (y=[{l2["y0"]:.2f}, {l2["y1"]:.2f}]): "{l2["text"][:65]}"')
                    print(f'   Separación: {vert_diff:.2f} pt\n')

print('================================================================================')
print(f'RESUMEN: {total_overlaps} superposiciones detectadas | {total_near_collisions} distancias críticas detectadas.')
print('================================================================================')

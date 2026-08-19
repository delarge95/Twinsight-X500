# Órdenes al agente — Flotantes a tope de página ([tp]): eliminar el caso a mitad de texto

## Por qué cambiamos de estrategia (léelo antes de tocar nada)

Cinco rondas de ajustar separadores y `\lineskip` demostraron que el hueco **antes** de un flotante a mitad de texto no responde de forma fiable a ningún parámetro: la evidencia muestra que ese hueco es ≈ `\intextsep` + ~1pt, mientras que el hueco **después** es `\intextsep` + 18.5pt. La asimetría es estructural (la caja alta del flotante colapsa el interlineado previo; la línea de texto posterior no colapsa), así que ningún valor único de separador iguala ambos lados.

**Solución: eliminar el caso.** Forzamos todos los flotantes a `[tp]` (tope de página o página de flotantes). Entonces nunca hay texto encima de un flotante en la misma página (no existe "hueco previo"), y el único contacto flotante-texto es "nota → texto debajo del flotante al tope", que queda en `\textfloatsep`(0) + interlineal normal = **18.5pt uniforme**. Esto además cumple literalmente la p. 22 del instructivo: la figura va en la misma página donde se menciona o en la siguiente, y "la página antes de la figura debe ser una página llena de texto".

## Tarea 1 — Reemplazar el main COMPLETO

Reemplaza **todo** `informe_final_definitivo.tex` con el `informe_final_definitivo_v5.tex` entregado. Sin merge manual. Diferencias vs. la versión actual:

- **Eliminado** el bloque `\setlength{\lineskip}{18.5pt}` y los dos `\AtBeginEnvironment{tabular/longtable}{\setlength{\lineskip}{1pt}}` (la palanca `\lineskip` no movió el hueco; se retira). `\lineskip` vuelve a su valor por defecto (1pt).
- Los separadores quedan en `0pt` (ya lo estaban).
- `\apanote` se conserva limpio (sin `\vspace`, con `\setstretch{2}`).

## Tarea 2 — Forzar `[tp]` en TODOS los flotantes de los capítulos

En `chapters/01_introduccion_toc_corto.tex` y `chapters/02_*.tex` … `chapters/08_*.tex`:

- Buscar: `[htbp]` → Reemplazar: `[tp]` (en entornos `figure` y `table`).
- Verifica con `grep -n "begin{figure}\[\|begin{table}\[" chapters/` que **todos** quedaron en `[tp]` y que no queda ningún `[H]`, `[!htbp]` ni `[htbp]`.

Efecto esperado: cada figura/tabla aparece al tope de una página (la de su mención o la siguiente), con el texto llenando la página anterior. La paginación cambiará — es esperado y conforme.

## Tarea 3 — Compilar y verificar

1. Compila 3 veces con `pdflatex -interaction=nonstopmode informe_final_definitivo.tex`.
2. Verificación (visual, sobre el PDF — no el script de medición, que resultó poco fiable):
   - [ ] Ningún flotante queda a mitad de texto (texto encima y debajo en la misma página). Todos al tope de página o en página de flotantes.
   - [ ] El hueco entre la última línea de la nota y el texto debajo del flotante es uniforme en todo el documento (un doble espacio normal).
   - [ ] Las páginas de texto siguen llenas (sin medias páginas en blanco antes de una figura).
   - [ ] Las figuras siguen apareciendo en la página de su mención o la inmediatamente siguiente.
3. `??` = 0 en el PDF. Reporta cualquier `Overfull` o flotante que haya quedado muy lejos de su referencia.

## Tarea 4 (separada, contenido) — Hueco grande entre imagen y nota

Si tras lo anterior persiste un hueco grande entre la **imagen** y la nota en algunas figuras (Figuras 3, 7…), la causa NO es el código: es **espacio en blanco horneado dentro de los propios archivos de imagen** (cada PDF/PNG de diagrama trae un margen inferior distinto, por eso es selectivo). Para cerrarlo, recorta los archivos:

- Para los PDF vectoriales de `figures/`: `pdfcrop --margins 2 archivo.pdf` (con respaldo previo), y recompila.
- Para los PNG: recórtalos con un editor de imagen o una herramienta batch.

No compense con `\vspace` negativo: eso fue lo que causó los solapamientos.

## Reporta al usuario

- Confirmación de que todos los flotantes quedaron en `[tp]` (salida del grep).
- Capturas de 2-3 páginas con flotantes al tope mostrando el hueco uniforme tras la nota.
- Confirmación de que no quedan flotantes a mitad de texto.
- La decisión del usuario queda para la Tarea 4 (recortar imágenes o no).

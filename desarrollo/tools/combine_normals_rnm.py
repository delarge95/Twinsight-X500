"""
Combina dos normal maps usando Reoriented Normal Mapping (RNM).

Uso:
    python combine_normals_rnm.py <normal_base.png> <normal_detail.png> <output.png>

Ejemplo:
    python combine_normals_rnm.py HP_Normal.png Base_Normal.png Drone_Normal_Combined.png

Requisitos:
    pip install Pillow numpy

Notas:
    - normal_base  = tu normal map principal (HP bake, geometría)
    - normal_detail = tu normal map de detalle (materiales procedurales, fibra de carbono)
    - El script preserva la resolución de la imagen base
    - Soporta PNG 8-bit y 16-bit
"""

import sys
import numpy as np
from PIL import Image


def load_normal_map(path):
    """Carga un normal map y lo convierte a float32 en rango [-1, 1]."""
    img = Image.open(path)

    # Asegurar que sea RGB (no RGBA)
    if img.mode == 'RGBA':
        img = img.convert('RGB')
    elif img.mode != 'RGB':
        img = img.convert('RGB')

    # Detectar si es 16-bit
    arr = np.array(img, dtype=np.float32)

    if arr.max() > 255:
        # 16-bit image
        arr = arr / 65535.0
    else:
        arr = arr / 255.0

    # Convertir de [0, 1] a [-1, 1]
    arr = arr * 2.0 - 1.0

    return arr, img.size


def save_normal_map(arr, path):
    """Guarda un normal map de float32 [-1, 1] a PNG 8-bit RGB."""
    # Convertir de [-1, 1] a [0, 1]
    arr = arr * 0.5 + 0.5

    # Clamp y convertir a 8-bit RGB
    arr_8 = np.clip(arr * 255.0, 0, 255).astype(np.uint8)

    img = Image.fromarray(arr_8, mode='RGB')
    img.save(path)
    print(f"  Guardado exitosamente: {path} ({arr_8.shape[1]}x{arr_8.shape[0]}, 8-bit RGB)")


def combine_rnm(base, detail):
    """
    Combina dos normal maps usando Reoriented Normal Mapping (RNM).

    Referencia: "Reoriented Normal Mapping" (Blinn, revisado por Barré-Brisebois & Hill)

    Fórmula:
        t = base.xyz + vec3(0, 0, 1)
        u = detail.xyz * vec3(-1, -1, 1)
        result = t * dot(t, u) - u * t.z
        result = normalize(result)
    """
    # Extraer componentes
    # base y detail están en [-1, 1]
    t = base.copy()
    t[:, :, 2] += 1.0  # t = base + (0, 0, 1)

    u = detail.copy()
    u[:, :, 0] *= -1.0  # u = detail * (-1, -1, 1)
    u[:, :, 1] *= -1.0

    # dot(t, u) — producto punto por píxel
    dot = np.sum(t * u, axis=2, keepdims=True)

    # result = t * dot(t, u) - u * t.z
    tz = t[:, :, 2:3]  # componente z de t, mantener dimensión
    result = t * dot - u * tz

    # Normalizar
    length = np.sqrt(np.sum(result ** 2, axis=2, keepdims=True))
    length = np.maximum(length, 1e-8)  # evitar división por cero
    result = result / length

    return result


def combine_udn(base, detail):
    """
    Combina usando UDN (Unreal Derivative Normal) — más simple, casi tan bueno.

    Fórmula:
        result.xy = base.xy + detail.xy
        result.z  = base.z
        result = normalize(result)
    """
    result = base.copy()
    result[:, :, 0] += detail[:, :, 0]  # R
    result[:, :, 1] += detail[:, :, 1]  # G
    # Z se mantiene del base

    # Normalizar
    length = np.sqrt(np.sum(result ** 2, axis=2, keepdims=True))
    length = np.maximum(length, 1e-8)
    result = result / length

    return result


def main():
    if len(sys.argv) < 4:
        print("Uso: python combine_normals_rnm.py <normal_base.png> <normal_detail.png> <output.png> [--method rnm|udn]")
        print()
        print("  normal_base   = Normal map principal (HP bake)")
        print("  normal_detail = Normal map de detalle (materiales procedurales)")
        print("  output        = Resultado combinado")
        print("  --method      = 'rnm' (default, más preciso) o 'udn' (más rápido)")
        sys.exit(1)

    base_path = sys.argv[1]
    detail_path = sys.argv[2]
    output_path = sys.argv[3]

    method = 'rnm'
    if '--method' in sys.argv:
        idx = sys.argv.index('--method')
        if idx + 1 < len(sys.argv):
            method = sys.argv[idx + 1].lower()

    print(f"Combinando normal maps ({method.upper()}):")
    print(f"  Base:   {base_path}")
    print(f"  Detail: {detail_path}")

    # Cargar
    base, base_size = load_normal_map(base_path)
    detail, detail_size = load_normal_map(detail_path)

    print(f"  Base size:   {base_size[0]}x{base_size[1]}")
    print(f"  Detail size: {detail_size[0]}x{detail_size[1]}")

    # Si tienen diferente tamaño, redimensionar detail al tamaño de base
    if base.shape != detail.shape:
        print(f"  ⚠️ Tamaños diferentes — redimensionando detail a {base_size[0]}x{base_size[1]}")
        detail_img = Image.fromarray(((detail * 0.5 + 0.5) * 255).astype(np.uint8))
        detail_img = detail_img.resize(base_size, Image.LANCZOS)
        detail = np.array(detail_img, dtype=np.float32) / 255.0 * 2.0 - 1.0

    # Combinar
    if method == 'udn':
        result = combine_udn(base, detail)
    else:
        result = combine_rnm(base, detail)

    # Guardar
    save_normal_map(result, output_path)
    print("  [OK] Listo!")


if __name__ == '__main__':
    main()

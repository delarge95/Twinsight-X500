"""Mueve el átomo `moov` de un MP4 al principio («faststart», como `ffmpeg -movflags +faststart`) sin ffmpeg.
La grabadora de Gazebo deja `moov` al final: el navegador tendría que bajar el archivo entero antes de empezar.
Uso: python desarrollo/landing/mp4-faststart.py entrada.mp4 [salida.mp4]   (sin salida, reescribe la entrada)
Solo corrige stco/co64 (desplazamientos de los trozos); basta para los MP4 H.264 de Gazebo."""
import struct
import sys

CONTAINERS = {b"moov", b"trak", b"mdia", b"minf", b"stbl", b"edts", b"dinf", b"udta"}


def atoms(buf, start, end):
    i = start
    while i < end:
        size, kind = struct.unpack(">I4s", buf[i:i + 8])
        head = 8
        if size == 1:
            size = struct.unpack(">Q", buf[i + 8:i + 16])[0]
            head = 16
        elif size == 0:
            size = end - i
        yield i, size, kind, head
        i += size


def patch(moov, delta):
    def walk(start, end):
        for i, size, kind, head in atoms(moov, start, end):
            if kind in CONTAINERS:
                walk(i + head, i + size)
            elif kind in (b"stco", b"co64"):
                n = struct.unpack(">I", moov[i + head + 4:i + head + 8])[0]
                p = i + head + 8
                fmt, w = (">I", 4) if kind == b"stco" else (">Q", 8)
                for k in range(n):
                    off = struct.unpack(fmt, moov[p + k * w:p + (k + 1) * w])[0]
                    moov[p + k * w:p + (k + 1) * w] = struct.pack(fmt, off + delta)
    walk(8, len(moov))


src = sys.argv[1]
dst = sys.argv[2] if len(sys.argv) > 2 else src
data = open(src, "rb").read()
top = list(atoms(data, 0, len(data)))
kinds = [a[2] for a in top]
if b"moov" not in kinds or b"mdat" not in kinds:
    sys.exit("no es un MP4 con moov y mdat")
if kinds.index(b"moov") < kinds.index(b"mdat"):
    print("ya es faststart:", src)
    if dst != src:
        open(dst, "wb").write(data)
    sys.exit(0)
mi = kinds.index(b"moov")
i, size, _, _ = top[mi]
moov = bytearray(data[i:i + size])
patch(moov, size)  # todo lo que va detrás del nuevo moov se desplaza su tamaño
first_media = top[kinds.index(b"mdat")][0]
rest = b"".join(data[a[0]:a[0] + a[1]] for k, a in enumerate(top) if k != mi and a[0] >= first_media)
out = data[:first_media] + bytes(moov) + rest
open(dst, "wb").write(out)
print(f"faststart: {dst} ({len(out)} bytes, moov {size} bytes delante de mdat)")

import sys, glob, io
out = sys.argv[1]
pc = r"E:/WebGL_tesis/desarrollo/unity_project/Library/PackageCache/"
def rd(p):
    return io.open(p, encoding="utf-8-sig").read().strip("\r\n")
ofl = rd(r"E:/WebGL_tesis/desarrollo/unity_project/Assets/UI/Fonts/SpaceGrotesk/SpaceGrotesk-2.0.0/OFL.txt")
ofl_body = ofl[ofl.index("-----------------------------------------------------------\nSIL OPEN FONT LICENSE"):] if "-----------------------------------------------------------\nSIL OPEN FONT LICENSE" in ofl.replace("\r\n", "\n") else ofl
ofl_body = ofl.replace("\r\n", "\n")
ofl_body = ofl_body[ofl_body.index("-----------------------------------------------------------\nSIL OPEN FONT LICENSE"):]
mit = rd(glob.glob(pc + "com.lilytech-lab.wireframe-shader@*/LICENSE")[0])
urp = rd(glob.glob(pc + "com.unity.render-pipelines.universal@*/Third Party Notices.md")[0])
core = rd(glob.glob(pc + "com.unity.render-pipelines.core@*/THIRD PARTY NOTICES.md")[0])

head = """TwinSight X500 para PC - version 0.0.3
AVISOS DE LICENCIA DE TERCEROS (THIRD PARTY NOTICES)
======================================================================

TwinSight X500 incluye o usa los componentes de terceros que se listan
aqui. Este archivo viaja con la aplicacion. Donde no he podido confirmar
la licencia desde el proyecto, lo digo como "licencia por confirmar" en
lugar de afirmar una. Lo propio (codigo de la aplicacion, interfaz,
iconos vectoriales dibujados por codigo, texturas de relieve generadas
para el proyecto, datos de las fichas) es de los autores de TwinSight.

El modelo 3D esta basado en el CAD que publica Holybro para el X500 V2;
sin afiliacion con Holybro. Holybro y sus marcas son de Holybro.
PX4 es una marca de sus propietarios; esta aplicacion no es PX4.


RESUMEN
----------------------------------------------------------------------
 1. Motor Unity 6 (6000.6.5f1) y Mono ............ licencia de Unity: por confirmar
 2. Paquetes de Unity (URP, Core RP, Collections, Burst, UGUI,
    Shader Graph library) ......................... Unity Companion License
 3. Space Grotesk (tipografia) ..................... SIL OFL 1.1
 4. Inter (tipografia) ............................. SIL OFL 1.1
 5. HDRI "Studio Small 09" (Poly Haven) ............ CC0 1.0
 6. DOTween (Demigiant) ............................ licencia de Demigiant: por confirmar
 7. Wireframe Shader (Lilytech Lab) ................ MIT
 8. Componentes Microsoft (D3D12, DirectStorage) ... terminos de Microsoft: por confirmar
 9. Datos del vuelo de ejemplo (PX4 Flight Review) . licencia por confirmar
10. Modelo 3D basado en el CAD de Holybro .......... propiedad de Holybro; sin afiliacion
11. Iconos PNG heredados de la interfaz ............ origen y licencia por confirmar


1. UNITY 6 (6000.6.5f1) Y MONO
----------------------------------------------------------------------
La aplicacion se ejecuta sobre el motor Unity (UnityPlayer.dll, carpeta
MonoBleedingEdge con el runtime Mono y las librerias de sistema de
TwinSightX500_Data\\Managed). Copyright Unity Technologies ApS y sus
licenciantes; el motor incluye componentes de terceros (Mono, entre
otros) bajo sus propias licencias.
Licencia de uso con la que se compilo y terminos de redistribucion
exactos: por confirmar (no consta en el proyecto cual es el plan de
licencia de Unity del equipo que compilo). Terminos de Unity:
https://unity.com/legal


2. PAQUETES DE UNITY INCLUIDOS EN LA COMPILACION
----------------------------------------------------------------------
Paquetes de Unity Technologies, bajo la Unity Companion License for
Unity-dependent projects
(https://unity.com/legal/licenses/unity-companion-license):
  - Universal Render Pipeline (com.unity.render-pipelines.universal) 17.6.0
  - Core RP Library (com.unity.render-pipelines.core)
  - Collections (com.unity.collections) 6.6.0
  - Burst (com.unity.burst) 2.0.0
  - Unity UI / uGUI (com.unity.ugui) 2.6.0 (incluye TextMeshPro)
  - Shader Graph (biblioteca de shaders en tiempo de ejecucion)
  - Universal RP Config
Cada uno: copyright (c) Unity Technologies, "AS IS", sin garantia de
ningun tipo. Para el texto completo de la licencia, ver la direccion de
arriba (el texto no viaja en el paquete; el archivo LICENSE.md de cada
paquete solo remite a ella).

Avisos de terceros que esos paquetes declaran (copiados tal cual al
final de este archivo, secciones A y B).


3. SPACE GROTESK
----------------------------------------------------------------------
Copyright 2020 The Space Grotesk Project Authors
(https://github.com/floriankarsten/space-grotesk)
Licencia: SIL Open Font License 1.1 (texto completo en la seccion C).
Se usa para titulos y botones.


4. INTER
----------------------------------------------------------------------
Copyright 2020 The Inter Project Authors (https://github.com/rsms/inter)
(segun los metadatos del archivo Inter-Regular.otf del proyecto).
Licencia: SIL Open Font License 1.1 (texto completo en la seccion C).
Se usa para el texto base de la interfaz.


5. HDRI "STUDIO SMALL 09"
----------------------------------------------------------------------
Autor: Sergej Majboroda. Fuente: https://polyhaven.com/a/studio_small_09
Licencia: CC0 1.0 (dominio publico); no exige atribucion, se cita por
cortesia. Se usa en la build de PC como entorno de reflejos y de luz
ambiental (no se muestra de fondo). Poly Haven: https://polyhaven.com


6. DOTWEEN
----------------------------------------------------------------------
DOTween is copyright (c) 2014-2018 Daniele Giardini - Demigiant
(DOTween.dll, version gratuita, usada para animaciones de interfaz).
Licencia: la publica Demigiant en http://dotween.demigiant.com/license.php
y no la he podido verificar desde aqui: licencia por confirmar.


7. WIREFRAME SHADER (Lilytech Lab)
----------------------------------------------------------------------
Paquete com.lilytech-lab.wireframe-shader
(https://github.com/lilytech-lab/WireframeShader). Su shader
"Custom/Wireframe" se usa en el material de alambre del visor de PC.
Licencia MIT (texto completo en la seccion D).


8. COMPONENTES DE MICROSOFT
----------------------------------------------------------------------
Las carpetas y archivos D3D12\\ (D3D12Core.dll), dstorage.dll y
dstoragecore.dll los copia el reproductor de Unity (DirectX 12 /
DirectStorage). Son componentes de Microsoft Corporation, que mantiene
sus derechos; sus terminos de redistribucion no estan en el proyecto:
licencia por confirmar.


9. DATOS DE VUELO DE EJEMPLO
----------------------------------------------------------------------
TwinSightX500_Data\\StreamingAssets\\Telemetry\\x500v2_flight.twf se
deriva del registro publico de PX4 Flight Review
0f3c4a74-4648-4b99-8992-c7fdc8681f6e (https://review.px4.io, fuselaje 4019
Holybro X500 V2, PX4 v1.15.4), remuestreado a 20 Hz; no incluye
coordenadas GPS absolutas, origen del EKF ni identificador de hardware.
Gracias al piloto que lo hizo publico. Licencia del registro: por
confirmar (Flight Review no declara aqui una licencia concreta).


10. MODELO 3D
----------------------------------------------------------------------
Derivado del CAD publicado por Holybro para el kit de marco X500 V2,
reorganizado y optimizado para visualizacion (solo muestra; no es un
archivo de ingenieria). Holybro (Shenzhen Holybro Technology Co., Ltd.)
es la duena del diseno y de sus marcas. Sin afiliacion con Holybro.
Licencia de redistribucion del CAD: por confirmar.


11. ICONOS PNG HEREDADOS
----------------------------------------------------------------------
Algunos iconos PNG de Assets\\UI\\Icons (heredados de la primera version
de la interfaz) tienen origen y licencia sin documentar en el proyecto:
licencia por confirmar. Los iconos de la interfaz de escritorio son
vectoriales y se dibujan por codigo.


======================================================================
TEXTOS DE LICENCIA
======================================================================

A. AVISOS DE TERCEROS DEL PAQUETE Universal Render Pipeline
----------------------------------------------------------------------
"""

doc = (head + urp + "\n\n\nB. AVISOS DE TERCEROS DEL PAQUETE Core RP Library\n"
       "----------------------------------------------------------------------\n" + core +
       "\n\n\nC. SIL OPEN FONT LICENSE 1.1 (Space Grotesk e Inter)\n"
       "----------------------------------------------------------------------\n"
       "Copyright 2020 The Space Grotesk Project Authors (https://github.com/floriankarsten/space-grotesk)\n"
       "Copyright 2020 The Inter Project Authors (https://github.com/rsms/inter)\n\n" + ofl_body +
       "\n\n\nD. MIT - Wireframe Shader (Lilytech Lab)\n"
       "----------------------------------------------------------------------\n" + mit + "\n")
doc = doc.replace("\\\\", "\\")
doc = doc.replace("\r\n", "\n").replace("\n", "\r\n")
io.open(out, "w", encoding="utf-8-sig", newline="").write(doc)
print(len(doc))

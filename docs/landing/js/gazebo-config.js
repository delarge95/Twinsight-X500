// Sección «Gazebo»: vídeo grabado de Gazebo como ventana plana dentro de la escena 3D y el gemelo (el GLB de la landing) volando delante.
// TODAS las constantes del efecto están aquí. Para cambiar a la captura final basta con sustituir los archivos de media/gazebo/
// (mismos nombres) y, si cambió el encuadre, la pose de la cámara de abajo (o mandarla en sincronia.json).
// Estudio y fórmulas: TwinSight_coord/GAZEBO_VIDEO_LANDING_PLAN.md §4.

export const GAZEBO = {
  // ── archivos (rutas relativas a docs/) ──
  // Fuentes del <video>, por orden de preferencia. Solo se listan las que existen (una fuente ausente deja un 404 en la consola).
  // Para la edición de color: sustituir gazebo_demo.webm / .mp4 y poster.png por archivos del mismo tamaño y duración, sin tocar código.
  video: [
    { src: 'media/gazebo/gazebo_demo.webm', type: 'video/webm' },
    { src: 'media/gazebo/gazebo_demo.mp4', type: 'video/mp4' },
  ],
  poster: 'media/gazebo/poster.png',
  telemetria: 'media/gazebo/telemetria.json', // JSON compacto de run_demo_flight.py (30 Hz, t_ms, cm, cuaternión ×1e4, u ×1000, flags)
  sincronia: 'media/gazebo/sincronia.json',   // opcional: { desfase_s, camara: { pose, hfov } }. null = no se pide.

  // ── sincronía ──
  // tiempo de telemetría (s desde t0_lt) = mediaTime del vídeo + desfase.
  // Orden: sincronia.json → «record» de los eventos de la telemetría (corregido por la duración real del vídeo) → este valor.
  desfase: 0,
  // Instante del póster (s de vídeo): con movimiento reducido el gemelo se queda quieto en este fotograma.
  tPoster: 48.2, // captura v2: punto más cercano (4,1 m). El póster es este fotograma, para que el gemelo quieto coincida con él

  // ── cámara de Gazebo (camara_demo.sdf) ──
  // pose = x y z (ENU, m) roll pitch yaw (rad), como en el SDF. Es la de la captura v2 (8,5 m al norte del despegue de referencia,
  // 1,6 m, mira al sur 3,5° hacia abajo). Manda la de sincronia.json (camara_gazebo.pose_enu_xyz_rpy) si la trae.
  camara: {
    pose: [0, 8.5, 1.6, 0, 0.0611, -1.5708],
    hfov: 0.5585,          // rad (32°) → vFOV ≈ 18,32° a 16:9
    ancho: 1280, alto: 720, // resolución del vídeo: fija la proporción del plano
  },

  // ── mapeo cámara de Gazebo → escena ──
  D: 10,        // distancia del plano del vídeo a la cámara three, en unidades de escena (la escala es k = D / Z_ref)
  Z_ref: 11.7,  // m: profundidad (en la cámara de Gazebo) que cae exactamente sobre el plano: la del despegue (N = −3,2 m)
  radioDron: null, // m: radio del X500 en Gazebo. null = el del GLB de la landing (son el mismo dron)
  // m que se suman a la altura del EKF: centro del cuerpo sobre el punto del EKF (~0,2) + deriva del barómetro en esta sesión
  // (el EKF da ~0,15 m menos que la pose real de Gazebo). Medido contra verdad_j.jsonl: con 0,35 el error mediano es 6 px de 1280.
  alturaCuerpo: 0.35,

  // ── recorte del vídeo (px del vídeo original, por UV: no se recodifica) ──
  // Caja de la trayectoria proyectada de todo el vuelo (telemetria.json + pose y FOV de sincronia.json) con el dron entero
  // (±0,42 m a los lados, +0,15/−0,32 m arriba/abajo) y su sombra en el suelo, más ~25 px de margen: x 194–1055, y 0–720.
  // Se amplía a 5:4. El póster y el fotograma de movimiento reducido se recortan igual.
  recorte: { x0: 175, y0: 0, x1: 1075, y1: 720 },

  // ── el gemelo AL LADO del vídeo, no encima ──
  // Se dibuja en su propio «escenario» (un recuadro gris grafito vacío, #gzLane en el HTML) en la misma posición relativa que el dron
  // ocupa en el vídeo: misma pose, desplazada un recuadro. Así se ven a la vez Gazebo y el gemelo sin taparse, y el gemelo puede
  // salir de su recuadro hacia el espectador. desplazamiento: null = el que marca el maquetado (centro de #gzLane − centro del vídeo);
  // [dx, dy] = fracciones del ancho/alto del recuadro del vídeo (p. ej. [1.08, 0] a la derecha, [0, 1.1] debajo).
  // sinSolape: px mínimos entre la silueta del gemelo y la del dron del vídeo; el tamaño del gemelo se limita para respetarlos.
  hilo: 0.55, // opacidad de la línea grafito que une el dron del vídeo con el gemelo (0 = sin línea)

  // ── «salir de la pantalla» ──
  // ρ = z / Z_ref. Si ρ < 1 el gemelo se adelanta al plano: z′ = Z_ref·max(ρ_min, ρ^γ) y (x′, y′) = (x, y)·z′/z
  // (mismo rayo → mismo píxel que el dron del vídeo, pero más cerca del espectador; se nota con el paralaje).
  // Si ρ ≥ 1 se queda justo delante del plano (z′ = Z_ref·planoMax) para no quedar tapado por el vídeo.
  // Pensado para la captura nueva: el dron llega a 4–6 m (ρ ≈ 0,3–0,43) y mide 250–400 px en el vídeo.
  escritorio: {
    gamma: 1.6,       // >1 exagera el acercamiento
    rhoMin: 0.25,     // tope de profundidad: el gemelo nunca pasa de 0,25·Z_ref (muy delante de la ventana)
    rhoExag: 0.9,     // la exageración de tamaño empieza en este ρ y es máxima en rhoMin
    exageracion: 3.4, // tamaño del gemelo en el punto más cercano respecto a la proyección exacta (1 = exacta). ROMPE EL MARCO
    radioMax: 0.6,    // tope del radio del gemelo en pantalla, en fracción del lado corto del viewport (no se sale de cuadro)
    paralaje: 0.6,    // unidades de escena que se mueve la «cabeza» (cámara three) con el puntero y el scroll; delata la profundidad
    vaiven: 0.1,      // vaivén lento automático de la cámara (0 = nada); se suma al paralaje
    inclinacion: 4,   // grados máximos que se inclina la ventana con el puntero y el scroll (0 con movimiento reducido)
    desplazamiento: null, // escritorio: el gemelo a la derecha del vídeo (lo marca #gzLane)
    sinSolape: 16,
  },
  movil: { // pantallas ≤ 860 px: menos profundidad y ventana casi plana, mismo vídeo
    gamma: 1.3,
    rhoMin: 0.4,
    rhoExag: 0.9,
    exageracion: 3.0,
    radioMax: 0.5,
    paralaje: 0.3,
    vaiven: 0.06,
    inclinacion: 1,
    desplazamiento: null, // móvil: debajo del vídeo (lo marca #gzLane)
    sinSolape: 10,
  },
  planoMax: 0.97,

  // ── opacidad del gemelo ── smoothstep(rhoLejos, rhoCerca, ρ): algo traslúcido lejos, macizo cerca.
  // La misma rampa enciende la sombra de contacto del gemelo sobre su recuadro.
  opacidad: {
    lejos: 1,       // opacidad con ρ ≥ rhoLejos (ya no tapa al dron del vídeo: macizo siempre; bajar si se quiere un gemelo «fantasma»)
    cerca: 1,       // opacidad con ρ ≤ rhoCerca
    rhoLejos: 1.0,
    rhoCerca: 0.6,
  },

  // ── carga ──
  margenCarga: '150% 0px', // IntersectionObserver: empieza a bajar vídeo y datos cuando la sección está a 1,5 pantallas
  giroHelices: 55,         // rad/s de las hélices con u = 1 (u = orden de PX4 a los motores, no medida)
};

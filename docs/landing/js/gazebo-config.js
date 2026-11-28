// Sección «Gazebo»: vídeo grabado de Gazebo como ventana plana dentro de la escena 3D y el gemelo (el GLB de la landing) volando delante.
// TODAS las constantes del efecto están aquí. Para cambiar a la captura final basta con sustituir los archivos de media/gazebo/
// (mismos nombres) y, si cambió el encuadre, la pose de la cámara de abajo (o mandarla en sincronia.json).
// Estudio y fórmulas: TwinSight_coord/GAZEBO_VIDEO_LANDING_PLAN.md §4.

export const GAZEBO = {
  // ── archivos (rutas relativas a docs/) ── vídeo recortado y acelerado: sincronia.json con mapaTiempo [[t_vídeo, t_telemetría], …]
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
  // tiempo de telemetría (s desde t0_lt) = mapaTiempo de sincronia.json (vídeo recortado y acelerado, lineal a tramos) o, si no
  // lo trae, mediaTime del vídeo + desfase (sincronia.json → «record» de los eventos → este valor).
  desfase: 0,
  // Instante del póster (s del vídeo ACTUAL): con movimiento reducido el gemelo se queda quieto en este fotograma.
  // Vídeo corto: el póster de la grabación es el fotograma 22,53 s (dron grande, cerca de la cámara).
  tPoster: 22.53,

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
  // Ventana pequeña: recorte ceñido a donde vuela el dron (centro del dron entre x 590–726 y y 80–508 el 90 % del vuelo). En el punto
  // más cercano (4 m) el dron se sale del recorte por arriba: aceptado. 16:10. El póster y el fotograma estático se recortan igual.
  recorte: { x0: 340, y0: 150, x1: 940, y1: 525 },

  // ── ventana del simulador alrededor del vídeo (interfaz estilizada; ver gazebo-ventana.js) ──
  // Medidas en fracciones del ancho del vídeo recortado. panel: 0 = sin panel lateral.
  ventana: { titulo: 0.09, herramientas: 0.075, estado: 0.06, panel: 0.3, borde: 0.006 },

  // ── el gemelo: vuela ALREDEDOR de la ventana, grande, sin tapar al dron del vídeo ──
  // Posición en pantalla (px), a partir de la del dron en el recorte del vídeo (nx, ny ∈ −1…1, y hacia abajo):
  //   x = centro_ventana_x + desvioX·ancho_ventana + ampX·nx·ancho_ventana/2
  //   y = centro_ventana_y − elevacion·alto_ventana + ampY·ny·alto_ventana/2
  // Tamaño (radio en px): tamBase·lado_corto_pantalla·(1/max(ρ, rhoMin))^crecimiento, con tope radioMax·lado_corto.
  // Profundidad (para el paralaje, «salir de la pantalla»): z′ = Z_ref·max(rhoMin, ρ^γ), delante de la ventana.
  // Topes: nunca fuera de la pantalla (margen 8 px, barra de navegación arriba) y nunca encima del dron del vídeo (sinSolape px):
  // si lo taparía, sube; si arriba no cabe, se encoge. La actitud copia la telemetría.
  escritorio: {
    ampX: 2.4, ampY: 1.0,   // amplificación del movimiento del dron (en medias ventanas)
    elevacion: 1.05,        // el gemelo vuela por encima de la ventana (en altos de ventana)
    desvioX: 0,             // desplazamiento lateral fijo (en anchos de ventana)
    tamBase: 0.1,           // radio con el dron a Z_ref (en fracción del lado corto de la pantalla): ≈ 3–4× el dron del vídeo
    crecimiento: 1.1,       // cuánto crece al acercarse a la cámara de Gazebo
    radioMax: 0.3,
    sinSolape: 14,
    gamma: 1.6, rhoMin: 0.3, // profundidad del gemelo delante de la ventana
    paralaje: 0.6,    // unidades de escena que se mueve la «cabeza» (cámara three) con el puntero y el scroll; delata la profundidad
    vaiven: 0.1,      // vaivén lento automático de la cámara (0 = nada); se suma al paralaje
    inclinacion: 4,   // grados máximos que se inclina la ventana con el puntero y el scroll (0 con movimiento reducido)
  },
  movil: { // pantallas ≤ 860 px: ventana pequeña abajo, el gemelo encima, menos profundidad y ventana casi plana
    ampX: 1.5, ampY: 0.8,
    elevacion: 1.15,
    desvioX: 0,
    tamBase: 0.14,
    crecimiento: 0.9,
    radioMax: 0.36,
    sinSolape: 10,
    gamma: 1.3, rhoMin: 0.45,
    paralaje: 0.3,
    vaiven: 0.06,
    inclinacion: 1,
  },
  planoMax: 0.97,

  // ── opacidad del gemelo y sombra de contacto ── smoothstep(rhoLejos, rhoCerca, ρ) enciende la sombra al acercarse.
  opacidad: {
    lejos: 1,       // el gemelo no tapa al dron del vídeo: macizo siempre
    cerca: 1,
    rhoLejos: 1.0,
    rhoCerca: 0.6,
  },

  // ── carga ──
  margenCarga: '150% 0px', // IntersectionObserver: empieza a bajar vídeo y datos cuando la sección está a 1,5 pantallas
  giroHelices: 55,         // rad/s de las hélices con u = 1 (u = orden de PX4 a los motores, no medida)
};

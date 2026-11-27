// Sección «Gazebo»: vídeo grabado de Gazebo como ventana plana dentro de la escena 3D y el gemelo (el GLB de la landing) volando delante.
// TODAS las constantes del efecto están aquí. Para cambiar a la captura final basta con sustituir los archivos de media/gazebo/
// (mismos nombres) y, si cambió el encuadre, la pose de la cámara de abajo (o mandarla en sincronia.json).
// Estudio y fórmulas: TwinSight_coord/GAZEBO_VIDEO_LANDING_PLAN.md §4.

export const GAZEBO = {
  // ── archivos (rutas relativas a docs/) ──
  // Fuentes del <video>, por orden de preferencia. Solo se listan las que existen (una fuente ausente deja un 404 en la consola).
  // Con la captura final: añadir delante { src: 'media/gazebo/gazebo_demo.webm', type: 'video/webm' }.
  video: [
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
  tPoster: 22.2,

  // ── cámara de Gazebo (camara_demo.sdf) ──
  // pose = x y z (ENU, m) roll pitch yaw (rad), como en el SDF. Esta es la de la captura final; las pruebas 02–04 se grabaron con
  // «0 14 1.6 0 -0.2 -1.5708», que va en el sincronia.json de prueba. Si sincronia.json trae camara.pose, manda sobre esta.
  camara: {
    pose: [0, 14, 2.0, 0, -0.06, -1.5708],
    hfov: 1.0472,          // rad (60°) → vFOV ≈ 35,98° a 16:9
    ancho: 1280, alto: 720, // resolución del vídeo: fija la proporción del plano
  },

  // ── mapeo cámara de Gazebo → escena ──
  D: 10,        // distancia del plano del vídeo a la cámara three, en unidades de escena (la escala es k = D / Z_ref)
  Z_ref: 14,    // m: profundidad (en la cámara de Gazebo) que cae exactamente sobre el plano; ≈ distancia del despegue
  radioDron: null, // m: radio del X500 en Gazebo. null = el del GLB de la landing (son el mismo dron)
  alturaCuerpo: 0.24, // m: el EKF marca 0 con el dron posado; el centro del cuerpo (y del GLB) queda esto más arriba

  // ── «salir de la pantalla» ──
  // ρ = z / Z_ref. Si ρ < 1 el gemelo se adelanta al plano: z′ = Z_ref·max(ρ_min, ρ^γ) y (x′, y′) = (x, y)·z′/z
  // (mismo rayo → mismo píxel que el dron del vídeo, pero más cerca del espectador; se nota con el paralaje).
  // Si ρ ≥ 1 se queda justo delante del plano (z′ = Z_ref·planoMax) para no quedar tapado por el vídeo.
  escritorio: {
    gamma: 1.6,      // >1 exagera el acercamiento
    rhoMin: 0.3,     // tope: el gemelo nunca pasa de 0,3·Z_ref
    exageracion: 1.3, // tamaño extra del gemelo cuando está cerca (1 = proyección exacta). Se aplica con la misma rampa que la opacidad
    paralaje: 0.9,   // unidades de escena que se mueve la «cabeza» (cámara three) con el puntero y el scroll; delata la profundidad
    vaiven: 0.25,    // vaivén lento automático de la cámara (0 = nada); se suma al paralaje
  },
  movil: { // pantallas ≤ 860 px: menos profundidad, mismo vídeo
    gamma: 1.25,
    rhoMin: 0.55,
    exageracion: 1.12,
    paralaje: 0.45,
    vaiven: 0.2,
  },
  planoMax: 0.97,

  // ── opacidad del gemelo ── smoothstep(rhoLejos, rhoCerca, ρ): lejos se ve sobre todo el dron del vídeo; cerca, el gemelo macizo.
  opacidad: {
    lejos: 0.4,     // opacidad con ρ ≥ rhoLejos (0 = invisible: solo el vídeo)
    cerca: 1,       // opacidad con ρ ≤ rhoCerca
    rhoLejos: 1.0,
    rhoCerca: 0.75,
  },

  // ── carga ──
  margenCarga: '150% 0px', // IntersectionObserver: empieza a bajar vídeo y datos cuando la sección está a 1,5 pantallas
  giroHelices: 55,         // rad/s de las hélices con u = 1 (u = orden de PX4 a los motores, no medida)
};

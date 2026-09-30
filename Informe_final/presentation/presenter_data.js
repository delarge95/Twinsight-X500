/* Generado por tools/build_presenter_data.py a partir de PRESENTATION_SCRIPT.md y SPEAKER_CARDS.md. No editar a mano. */
window.__TW_SCRIPT = {
 "slides": [
  {
   "n": 1,
   "title": "Portada",
   "start": 0,
   "end": 40,
   "steps": 0,
   "screen": "título, ficha del proyecto y captura del visor.",
   "segments": [
    {
     "k": 0,
     "label": "",
     "html": "Buenos días. Soy Alexander Woodcock Salomón y les presento TwinSight X500, mi trabajo de grado de Ingeniería Multimedia en la UNAD. <span class=\"cue\">mirar jurado</span>\n\nEs una aplicación web para inspeccionar en 3D el dron Holybro X500 V2. El punto de partida son sus modelos <b>CAD</b>, los archivos de diseño que usan los ingenieros, que son precisos pero pesados y no están hechos para abrirse en un navegador. El resultado corre en <b>WebGL</b>, la tecnología que dibuja 3D dentro del navegador sin instalar nada."
    }
   ],
   "transition": "Empiezo por el problema que me llevó a hacerlo.",
   "contingency": "",
   "procedure": [],
   "avoid": "\"gemelo digital completo\" · \"simulador\" · \"producto terminado\"",
   "key": "App web para inspeccionar en 3D el Holybro X500 V2. CAD = archivos de diseño, precisos y pesados. WebGL = 3D en el navegador, sin instalar nada."
  },
  {
   "n": 2,
   "title": "El problema",
   "start": 40,
   "end": 100,
   "steps": 4,
   "screen": "",
   "segments": [
    {
     "k": 0,
     "label": "",
     "html": "Imaginen que tienen que ubicar un motor del dron y entender cómo se sujeta a su brazo, con el manual en PDF abierto."
    },
    {
     "k": 1,
     "label": "Documentación",
     "html": "La información está. Hay planos, manuales, listas de piezas. Pero está repartida en varios documentos, y cada uno muestra una parte."
    },
    {
     "k": 2,
     "label": "Fricción",
     "html": "Entonces el trabajo lo hace la cabeza de quien lee: mirar vistas planas y armar mentalmente dónde está cada cosa, qué va delante, qué va detrás. A eso lo llamo <b>reconstrucción espacial</b>. Con un dron de decenas de piezas, se vuelve cansado muy rápido. <span class=\"cue\">pausa</span>"
    },
    {
     "k": 3,
     "label": "Respuesta",
     "html": "Mi propuesta fue llevar el dron a una vista 3D en la web, donde uno pueda girarlo, tocar una pieza, aislarla y ver con qué se conecta."
    },
    {
     "k": 4,
     "label": "Idea clave",
     "html": "Los datos existen. Lo que faltaba era un puente entre esos datos y la comprensión del conjunto. <span class=\"cue\">mirar jurado</span>"
    }
   ],
   "transition": "Hay una teoría que explica por qué ese puente importa.",
   "contingency": "",
   "procedure": [],
   "avoid": "\"el 2D no sirve\" · \"el 3D siempre es mejor\"",
   "key": "Ubicar un motor con el PDF. La información está, pero repartida. Quien lee arma el objeto en la cabeza: reconstrucción espacial. Faltaba un puente."
  },
  {
   "n": 3,
   "title": "Carga cognitiva",
   "start": 100,
   "end": 175,
   "steps": 4,
   "screen": "",
   "segments": [
    {
     "k": 0,
     "label": "",
     "html": "La teoría de la carga cognitiva parte de algo simple: la memoria de trabajo es limitada. Sweller distingue tres tipos de carga."
    },
    {
     "k": 1,
     "label": "Intrínseca",
     "html": "La intrínseca viene del contenido. Un dron con muchas piezas es difícil de entender por sí mismo, y eso ninguna interfaz lo cambia."
    },
    {
     "k": 2,
     "label": "Extrínseca",
     "html": "La extrínseca viene de cómo se presenta. Saltar de un plano a una tabla gasta esfuerzo que no enseña nada. Esta es la que el diseño puede bajar."
    },
    {
     "k": 3,
     "label": "Germana",
     "html": "Y la germana es el esfuerzo que vale la pena: entender para qué sirve una pieza y dónde encaja."
    },
    {
     "k": 4,
     "label": "Límite declarado",
     "html": "Aclaro algo desde ya. La tesis no mide estos tres tipos de carga. Los usa para justificar decisiones de diseño. Lo que sí se mide más adelante, con NASA-TLX, es la carga de trabajo que cada persona percibe.\n\nCon esa idea diseñé la interfaz: que la app haga parte del trabajo mental. Girar el dron con el dedo reemplaza el giro que antes se hacía en la cabeza, y la ficha de cada pieza funciona como memoria externa."
    }
   ],
   "transition": "Antes de seguir, conviene dejar claro qué es TwinSight y qué no.",
   "contingency": "",
   "procedure": [],
   "avoid": "\"NASA-TLX mide la carga intrínseca\" · \"el 3D elimina la carga cognitiva\"",
   "key": "Memoria de trabajo limitada. Intrínseca, extrínseca (la que el diseño baja), germana. La tesis no las mide. La app hace parte del trabajo mental: girar con el dedo, ficha como memoria externa."
  },
  {
   "n": 4,
   "title": "Alcance",
   "start": 175,
   "end": 235,
   "steps": 2,
   "screen": "",
   "segments": [
    {
     "k": 0,
     "label": "",
     "html": "Esta slide es importante porque evita malentendidos. <span class=\"cue\">mirar jurado</span>\n\nUn <b>digital twin</b> operacional es una copia digital conectada al objeto real: recibe sus datos en vivo, está calibrada y ayuda a tomar decisiones. Un <b>digital shadow</b> recibe datos del objeto, pero no actúa sobre él."
    },
    {
     "k": 1,
     "label": "Exclusiones",
     "html": "TwinSight no hace nada de eso. No recibe datos del dron físico, no predice mantenimiento y no se integra con sistemas industriales. Tampoco hace <b>FEA</b>, que es la simulación por elementos finitos que usan los ingenieros para calcular esfuerzos o calor con precisión. <span class=\"cue\">pausa</span>"
    },
    {
     "k": 2,
     "label": "Visual Product Twin",
     "html": "Lo que sí hace es representar el producto: su forma, sus piezas, cómo se relacionan y distintas maneras de mirarlo. Por eso lo llamo <b>visual product twin</b>. Es el primer escalón. Los otros dos quedan como trabajo futuro."
    }
   ],
   "transition": "Con ese alcance, los objetivos se pueden verificar uno por uno.",
   "contingency": "",
   "procedure": [],
   "avoid": "\"TwinSight ya es un gemelo digital\" · \"Thermal mide temperatura real\"",
   "key": "Digital twin = conectado y calibrado. Shadow = recibe datos. TwinSight no hace eso ni FEA. Representa el producto: visual product twin, primer escalón."
  },
  {
   "n": 5,
   "title": "Objetivos",
   "start": 235,
   "end": 290,
   "steps": 4,
   "screen": "",
   "segments": [
    {
     "k": 0,
     "label": "",
     "html": "La pregunta de investigación está arriba: qué diferencias aparecen en desempeño y en carga percibida entre un visor 3D web y la documentación 2D, y si ese visor puede funcionar bien en un navegador."
    },
    {
     "k": 1,
     "label": "OE1",
     "html": "El primer objetivo fue técnico: llevar el CAD a un modelo liviano sin perder forma ni piezas."
    },
    {
     "k": 2,
     "label": "OE2",
     "html": "El segundo, lograr buenos materiales y modos visuales manteniendo al menos 30 cuadros por segundo."
    },
    {
     "k": 3,
     "label": "OE3",
     "html": "El tercero, construir la app con selección, ficha, vista explosionada y corte."
    },
    {
     "k": 4,
     "label": "OE4",
     "html": "Y el cuarto, probarla con personas y compararla con la documentación 2D.\n\nEl método fue Design Science Research: investigar construyendo un artefacto y evaluándolo. La evaluación fue formativa, con doce personas, así que describe lo que pasó en esa muestra sin generalizar."
    }
   ],
   "transition": "Para evaluarlo usé cinco fuentes.",
   "contingency": "",
   "procedure": [],
   "avoid": "\"la evaluación generaliza a la población\" · \"30 FPS en todos los dispositivos\"",
   "key": "Pregunta: diferencias de desempeño y carga 3D vs 2D, y si funciona en el navegador. Cuatro objetivos. Método: Design Science Research, evaluación formativa con 12 personas."
  },
  {
   "n": 6,
   "title": "Evaluación en cinco capas",
   "start": 290,
   "end": 325,
   "steps": 6,
   "screen": "",
   "segments": [
    {
     "k": 0,
     "label": "",
     "html": "Ninguna medida sola cuenta la historia completa."
    },
    {
     "k": 1,
     "label": "KPIs técnicos",
     "html": "El rendimiento de la app,"
    },
    {
     "k": 2,
     "label": "SUS",
     "html": "el cuestionario de usabilidad SUS,"
    },
    {
     "k": 3,
     "label": "Tareas",
     "html": "cuatro tareas cronometradas,"
    },
    {
     "k": 4,
     "label": "Think-Aloud",
     "html": "lo que las personas decían en voz alta"
    },
    {
     "k": 5,
     "label": "NASA-TLX Raw",
     "html": "y el cuestionario de carga NASA-TLX, en 3D y en 2D."
    },
    {
     "k": 6,
     "label": "Triangulación",
     "html": "Cuando las cinco apuntan en la misma dirección, el resultado pesa más."
    }
   ],
   "transition": "Antes de los resultados, cómo se construyó. Empiezo por el modelo 3D.",
   "contingency": "",
   "procedure": [],
   "avoid": "\"NASA-TLX mide la carga cognitiva directamente\" · \"SUS demuestra que el 3D es mejor\" · \"telemetría\" para hablar del medidor interno",
   "key": "Cinco fuentes: rendimiento, SUS, tareas, Think-Aloud, NASA-TLX. Si coinciden, el resultado pesa más.\" (solo nombrarlas)"
  },
  {
   "n": 7,
   "title": "Del CAD al activo WebGL",
   "start": 325,
   "end": 400,
   "steps": 8,
   "screen": "",
   "segments": [
    {
     "k": 0,
     "label": "",
     "html": "Un archivo CAD, como el STEP del fabricante, no tiene triángulos. Describe superficies con ecuaciones: un cilindro es un radio y una altura. Para dibujarlo hay que convertir esas superficies en triángulos, y eso se llama <b>teselar</b>. Según la herramienta, el dron dio entre 6,5 y 6,9 millones de triángulos. Imposible de mover en un celular. <span class=\"cue\">señalar título</span>"
    },
    {
     "k": 1,
     "label": "MoI3D · STEPper",
     "html": "Teselé el STEP con MoI3D o con STEPper, según cuánto control necesitaba sobre la densidad."
    },
    {
     "k": 2,
     "label": "Blender",
     "html": "En Blender limpié la malla."
    },
    {
     "k": 3,
     "label": "Retopo",
     "html": "Las piezas importantes las rehice con menos polígonos y las repetidas las cambié por versiones livianas, que llamo proxies."
    },
    {
     "k": 4,
     "label": "Bake",
     "html": "Después pasé el detalle fino a texturas, con mapas de normales y de oclusión."
    },
    {
     "k": 5,
     "label": "FBX",
     "html": "Exporté a Unity"
    },
    {
     "k": 6,
     "label": "WebGL",
     "html": "y compilé para la web."
    },
    {
     "k": 7,
     "label": "Por qué optimizar",
     "html": "El problema de la teselación es que llena el modelo de caras internas, vértices duplicados y tornillos repetidos, que cuestan mucho y no se ven."
    },
    {
     "k": 8,
     "label": "Qué se conserva",
     "html": "Cuidé que cada pieza siguiera siendo una pieza, con su nombre y su lugar. Si no, después no se podría seleccionar nada. El resultado: 95 617 triángulos. <span class=\"cue\">mirar jurado</span>"
    }
   ],
   "transition": "Ahora, cómo está organizada la app por dentro.",
   "contingency": "",
   "procedure": [],
   "avoid": "\"el CAD tenía 6,5 millones de triángulos\" (los tuvo al teselarlo) · \"optimizar es solo bajar polígonos\"",
   "key": "El CAD no tiene triángulos: se generan al teselar. Salieron 6,5–6,9 M. Limpieza, retopología, proxies y bake → 95 617, sin perder las piezas."
  },
  {
   "n": 8,
   "title": "Arquitectura",
   "start": 400,
   "end": 450,
   "steps": 1,
   "screen": "",
   "segments": [
    {
     "k": 0,
     "label": "",
     "html": "La app está dividida en cuatro grupos: interfaz, coordinación, servicios que modifican el modelo y datos de cada pieza. Lo que los une es un <b>bus de eventos</b>, que funciona como un tablero de avisos. <span class=\"cue\">señalar EventBus</span>"
    },
    {
     "k": 1,
     "label": "Ejemplo real",
     "html": "Un ejemplo. Cuando tocas una pieza, el módulo de selección publica un solo aviso. La interfaz abre la ficha, las marcas se reacomodan y, si es un tornillo, otro módulo arma su versión detallada. El que avisó no conoce a ninguno. Por eso agregar una función no obliga a tocar lo que ya funciona. <span class=\"cue\">mirar jurado</span>"
    }
   ],
   "transition": "Para que ese aviso diga qué pieza se tocó, la app necesita una clasificación.",
   "contingency": "",
   "procedure": [],
   "avoid": "\"es solo un visor 3D\" · nombres de clases que no estén en la slide o en el código",
   "key": "Cuatro grupos unidos por un bus de eventos. Tocar una pieza: selección avisa; ficha, marcas y tornillo reaccionan sin conocerse."
  },
  {
   "n": 9,
   "title": "Taxonomía y tornillería",
   "start": 450,
   "end": 525,
   "steps": 7,
   "screen": "",
   "segments": [
    {
     "k": 0,
     "label": "",
     "html": "La taxonomía es la clasificación que le dice a la app qué tocó el usuario y qué mostrar."
    },
    {
     "k": 1,
     "label": "28",
     "html": "Hay 28 piezas principales, cada una con su ficha."
    },
    {
     "k": 2,
     "label": "30",
     "html": "En la escena son 30 anclas: esas 28, más un grupo de tornillería y otro de piezas menores."
    },
    {
     "k": 3,
     "label": "257",
     "html": "La auditoría contó 257 elementos que se dibujan o se pueden tocar."
    },
    {
     "k": 4,
     "label": "Hotspots",
     "html": "Los <b>hotspots</b> son marcas sobre el modelo: con un toque seleccionas un grupo sin apuntarle a una pieza diminuta."
    },
    {
     "k": 5,
     "label": "Tornillería",
     "html": "<b>Fastener</b> es cualquier sujetador: tornillos, tuercas, separadores. Eran el mayor problema. En la importación con STEPper sumaban 425 208 triángulos, el 79 % del archivo, y en pantalla apenas se ven. <span class=\"cue\">pausa</span>"
    },
    {
     "k": 6,
     "label": "Tornillo modular",
     "html": "En la escena, cada sujetador es una forma simple de 88 triángulos o menos: 14 408 en total. Cuando alguien inspecciona un tornillo, la app lo arma en detalle."
    },
    {
     "k": 7,
     "label": "figura",
     "html": "Con estas piezas: tres cabezas, una vuelta de rosca y una punta. Se elige la cabeza, se ajusta el diámetro y la vuelta se repite según el largo dividido por el paso de la rosca. Cinco piezas cubren todos los tornillos del dron. <span class=\"cue\">mirar jurado</span>"
    }
   ],
   "transition": "Con esa estructura, veamos cómo la recorre el usuario.",
   "contingency": "",
   "procedure": [],
   "avoid": "<b>Si preguntan por la BOM</b> (lista de materiales industrial): la taxonomía se le parece, pero está pensada para navegar, no para fabricar. \"28 categorías\" (son 28 piezas) · \"todos los fasteners son modulares\" (el sistema modular es solo para tornillos)",
   "key": "28 piezas, 30 anclas, 257 elementos. Hotspots = entrada por grupos. Tornillería: 425 208 → 14 408 triángulos. Tornillo detallado al inspeccionar: 3 cabezas, 1 vuelta de rosca, 1 punta."
  },
  {
   "n": 10,
   "title": "Flujo de uso",
   "start": 525,
   "end": 570,
   "steps": 7,
   "screen": "",
   "segments": [
    {
     "k": 0,
     "label": "",
     "html": "La app arranca en una pantalla de entrada."
    },
    {
     "k": 1,
     "label": "Explore",
     "html": "Luego, en Explore, giras y acercas el dron."
    },
    {
     "k": 2,
     "label": "Selección",
     "html": "Tocas una pieza"
    },
    {
     "k": 3,
     "label": "Ficha",
     "html": "y sube la ficha con sus datos, lo que en diseño de interfaces se llama <b>bottom sheet</b>."
    },
    {
     "k": 4,
     "label": "Barra inferior",
     "html": "Abajo está la barra con Inspect, Analyze y Studio. Esa barra está siempre visible, desde el primer momento. Cambiar de modo nunca exige buscar un menú."
    },
    {
     "k": 5,
     "label": "Detalle bajo demanda",
     "html": "Lo que aparece por etapas es el detalle: la ficha sube solo cuando eliges una pieza."
    },
    {
     "k": 6,
     "label": "Build pública",
     "html": "Y los paneles que usé para medir y depurar quedaron fuera de la versión pública."
    },
    {
     "k": 7,
     "label": "captura",
     "html": "En la captura: la pieza, Analyze y la barra. <span class=\"cue\">señalar</span>"
    }
   ],
   "transition": "Qué hace cada uno de esos modos.",
   "contingency": "",
   "procedure": [],
   "avoid": "\"los modos aparecen al seleccionar una pieza\" · \"todos los módulos experimentales quedaron publicados\"",
   "key": "Entrada → Explore → tocar pieza → ficha. La barra Inspect · Analyze · Studio está siempre visible."
  },
  {
   "n": 11,
   "title": "La app en el móvil",
   "start": 570,
   "end": 600,
   "steps": 3,
   "screen": "tres capturas de la build en el Redmi Note 10S.",
   "segments": [
    {
     "k": 0,
     "label": "",
     "html": "Así se ve en un celular."
    },
    {
     "k": 1,
     "label": "Inspect",
     "html": "Inspect responde qué es una pieza y dónde está: la aísla y abre su ficha."
    },
    {
     "k": 2,
     "label": "Analyze",
     "html": "Analyze responde cómo se conecta: separa las piezas y corta el modelo."
    },
    {
     "k": 3,
     "label": "Studio",
     "html": "Y Studio cambia la forma de mirarlo: rayos X, calor o solo la forma."
    }
   ],
   "transition": "El modo térmico merece su propia explicación.",
   "contingency": "",
   "procedure": [],
   "avoid": "\"Explode es un ensamblaje físicamente exacto\" · \"los shaders simulan física\"",
   "key": "Inspect: qué es y dónde está. Analyze: cómo se conecta. Studio: qué hay adentro."
  },
  {
   "n": 12,
   "title": "Thermal",
   "start": 600,
   "end": 670,
   "steps": 2,
   "screen": "",
   "segments": [
    {
     "k": 0,
     "label": "",
     "html": "Thermal muestra cómo se distribuye el calor en el dron, calculado con un modelo físico simplificado."
    },
    {
     "k": 1,
     "label": "Cómo calcula",
     "html": "Cada pieza tiene su propia temperatura. Los motores, los controladores de velocidad y la batería generan calor según lo que haga el dron: apagado, arrancando, en reposo o volando. Ese calor pasa a las piezas en contacto, y cuánto pasa depende del área de contacto, la distancia y el material. El cobre conduce mucho y la fibra de carbono, poco. Y cada pieza se enfría con el aire según cuánto esté expuesta. Es la física de transferencia de calor, simplificada."
    },
    {
     "k": 2,
     "label": "diagrama y límite",
     "html": "¿Por qué simplificada? Una simulación por elementos finitos divide cada pieza en miles de celdas y tarda minutos. Esto corre en un celular, en tiempo real. Así que cada pieza es un punto, el tiempo va acelerado y las conductividades están escaladas. Sirve para entender por dónde viaja el calor. Para diagnosticar habría que calibrarlo con mediciones reales. <span class=\"cue\">mirar jurado</span>"
    }
   ],
   "transition": "Ahora lo muestro funcionando.",
   "contingency": "",
   "procedure": [],
   "avoid": "\"Thermal mide la temperatura real\" · \"Thermal reemplaza un FEA\" · \"son valores inventados\" (salen de un modelo con base física) · leer los °C como medición",
   "key": "Modelo físico simplificado: fuentes según carga, conducción por área, distancia y material, enfriamiento por aire. Sirve para entender; diagnosticar exige calibrar."
  },
  {
   "n": 13,
   "title": "Demo en vivo",
   "start": 670,
   "end": 850,
   "steps": 2,
   "screen": "",
   "segments": [
    {
     "k": 1,
     "label": "ruta",
     "html": "Lo muestro en vivo. <span class=\"cue\">Alt+Tab a la landing</span>\n\n<b><span class=\"cue\">0:00</span></b> Esta es la página del proyecto. Está hecha con WebGL directo, sin Unity. El dron empieza desarmado, como una nube de puntos, y se arma cuando paso el cursor. Cada punto es un vértice del modelo. <span class=\"cue\">pausa</span>\n\n<b><span class=\"cue\">0:15</span></b> Al bajar, la página cuenta el proyecto con el mismo modelo: el paso del CAD a la web, las piezas por subsistema, los rayos X, la vista explosionada, el calor y los resultados. <b><span class=\"cue\">1:00</span></b> Al final queda una versión pequeña de la app, donde puedo girarlo y separarlo.\n\n<b><span class=\"cue\">1:10</span></b> Desde aquí abro la aplicación completa. <span class=\"cue\">«Abrir visor»</span> Toco el soporte de la batería. Isolate lo deja solo en pantalla y la ficha me dice qué es, sus medidas y cómo se monta. <span class=\"cue\">señalar ficha</span>\n\n<b><span class=\"cue\">1:40</span></b> En Inspect cambio el estado del dron: arranque, reposo, vuelo. Eso es lo que alimenta el cálculo térmico. <b><span class=\"cue\">1:55</span></b> En Analyze separo las piezas y hago un corte para ver el interior. <b><span class=\"cue\">2:20</span></b> En Studio, X-Ray deja ver lo que está adentro, Thermal muestra el calor con el dron en vuelo y Solid deja solo la forma.\n\n<b><span class=\"cue\">2:45</span></b> La página usa WebGL directo para contar el proyecto, y la app usa Unity para inspeccionarlo. <span class=\"cue\">Alt+Tab al deck</span>"
    },
    {
     "k": 2,
     "label": "contingencia",
     "html": "Lo que falta saber es qué tan bien corre y qué pasó cuando la usaron otras personas."
    }
   ],
   "transition": "",
   "contingency": "Demo en vivo como principal. Si la app no responde en 10 s o se congela, se narra el video de esta slide, que registra el mismo recorrido. Si falla la landing, se pasa directo a la app.",
   "procedure": [
    "[click 1 — ruta] Mostrar la ruta y pasar a la landing (Alt+Tab).",
    "<b>0:00–1:10 · Landing.</b> Pasar el cursor hasta que el dron se ensamble (~13 s). Luego scroll a ritmo parejo por los seis capítulos (~7 s cada uno). En la mini app final, activar «Explosionar» y desactivarlo.",
    "<b>1:10–2:50 · App Unity.</b> Volver arriba y pulsar «Abrir visor». Recorrer: selección → Isolate → ficha → Power → Explode → Cut → X-Ray → Thermal → Solid.",
    "Volver al deck (Alt+Tab), [click 2 — contingencia] y cerrar.",
    "<b>Regla de corte:</b> si la app tarda más de 10 s en responder o se congela, volver al deck sin comentarlo, hacer clic en el video y narrar el mismo recorrido. Si falla la landing, pasar directo a la app."
   ],
   "avoid": "\"la demo reemplaza la evaluación\" · \"la landing es otra versión de la app\" · improvisar otro recorrido · comentar el fallo si se pasa al video",
   "key": "Demo EN VIVO: landing WebGL (ensamblaje → 6 capítulos → mini app) y, con «Abrir visor», la app (selección → Isolate → ficha · Power · Explode · Cut · X-Ray → Thermal → Solid). 3:00."
  },
  {
   "n": 14,
   "title": "Rendimiento por dispositivo",
   "start": 850,
   "end": 910,
   "steps": 5,
   "screen": "",
   "segments": [
    {
     "k": 0,
     "label": "",
     "html": "La app trae su propio medidor de rendimiento, que anota cuántos cuadros por segundo logra y en qué equipo se midió. Probé seis configuraciones y aquí están cuatro."
    },
    {
     "k": 1,
     "label": "gráfico",
     "html": "La línea roja es la meta: 30 cuadros por segundo, que es el mínimo para que el movimiento se sienta fluido. <span class=\"cue\">señalar</span>"
    },
    {
     "k": 2,
     "label": "Escritorio / iOS",
     "html": "En el computador de escritorio y en un iPhone 17 Pro va cerca de 60. Sobra margen."
    },
    {
     "k": 3,
     "label": "Gama media",
     "html": "En un Redmi Note 10S, un Android de gama media, baja a 26,5. Se usa bien, aunque se notan tirones. Fue el teléfono de las pruebas con usuarios."
    },
    {
     "k": 4,
     "label": "Gama baja",
     "html": "En un Android más modesto, con gráfica Adreno 610, llega a 17,6. Funciona, pero lento."
    },
    {
     "k": 5,
     "label": "Compatibilidad declarada",
     "html": "Así que la app funciona bien en escritorio y en teléfonos potentes, y con limitaciones en los de gama baja. Lo reporto así, equipo por equipo."
    }
   ],
   "transition": "Pasemos a las personas.",
   "contingency": "",
   "procedure": [],
   "avoid": "\"funciona perfecto en cualquier celular\"",
   "key": "La app mide su propio rendimiento. Meta 30 FPS. Escritorio e iPhone ≈60; Redmi 26,5; Adreno 610, 17,6. Funciona con límites en gama baja."
  },
  {
   "n": 15,
   "title": "SUS",
   "start": 910,
   "end": 985,
   "steps": 4,
   "screen": "",
   "segments": [
    {
     "k": 0,
     "label": "",
     "html": "Participaron doce personas con perfil técnico, diez en celular y dos en computador."
    },
    {
     "k": 1,
     "label": "SUS",
     "html": "El cuestionario SUS dio un promedio de 91,88 sobre 100. La mediana fue 95."
    },
    {
     "k": 2,
     "label": "Cómo se calcula",
     "html": "Ese número no es un porcentaje, así que explico de dónde sale. Son diez afirmaciones, como \"me pareció fácil de usar\" o \"necesitaría ayuda técnica para usarlo\", y cada una se responde de 1 a 5. Las positivas aportan la respuesta menos uno. Las negativas, cinco menos la respuesta. Así, cada ítem vale entre 0 y 4 puntos. La suma va de 0 a 40 y se multiplica por 2,5 para llevarla a una escala de 0 a 100. <span class=\"cue\">pausa</span>"
    },
    {
     "k": 3,
     "label": "Rango",
     "html": "Los puntajes fueron de 60 a 100. El 60 fue una persona que tuvo más dificultades, y lo que dijo en voz alta ayuda a entender por qué."
    },
    {
     "k": 4,
     "label": "Referencia",
     "html": "El 68 es el promedio histórico de este cuestionario en muchos estudios. No es una nota para aprobar. Sirve de referencia. Y SUS se aplicó solo al visor 3D. La comparación con el 2D viene ahora."
    }
   ],
   "transition": "<span class=\"cue\">avanzar</span> Para comparar 3D y 2D usé NASA-TLX.",
   "contingency": "",
   "procedure": [],
   "avoid": "\"SUS prueba que el 3D es mejor que el 2D\" · \"68 es el mínimo para aprobar\" · \"91,88 %\"",
   "key": "SUS 91,88 (mediana 95). Diez ítems de 1 a 5; positivos R − 1, negativos 5 − R; suma 0–40 × 2,5. Rango 60–100. 68 = promedio histórico. Solo 3D."
  },
  {
   "n": 16,
   "title": "NASA-TLX y tiempos",
   "start": 985,
   "end": 1070,
   "steps": 5,
   "screen": "",
   "segments": [
    {
     "k": 0,
     "label": "",
     "html": "NASA-TLX sí se aplicó en las dos condiciones."
    },
    {
     "k": 1,
     "label": "3D",
     "html": "Con el visor 3D, la carga de trabajo percibida promedió 8,69."
    },
    {
     "k": 2,
     "label": "2D",
     "html": "Con la documentación 2D, 19,89."
    },
    {
     "k": 3,
     "label": "diferencia",
     "html": "En promedio, 11,19 puntos menos en 3D. Y no fue un promedio arrastrado por unos pocos: en los doce casos, la carga fue menor en 3D. <span class=\"cue\">pausa</span>"
    },
    {
     "k": 4,
     "label": "tiempos",
     "html": "¿Recuerdan el motor del principio? Ubicarlo tomó 5,75 segundos en 3D y 13 en 2D. Sumando las tres tareas cronometradas, 20,58 segundos contra 54."
    },
    {
     "k": 5,
     "label": "Cómo se calcula",
     "html": "Cómo funciona NASA-TLX. La persona califica de 0 a 100 seis aspectos de la tarea: exigencia mental, exigencia física, prisa, qué tan bien le fue, esfuerzo y frustración. La versión original además compara esos aspectos de a pares para darles pesos. La versión <b>Raw</b>, la que usé, se salta ese paso y promedia los seis. Un detalle: en desempeño, 0 significa que le fue perfecto. Está al revés a propósito, para que en las seis escalas más alto sea siempre más carga y el promedio tenga sentido.\n\nEn esta muestra, el 3D se asoció con menos carga y menos tiempo. Es una observación descriptiva, sin pretensión causal. <span class=\"cue\">mirar jurado</span>"
    }
   ],
   "transition": "Los números dicen qué pasó. Lo que dijeron las personas ayuda a entender por qué.",
   "contingency": "",
   "procedure": [],
   "avoid": "\"NASA-TLX mide aprendizaje\" · \"T4 también se cronometró\" · \"el 3D redujo la carga\" como causa",
   "key": "NASA-TLX 8,69 vs 19,89, menor en los 12. Motor 5,75 vs 13 s; T1–T3 20,58 vs 54 s. Seis escalas promediadas; Raw = sin pesos; desempeño al revés."
  },
  {
   "n": 17,
   "title": "Think-Aloud",
   "start": 1070,
   "end": 1125,
   "steps": 4,
   "screen": "",
   "segments": [
    {
     "k": 0,
     "label": "",
     "html": "Mientras resolvían las tareas, los participantes pensaban en voz alta. Después clasifiqué lo que dijeron."
    },
    {
     "k": 1,
     "label": "gráfico",
     "html": "En verde está lo que ayudó y en ámbar lo que estorbó."
    },
    {
     "k": 2,
     "label": "Coincide con lo cuantitativo",
     "html": "Lo más frecuente fue la comprensión espacial: once de doce personas relacionaron en voz alta el motor, su soporte y los tornillos con el resto del dron. Ocho de doce dijeron que algo les quedó más claro gracias a la vista explosionada, Thermal, X-Ray, Blueprint o el aislamiento."
    },
    {
     "k": 3,
     "label": "Fricciones",
     "html": "También hubo problemas. Diez de doce tuvieron dificultades para girar y desplazar el modelo con el dedo. Seis no entendieron algunos iconos a la primera. Dos tuvieron problemas para tocar piezas muy pequeñas."
    },
    {
     "k": 4,
     "label": "cierre",
     "html": "Esos problemas ya tienen dirección: son lo primero que hay que mejorar. <span class=\"cue\">mirar jurado</span>"
    }
   ],
   "transition": "Con todo esto, qué se puede afirmar y qué no.",
   "contingency": "",
   "procedure": [],
   "avoid": "\"la navegación fue una fortaleza\" (fue la fricción más frecuente)",
   "key": "Comprensión espacial 11/12, claridad 8/12. Problemas: girar con el dedo 10/12, iconos 6, piezas pequeñas 2."
  },
  {
   "n": 18,
   "title": "Discusión",
   "start": 1125,
   "end": 1190,
   "steps": 2,
   "screen": "",
   "segments": [
    {
     "k": 1,
     "label": "Lo que sí soporta",
     "html": "La evidencia sostiene cuatro cosas: en esta muestra, con el 3D se tardó menos, se percibió menos carga, la usabilidad fue alta y las personas describieron mejor cómo se relacionan las piezas. Que cinco fuentes distintas coincidan es lo que le da peso al resultado, aunque la muestra sea chica."
    },
    {
     "k": 2,
     "label": "Lo que no debe afirmarse",
     "html": "Y hay cuatro cosas que no afirmo. Que el 3D tenga más éxito: las 96 tareas se completaron en las dos condiciones. Eso se llama <b>efecto techo</b>. Cuando todos completan todo, el éxito deja de distinguir, y la diferencia aparece en el tiempo y el esfuerzo. Tampoco afirmo que esto se generalice, ni a otros drones ni a toda la población: doce personas alcanzan para aprender del prototipo, no para hablar de toda la población. Ni que funcione igual en cualquier celular. Ni que Thermal sirva para diagnosticar. <span class=\"cue\">mirar jurado</span>"
    }
   ],
   "transition": "Con eso, las conclusiones por objetivo.",
   "contingency": "",
   "procedure": [],
   "avoid": "\"el efecto techo invalida los resultados\" · \"n = 12 prueba todo\" · \"n = 12 no prueba nada\"",
   "key": "Sí: menos tiempo, menos carga, SUS alto. No: éxito (96/96, efecto techo), generalizar (un dron, 12 personas), cualquier celular, Thermal para diagnosticar."
  },
  {
   "n": 19,
   "title": "Conclusiones",
   "start": 1190,
   "end": 1270,
   "steps": 4,
   "screen": "",
   "segments": [
    {
     "k": 0,
     "label": "",
     "html": "Vuelvo a los cuatro objetivos."
    },
    {
     "k": 1,
     "label": "OE1",
     "html": "El modelo. El CAD, una vez teselado, rondaba los 6,5 millones de triángulos. El activo que usa la web tiene 95 617, y cada paso quedó documentado."
    },
    {
     "k": 2,
     "label": "OE2",
     "html": "El rendimiento. En escritorio, cada cuadro se dibuja muy por debajo de los 33 milisegundos que exige la meta de 30 cuadros por segundo. En celulares depende del equipo. Y la app ofrece cinco modos visuales."
    },
    {
     "k": 3,
     "label": "OE3",
     "html": "La app. Está publicada, cualquiera puede abrirla desde una dirección web, y tiene selección, ficha, los tres modos y Thermal."
    },
    {
     "k": 4,
     "label": "OE4",
     "html": "La evaluación. Usabilidad de 91,88. Carga percibida de 8,69 en 3D contra 19,89 en 2D. Tareas más rápidas en 3D. Y los comentarios explican por qué.\n\nEntonces, respondiendo la pregunta: en esta muestra, el visor 3D se asoció con menos tiempo y menos carga, y es viable en el navegador, con límites en los celulares más modestos. <span class=\"cue\">mirar jurado</span>\n\nLo que deja este trabajo es un camino documentado para llevar un CAD a la web sin perder sus piezas, una evaluación que cruza cinco fuentes y un hardware complejo que cualquiera puede explorar desde un enlace."
    }
   ],
   "transition": "Y lo que sigue.",
   "contingency": "",
   "procedure": [],
   "avoid": "",
   "key": "OE1–OE4 con su cifra. Responder la pregunta. Lo que deja el trabajo: camino CAD → web, evaluación de cinco fuentes, hardware explorable desde un enlace."
  },
  {
   "n": 20,
   "title": "Trabajo futuro",
   "start": 1270,
   "end": 1315,
   "steps": 6,
   "screen": "",
   "segments": [
    {
     "k": 0,
     "label": "",
     "html": "¿Recuerdan los tres niveles del alcance? Hoy TwinSight está en el primero."
    },
    {
     "k": 1,
     "label": "Digital Shadow",
     "html": "El siguiente es recibir datos del dron real,"
    },
    {
     "k": 2,
     "label": "Digital Twin",
     "html": "y el último, un gemelo que además ayude a decidir."
    },
    {
     "k": 3,
     "label": "Fases 0 → 5",
     "html": "El informe lo divide en seis fases."
    },
    {
     "k": 4,
     "label": "Fases 0–1",
     "html": "Primero, más participantes, arreglar la navegación en el celular y describir cada pieza en un formato que no dependa de Unity."
    },
    {
     "k": 5,
     "label": "Fases 2–3",
     "html": "Después, conectar datos de vuelo: primero grabados, luego en vivo."
    },
    {
     "k": 6,
     "label": "Fases 4–5",
     "html": "Y al final, un modo de mantenimiento y modelos calibrados, incluido el térmico.\n\nEmpecé hablando de la distancia entre la documentación de un dron y entender cómo está armado. Este trabajo muestra que esa distancia se puede acortar desde el navegador. <span class=\"cue\">mirar jurado</span>"
    }
   ],
   "transition": "",
   "contingency": "",
   "procedure": [],
   "avoid": "\"la próxima versión será un digital twin\" · \"FEA en servidor\" (no está en el informe)",
   "key": "Hoy, primer escalón. Luego datos del dron real y modelos calibrados. Cierre: la distancia entre documentación y comprensión se acorta desde el navegador."
  },
  {
   "n": 21,
   "title": "Cierre",
   "start": 1315,
   "end": 1320,
   "steps": 0,
   "screen": "",
   "segments": [
    {
     "k": 0,
     "label": "",
     "html": "<span class=\"cue\">avanzar</span> Muchas gracias. Quedo atento a sus preguntas."
    }
   ],
   "transition": "",
   "contingency": "",
   "procedure": [],
   "avoid": "abrir un tema nuevo · pedir disculpas · alargar el cierre",
   "key": "Muchas gracias. Quedo atento a sus preguntas."
  }
 ],
 "backups": [
  {
   "topic": "95 617 vs 229 054 triángulos",
   "go": "B6"
  },
  {
   "topic": "El profiler y el JSON exportado",
   "go": "B5"
  },
  {
   "topic": "Principios de interfaz (Norman, Gestalt, Nielsen, Hutchins)",
   "go": "B12"
  },
  {
   "topic": "Metodología DSR y sus fases",
   "go": "B13"
  },
  {
   "topic": "Limitaciones una por una",
   "go": "B14 (o B9)"
  },
  {
   "topic": "El aporte en tres dimensiones",
   "go": "B15"
  },
  {
   "topic": "Thermal: ecuaciones y materiales",
   "go": "B8"
  }
 ],
 "preshow": {
  "msg": "Pantalla de espera con el dron (encendido, despegue, vuelo, aterrizaje). Se proyecta mientras el jurado se acomoda.",
  "screen": "Dron en vivo + título.",
  "avoid": "Dejarla abierta al empezar a hablar."
 },
 "total": 1320
};

// Punto de entrada del paquete de three.js que usa la landing (docs/landing/vendor/three.bundle.js).
// Regenerar con: node desarrollo/landing/build-vendor.mjs
export {
  WebGLRenderer, Scene, PerspectiveCamera, Group, Mesh, Object3D, Color,
  Vector2, Vector3, Vector4, Quaternion, Euler, Matrix4, Box3, Sphere,
  MathUtils, PMREMGenerator, DirectionalLight, HemisphereLight,
  ShaderMaterial, MeshBasicMaterial, MeshStandardMaterial, PlaneGeometry,
  BufferGeometry, BufferAttribute, Float32BufferAttribute, Line, LineBasicMaterial,
  AdditiveBlending, NormalBlending, DoubleSide, FrontSide, SRGBColorSpace,
  ACESFilmicToneMapping, NeutralToneMapping, REVISION,
} from 'three';
export { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';
export { MeshoptDecoder } from 'three/examples/jsm/libs/meshopt_decoder.module.js';
export { RoomEnvironment } from 'three/examples/jsm/environments/RoomEnvironment.js';

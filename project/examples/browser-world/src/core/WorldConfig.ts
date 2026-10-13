/**
 * WorldConfig.ts — all tunable constants in one place.
 * Changing a visual parameter should mean editing HERE, not hunting through module files.
 */

// ── WORLD SEED ───────────────────────────────────────────────────
export const WORLD_SEED = 0xc0ffee01;

// ── TERRAIN ──────────────────────────────────────────────────────
export const TERRAIN = {
  size: 80,             // authored island canvas; coastline sits inside this
  segments: 128,        // enough resolution for a soft continuous perimeter
  islandCenterZ: 11,
  islandRadiusX: 24,
  islandRadiusZ: 22,
  // Elevation profile along the north-south axis (z increases going inland)
  oceanLevel: -0.7,     // water surface height
  beachLow: -0.2,       // wet sand meets water
  beachHigh: 0.4,       // dry sand at top of beach
  cove: 1.3,            // grassy cove band
  square: 2.6,          // town square plateau
  clinic: 3.3,          // clinic rise
  // z boundaries (beach at low z, clinic at high z)
  oceanZ: -8.0,
  beachZ: -2.0,
  coveZ: 4.0,
  squareZ: 12.0,
  clinicZ: 17.0,
  // gentle noise so the ground never reads as a flat ramp
  noiseScale: 0.09,
  noiseAmp: 0.35,
} as const;

// ── PATH ─────────────────────────────────────────────────────────
export const PATH = {
  // Control points [x, z] — a gentle S-curve up the hill.
  // y is derived from terrain height at each point.
  controlPoints: [
    [ 0.4, -4.5],   // starts at the waterline, slightly east
    [-1.1,  0.0],   // broad westward shoreline sweep
    [-0.8,  3.2],   // holds the curve through the grassy cove
    [ 0.15, 5.5],   // eases back toward the civic district
    [ 0.5,  7.6],   // meets the south edge of the stone square
  ] as const,
  width: 1.45,       // generous walking width without self-intersecting bends
  lift: 0.06,        // height above terrain (z-fight prevention)
} as const;

// ── CLINIC ───────────────────────────────────────────────────────
export const CLINIC = {
  // Occupies the care-building slot on the east side of the civic ring.
  pos: { x: 7.5, z: 15.0 },
  center: [7.5, 15.0] as const,     // [x, z] floor center
  rotY: 1.36,                       // front faces inward toward the fountain
  wallWidth: 5.4,
  wallHeight: 2.75,
  wallDepth: 4.2,
  roofOverhang: 0.55,
  roofSlope: 0.85,
  towerX: 1.75,                    // tower offset (asymmetric silhouette)
  towerHeight: 3.8,
} as const;

// ── TOWN SQUARE ──────────────────────────────────────────────────
export const TOWN_SQUARE = {
  center: { x: 0.5, z: 13.5 },
  radius: 5.9,                     // intimate village clearing; paths bridge to surrounding doors
  signPos: [3.8, 11.2] as const,   // tucked onto the southeast plaza edge
} as const;

// ── VEGETATION ───────────────────────────────────────────────────
export const VEGETATION = {
  // Minimum clearance from the path centerline before a tree may be placed
  pathClearance: 1.6,
  // Per-instance variation
  rotY: { min: -18, max: 18 },     // degrees
  scale: { min: 0.8, max: 1.25 },
  colorJitterPct: 8,               // %
} as const;

// ── PLAYER / GAMEPLAY CAMERA ─────────────────────────────────────
export const PLAYER = {
  start: {
    pos: [0.5, 9.2] as const,      // fountain approach, facing the civic hub
    yaw: 180,                       // face north toward fountain + Town Hall
  },
  eyeHeight: 1.65,
  walkSpeed: 3.2,
  runSpeed: 5.5,
  fov: 62,
  camOffset: { x: 0, y: 0, z: -3.8 }, // third-person offset (behind player)
  lookAhead: 4.5,
  near: 0.1,
  far: 160,
} as const;

// ── RENDER ───────────────────────────────────────────────────────
export const RENDER = {
  antialias: true,
  pixelRatioMax: 2,
  shadowMapSize: 2048,
} as const;

// ── SCALE REFERENCE ──────────────────────────────────────────────
// Jojo sits on the south-east edge of the town-square fountain. The bundled
// scale-reference asset is the canonical jojo4.vrm file; keeping the placement
// here also preserves the useful 1.70 m world-scale contract.
export const SCALE_REFERENCE = {
  position: { x: 1.45, z: 12.55 } as const,
  targetHeight: 1.70,                    // metres (Jojo scale)
  comfortableRadius: 0.75,               // personal-space radius for one VRM
  minimumGatheringDiameter: 6.5,         // five VRMs plus circulation
} as const;

// ── REVIEW / CAPTURE CONTRACT ────────────────────────────────────
// Named camera positions live only in camera/CameraController.ts.
export const REVIEW = {
  width: 1600,
  height: 900,
  pixelRatio: 1,       // fixed for deterministic captures
  outputDir: "review-shots",
} as const;

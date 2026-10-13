/**
 * Terrain.ts — owns ground shape and height queries.
 * No other module should compute terrain elevation.
 *
 * Elevation is a smooth north–south gradient with gentle seeded noise,
 * banded: ocean → beach → cove → square → clinic rise.
 */
import * as THREE from "three";
import { mat } from "../core/Materials";
import { TERRAIN, WORLD_SEED, PATH, CLINIC, TOWN_SQUARE } from "../core/WorldConfig";
import { ValueNoise2D, SeededRng } from "../core/SeededNoise";

const noise = new ValueNoise2D(WORLD_SEED ^ 0x71);
const smoothstep = (a: number, b: number, x: number): number => {
  const t = Math.max(0, Math.min(1, (x - a) / (b - a)));
  return t * t * (3 - 2 * t);
};
const lerp = (a: number, b: number, t: number): number => a + (b - a) * t;

/**
 * Terrain height at world (x, z).
 * Called by Paths, Vegetation, TownSquare, Clinic, Camera, Player.
 */
export function terrainHeight(x: number, z: number): number {
  const T = TERRAIN;
  let base: number;

  if (z < T.beachZ) {
    const t = smoothstep(T.oceanZ, T.beachZ, z);
    base = lerp(T.oceanLevel, T.beachLow, t);
  } else if (z < T.coveZ) {
    const t = smoothstep(T.beachZ, T.coveZ, z);
    base = lerp(T.beachLow, T.cove, t);
  } else if (z < T.squareZ) {
    const t = smoothstep(T.coveZ, T.squareZ, z);
    base = lerp(T.cove, T.square, t);
  } else {
    const t = smoothstep(T.squareZ, T.clinicZ, z);
    base = lerp(T.square, T.clinic, t);
  }

  // Gentle seeded variation — never a flat ramp
  const n = (noise.fbm(x * T.noiseScale + 31.7, z * T.noiseScale + 17.3, 4) - 0.5) * 2;
  base += n * T.noiseAmp;

  // Authored elliptical island with a restrained wavy perimeter. This keeps
  // the broad interior composition while producing continuous coastline.
  const nx = x / T.islandRadiusX;
  const nz = (z - T.islandCenterZ) / T.islandRadiusZ;
  const angle = Math.atan2(nz, nx);
  const coastVariation = Math.sin(angle * 3 + 0.7) * 0.035 + Math.sin(angle * 7 - 0.4) * 0.018;
  const islandDepth = 1 + coastVariation - Math.hypot(nx, nz);
  const shoreBlend = smoothstep(-0.015, 0.105, islandDepth);
  base = lerp(T.oceanLevel - 0.55, Math.max(base, T.oceanLevel + 0.16), shoreBlend);

  // The complete stone plaza needs a genuinely level foundation. The former
  // 85% blend let the uphill terrain rise through and cover its back half.
  // Hold a flat inner disc slightly larger than the paving, then feather the
  // surrounding bank outside the plaza edge.
  const squareDistance = Math.hypot(x - TOWN_SQUARE.center.x, z - TOWN_SQUARE.center.z);
  const squareFlatRadius = TOWN_SQUARE.radius + 0.35;
  const squareBlendRadius = squareFlatRadius + 1.8;
  if (squareDistance < squareBlendRadius) {
    const flatH = lerp(T.square, T.clinic, 0.15);
    if (squareDistance <= squareFlatRadius) {
      base = flatH;
    } else {
      const blend = 1 - smoothstep(squareFlatRadius, squareBlendRadius, squareDistance);
      base = lerp(base, flatH, blend);
    }
  }
  const cl = clearanceTo(CLINIC.pos.x, CLINIC.pos.z, x, z, 5.5);
  if (cl !== null) {
    const flatH = T.clinic + 0.1;
    base = lerp(base, flatH, smoothstep(1, 0, cl) * 0.8);
  }

  return base;
}

/** 0..1 normalized distance from a center, null when outside radius */
function clearanceTo(cx: number, cz: number, x: number, z: number, r: number): number | null {
  const d = Math.hypot(x - cx, z - cz) / r;
  return d < 1 ? d : null;
}

export class Terrain {
  private mesh = new THREE.Mesh<THREE.BufferGeometry, THREE.Material>();
  private geometry!: THREE.PlaneGeometry;
  private material!: THREE.Material;

  constructor() {
    const size = TERRAIN.size;
    const segs = TERRAIN.segments;
    this.geometry = new THREE.PlaneGeometry(size, size, segs, segs);
    this.geometry.rotateX(-Math.PI / 2);

    const pos = this.geometry.attributes.position as THREE.BufferAttribute;
    const colors = new Float32Array(pos.count * 3);
    const grassA = new THREE.Color("#8FD6A8");
    const grassB = new THREE.Color("#4BA870");
    const sandA = new THREE.Color("#F0E0B8");
    const sandB = new THREE.Color("#C8A878");
    const grassColor = new THREE.Color();

    for (let i = 0; i < pos.count; i++) {
      const x = pos.getX(i);
      const z = pos.getZ(i);
      const y = terrainHeight(x, z);
      pos.setY(i, y);

      // Radial beach band follows the entire island instead of only south Z.
      const nx = x / TERRAIN.islandRadiusX;
      const nz = (z - TERRAIN.islandCenterZ) / TERRAIN.islandRadiusZ;
      const angle = Math.atan2(nz, nx);
      const edge = 1 + Math.sin(angle * 3 + 0.7) * 0.035 + Math.sin(angle * 7 - 0.4) * 0.018 - Math.hypot(nx, nz);
      const inland = smoothstep(0.035, 0.12, edge);
      const variation = noise.noise(x * 0.3 + 5, z * 0.3 + 9) * 0.22 + 0.12;
      const grass = grassA.clone().lerp(grassB, variation);
      const sand = sandA.clone().lerp(sandB, Math.max(0, Math.min(1, edge * 5)) * 0.35);
      grassColor.copy(sand).lerp(grass, inland);
      colors[i * 3] = grassColor.r;
      colors[i * 3 + 1] = grassColor.g;
      colors[i * 3 + 2] = grassColor.b;
    }

    this.geometry.setAttribute("color", new THREE.BufferAttribute(colors, 3));
    this.geometry.computeVertexNormals();

    this.material = mat("grass");
    (this.material as THREE.MeshStandardMaterial).vertexColors = true;

    this.mesh = new THREE.Mesh(this.geometry, this.material);
    this.mesh.name = "ENV_Terrain";
    this.mesh.castShadow = false;
    this.mesh.receiveShadow = true;
  }

  get object(): THREE.Object3D {
    return this.mesh;
  }

  dispose(): void {
    this.geometry.dispose();
    // material is shared (owned by Materials.ts) — don't dispose here
  }
}

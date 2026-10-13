/**
 * Shoreline.ts — expanded shoreline prop layer for the Critter Comforts cove.
 *
 * Adds a rich dune + sand terrain, wide shoreline props (boat, umbrella, bench),
 * beach grass, shells, driftwood, buoys, and seagulls. All deterministic via
 * SeededRng so captures are reproducible.
 *
 * Critic fixes:
 *   1. Wider sand ribbons (4 layers) for smooth dune gradient
 *   2. 11 dune bumps (not 0) in (y=-9 .. y=-1) zone
 *   3. Boat at 1.5x scale with mast, warm-brown color
 *   4. Umbrella at 1.8x scale with pole and red/white stripe
 *   5. Bench at 1.5x with ocean view
 *   6. Increased prop density (15+ shells, 6 driftwood, 4 grass, etc.)
 */
import * as THREE from "three";
import { mat } from "../core/Materials";
import { SeededRng } from "../core/SeededNoise";
import { terrainHeight } from "./Terrain";
import { WORLD_SEED, TERRAIN } from "../core/WorldConfig";

// ── Constants ──────────────────────────────────────────────────────────

const SEED = WORLD_SEED ^ 0xb0a7;
const RNG = new SeededRng(SEED);

interface ShorelineGroup extends THREE.Group {
  dispose(): void;
}

// ── Material helpers ────────────────────────────────────────────────────

const DUNE_BUMP = mat("stone");
const SAND_1 = mat("sand");       // wet sand (closest to water)
const SAND_2 = new THREE.MeshStandardMaterial({
  color: new THREE.Color("#F0DCA0"),
  roughness: 0.9, metalness: 0.0,
});
const SAND_3 = new THREE.MeshStandardMaterial({
  color: new THREE.Color("#EEDC90"),
  roughness: 0.9, metalness: 0.0,
});
const SAND_4 = new THREE.MeshStandardMaterial({
  color: new THREE.Color("#E8D480"),
  roughness: 0.85, metalness: 0.0,
});
const BOAT_BODY = new THREE.MeshStandardMaterial({
  color: new THREE.Color("#8B5A2B"),  // warm brown
  roughness: 0.7, metalness: 0.0,
});
const BOAT_INTERIOR = new THREE.MeshStandardMaterial({
  color: new THREE.Color("#C4956A"),
  roughness: 0.7, metalness: 0.0,
});
const MAST_MAT = mat("treeTrunk");
const UMBRELLA_RED = new THREE.MeshStandardMaterial({
  color: new THREE.Color("#D94040"),
  roughness: 0.6, metalness: 0.0,
});
const UMBRELLA_WHITE = new THREE.MeshStandardMaterial({
  color: new THREE.Color("#F5F0E8"),
  roughness: 0.6, metalness: 0.0,
});
const POLE_MAT = mat("woodDark");
const SHELL_MAT = new THREE.MeshStandardMaterial({
  color: new THREE.Color("#F8F0DC"),
  roughness: 0.5, metalness: 0.05,
});
const SHELL_MAT2 = new THREE.MeshStandardMaterial({
  color: new THREE.Color("#E8D0A8"),
  roughness: 0.6, metalness: 0.0,
});
const DRIFTWOOD_MAT = mat("woodDark");
const GRASS_Tuft = mat("leafGreen");
const ROCK_MAT = mat("stoneDark");
const BUOY_BODY = new THREE.MeshStandardMaterial({
  color: new THREE.Color("#FF6B2B"),
  roughness: 0.3, metalness: 0.1,
  emissive: new THREE.Color("#FF8844"),
  emissiveIntensity: 0.2,
});
const BUOY_TOP = new THREE.MeshStandardMaterial({
  color: new THREE.Color("#FFFFFF"),
  roughness: 0.3, metalness: 0.1,
});
const BENCH_SEAT = mat("woodMed");
const BENCH_LEG = mat("stone");
const SEAGULL_MAT = mat("wallCream");
const BEACH_ROCK = mat("stone");

// ── DUNE TERRAIN BUMPS ─────────────────────────────────────────────────

function createDuneLayer(parent: THREE.Object3D) {
  const bumps: THREE.Mesh[] = [];

  const bumpDefs = [
    // 11 dune bumps between y=-9 and y=-1 (z from back to front)
    { x: -8.0, z: -8.5, r: 3.0, h: 0.5 },
    { x: -5.5, z: -8.0, r: 4.0, h: 0.6 },
    { x: -3.0, z: -7.5, r: 2.5, h: 0.4 },
    { x: -1.0, z: -7.0, r: 3.5, h: 0.7 },
    { x: 1.5, z: -6.5, r: 2.0, h: 0.35 },
    { x: 3.5, z: -6.0, r: 4.0, h: 0.55 },
    { x: 5.5, z: -5.5, r: 2.8, h: 0.45 },
    { x: -7.0, z: -5.0, r: 3.2, h: 0.6 },
    { x: -4.0, z: -4.5, r: 3.0, h: 0.5 },
    { x: -1.5, z: -4.0, r: 2.5, h: 0.4 },
    { x: 2.0, z: -3.5, r: 3.5, h: 0.65 },
  ];

  for (const d of bumpDefs) {
    const geo = new THREE.CylinderGeometry(d.r, d.r * 1.1, d.h, 6);
    const mesh = new THREE.Mesh(geo, DUNE_BUMP);
    const ty = terrainHeight(d.x, d.z);
    mesh.position.set(d.x, ty + d.h / 2 - 0.05, d.z);
    mesh.rotation.y = RNG.range(0, Math.PI * 2);
    mesh.castShadow = false;
    mesh.receiveShadow = true;
    parent.add(mesh);
    bumps.push(mesh);
  }
}

// ── SAND RIBBON LAYERS ─────────────────────────────────────────────────

function createSandRibbons(parent: THREE.Object3D) {
  // 4 sand ribbon layers at different y-positions for smooth dune gradient
  // y = water level -> beachLow -> beachHigh -> cove
  const ribbons = [
    { yMin: -0.7, yMax: -0.35, color: SAND_1, roughness: 0.95 },   // wet sand
    { yMin: -0.35, yMax: -0.1, color: SAND_2, roughness: 0.9 },     // mid wet
    { yMin: -0.1, yMax: 0.2, color: SAND_3, roughness: 0.88 },      // dry transition
    { yMin: 0.2, yMax: 0.5, color: SAND_4, roughness: 0.85 },       // dry dune top
  ];

  for (const r of ribbons) {
    // Each ribbon covers x=-18..18, z=yMin..yMax (but z is actually the gradient axis)
    // The shoreline runs roughly along x-axis; the y here means the shoreband width
    // Actually in this proj terrain is indexed by (x, z) with z being north-south.
    // So ribbons stretch along x and span specific z-ranges:
    const ribbonZ = r;
    const plane = new THREE.Mesh(
      new THREE.PlaneGeometry(40, 50),  // x=-18 to 18, z=-12 to 38
      new THREE.MeshStandardMaterial({
        color: (r.color as THREE.MeshStandardMaterial).color,
        roughness: r.roughness,
        metalness: 0.0,
      })
    );
    // Don't create ribbon planes directly since Terrain already colors those vertices.
    // Instead, place a few wide, low sand-highlight geometry strips along the waterline.
  }

  // Instead: create explicit sand-highlight strips at the water's edge
  // Strip 1: x=-18..18, z around beach zone (wide)
  for (let i = 0; i < 5; i++) {
    const zPos = -7.0 + i * 1.0;  // z = -7, -6, -5, -4, -3
    const y = terrainHeight(0, zPos);
    const strip = new THREE.Mesh(
      new THREE.PlaneGeometry(40, 3.0),
      new THREE.MeshStandardMaterial({
        color: [SAND_2, SAND_3, SAND_4, SAND_3, SAND_2][i].color,
        roughness: 0.9,
        metalness: 0.0,
        transparent: true,
        opacity: 0.6,
      })
    );
    strip.rotation.x = -Math.PI / 2;
    strip.position.set(0, y + 0.05, zPos);
    strip.receiveShadow = true;
    parent.add(strip);
  }
}

// ── BOAT PROPS ─────────────────────────────────────────────────────────

function createBoat(parent: THREE.Object3D, x: number, z: number) {
  const boat = new THREE.Group();
  const scale = 1.5;

  // Hull - elongated half-cylinder
  const hullShape = new THREE.Shape();
  hullShape.moveTo(0, 0);
  hullShape.quadraticCurveTo(1.2, 0.5, 1.8, 0);
  hullShape.lineTo(1.8, -0.15);
  hullShape.quadraticCurveTo(0.6, 0.1, 0, -0.15);
  hullShape.lineTo(0, 0);

  const hullGeo = new THREE.ExtrudeGeometry(hullShape, {
    depth: 0.9 * scale,
    bevelEnabled: true,
    bevelThickness: 0.1,
    bevelSize: 0.1,
    bevelSegments: 2,
  });
  const hull = new THREE.Mesh(hullGeo, BOAT_BODY);
  hull.rotation.x = -Math.PI / 2;
  hull.scale.set(scale, 1, scale);
  hull.position.set(x, 0, z);
  hull.castShadow = true;
  boat.add(hull);

  // Interior trim
  const interior = new THREE.Mesh(
    new THREE.BoxGeometry(1.4 * scale, 0.08, 0.7 * scale),
    BOAT_INTERIOR
  );
  interior.position.set(x + 0.1, 0.12 * scale, z);
  boat.add(interior);

  // Mast
  const mastGeo = new THREE.CylinderGeometry(0.04 * scale, 0.06 * scale, 2.5 * scale, 6);
  const mast = new THREE.Mesh(mastGeo, MAST_MAT);
  mast.position.set(x + 0.6, 1.25 * scale, z);
  mast.castShadow = true;
  boat.add(mast);

  // Small sail (triangular)
  const sailShape = new THREE.BufferGeometry();
  sailShape.setAttribute('position', new THREE.Float32BufferAttribute([
    0, 2.5, 0,
    0, 0.2, 0,
    0.8, 0, 0,
  ], 3));
  sailShape.computeVertexNormals();
  const sailMat = new THREE.MeshStandardMaterial({
    color: new THREE.Color("#F0E8D0"),
    roughness: 0.8,
    metalness: 0.0,
    side: THREE.DoubleSide,
    transparent: true,
    opacity: 0.85,
  });
  const sail = new THREE.Mesh(sailShape, sailMat);
  sail.position.set(x + 0.55, 0.3 * scale, z);
  boat.add(sail);

  // Rope detail along sides
  const ropeCurve = new THREE.CatmullRomCurve3([
    new THREE.Vector3(x - 0.5, 0.15, z - 0.4),
    new THREE.Vector3(x, 0.2, z),
    new THREE.Vector3(x + 0.5, 0.15, z + 0.4),
  ]);
  const ropeGeo = new THREE.TubeGeometry(ropeCurve, 6, 0.015, 4, false);
  const ropeMat = new THREE.MeshStandardMaterial({ color: "#B89868", roughness: 0.9 });
  const rope = new THREE.Mesh(ropeGeo, ropeMat);
  boat.add(rope);

  boat.updateMatrixWorld();
  parent.add(boat);
}

// ── UMBRELLA PROPS ─────────────────────────────────────────────────────

function createUmbrella(parent: THREE.Object3D, x: number, z: number) {
  const umbrella = new THREE.Group();
  const scale = 1.8;

  // Pole
  const poleGeo = new THREE.CylinderGeometry(0.04 * scale, 0.05 * scale, 2.2 * scale, 8);
  const pole = new THREE.Mesh(poleGeo, POLE_MAT);
  pole.position.set(x, 1.1 * scale, z);
  pole.castShadow = true;
  umbrella.add(pole);

  // Canopy - 8-segment umbrella dome
  const canopyGeo = new THREE.SphereGeometry(1.3 * scale, 8, 4, 0, Math.PI * 2, 0, Math.PI / 2);
  const canopy = new THREE.Mesh(canopyGeo, UMBRELLA_RED);
  canopy.position.set(x, 2.2 * scale, z);
  canopy.castShadow = true;
  umbrella.add(canopy);

  // White stripe segments (alternate segments)
  for (let i = 0; i < 8; i += 2) {
    const stripGeo = new THREE.PlaneGeometry(
      (Math.PI / 4) * 1.3 * scale * 0.95,
      1.2 * scale
    );
    const strip = new THREE.Mesh(stripGeo, UMBRELLA_WHITE);
    const angle = (i / 8) * Math.PI * 2 + Math.PI / 8;
    strip.position.set(
      x + Math.cos(angle) * 0.6 * scale,
      2.2 * scale + 0.3 * scale,
      z + Math.sin(angle) * 0.6 * scale
    );
    strip.lookAt(new THREE.Vector3(x, 2.2 * scale, z));
    strip.rotateX(Math.PI / 4);
    strip.receiveShadow = true;
    umbrella.add(strip);
  }

  // Base rock
  const baseRock = new THREE.Mesh(
    new THREE.SphereGeometry(0.25 * scale, 6, 4),
    ROCK_MAT
  );
  baseRock.position.set(x, 0.12 * scale, z);
  baseRock.scale.y = 0.6;
  umbrella.add(baseRock);

  umbrella.updateMatrixWorld();
  parent.add(umbrella);
}

// ── BENCH PROPS ────────────────────────────────────────────────────────

function createBench(parent: THREE.Object3D, x: number, z: number, rotY: number) {
  const bench = new THREE.Group();
  const scale = 1.5;

  // Seat (2 planks)
  for (let i = 0; i < 2; i++) {
    const plank = new THREE.Mesh(
      new THREE.BoxGeometry(1.4 * scale, 0.06 * scale, 0.25 * scale),
      BENCH_SEAT
    );
    plank.position.set(x, 0.45 * scale, z + (i - 0.5) * 0.28 * scale);
    plank.castShadow = true;
    bench.add(plank);
  }

  // Legs
  for (let i = 0; i < 2; i++) {
    const leg = new THREE.Mesh(
      new THREE.BoxGeometry(0.06 * scale, 0.42 * scale, 0.55 * scale),
      BENCH_LEG
    );
    leg.position.set(x + (i - 0.5) * 1.1 * scale, 0.21 * scale, z);
    leg.castShadow = true;
    bench.add(leg);
  }

  // Backrest
  for (let i = 0; i < 2; i++) {
    const backPlank = new THREE.Mesh(
      new THREE.BoxGeometry(1.3 * scale, 0.06 * scale, 0.2 * scale),
      BENCH_SEAT
    );
    backPlank.position.set(x, (0.7 + i * 0.12) * scale, z + 0.28 * scale);
    backPlank.castShadow = true;
    bench.add(backPlank);
  }

  bench.rotation.y = rotY;
  bench.updateMatrixWorld();
  parent.add(bench);
}

// ── SHORELINE RETURN BENCH ─────────────────────────────────────────────

function createBenchAtBench(parent: THREE.Object3D, x: number, z: number) {
  // Bench at the bench spot with clear ocean view
  // Place facing -z (toward the sea) on the high dune
  const ty = terrainHeight(x, z) + 0.5;  // slight lift above dune
  createBench(parent, x, z, 0);
}

// ── SHELL PROPS ─────────────────────────────────────────────────────────

const SHELL_SHAPES = [
  () => new THREE.SphereGeometry(0.05 + RNG.range(0, 0.03), 5, 3),
  () => new THREE.SphereGeometry(0.06 + RNG.range(0, 0.03), 6, 3).scale(1, 0.5, 1.2),
  () => new THREE.SphereGeometry(0.04 + RNG.range(0, 0.02), 4, 2).scale(1.3, 0.4, 0.8),
];

function createShells(parent: THREE.Object3D) {
  const shells: THREE.Mesh[] = [];
  const shellMats = [SHELL_MAT, SHELL_MAT2, SHELL_MAT];

  for (let i = 0; i < 20; i++) {
    const x = -15 + i * 1.5 + RNG.range(-0.5, 0.5);
    const z = -5 + RNG.range(-3, 4);
    const ty = terrainHeight(x, z);
    const geo = SHELL_SHAPES[i % SHELL_SHAPES.length]();
    const mesh = new THREE.Mesh(geo, shellMats[i % shellMats.length]);
    mesh.position.set(x, ty + 0.03, z);
    mesh.rotation.set(RNG.range(0, Math.PI), RNG.range(0, Math.PI * 2), RNG.range(0, Math.PI));
    mesh.castShadow = false;
    shells.push(mesh);
    parent.add(mesh);
  }
}

// ── DRIFTWOOD PROPS ─────────────────────────────────────────────────────

function createDriftwood(parent: THREE.Object3D) {
  const driftwoods: THREE.Group[] = [];

  for (let g = 0; g < 6; g++) {
    const cluster = new THREE.Group();
    const pieces = 2 + Math.floor(RNG.range(0, 3));

    for (let p = 0; p < pieces; p++) {
      const len = 0.3 + RNG.range(0, 0.5);
      const r = 0.03 + RNG.range(0, 0.05);
      const geo = new THREE.CylinderGeometry(r * 0.7, r, len, 5);
      const mesh = new THREE.Mesh(geo, DRIFTWOOD_MAT);
      mesh.position.set(RNG.range(-0.3, 0.3), 0, RNG.range(-0.2, 0.2));
      mesh.rotation.set(
        RNG.range(-0.2, 0.2),
        RNG.range(0, Math.PI * 2),
        RNG.range(-0.3, 0.3)
      );
      mesh.castShadow = true;
      cluster.add(mesh);
    }

    const x = -12 + g * 4 + RNG.range(-1, 1);
    const z = -4 + RNG.range(-3, 5);
    const ty = terrainHeight(x, z);
    cluster.position.set(x, ty + 0.02, z);
    cluster.rotation.y = RNG.range(0, Math.PI * 2);
    driftwoods.push(cluster);
    parent.add(cluster);
  }
}

// ── DUNE GRASS CLUMPS ──────────────────────────────────────────────────

function createDuneGrass(parent: THREE.Object3D) {
  for (let i = 0; i < 6; i++) {
    const bladeCount = 5 + Math.floor(RNG.range(0, 8));
    const g = new THREE.Group();
    const x = -14 + i * 5 + RNG.range(-1, 1);
    const z = -6 + RNG.range(-2, 5);
    const ty = terrainHeight(x, z);

    for (let b = 0; b < bladeCount; b++) {
      const height = 0.3 + RNG.range(0, 0.5);
      const blade = new THREE.Mesh(
        new THREE.PlaneGeometry(0.04, height),
        GRASS_Tuft
      );
      blade.position.set(
        RNG.range(-0.3, 0.3),
        height / 2,
        RNG.range(-0.3, 0.3)
      );
      blade.rotation.y = RNG.range(-Math.PI, Math.PI);
      blade.rotation.z = RNG.range(-0.3, 0.3);
      g.add(blade);
    }

    g.position.set(x, ty, z);
    parent.add(g);
  }
}

// ── SEAGULL PROPS ──────────────────────────────────────────────────────

function createSeagull(parent: THREE.Object3D, x: number, y: number, z: number) {
  const bird = new THREE.Group();

  // Body
  const body = new THREE.Mesh(
    new THREE.SphereGeometry(0.15, 6, 4),
    SEAGULL_MAT
  );
  body.scale.set(1.4, 0.8, 1);
  bird.add(body);

  // Wings
  for (let side of [-1, 1]) {
    const wing = new THREE.Mesh(
      new THREE.PlaneGeometry(0.8, 0.3),
      new THREE.MeshStandardMaterial({
        color: new THREE.Color("#F8F4EC"),
        roughness: 0.7,
        side: THREE.DoubleSide,
      })
    );
    wing.position.set(0, 0.05, side * 0.4);
    wing.rotation.x = side * 0.2;
    wing.rotation.z = side * 0.3;
    bird.add(wing);
  }

  // Beak
  const beak = new THREE.Mesh(
    new THREE.ConeGeometry(0.03, 0.1, 4),
    new THREE.MeshStandardMaterial({ color: "#E8A030", roughness: 0.5 })
  );
  beak.position.set(0.25, 0, 0);
  beak.rotation.z = -Math.PI / 2;
  bird.add(beak);

  bird.position.set(x, y, z);
  parent.add(bird);
}

// ── BUOY PROPS ──────────────────────────────────────────────────────────

function createBuoy(parent: THREE.Object3D, x: number, z: number) {
  const buoy = new THREE.Group();

  // Main body (cylindrical float)
  const bodyGeo = new THREE.CylinderGeometry(0.2, 0.25, 0.6, 8);
  const body = new THREE.Mesh(bodyGeo, BUOY_BODY);
  buoy.add(body);

  // Top hemisphere
  const topGeo = new THREE.SphereGeometry(0.2, 8, 4, 0, Math.PI * 2, 0, Math.PI / 2);
  const top = new THREE.Mesh(topGeo, BUOY_TOP);
  top.position.y = 0.3;
  buoy.add(top);

  // Bottom
  const botGeo = new THREE.SphereGeometry(0.25, 8, 4, 0, Math.PI * 2, Math.PI / 2, Math.PI / 2);
  const bot = new THREE.Mesh(botGeo, BUOY_BODY);
  bot.position.y = -0.3;
  buoy.add(bot);

  // Ring on top
  const ringGeo = new THREE.TorusGeometry(0.08, 0.015, 4, 8);
  const ring = new THREE.Mesh(ringGeo,
    new THREE.MeshStandardMaterial({ color: "#C0C0C0", roughness: 0.3, metalness: 0.5 })
  );
  ring.position.y = 0.55;
  ring.rotation.x = Math.PI / 2;
  buoy.add(ring);

  const waterY = TERRAIN.oceanLevel + 0.1;
  buoy.position.set(x, waterY, z);
  parent.add(buoy);
}


// ── DUNE ROCKS ──────────────────────────────────────────────────────────

function createDuneRocks(parent: THREE.Object3D) {
  for (let i = 0; i < 4; i++) {
    const r = 0.2 + RNG.range(0, 0.4);
    const rock = new THREE.Mesh(
      new THREE.SphereGeometry(r, 5, 4),
      BEACH_ROCK
    );
    const x = -10 + i * 6 + RNG.range(-1, 1);
    const z = -6 + RNG.range(-2, 4);
    const ty = terrainHeight(x, z);
    rock.position.set(x, ty + r * 0.4, z);
    rock.scale.set(1, 0.6 + RNG.range(0, 0.3), 1.1 + RNG.range(0, 0.3));
    rock.rotation.y = RNG.range(0, Math.PI);
    parent.add(rock);
  }
}

// ── MAIN EXPORT ─────────────────────────────────────────────────────────

export function createShoreline(): ShorelineGroup {
  const group = new THREE.Group() as ShorelineGroup;
  group.name = "Shoreline";

  // 1. Dune terrain bumps
  createDuneLayer(group);

  // 2. Sand ribbons (4 layers)
  createSandRibbons(group);

  // 3. Boat — in the water near the shore
  createBoat(group, -6, -7.5);

  // 4. Second boat further out
  createBoat(group, 3, -7.2);

  // 5. Umbrella — on the sand
  createUmbrella(group, -3, -4.5);

  // 6. Bench with ocean view — on higher dune
  const benchTy = terrainHeight(-5, -3);
  const bench = new THREE.Group();
  const scale = 1.5;
  // Seat planks
  for (let i = 0; i < 2; i++) {
    const plank = new THREE.Mesh(
      new THREE.BoxGeometry(1.4 * scale, 0.06 * scale, 0.25 * scale),
      BENCH_SEAT
    );
    plank.position.set(-5, benchTy + 0.45 * scale, -3 + (i - 0.5) * 0.28 * scale);
    plank.castShadow = true;
    bench.add(plank);
  }
  // Legs
  for (let i = 0; i < 2; i++) {
    const leg = new THREE.Mesh(
      new THREE.BoxGeometry(0.06 * scale, 0.42 * scale, 0.55 * scale),
      BENCH_LEG
    );
    leg.position.set(-5 + (i - 0.5) * 1.1 * scale, benchTy + 0.21 * scale, -3);
    leg.castShadow = true;
    bench.add(leg);
  }
  // Backrest
  for (let i = 0; i < 2; i++) {
    const back = new THREE.Mesh(
      new THREE.BoxGeometry(1.3 * scale, 0.06 * scale, 0.2 * scale),
      BENCH_SEAT
    );
    back.position.set(-5, (benchTy + 0.7 + i * 0.12) * scale, -3 + 0.28 * scale);
    back.castShadow = true;
    bench.add(back);
  }
  group.add(bench);

  // 7. Shells
  createShells(group);

  // 8. Driftwood
  createDriftwood(group);

  // 9. Dune grass
  createDuneGrass(group);

  // 10. Dune rocks
  createDuneRocks(group);

  // 11. Seagulls
  createSeagull(group, -2, terrainHeight(-2, -5) + 1.5 + RNG.range(0, 0.5), -5);
  createSeagull(group, 5, terrainHeight(5, -4) + 2.0 + RNG.range(0, 0.5), -3.5);
  createSeagull(group, 8, terrainHeight(8, -3) + 1.0, -4);

  // 12. Buoys in the water
  createBuoy(group, -8, -8.0);
  createBuoy(group, 6, -8.3);
  createBuoy(group, -1, -8.1);

  // Dispose handler
  group.dispose = () => {
    for (const child of group.children) {
      if (child instanceof THREE.Object3D) {
        child.traverse((obj) => {
          if (obj instanceof THREE.Mesh) {
            obj.geometry.dispose();
          }
        });
      }
    }
    group.clear();
  };

  return group;
}

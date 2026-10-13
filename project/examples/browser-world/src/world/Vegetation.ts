/**
 * Vegetation.ts — owns reusable tree species and intentional placement.
 * Uses InstancedMesh for performance. No random grid placement.
 */
import * as THREE from "three";
import { mat } from "../core/Materials";
import { VEGETATION, TERRAIN, PATH, CLINIC, TOWN_SQUARE } from "../core/WorldConfig";
import { SeededRng } from "../core/SeededNoise";
import { terrainHeight } from "./Terrain";

export class Vegetation {
  private group = new THREE.Group();

  constructor() {
    this.buildTrees();
    this.buildFlowers();
    this.buildRocks();
  }

  private buildTrees(): void {
    const rng = new SeededRng(0x1EE7);

    // Generate candidate positions, filter by distance from path and clinic
    const candidates: { x: number; z: number; size: number; species: number }[] = [];

    // Cluster trees: use a small number of cluster centers
    const clusterCenters: [number, number][] = [
      [-13, 7], [13, 9], [-16, 21], [15, 20],
      [-10, 28], [10, 28],
    ];

    for (const [cx, cz] of clusterCenters) {
      const count = 2 + Math.floor(rng.next() * 3); // 2–4 trees per cluster
      for (let i = 0; i < count; i++) {
        const x = cx + (rng.next() - 0.5) * 8;
        const z = cz + (rng.next() - 0.5) * 8;
        // The authored terrain is 64×64. Earlier clusters extended to z=45,
        // leaving their instanced trunks visibly suspended beyond the map.
        if (Math.abs(x) > TERRAIN.size * 0.47 || Math.abs(z) > TERRAIN.size * 0.47) continue;
        // Filter: not in beach zone
        if (z < TERRAIN.beachZ + 1) continue;
        // Filter: not too close to path
        if (this.distToPath(x, z) < PATH.width / 2 + 2.0) continue;
        if (this.inTownSquareClearance(x, z)) continue;
        // Filter: not too close to clinic footprint
        if (Math.abs(x - CLINIC.pos.x) < 8 && Math.abs(z - CLINIC.pos.z) < 8) continue;
        // Preserve the hospital's facade, gathering pad, and front approach.
        // This also prevents canopies from swallowing its review silhouette.
        if (Math.abs(x + 16) < 7 && Math.abs(z - 24) < 10) continue;
        const size = 0.7 + rng.next() * 0.6;
        const species = Math.floor(rng.next() * 3);
        candidates.push({ x, z, size, species });
      }
    }

    // Build instanced meshes per species
    for (let s = 0; s < 3; s++) {
      const trees = candidates.filter((c) => c.species === s);
      if (trees.length === 0) continue;
      this.addTreeInstances(trees, s);
    }
  }

  private addTreeInstances(
    trees: { x: number; z: number; size: number }[],
    species: number
  ): void {
    // Trunk
    const trunkGeo = new THREE.CylinderGeometry(
      0.15 * (species === 1 ? 0.8 : 1),
      0.25 * (species === 1 ? 0.8 : 1),
      2.0, 8
    );
    const trunkMat = mat("treeTrunk");
    const trunkMesh = new THREE.InstancedMesh(trunkGeo, trunkMat, trees.length);
    trunkMesh.castShadow = true;
    trunkMesh.receiveShadow = true;

    // Canopy (varying shape per species)
    let canopyGeo: THREE.BufferGeometry;
    if (species === 0) {
      // Round blob
      canopyGeo = new THREE.IcosahedronGeometry(1.8, 1);
    } else if (species === 1) {
      // Slim
      canopyGeo = new THREE.ConeGeometry(1.2, 2.8, 8);
    } else {
      // Flat wide
      canopyGeo = new THREE.SphereGeometry(1.6, 12, 8);
    }
    const canopyMat = mat("leafGreen");
    const canopyMesh = new THREE.InstancedMesh(canopyGeo, canopyMat, trees.length);
    canopyMesh.castShadow = true;

    const m4 = new THREE.Matrix4();
    trees.forEach((t, i) => {
      const h = terrainHeight(t.x, t.z);
      // Trunk
      m4.makeTranslation(t.x, h + 1.0 * t.size, t.z);
      m4.scale(new THREE.Vector3(t.size, t.size, t.size));
      trunkMesh.setMatrixAt(i, m4);
      // Canopy
      m4.makeTranslation(t.x, h + (species === 1 ? 3.2 : 2.5) * t.size, t.z);
      m4.scale(new THREE.Vector3(t.size, t.size * (species === 1 ? 1.1 : 0.85), t.size));
      canopyMesh.setMatrixAt(i, m4);
    });

    trunkMesh.instanceMatrix.needsUpdate = true;
    canopyMesh.instanceMatrix.needsUpdate = true;
    this.group.add(trunkMesh, canopyMesh);
  }

  private buildFlowers(): void {
    const rng = new SeededRng(0xF1075);
    const petalMat = mat("petal0");
    const petalGeo = new THREE.SphereGeometry(0.12, 6, 4);
    const centerMat = mat("leafGreen");
    const centerGeo = new THREE.CylinderGeometry(0.06, 0.08, 0.2, 5);

    const positions: [number, number][] = [];
    // Scatter along the path and in coves
    for (let z = 2; z < 50; z += 0.8) {
      if (rng.next() < 0.4) continue;
      const side = rng.next() > 0.5 ? 1 : -1;
      const offset = PATH.width / 2 + 0.8 + rng.next() * 3;
      const x = this.pathXAt(z) + side * offset;
      if (z < TERRAIN.beachZ) continue;
      if (this.inTownSquareClearance(x, z)) continue;
      positions.push([x, z]);
    }
    // Also scatter in coves
    for (let i = 0; i < 30; i++) {
      const x = (rng.next() - 0.5) * 30;
      const z = TERRAIN.coveZ + rng.next() * (TERRAIN.squareZ - TERRAIN.coveZ);
      if (this.distToPath(x, z) < PATH.width / 2 + 1.5) continue;
      if (this.inTownSquareClearance(x, z)) continue;
      positions.push([x, z]);
    }

    if (positions.length === 0) return;
    const flowerInst = new THREE.InstancedMesh(petalGeo, petalMat, positions.length);
    const centerInst = new THREE.InstancedMesh(centerGeo, centerMat, positions.length);
    const m4 = new THREE.Matrix4();
    positions.forEach(([x, z], i) => {
      const h = terrainHeight(x, z);
      m4.makeTranslation(x, h + 0.25, z);
      flowerInst.setMatrixAt(i, m4);
      m4.makeTranslation(x, h + 0.15, z);
      centerInst.setMatrixAt(i, m4);
    });
    flowerInst.instanceMatrix.needsUpdate = true;
    centerInst.instanceMatrix.needsUpdate = true;
    this.group.add(flowerInst, centerInst);
  }

  private buildRocks(): void {
    const rng = new SeededRng(0x50C51);
    const rockGeo = new THREE.DodecahedronGeometry(0.3, 0);
    const rockMat = mat("stoneLight");

    const positions: [number, number, number][] = [];
    // Rocks on beach and shoreline
    for (let i = 0; i < 12; i++) {
      const x = (rng.next() - 0.5) * 24;
      const z = TERRAIN.oceanZ + rng.next() * (TERRAIN.beachZ + 2 - TERRAIN.oceanZ);
      positions.push([x, z, 0.5 + rng.next() * 0.8]);
    }
    // A few rocks near the clinic
    for (let i = 0; i < 5; i++) {
      const x = CLINIC.pos.x + (rng.next() - 0.5) * 10;
      const z = CLINIC.pos.z + (rng.next() - 0.5) * 8;
      if (Math.abs(x - CLINIC.pos.x) < 6 && Math.abs(z - CLINIC.pos.z) < 6) continue;
      positions.push([x, z, 0.4 + rng.next() * 0.5]);
    }

    const rockInst = new THREE.InstancedMesh(rockGeo, rockMat, positions.length);
    rockInst.castShadow = true;
    rockInst.receiveShadow = true;
    const m4 = new THREE.Matrix4();
    positions.forEach(([x, z, s], i) => {
      const h = terrainHeight(x, z);
      m4.makeTranslation(x, h + s * 0.3, z);
      m4.scale(new THREE.Vector3(s, s * 0.7, s));
      rockInst.setMatrixAt(i, m4);
    });
    rockInst.instanceMatrix.needsUpdate = true;
    this.group.add(rockInst);
  }

  private distToPath(x: number, z: number): number {
    const d = 1e9;
    for (let i = 0; i < PATH.controlPoints.length - 1; i++) {
      const [x0, z0] = PATH.controlPoints[i];
      const [x1, z1] = PATH.controlPoints[i + 1];
      const t = Math.max(0, Math.min(1, ((x - x0) * (x1 - x0) + (z - z0) * (z1 - z0)) /
        ((x1 - x0) ** 2 + (z1 - z0) ** 2)));
      const px = x0 + (x1 - x0) * t;
      const pz = z0 + (z1 - z0) * t;
      const dd = Math.sqrt((x - px) ** 2 + (z - pz) ** 2);
      if (dd < d) return dd;
    }
    return d;
  }

  private pathXAt(z: number): number {
    // Find segment where z is between control points
    for (let i = 0; i < PATH.controlPoints.length - 1; i++) {
      const [x0, z0] = PATH.controlPoints[i];
      const [x1, z1] = PATH.controlPoints[i + 1];
      if (z0 <= z && z <= z1) {
        const t = (z - z0) / (z1 - z0);
        return x0 + (x1 - x0) * t;
      }
    }
    return PATH.controlPoints[0][0];
  }

  private inTownSquareClearance(x: number, z: number): boolean {
    const dx = x - TOWN_SQUARE.center.x;
    const dz = z - TOWN_SQUARE.center.z;
    return Math.hypot(dx, dz) < TOWN_SQUARE.radius + 1.15;
  }

  get object(): THREE.Object3D {
    return this.group;
  }

  dispose(): void {
    this.group.traverse((o) => {
      if (o instanceof THREE.Mesh) o.geometry.dispose();
    });
  }
}

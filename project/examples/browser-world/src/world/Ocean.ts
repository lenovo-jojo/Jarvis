/**
 * Ocean.ts — owns the ocean surface and foam ring.
 * NOT a player or prop system. Only ocean geometry + animation state.
 */
import * as THREE from "three";
import { mat } from "../core/Materials";
import { TERRAIN } from "../core/WorldConfig";

export interface OceanState {
  time: number;
}

export class Ocean {
  private group = new THREE.Group();
  private surface: THREE.Mesh;
  private foamRing: THREE.Mesh;
  private waterMat: THREE.MeshPhongMaterial;

  constructor() {
    // Water surface — large horizontal plane at elevation 0
    // Broad playable ocean surrounds the complete island and leaves generous
    // room for future swimming and the ocean railway.
    const geo = new THREE.PlaneGeometry(150, 150, 72, 72);
    this.waterMat = mat("ocean") as THREE.MeshPhongMaterial;
    this.waterMat.transparent = false;
    this.waterMat.opacity = 1;
    this.surface = new THREE.Mesh(geo, this.waterMat);
    this.surface.rotation.x = -Math.PI / 2;
    this.surface.position.y = TERRAIN.oceanLevel;
    this.surface.name = "ENV_Ocean";
    this.surface.receiveShadow = true;
    this.group.add(this.surface);

    // Closed, irregular foam trace makes the full island silhouette readable.
    const coastPoints: THREE.Vector3[] = [];
    for (let i = 0; i < 72; i++) {
      const a = (i / 72) * Math.PI * 2;
      const wobble = 1 + Math.sin(a * 3 + 0.7) * 0.035 + Math.sin(a * 7 - 0.4) * 0.018;
      coastPoints.push(new THREE.Vector3(
        Math.cos(a) * TERRAIN.islandRadiusX * wobble,
        TERRAIN.oceanLevel + 0.025,
        TERRAIN.islandCenterZ + Math.sin(a) * TERRAIN.islandRadiusZ * wobble,
      ));
    }
    const coastCurve = new THREE.CatmullRomCurve3(coastPoints, true, "catmullrom", 0.35);
    const foamGeo = new THREE.TubeGeometry(coastCurve, 144, 0.075, 5, true);
    this.foamRing = new THREE.Mesh(foamGeo, mat("oceanFoam"));
    this.foamRing.name = "ENV_ShoreFoam";
    this.group.add(this.foamRing);
  }

  get object(): THREE.Object3D {
    return this.group;
  }

  /** Animate gentle wave motion. Call each frame with dt. */
  update(dt: number): void {
    const t = performance.now() * 0.001;
    const pos = this.surface.geometry.attributes.position;
    for (let i = 0; i < pos.count; i++) {
      const x = pos.getX(i);
      const y = pos.getY(i);
      const wave =
        Math.sin(x * 0.4 + t * 0.8) * 0.04 +
        Math.sin(y * 0.3 + t * 0.6) * 0.03 +
        Math.sin((x + y) * 0.2 + t * 0.4) * 0.02;
      pos.setZ(i, wave);
    }
    pos.needsUpdate = true;
    this.surface.geometry.computeVertexNormals();

    // Gentle foam pulse
    this.foamRing.position.y = Math.sin(t * 0.7) * 0.018;
  }

  dispose(): void {
    this.surface.geometry.dispose();
    this.foamRing.geometry.dispose();
  }
}

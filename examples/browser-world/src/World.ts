/**
 * World.ts — orchestrates all world modules.
 * Owns: assembly order, seed wiring, shared RNG.
 * Does NOT create: lights, terrain height, palettes, materials.
 */

import * as THREE from "three";
import { Terrain } from "./world/Terrain";
import { Ocean } from "./world/Ocean";
import { Atmosphere } from "./world/Atmosphere";
import { Paths } from "./world/Paths";
import { Vegetation } from "./world/Vegetation";
import { TownSquare } from "./world/TownSquare";
import { createShoreline } from "./world/Shoreline";
import { WorldBlockout } from "./world/WorldBlockout";
import { terrainHeight } from "./world/Terrain";
import { Mae } from "./characters/Mae";
import { ScaleReference } from "./characters/ScaleReference";
import { Clinic } from "./buildings/Clinic";
import { WORLD_SEED, SCALE_REFERENCE } from "./core/WorldConfig";

export interface BuildingEntrance {
  doorPosition: THREE.Vector3;
  buildingCenter: THREE.Vector3;
}

export class World {
  private scene = new THREE.Scene();
  private terrain!: Terrain;
  private ocean!: Ocean;
  private atmosphere!: Atmosphere;
  private paths!: Paths;
  private vegetation!: Vegetation;
  private townSquare!: TownSquare;
  private shoreline!: THREE.Group;
  private blockout!: WorldBlockout;
  private clinic!: Clinic;
  private mae!: Mae;
  private scaleRef: ScaleReference | null = null;
  private disposed = false;
  private disposables: Array<{ dispose(): void }> = [];

  constructor() {
    // Order matters: terrain first (others query height),
    // then atmosphere (lights), then ocean, props, vegetation, buildings
    this.terrain = new Terrain();
    this.atmosphere = new Atmosphere();
    this.ocean = new Ocean();
    this.shoreline = createShoreline();
    this.paths = new Paths();
    this.vegetation = new Vegetation();
    this.townSquare = new TownSquare();
    this.blockout = new WorldBlockout();
    this.clinic = new Clinic();
    this.mae = new Mae();

    // Mae is currently a primitive engineering proxy, not presentation-ready
    // world art. Keep the implementation available for future summon work but
    // do not park the mannequin in the player's central sightline.
    this.mae.visible = false;

    // Scale Reference
    this.scaleRef = new ScaleReference();

    this.scene.add(this.scaleRef);
    this.scene.add(this.terrain.object);
    this.scene.add(this.atmosphere.object);
    this.scene.add(this.ocean.object);
    this.scene.add(this.shoreline);
    this.scene.add(this.paths.object);
    this.scene.add(this.vegetation.object);
    this.scene.add(this.townSquare.object);
    this.scene.add(this.blockout.object);
    this.scene.add(this.clinic.object);
    this.scene.add(this.mae);

    this.disposables = [
      this.terrain,
      this.ocean,
      this.atmosphere,
      this.shoreline as any,
      this.paths,
      this.vegetation,
      this.townSquare,
      this.blockout,
      this.clinic,
      this.scaleRef,
    ];
  }

  get sceneObj(): THREE.Scene {
    return this.scene;
  }

  // spec: mae-design.md — relative positioning: player + 1.55 m right, 0.15 m behind
  updateMae(
    dt: number,
    playerPos: THREE.Vector3,
    playerYaw: number,
    viewpointPosition: THREE.Vector3 = playerPos
  ): void {
    const rightX = Math.cos(playerYaw);
    const rightZ = -Math.sin(playerYaw);
    const backX = -Math.sin(playerYaw);
    const backZ = -Math.cos(playerYaw);

    this.mae.position.set(
      playerPos.x + rightX * 1.55 + backX * 0.15,
      terrainHeight(playerPos.x, playerPos.z),
      playerPos.z + rightZ * 1.55 + backZ * 0.15
    );

    this.mae.tick(dt);
    this.townSquare.update(dt);
    this.scaleRef?.tick(dt, viewpointPosition);
  }

  toggleBlockoutLabels(): boolean {
    return this.blockout.toggleLabels();
  }

  setPlanningVisible(visible: boolean): void {
    this.blockout.object.visible = visible;
  }

  setScaleReferenceVisible(visible: boolean): void {
    if (this.scaleRef) this.scaleRef.visible = visible;
  }

  togglePlanningVisible(): boolean {
    const visible = !this.blockout.object.visible;
    this.blockout.object.visible = visible;
    return visible;
  }

  setClinicCutawayVisible(visible: boolean): void {
    this.clinic.setCutawayVisible(visible);
  }

  toggleClinicCutaway(): boolean {
    return this.clinic.toggleCutaway();
  }

  /**
   * Raycast the clinic's actual visible door panels.
   *
   * If the user hit the door, reveal the interior and return enough
   * world-space information for PlayerController to move through it.
   */
  enterClinicDoor(
    camera: THREE.Camera,
    ndcX: number,
    ndcY: number
  ): BuildingEntrance | null {
    const raycaster = new THREE.Raycaster();

    raycaster.setFromCamera(
      new THREE.Vector2(ndcX, ndcY),
      camera
    );

    const hit = raycaster
      .intersectObject(this.clinic.object, true)
      .find((entry) =>
        this.clinic.isDoorTarget(entry.object)
      );

    if (!hit) return null;

    // Entering always reveals the interior. Never toggle it off here.
    this.clinic.setCutawayVisible(true);

    const doorPosition = this.clinic.doorWorldPos;

    // The clinic group's world position is its center.
    const buildingCenter = new THREE.Vector3();
    this.clinic.object.getWorldPosition(buildingCenter);

    return {
      doorPosition,
      buildingCenter,
    };
  }

  dispose(): void {
    if (this.disposed) return;
    this.disposed = true;

    for (const d of this.disposables) {
      d.dispose();
    }

    this.scene.clear();
  }
}

/**
 * CameraController.ts — review shot cameras and default gameplay camera setup.
 * Deterministic: same seed → same camera positions.
 */
import * as THREE from "three";
import { terrainHeight } from "../world/Terrain";
import { TOWN_SQUARE, CLINIC, PLAYER, TERRAIN } from "../core/WorldConfig";

export interface ReviewShot {
  name: string;
  position: [number, number, number];
  lookAt: [number, number, number];
  fov: number;
}

function shotPos(
  x: number,
  z: number,
  lookX?: number,
  lookZ?: number
): { position: [number, number, number]; lookAt: [number, number, number] } {
  const y = terrainHeight(x, z) + 1.65;
  return {
    position: [x, y, z],
    lookAt: lookX !== undefined && lookZ !== undefined ? [lookX, 1.4, lookZ] : [0, 1.4, 0],
  };
}

function elevShotPos(
  x: number,
  z: number,
  elevationAboveTerrain: number,
  lookX: number,
  lookY: number,
  lookZ: number
): { position: [number, number, number]; lookAt: [number, number, number] } {
  const ty = terrainHeight(x, z);
  return {
    position: [x, ty + elevationAboveTerrain, z],
    lookAt: [lookX, lookY, lookZ],
  };
}

export function buildReviewShots(): ReviewShot[] {
  const shots: ReviewShot[] = [];
  const cx = CLINIC.center[0];
  const cz = CLINIC.center[1];
  const sx = TOWN_SQUARE.center.x;
  const sz = TOWN_SQUARE.center.z;
  const clinicLocal = (x: number, z: number): [number, number] => {
    const c = Math.cos(CLINIC.rotY);
    const s = Math.sin(CLINIC.rotY);
    return [cx + x * c + z * s, cz - x * s + z * c];
  };

  // 1. arrival_shore — at the cove, looking at the shoreline props and dune terrain
  //   Moved back to -9.5 (from -7.0) to avoid Cafe wall clipping.
  //   LookAt aims toward the central shoreline props area.
  shots.push({
    name: "arrival_shore",
    position: [-9.5, terrainHeight(-9.5, -7.5) + PLAYER.eyeHeight, -7.5],
    lookAt: [-4.0, 1.2, -5.0],
    fov: PLAYER.fov,
  });

  // 2. path_first_reveal — mid-path, clinic partially visible
  shots.push({
    name: "path_first_reveal",
    position: [1.0, terrainHeight(1.0, 7.2) + PLAYER.eyeHeight, 7.2],
    lookAt: [cx, 2.0, cz],
    fov: PLAYER.fov,
  });

  // 3. square_entry — at the square edge looking at fountain
  shots.push({
    name: "square_entry",
    position: [sx - 4.6, terrainHeight(sx - 4.6, sz - 4.0) + PLAYER.eyeHeight, sz - 4.0],
    lookAt: [sx, 1.5, sz],
    fov: PLAYER.fov,
  });

  // 4. clinic_hero — elevated wide angle showing full clinic silhouette
  const clinicHero = clinicLocal(0, -10);
  shots.push({
    name: "clinic_hero",
    position: elevShotPos(clinicHero[0], clinicHero[1], 3.2, cx, terrainHeight(cx, cz) + 2.2, cz).position,
    lookAt: [cx, terrainHeight(cx, cz) + 2.2, cz],
    fov: 48,
  });

  // 4b. clinic_door — elevated approach shot at proper walk-up height
  const clinicDoor = clinicLocal(0.8, -5.5);
  shots.push({
    name: "clinic_door",
    position: elevShotPos(clinicDoor[0], clinicDoor[1], 2.2, cx, terrainHeight(cx, cz) + 1.7, cz).position,
    lookAt: [cx, terrainHeight(cx, cz) + 1.7, cz],
    fov: 40,
  });

  // 4c. clinic_wide_context — wide context showing clinic in town
  shots.push({
    name: "clinic_wide_context",
    position: elevShotPos(0, 30, 25, cx, 2.6, cz).position,
    lookAt: elevShotPos(0, 30, 25, cx, 2.6, cz).lookAt,
    fov: 60,
  });

  // 5. clinic_interior — positioned inside the doorway looking into the furnished room
  // Cutaway overview from outside the open facade. This deliberately shows the
  // entire furnished footprint instead of placing the lens against one prop.
  const clinicInside = clinicLocal(0, -6.5);
  const clinicBack = clinicLocal(0, 0.45);
  shots.push({
    name: "clinic_interior",
    position: [clinicInside[0], terrainHeight(cx, cz) + 5.1, clinicInside[1]],
    lookAt: [clinicBack[0], terrainHeight(cx, cz) + 1.15, clinicBack[1]],
    fov: 58,
  });

  // 6. shoreline_return — looking back from square toward shore
  shots.push({
    name: "shoreline_return",
    position: [sx + 1, terrainHeight(sx + 1, sz + 1) + PLAYER.eyeHeight, sz + 1],
    lookAt: [0, 0.5, 2],
    fov: PLAYER.fov,
  });

  // 7. map_overview — complete island planning view, south-to-north
  shots.push({
    name: "map_overview",
    position: [0, 45, -36],
    lookAt: [0, 2.5, 11],
    fov: 52,
  });

  // 8. scale_square — Jojo scale reference beside the square/path furniture
  shots.push({
    name: "scale_square",
    position: [7.5, terrainHeight(7.5, 10.0) + 2.35, 10.0],
    lookAt: [2.2, terrainHeight(2.2, 14) + 1.15, 14],
    fov: 54,
  });

  // 9. station_platform — human-height arrival and rail scale
  shots.push({
    name: "station_platform",
    position: [-10.0, terrainHeight(-10.0, -0.5) + 2.4, -0.5],
    lookAt: [0.2, terrainHeight(0.2, -6.3) + 1.4, -6.3],
    fov: 55,
  });

  // 10. boardwalk_scale — readable boardwalk and Ferris-wheel silhouette
  shots.push({
    name: "boardwalk_scale",
    position: [8.5, terrainHeight(8.5, -2) + PLAYER.eyeHeight, -2],
    lookAt: [15.5, terrainHeight(15.5, 2.5) + 2.0, 2.5],
    fov: 54,
  });

  // 11. hospital_hero — close planning read on the care district facade
  shots.push({
    name: "hospital_hero",
    position: [-12.0, terrainHeight(-12.0, 31.0) + 3.5, 31.0],
    lookAt: [-12.0, terrainHeight(-12.0, 23.0) + 2.15, 23.0],
    fov: 53,
  });

  // 12. shoreline_wide — elevated wide view of the entire cove / shoreline
  shots.push({
    name: "shoreline_wide",
    position: [0, terrainHeight(0, -17) + 7.0, -17],
    lookAt: [0, terrainHeight(0, -5) + 1.2, -5],
    fov: 52,
  });

  // 13. shore_boardwalk — elevated eye-level from the bench looking out toward the water
  //   Fixed Y: terrainHeight(-5.5, -4) would return water-level Y. Lift with +5.0
  //   to get the camera above the beach/dune terrain. Look toward the horizon.
  const sbTerr = terrainHeight(-5.5, -4);
  shots.push({
    name: "shore_boardwalk",
    position: [-5.5, sbTerr + 5.0, -4],
    lookAt: [-5.5, sbTerr + 0.8, -9],
    fov: 54,
  });

  // 14. path_lit_edge — on the path looking at the edge-lit warm glow, town buildings behind
  shots.push({
    name: "path_lit_edge",
    position: [0, terrainHeight(0, 5) + 1.7, 5],
    lookAt: [0, terrainHeight(0, 10) + 1.0, 10],
    fov: 50,
  });

  return shots;
}

export function applyReviewShot(
  camera: THREE.PerspectiveCamera,
  shot: ReviewShot
): void {
  camera.position.set(...shot.position);
  camera.lookAt(new THREE.Vector3(...shot.lookAt));
  camera.fov = shot.fov;
  camera.updateProjectionMatrix();
}

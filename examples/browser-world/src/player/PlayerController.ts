/**
 * PlayerController.ts — keyboard-controlled third-person player and camera.
 *
 * WASD = move
 * Q/E = turn
 * Z/C = look up/down
 * R/F = rise/fall
 * Mouse = normal cursor for interaction
 */

import * as THREE from "three";
import { PLAYER } from "../core/WorldConfig";
import { terrainHeight } from "../world/Terrain";

const _dir = new THREE.Vector3();

export class PlayerController {
  private camera: THREE.PerspectiveCamera;
  private keys: Record<string, boolean> = {};

  private yaw = 0;
  private pitch = 0;
  private heightOffset = 0;

  private position = new THREE.Vector3(
    PLAYER.start.pos[0],
    0,
    PLAYER.start.pos[1]
  );

  private velocity = new THREE.Vector3();
  private cameraInitialized = false;
  private indoors = false;

  get pose(): { position: THREE.Vector3; yaw: number } {
    return {
      position: this.position,
      yaw: this.yaw,
    };
  }

  constructor(camera: THREE.PerspectiveCamera) {
    this.camera = camera;
    this.yaw = THREE.MathUtils.degToRad(PLAYER.start.yaw);

    this.position.y =
      terrainHeight(this.position.x, this.position.z) +
      PLAYER.eyeHeight;

    document.addEventListener("keydown", this.onKeyDown);
    document.addEventListener("keyup", this.onKeyUp);
  }

  travelTo(
    destination: THREE.Vector3,
    lookAt: THREE.Vector3,
    arrivalPitch = 0,
    arrivalHeight = 0
  ): void {
    const facing = lookAt.clone().sub(destination);
    facing.y = 0;

    if (facing.lengthSq() < 0.0001) {
      console.warn("[player] travelTo ignored: destination and lookAt overlap");
      return;
    }

    facing.normalize();

    this.position.x = destination.x;
    this.position.z = destination.z;
    this.position.y =
      terrainHeight(destination.x, destination.z) +
      PLAYER.eyeHeight;

    this.yaw = Math.atan2(
      -facing.x,
      -facing.z
    );

    this.pitch = arrivalPitch;
    this.heightOffset = arrivalHeight;
    this.velocity.set(0, 0, 0);
    this.indoors = false;
    this.cameraInitialized = false;

    this.update(0.016);
  }

  enterBuilding(
    doorWorldPos: THREE.Vector3,
    buildingCenter: THREE.Vector3
  ): void {
    const inward = buildingCenter
      .clone()
      .sub(doorWorldPos);

    inward.y = 0;

    if (inward.lengthSq() < 0.0001) return;

    inward.normalize();

    const destination = doorWorldPos
      .clone()
      .addScaledVector(inward, 2.6);

    this.position.x = destination.x;
    this.position.z = destination.z;

    this.position.y =
      terrainHeight(destination.x, destination.z) +
      PLAYER.eyeHeight;

    // Face into the building.
    this.yaw = Math.atan2(
      -inward.x,
      -inward.z
    );

    this.pitch = 0;
    this.heightOffset = 0;
    this.velocity.set(0, 0, 0);

    // Buildings use a tight follow camera so the view is actually inside
    // the room instead of leaving the outdoor camera behind the facade.
    this.indoors = true;

    // Snap camera to the new location.
    this.cameraInitialized = false;
    this.update(0.016);
  }

  private onKeyDown = (e: KeyboardEvent): void => {
    this.keys[e.code] = true;
  };

  private onKeyUp = (e: KeyboardEvent): void => {
    this.keys[e.code] = false;
  };

  update(dt: number): void {
    const turnSpeed = THREE.MathUtils.degToRad(90);
    const lookSpeed = THREE.MathUtils.degToRad(70);
    const riseSpeed = 6;

    if (this.keys["KeyQ"]) {
      this.yaw += turnSpeed * dt;
    }

    if (this.keys["KeyE"]) {
      this.yaw -= turnSpeed * dt;
    }

    if (this.keys["KeyZ"]) {
      this.pitch += lookSpeed * dt;
    }

    if (this.keys["KeyC"]) {
      this.pitch -= lookSpeed * dt;
    }

    this.pitch = THREE.MathUtils.clamp(
      this.pitch,
      -THREE.MathUtils.degToRad(80),
      THREE.MathUtils.degToRad(80)
    );

    if (this.keys["KeyR"]) {
      this.heightOffset += riseSpeed * dt;
    }

    if (this.keys["KeyF"]) {
      this.heightOffset -= riseSpeed * dt;
    }

    this.heightOffset = THREE.MathUtils.clamp(
      this.heightOffset,
      -2.75,
      30
    );

    const fwd = new THREE.Vector3(
      -Math.sin(this.yaw),
      0,
      -Math.cos(this.yaw)
    ).normalize();

    const right = new THREE.Vector3();

    right.crossVectors(
      fwd,
      new THREE.Vector3(0, 1, 0)
    );

    const speed =
      this.keys["ShiftLeft"] ||
      this.keys["ShiftRight"]
        ? PLAYER.runSpeed
        : PLAYER.walkSpeed;

    const forwardInput =
      (this.keys["KeyW"] ? 1 : 0) -
      (this.keys["KeyS"] ? 1 : 0);

    const strafeInput =
      (this.keys["KeyD"] ? 1 : 0) -
      (this.keys["KeyA"] ? 1 : 0);

    _dir
      .set(0, 0, 0)
      .addScaledVector(fwd, forwardInput)
      .addScaledVector(right, strafeInput);

    if (_dir.lengthSq() > 0) {
      _dir
        .normalize()
        .multiplyScalar(speed);
    }

    this.velocity.lerp(
      _dir,
      1 - Math.exp(-10 * dt)
    );

    if (this.velocity.lengthSq() > 0.0001) {
      this.position.addScaledVector(
        this.velocity,
        dt
      );
    }

    this.position.x = THREE.MathUtils.clamp(
      this.position.x,
      -40,
      40
    );

    this.position.z = THREE.MathUtils.clamp(
      this.position.z,
      -8,
      44
    );

    const groundY =
      terrainHeight(
        this.position.x,
        this.position.z
      ) + PLAYER.eyeHeight;

    this.position.y +=
      (groundY - this.position.y) * 0.2;

    const followDistance = 5.55;
    const baseFollowHeight = 1.55;
    const horizontalDistance =
      Math.cos(this.pitch) * followDistance;
    const pitchHeight =
      Math.sin(this.pitch) * followDistance;

    let desired: THREE.Vector3;

    if (this.indoors) {
      // Player-following indoor orbit camera.
      // Q/E yaw and Z/C pitch remain fully active indoors.
      const indoorDistance = 3.15;

      const indoorBack = new THREE.Vector3(
        Math.sin(this.yaw),
        0,
        Math.cos(this.yaw)
      );

      const indoorHorizontal =
        Math.cos(this.pitch) * indoorDistance;

      const indoorPitchHeight =
        Math.sin(this.pitch) * indoorDistance;

      desired = this.position
        .clone()
        .addScaledVector(
          indoorBack,
          indoorHorizontal
        );

      desired.y =
        this.position.y +
        1.55 +
        indoorPitchHeight +
        this.heightOffset;

    } else {
      desired = this.position
        .clone()
        .addScaledVector(
          fwd,
          -horizontalDistance
        )
        .add(
          new THREE.Vector3(
            0,
            baseFollowHeight +
              pitchHeight +
              this.heightOffset,
            0
          )
        );
    }

    // Wider indoor perspective: spacious third-person framing
    // without true fisheye distortion.
    const desiredFov = this.indoors ? 70 : 60;

    if (Math.abs(this.camera.fov - desiredFov) > 0.01) {
      this.camera.fov = desiredFov;
      this.camera.updateProjectionMatrix();
    }

    if (!this.cameraInitialized) {
      this.camera.position.copy(desired);
      this.cameraInitialized = true;
    } else {
      this.camera.position.lerp(
        desired,
        1 - Math.exp(-8 * dt)
      );
    }

    let target: THREE.Vector3;

    if (this.indoors) {
      // Look into the room at the player's position.
      target = this.position.clone();
      target.y += 0.45 + this.heightOffset;
    } else {
      target = this.position
        .clone()
        .addScaledVector(fwd, 2.45);

      target.y +=
        0.28 + this.heightOffset;
    }

    this.camera.lookAt(target);
  }

  dispose(): void {
    document.removeEventListener(
      "keydown",
      this.onKeyDown
    );

    document.removeEventListener(
      "keyup",
      this.onKeyUp
    );
  }
}

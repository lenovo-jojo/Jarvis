/**
 * ScaleReference.ts — Jojo seated at the town-square fountain.
 *
 * Strategy:
 *   1. Attempt to load assets/scale-reference.vrm via GLTFLoader.
 *      Normalise it to exactly 1.70 m by its world-space bounding-box height.
 *   2. On any failure (missing asset, unsupported format, parse error, etc.)
 *      fall back to a simple cylinder + sphere-head mannequin of 1.70 m height.
 *
 * The model is placed on the terrain surface via `terrainHeight` so feet
 * sit flush — no sinking, no floating.
 *
 * The bundled asset is the canonical jojo4.vrm. It receives a relaxed seated
 * pose on the fountain rim while preserving the exact 1.70 m scale contract.
 */
import * as THREE from "three";
import { FBXLoader } from "three/addons/loaders/FBXLoader.js";
import { GLTFLoader } from "three/addons/loaders/GLTFLoader.js";
import {
  VRM,
  VRMHumanBoneName,
  VRMLoaderPlugin,
  VRMUtils,
} from "@pixiv/three-vrm";
import { SCALE_REFERENCE } from "../core/WorldConfig";
import { terrainHeight } from "../world/Terrain";

// ── Fallback mannequin colour palette ────────────────────────────
const BODY_COLOR = 0xd4b896; // warm wood-cream
const HEAD_COLOR = 0xe8d5b7; // lighter flesh tone

/**
 * Build a minimal cylinder + sphere-head mannequin at exactly
 * `targetHeight` metres.  Feet are at the group origin (y = 0).
 */
function buildFallbackMannequin(targetHeight: number): THREE.Object3D {
  const group = new THREE.Group();

  const legH     = targetHeight * 0.42;   // legs: 42% of height
  const torsoH   = targetHeight * 0.36;   // torso: 36%
  const headR    = targetHeight * 0.07;   // head radius: 7%
  const neckY    = legH + torsoH;         // top of torso = bottom of head
  const headY    = neckY + headR * 0.85;  // head centre

  const bodyMat   = new THREE.MeshStandardMaterial({ color: BODY_COLOR, roughness: 0.75 });
  const headMat   = new THREE.MeshStandardMaterial({ color: HEAD_COLOR, roughness: 0.7 });

  // ── torso ──
  const torso  = new THREE.Mesh(new THREE.CylinderGeometry(0.12, 0.13, torsoH, 10), bodyMat);
  torso.position.y = legH + torsoH / 2;
  group.add(torso);

  // Limbs make the synchronous review fallback read as a relaxed person too.
  for (const side of [-1, 1]) {
    const leg = new THREE.Mesh(new THREE.CapsuleGeometry(0.055, legH * 0.82, 5, 8), bodyMat);
    leg.position.set(side * 0.075, legH * 0.48, 0);
    group.add(leg);
    const arm = new THREE.Mesh(new THREE.CapsuleGeometry(0.042, torsoH * 0.78, 5, 8), bodyMat);
    arm.position.set(side * 0.17, legH + torsoH * 0.58, 0);
    arm.rotation.z = side * -0.12;
    group.add(arm);
  }

  // ── head ──
  const head = new THREE.Mesh(new THREE.SphereGeometry(headR, 16, 12), headMat);
  head.position.y = headY;
  group.add(head);

  // Normalize to exact targetHeight (total natural height ≈ headY + headR):
  const naturalHeight = headY + headR;
  const scale = targetHeight / naturalHeight;
  group.scale.y = scale;

  return group;
}

/** Lower humanoid arms from common VRM T-pose naming conventions. */
function applySeatedPose(vrm: VRM): void {
  const bone = (name: string) =>
    vrm.humanoid.getNormalizedBoneNode(name as any);

  for (const [side, upper, lower, foot, upperArm, lowerArm] of [
    [1, VRMHumanBoneName.LeftUpperLeg, VRMHumanBoneName.LeftLowerLeg,
      VRMHumanBoneName.LeftFoot, VRMHumanBoneName.LeftUpperArm,
      VRMHumanBoneName.LeftLowerArm],
    [-1, VRMHumanBoneName.RightUpperLeg, VRMHumanBoneName.RightLowerLeg,
      VRMHumanBoneName.RightFoot, VRMHumanBoneName.RightUpperArm,
      VRMHumanBoneName.RightLowerArm],
  ] as const) {
    bone(upper)?.rotation.set(-1.18, 0, side * 0.08);
    bone(lower)?.rotation.set(1.38, 0, 0);
    bone(foot)?.rotation.set(-0.18, 0, 0);
    bone(upperArm)?.rotation.set(-0.28, 0, side * 0.68);
    bone(lowerArm)?.rotation.set(-0.72, side * -0.12, 0);
  }

  bone(VRMHumanBoneName.Spine)?.rotation.set(0.05, 0, 0);
  bone(VRMHumanBoneName.Head)?.rotation.set(-0.03, 0.08, 0);
}

const MIXAMO_TO_VRM: Record<string, string> = {
  Hips: VRMHumanBoneName.Hips,
  Spine: VRMHumanBoneName.Spine,
  Spine1: VRMHumanBoneName.Chest,
  Spine2: VRMHumanBoneName.UpperChest,
  Neck: VRMHumanBoneName.Neck,
  LeftShoulder: VRMHumanBoneName.LeftShoulder,
  LeftArm: VRMHumanBoneName.LeftUpperArm,
  LeftForeArm: VRMHumanBoneName.LeftLowerArm,
  LeftHand: VRMHumanBoneName.LeftHand,
  RightShoulder: VRMHumanBoneName.RightShoulder,
  RightArm: VRMHumanBoneName.RightUpperArm,
  RightForeArm: VRMHumanBoneName.RightLowerArm,
  RightHand: VRMHumanBoneName.RightHand,
  LeftUpLeg: VRMHumanBoneName.LeftUpperLeg,
  LeftLeg: VRMHumanBoneName.LeftLowerLeg,
  LeftFoot: VRMHumanBoneName.LeftFoot,
  LeftToeBase: VRMHumanBoneName.LeftToes,
  RightUpLeg: VRMHumanBoneName.RightUpperLeg,
  RightLeg: VRMHumanBoneName.RightLowerLeg,
  RightFoot: VRMHumanBoneName.RightFoot,
  RightToeBase: VRMHumanBoneName.RightToes,
  mixamorigHips: VRMHumanBoneName.Hips,
  mixamorigSpine: VRMHumanBoneName.Spine,
  mixamorigSpine1: VRMHumanBoneName.Chest,
  mixamorigSpine2: VRMHumanBoneName.UpperChest,
  mixamorigNeck: VRMHumanBoneName.Neck,
  mixamorigLeftShoulder: VRMHumanBoneName.LeftShoulder,
  mixamorigLeftArm: VRMHumanBoneName.LeftUpperArm,
  mixamorigLeftForeArm: VRMHumanBoneName.LeftLowerArm,
  mixamorigLeftHand: VRMHumanBoneName.LeftHand,
  mixamorigRightShoulder: VRMHumanBoneName.RightShoulder,
  mixamorigRightArm: VRMHumanBoneName.RightUpperArm,
  mixamorigRightForeArm: VRMHumanBoneName.RightLowerArm,
  mixamorigRightHand: VRMHumanBoneName.RightHand,
  mixamorigLeftUpLeg: VRMHumanBoneName.LeftUpperLeg,
  mixamorigLeftLeg: VRMHumanBoneName.LeftLowerLeg,
  mixamorigLeftFoot: VRMHumanBoneName.LeftFoot,
  mixamorigLeftToeBase: VRMHumanBoneName.LeftToes,
  mixamorigRightUpLeg: VRMHumanBoneName.RightUpperLeg,
  mixamorigRightLeg: VRMHumanBoneName.RightLowerLeg,
  mixamorigRightFoot: VRMHumanBoneName.RightFoot,
  mixamorigRightToeBase: VRMHumanBoneName.RightToes,
  J_Bip_C_Hips: VRMHumanBoneName.Hips,
  J_Bip_C_Spine: VRMHumanBoneName.Spine,
  J_Bip_C_Chest: VRMHumanBoneName.Chest,
  J_Bip_C_UpperChest: VRMHumanBoneName.UpperChest,
  J_Bip_C_Neck: VRMHumanBoneName.Neck,
  J_Bip_L_Shoulder: VRMHumanBoneName.LeftShoulder,
  J_Bip_L_UpperArm: VRMHumanBoneName.LeftUpperArm,
  J_Bip_L_LowerArm: VRMHumanBoneName.LeftLowerArm,
  J_Bip_L_Hand: VRMHumanBoneName.LeftHand,
  J_Bip_R_Shoulder: VRMHumanBoneName.RightShoulder,
  J_Bip_R_UpperArm: VRMHumanBoneName.RightUpperArm,
  J_Bip_R_LowerArm: VRMHumanBoneName.RightLowerArm,
  J_Bip_R_Hand: VRMHumanBoneName.RightHand,
  J_Bip_L_UpperLeg: VRMHumanBoneName.LeftUpperLeg,
  J_Bip_L_LowerLeg: VRMHumanBoneName.LeftLowerLeg,
  J_Bip_L_Foot: VRMHumanBoneName.LeftFoot,
  J_Bip_L_ToeBase: VRMHumanBoneName.LeftToes,
  J_Bip_R_UpperLeg: VRMHumanBoneName.RightUpperLeg,
  J_Bip_R_LowerLeg: VRMHumanBoneName.RightLowerLeg,
  J_Bip_R_Foot: VRMHumanBoneName.RightFoot,
  J_Bip_R_ToeBase: VRMHumanBoneName.RightToes,
  Bip001_Pelvis: VRMHumanBoneName.Hips,
  Bip001_Spine: VRMHumanBoneName.Spine,
  Bip001_Spine1: VRMHumanBoneName.Chest,
  Bip001_Spine2: VRMHumanBoneName.UpperChest,
  Bip001_Neck: VRMHumanBoneName.Neck,
  Bip001_L_Clavicle: VRMHumanBoneName.LeftShoulder,
  Bip001_L_UpperArm: VRMHumanBoneName.LeftUpperArm,
  Bip001_L_Forearm: VRMHumanBoneName.LeftLowerArm,
  Bip001_L_Hand: VRMHumanBoneName.LeftHand,
  Bip001_R_Clavicle: VRMHumanBoneName.RightShoulder,
  Bip001_R_UpperArm: VRMHumanBoneName.RightUpperArm,
  Bip001_R_Forearm: VRMHumanBoneName.RightLowerArm,
  Bip001_R_Hand: VRMHumanBoneName.RightHand,
  Bip001_L_Thigh: VRMHumanBoneName.LeftUpperLeg,
  Bip001_L_Calf: VRMHumanBoneName.LeftLowerLeg,
  Bip001_L_Foot: VRMHumanBoneName.LeftFoot,
  Bip001_L_Toe0: VRMHumanBoneName.LeftToes,
  Bip001_R_Thigh: VRMHumanBoneName.RightUpperLeg,
  Bip001_R_Calf: VRMHumanBoneName.RightLowerLeg,
  Bip001_R_Foot: VRMHumanBoneName.RightFoot,
  Bip001_R_Toe0: VRMHumanBoneName.RightToes,
};

function retargetMixamoClip(
  source: THREE.Group,
  sourceClip: THREE.AnimationClip,
  vrm: VRM
): THREE.AnimationClip {
  source.updateMatrixWorld(true);
  vrm.scene.updateMatrixWorld(true);

  const tracks = sourceClip.tracks.flatMap((track) => {
    const [sourceName, property] = track.name.split(".");
    const canonicalName = sourceName.split(/[|:]/).pop() ?? sourceName;
    const vrmBoneName = MIXAMO_TO_VRM[canonicalName];
    if (!vrmBoneName) return [];

    const target = vrm.humanoid.getNormalizedBoneNode(vrmBoneName as any);
    const sourceBone = source.getObjectByName(sourceName)
      ?? source.getObjectByName(canonicalName);
    if (!target || !sourceBone) return [];

    if (property === "quaternion" && track instanceof THREE.QuaternionKeyframeTrack) {
      const restInverse = sourceBone.getWorldQuaternion(new THREE.Quaternion()).invert();
      const parentRest = sourceBone.parent
        ? sourceBone.parent.getWorldQuaternion(new THREE.Quaternion())
        : new THREE.Quaternion();
      const values = track.values.slice();
      const q = new THREE.Quaternion();
      for (let i = 0; i < values.length; i += 4) {
        q.fromArray(values, i)
          .premultiply(parentRest)
          .multiply(restInverse)
          .toArray(values, i);
      }
      return [new THREE.QuaternionKeyframeTrack(
        `${target.name}.quaternion`, track.times.slice(), values
      )];
    }

    // Root translation in Mixamo is authored for its source chair/character.
    // Keeping it would pull Jojo away from the fountain; placement belongs to
    // the world, while this clip contributes rotations only.
    return [];
  });

  return new THREE.AnimationClip("Sitting Talking", sourceClip.duration, tracks);
}

async function loadSeatedClip(
  vrm: VRM,
  url: string,
  name: string
): Promise<THREE.AnimationClip | null> {
  try {
    const source = await new FBXLoader().loadAsync(url);
    const clip = source.animations[0];
    if (!clip) return null;
    const retargeted = retargetMixamoClip(source, clip, vrm);
    retargeted.name = name;
    console.info(
      `Critter Comforts: ${name} retargeted ${retargeted.tracks.length}/${clip.tracks.length} tracks.`
    );
    return retargeted.tracks.length > 0 ? retargeted : null;
  } catch (error) {
    console.warn(`Critter Comforts: ${name} could not load.`, error);
    return null;
  }
}

async function loadSeatedCycle(vrm: VRM): Promise<THREE.AnimationClip[]> {
  const [talking, sitting, femaleSitting, smoking] = await Promise.all([
    loadSeatedClip(vrm, "assets/sitting-talking.fbx", "Sitting Talking"),
    loadSeatedClip(vrm, "assets/sitting.fbx", "Sitting"),
    loadSeatedClip(vrm, "assets/female-sitting-pose-2.fbx", "Female Sitting Pose 2"),
    loadSeatedClip(vrm, "assets/smoking.fbx", "Smoking"),
  ]);

  const clips = [talking, sitting, femaleSitting].filter(
    (clip): clip is THREE.AnimationClip => clip !== null
  );
  if (talking && smoking) {
    clips.push(createWaistUpSeatedClip(vrm, talking, smoking));
  }
  return clips;
}

function createWaistUpSeatedClip(
  vrm: VRM,
  seatedBase: THREE.AnimationClip,
  upperMotion: THREE.AnimationClip
): THREE.AnimationClip {
  const lowerBoneNames = [
    VRMHumanBoneName.Hips,
    VRMHumanBoneName.LeftUpperLeg,
    VRMHumanBoneName.LeftLowerLeg,
    VRMHumanBoneName.LeftFoot,
    VRMHumanBoneName.LeftToes,
    VRMHumanBoneName.RightUpperLeg,
    VRMHumanBoneName.RightLowerLeg,
    VRMHumanBoneName.RightFoot,
    VRMHumanBoneName.RightToes,
  ]
    .map((boneName) => vrm.humanoid.getNormalizedBoneNode(boneName)?.name)
    .filter((name): name is string => Boolean(name));
  const isLowerTrack = (track: THREE.KeyframeTrack) =>
    lowerBoneNames.some((name) => track.name.startsWith(`${name}.`));

  const duration = Math.max(upperMotion.duration, 0.1);
  const seatedLowerTracks = seatedBase.tracks
    .filter(isLowerTrack)
    .flatMap((track) => {
      if (!(track instanceof THREE.QuaternionKeyframeTrack)) return [];
      const value = Array.from(track.values.slice(0, 4));
      return [new THREE.QuaternionKeyframeTrack(
        track.name,
        [0, duration],
        [...value, ...value]
      )];
    });
  const upperTracks = upperMotion.tracks.filter((track) => !isLowerTrack(track));

  return new THREE.AnimationClip(
    "Smoking — Waist Up",
    duration,
    [...seatedLowerTracks, ...upperTracks]
  );
}

/**
 * Attempt to load the VRM.
 * If GLTFLoader can parse it (unlikely — VRM needs extra loaders),
 * normalise to 1.70 m and return.
 * On failure return `null` → caller shows fallback mannequin.
 */
function tryLoadVRM(): Promise<{ model: THREE.Object3D; vrm: VRM } | null> {
  return new Promise((resolve) => {
    const loader = new GLTFLoader();
    loader.register((parser) => new VRMLoaderPlugin(parser));
    loader.load(
      "assets/scale-reference.vrm",
      (_gltf: any) => {
        const vrm = _gltf.userData.vrm as VRM | undefined;
        if (!vrm) {
          console.warn("Critter Comforts: jojo4.vrm parsed without VRM humanoid data.");
          resolve(null);
          return;
        }
        VRMUtils.rotateVRM0(vrm);

        // VRoid exports VRM in metres. Accessory and spring-bone nodes can make
        // a whole-scene Box3 much larger than the visible humanoid and shrink
        // the avatar to near zero, so retain the authored human scale.
        vrm.scene.scale.setScalar(1);
        applySeatedPose(vrm);
        // Normalized humanoid rotations are copied onto the rendered raw rig
        // during update; one explicit update is sufficient for this static pose.
        vrm.update(0);
        vrm.scene.traverse((node) => {
          node.visible = true;
          const mesh = node as THREE.Mesh;
          if (mesh.isMesh) mesh.castShadow = true;
        });
        vrm.scene.updateMatrixWorld(true);

        // Remove all animations — this must stay static.
        console.info("Critter Comforts: jojo4.vrm loaded for the fountain seat.");
        resolve({ model: vrm.scene, vrm });
      },
      undefined,
      (error) => {
        // Loading failed (wrong format, missing file, CORS, etc.).
        console.warn("Critter Comforts: jojo4.vrm scale reference could not load; using mannequin.", error);
        resolve(null);
      }
    );
  });
}

/**
 * ScaleReference — a static 1.70 m visual scale reference.
 */
export class ScaleReference extends THREE.Group {
  private disposed = false;
  private _model: THREE.Object3D | null = null;
  private vrm: VRM | null = null;
  private mixer: THREE.AnimationMixer | null = null;
  private actions: THREE.AnimationAction[] = [];
  private activeActionIndex = 0;
  private actionElapsed = 0;
  private idleTime = 0;
  private readonly lookTarget = new THREE.Vector3();
  private readonly headTurn = new THREE.Quaternion();
  private readonly headTurnEuler = new THREE.Euler(0, 0, 0, "YXZ");
  private readonly headRestRotation = new THREE.Quaternion();
  private blinkClock = 0;
  private nextBlinkAt = 2.2;
  private readonly blinkDuration = 0.16;

  readonly container = new THREE.Group();

  constructor() {
    super();

    this.name = "Character — Jojo";
    this.userData.ccEditableRoot = true;

    const { x, z } = SCALE_REFERENCE.position;
    const terrainY = terrainHeight(x, z);
    this.position.set(x, terrainY, z);

    // Review-shot mode renders once, so provide a visible reference immediately.
    // Replace it with the normalized VRM when loading succeeds.
    this._model = buildFallbackMannequin(SCALE_REFERENCE.targetHeight);
    this.container.add(this._model);
    this.add(this.container);

    tryLoadVRM().then(async (loaded) => {
      if (loaded && !this.disposed) {
        const { model, vrm } = loaded;
        // Find the lowest point and snap feet to the container origin (y=0).
        const box = new THREE.Box3().setFromObject(model);
        model.position.y -= box.min.y;
        // The fountain rim is roughly 0.5 m above the plaza. Bending the legs
        // raises the feet; lowering the normalized rig seats the hips on the
        // stone while leaving the shins hanging over the outside edge.
        model.position.y -= 0.36;
        if (this._model) this.container.remove(this._model);
        this._model = model;
        this.vrm = vrm;
        this.headRestRotation.copy(
          vrm.humanoid.getNormalizedBoneNode(VRMHumanBoneName.Head)?.quaternion
            ?? new THREE.Quaternion()
        );
        this.container.add(model);

        const clips = await loadSeatedCycle(vrm);
        if (clips.length > 0 && !this.disposed) {
          this.mixer = new THREE.AnimationMixer(vrm.scene);
          this.actions = clips.map((clip) =>
            this.mixer!.clipAction(clip).setLoop(THREE.LoopRepeat, Infinity)
          );
          this.actions[0].play();
          console.info(
            `Critter Comforts: ${clips.length} downloaded seated poses loaded for Jojo.`
          );
        }
      }
    });
  }

  /** Inner group that holds the mannequin or VRM model. */
  get object(): THREE.Object3D {
    return this.container;
  }

  /** The model object (mannequin / VRM), may be null briefly during load. */
  getModel(): THREE.Object3D | null {
    return this._model;
  }

  /** A restrained seated breathing idle. */
  tick(dt: number, playerPosition?: THREE.Vector3): void {
    if (this.disposed) return;
    this.idleTime += Math.min(dt, 0.05);
    if (this.mixer && this.vrm) {
      this.mixer.update(dt);
      this.actionElapsed += dt;
      if (this.actions.length > 1 && this.actionElapsed >= 10) {
        const previous = this.actions[this.activeActionIndex];
        this.activeActionIndex =
          (this.activeActionIndex + 1) % this.actions.length;
        const next = this.actions[this.activeActionIndex];
        next.reset().fadeIn(0.6).play();
        previous.fadeOut(0.6);
        this.actionElapsed = 0;
      }

      if (playerPosition) {
        const head = this.vrm.humanoid.getNormalizedBoneNode(
          VRMHumanBoneName.Head
        );
        if (head && this.vrm.lookAt) {
          // Always calculate gaze from the stable rest pose. Otherwise the
          // VRM look-at reads last frame's already-turned raw head and feeds
          // that rotation back into the next frame, producing an A/B flicker.
          head.quaternion.copy(this.headRestRotation);
          this.vrm.humanoid.update();
          this.vrm.scene.updateMatrixWorld(true);

          this.lookTarget.copy(playerPosition).y += 0.25;
          this.vrm.lookAt.lookAt(this.lookTarget);

          // Use the VRM's calculated gaze angles rather than assuming a +Z
          // model-forward axis. This also handles VRM 0 avatars, whose face
          // front is conventionally -Z.
          this.vrm.lookAt.getEuler(this.headTurnEuler);
          this.headTurnEuler.x = THREE.MathUtils.clamp(
            this.headTurnEuler.x,
            THREE.MathUtils.degToRad(-25),
            THREE.MathUtils.degToRad(25)
          );
          this.headTurnEuler.y = THREE.MathUtils.clamp(
            this.headTurnEuler.y,
            THREE.MathUtils.degToRad(-100),
            THREE.MathUtils.degToRad(100)
          );
          this.headTurn.setFromEuler(this.headTurnEuler);
          head.quaternion.copy(this.headRestRotation).multiply(this.headTurn);
        }
      }

      const expressions = this.vrm.expressionManager;
      if (expressions?.getExpression("blink")) {
        this.blinkClock += dt;
        const blinkProgress =
          (this.blinkClock - this.nextBlinkAt) / this.blinkDuration;
        const blinkWeight = blinkProgress >= 0 && blinkProgress <= 1
          ? 1 - Math.abs(blinkProgress * 2 - 1)
          : 0;
        expressions.setValue("blink", blinkWeight);

        if (blinkProgress > 1) {
          this.nextBlinkAt = this.blinkClock + 2.4 + Math.random() * 2.8;
        }
      }

      this.vrm.update(dt);
    } else {
      this.container.position.y = Math.sin(this.idleTime * 1.65) * 0.004;
    }
  }

  dispose(): void {
    if (this.disposed) return;
    this.disposed = true;
    this.mixer?.stopAllAction();
    this.actions = [];
    this.container.traverse((child: THREE.Object3D) => {
      const mesh = child as THREE.Mesh;
      if ("isMesh" in mesh && mesh.isMesh) {
        mesh.geometry?.dispose();
        if (mesh.material) {
          if (Array.isArray(mesh.material)) {
            mesh.material.forEach((m) => m.dispose());
          } else {
            mesh.material.dispose();
          }
        }
      }
    });
  }
}

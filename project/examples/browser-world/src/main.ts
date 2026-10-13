import * as THREE from "three";
import permanentSceneEdits from "./data/scene-edits.json";
import permanentEditorAssets from "./data/editor-assets.json";

import { World } from "./World";
import { PlayerController } from "./player/PlayerController";
import { buildReviewShots, applyReviewShot } from "./camera/CameraController";
import { REVIEW } from "./core/WorldConfig";
import * as Materials from "./core/Materials";
import { terrainHeight } from "./world/Terrain";

let world: World | null = null;
let player: PlayerController | null = null;
let renderer: THREE.WebGLRenderer;
let camera: THREE.PerspectiveCamera;
let running = false;
let mapOpen = false;

type MapDestination = {
  name: string;
  objectName: string;
};

const MAP_DESTINATIONS: MapDestination[] = [
  { name: "Town Hall + Bank", objectName: "Blockout — Town Hall + Bank" },
  { name: "Cafe", objectName: "Blockout — Cafe" },
  { name: "Park", objectName: "Blockout — Park" },
  { name: "Clinic", objectName: "__CLINIC__" },
  { name: "Library", objectName: "Blockout — Library" },
  { name: "Home", objectName: "Blockout — Home" },
  { name: "Workshop", objectName: "Blockout — Workshop" },
  { name: "Treehouse", objectName: "Blockout — Treehouse" },
  { name: "Bliss Meadow", objectName: "Blockout — Bliss Meadow" },
  { name: "Train Station", objectName: "Blockout — Train Station" },
  { name: "Boardwalk", objectName: "Blockout — Boardwalk" },
  { name: "Cliff Overlook", objectName: "Blockout — Cliff Overlook" },
];

const findMapDestinationObject = (
  destination: MapDestination
): THREE.Object3D | null => {
  if (!world) return null;

  if (destination.objectName === "__CLINIC__") {
    // The authored Clinic replaces the old Hospital blockout.
    // Find the top-level scene child that owns the real clinic door.
    const clinicDoor = world.sceneObj.getObjectByName("hospitalDoorTarget");

    if (clinicDoor) {
      let object: THREE.Object3D = clinicDoor;
      while (object.parent && object.parent !== world.sceneObj) {
        object = object.parent;
      }
      return object;
    }

    return null;
  }

  return world.sceneObj.getObjectByName(destination.objectName) ?? null;
};

const getMapDestinationCenter = (
  destination: MapDestination
): THREE.Vector3 | null => {
  const object = findMapDestinationObject(destination);
  if (!object) return null;

  object.updateWorldMatrix(true, true);

  const box = new THREE.Box3().setFromObject(object);
  if (!box.isEmpty()) return box.getCenter(new THREE.Vector3());

  return object.getWorldPosition(new THREE.Vector3());
};

const frameLiveMap = (): boolean => {
  if (!world) return false;

  const bounds = new THREE.Box3();
  let found = false;

  for (const destination of MAP_DESTINATIONS) {
    const object = findMapDestinationObject(destination);
    if (!object) continue;

    object.updateWorldMatrix(true, true);

    const box = new THREE.Box3().setFromObject(object);
    if (box.isEmpty()) continue;

    bounds.union(box);
    found = true;
  }

  if (!found || bounds.isEmpty()) {
    console.warn("[map] no live destination bounds found");
    return false;
  }

  const center = bounds.getCenter(new THREE.Vector3());
  const size = bounds.getSize(new THREE.Vector3());

  // Perspective overhead framing. Fit the full live destination footprint
  // with breathing room, regardless of editor moves.
  const aspect = Math.max(0.1, camera.aspect);
  const verticalFov = THREE.MathUtils.degToRad(52);
  const horizontalFov =
    2 * Math.atan(Math.tan(verticalFov / 2) * aspect);

  const depthForZ =
    (size.z * 0.5) / Math.max(0.1, Math.tan(verticalFov / 2));
  const depthForX =
    (size.x * 0.5) / Math.max(0.1, Math.tan(horizontalFov / 2));

  const distance = Math.max(depthForX, depthForZ, 18) * 1.28;

  camera.fov = 52;
  camera.position.set(
    center.x,
    center.y + distance,
    center.z + distance * 0.18
  );
  camera.lookAt(center.x, center.y, center.z);
  camera.updateProjectionMatrix();

  return true;
};

const getMapArrival = (
  destination: MapDestination
): { arrival: THREE.Vector3; lookAt: THREE.Vector3 } | null => {
  if (!world) return null;

  const object = findMapDestinationObject(destination);
  const center = getMapDestinationCenter(destination);

  if (!object || !center) return null;

  let arrival: THREE.Vector3;
  let lookAt = center.clone();

  if (destination.name === "Clinic") {
    // Clinic is special: its actual visible door defines the real front.
    const door = world.sceneObj.getObjectByName("hospitalDoorTarget");
    if (!door) {
      console.warn("[map] Clinic door target not found");
      return null;
    }

    const doorPos = door.getWorldPosition(new THREE.Vector3());
    const buildingCenter = object.getWorldPosition(new THREE.Vector3());

    // Center -> door points outward through the real entrance.
    const outward = doorPos.clone().sub(buildingCenter).setY(0);
    if (outward.lengthSq() < 0.0001) outward.set(0, 0, 1);
    outward.normalize();

    // Tighter than the previous generic destination framing.
    arrival = doorPos.clone().addScaledVector(outward, 2.05);
    lookAt = doorPos.clone();

  } else if (destination.name === "Park") {
    // Park is an open running field, not a focal building.
    // Enter from the town-square side and look INTO the field.
    const fountain = new THREE.Vector3(0.5, 0, 13.5);
    const fromTown = fountain.clone().sub(center).setY(0);

    if (fromTown.lengthSq() < 0.0001) fromTown.set(0, 0, 1);
    fromTown.normalize();

    // Park is a big open running field. Arrive on the far edge and
    // look back across the field toward town, rather than out toward the coast.
    // Move well into the field so Park reads as a place to run around,
    // while still looking back across it toward the civic ring.
    // Park arrival belongs near the bottom of the broad sledding hill:
    // lots of runnable grass between the player and the civic ring.
    arrival = center.clone().addScaledVector(fromTown, -8.0);
    lookAt = center.clone().addScaledVector(fromTown, 2.5);

  } else {
    // Every authored blockout's local +Z is its front.
    // Read the CURRENT world rotation so editor moves/rotations remain authoritative.
    const outward = new THREE.Vector3(0, 0, 1)
      .applyQuaternion(object.getWorldQuaternion(new THREE.Quaternion()))
      .setY(0);

    if (outward.lengthSq() < 0.0001) outward.set(0, 0, 1);
    outward.normalize();

    const box = new THREE.Box3().setFromObject(object);
    const size = box.getSize(new THREE.Vector3());

    // Previous clearance was 58% of footprint, clamped 2.8–5m.
    // Pull everything roughly 25% closer for a more intentional arrival frame.
    let clearance =
      Math.max(1.85, Math.min(3.2, Math.max(size.x, size.z) * 0.37));

    // Home benefits from a particularly intimate arrival frame.
    if (destination.name === "Home") {
      // Split the difference between the previous good frame and the last overshoot.
      clearance *= 0.91;
    }

    arrival = center.clone().addScaledVector(outward, clearance);
  }

  // PlayerController.travelTo owns the final eye height. Supplying X/Z here
  // lets it query terrainHeight at the ACTUAL arrival point, avoiding the
  // low/half-buried Home view seen on slopes.
  arrival.y = 0;
  lookAt.y = 0;

  return { arrival, lookAt };
};

const closeMap = (): void => {
  mapOpen = false;
  document.body.classList.remove("cc-map-open");
  document.getElementById("cc-map-nav")?.remove();
};

const showMapNavigation = (): void => {
  document.getElementById("cc-map-nav")?.remove();

  const nav = document.createElement("div");
  nav.id = "cc-map-nav";
  nav.setAttribute("aria-label", "Island destinations");

  Object.assign(nav.style, {
    position: "fixed",
    inset: "0",
    zIndex: "40",
    pointerEvents: "none",
  });

  const panel = document.createElement("div");

  Object.assign(panel.style, {
    position: "absolute",
    top: "18px",
    left: "50%",
    transform: "translateX(-50%)",
    display: "flex",
    flexWrap: "wrap",
    justifyContent: "center",
    gap: "8px",
    maxWidth: "min(900px, 90vw)",
    padding: "10px",
    borderRadius: "16px",
    background: "rgba(20,29,39,.76)",
    backdropFilter: "blur(8px)",
    pointerEvents: "auto",
  });

  for (const destination of MAP_DESTINATIONS) {
    const live = getMapDestinationCenter(destination);
    if (!live) continue;

    const button = document.createElement("button");
    button.type = "button";
    button.textContent = destination.name;
    button.setAttribute("aria-label", destination.name);

    Object.assign(button.style, {
      appearance: "none",
      border: "1px solid rgba(255,255,255,.45)",
      borderRadius: "999px",
      padding: "7px 11px",
      background: "rgba(255,255,255,.92)",
      color: "#18212b",
      font: "600 13px system-ui, sans-serif",
      cursor: "pointer",
    });

    button.addEventListener("click", () => {
      if (!player) return;

      const target = getMapArrival(destination);
      if (!target) {
        console.warn(`[map] destination not found: ${destination.name}`);
        return;
      }

      // Start fast travel with a slightly elevated, downward-looking
      // establishing view. This keeps sloped terrain out of the foreground
      // while retaining the tighter destination framing.
      const arrivalPitch =
        destination.name === "Home" ? -0.20 :
        destination.name === "Clinic" ? -0.11 :
        destination.name === "Park" ? 0.02 :
        -0.09;

      const arrivalHeight =
        destination.name === "Home" ? 1.85 :
        destination.name === "Clinic" ? 0.85 :
        destination.name === "Park" ? 0.0 :
        0.65;

      player.travelTo(
        target.arrival,
        target.lookAt,
        arrivalPitch,
        arrivalHeight
      );
      closeMap();
    });

    panel.appendChild(button);
  }

  nav.appendChild(panel);
  document.body.appendChild(nav);
};

let labelToggleHandler: ((event: KeyboardEvent) => void) | null = null;
let planningToggleHandler: ((event: KeyboardEvent) => void) | null = null;
let interiorToggleHandler: ((event: KeyboardEvent) => void) | null = null;
let buildingDoorDoubleClickHandler: ((event: MouseEvent) => void) | null = null;

let editMode = false;

let editSelected: THREE.Object3D[] = [];
let editSelectionBoxes: THREE.BoxHelper[] = [];
let editTransformOverlay: HTMLDivElement | null = null;
let editPalette: HTMLElement | null = null;

let editClickHandler: ((event: MouseEvent) => void) | null = null;
let editKeyHandler: ((event: KeyboardEvent) => void) | null = null;
let editKeyUpHandler: ((event: KeyboardEvent) => void) | null = null;

const editRaycaster = new THREE.Raycaster();
const editPointer = new THREE.Vector2();

const EDIT_STORAGE_KEY = "critter-comforts-editor-v2";
const EDITOR_ASSET_STORAGE_KEY = "critter-comforts-editor-assets-v2";

type EditTransform = {
  id: string;
  position: [number, number, number];
  rotation: [number, number, number];
  scale: [number, number, number];
  visible: boolean;
  materialColors?: string[];
};

type EditSnapshot = EditTransform[];

type EditorAssetDefinition = {
  id: string;
  name: string;
  object: ReturnType<THREE.Object3D["toJSON"]>;
};

type EditorAssetInstance = {
  id: string;
  assetId: string;
};

type EditorAssetLibrary = {
  assets: EditorAssetDefinition[];
  instances: EditorAssetInstance[];
};

const editUndoStack: EditSnapshot[] = [];
const editRedoStack: EditSnapshot[] = [];

let editClipboard: THREE.Object3D[] = [];
const editHeldKeys = new Set<string>();
let editTransformGestureActive = false;
let editMotionStarted = false;
let editLastFrame = performance.now();

export function init(): void {
  const container = document.getElementById("app")!;
  container.innerHTML = "";

  // ── RENDERER ──────────────────────────────────────────────

  renderer = new THREE.WebGLRenderer({
    antialias: true,
    alpha: false,
    preserveDrawingBuffer: true,
  });

  renderer.setPixelRatio(REVIEW.pixelRatio);
  renderer.setSize(window.innerWidth, window.innerHeight);
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = THREE.PCFSoftShadowMap;
  renderer.outputColorSpace = THREE.SRGBColorSpace;
  renderer.toneMapping = THREE.ACESFilmicToneMapping;
  renderer.toneMappingExposure = 1.1;

  container.appendChild(renderer.domElement);

  editTransformOverlay = document.createElement("div");
  editTransformOverlay.className = "cc-transform-box";
  editTransformOverlay.hidden = true;
  editTransformOverlay.setAttribute("aria-label", "Selection scale and stretch controls");
  editTransformOverlay.innerHTML = `
    <button class="cc-transform-handle" data-handle="nw" aria-label="Scale from top left">↖</button>
    <button class="cc-transform-handle" data-handle="n" aria-label="Stretch height upward">↕</button>
    <button class="cc-transform-handle" data-handle="ne" aria-label="Scale from top right">↗</button>
    <button class="cc-transform-handle" data-handle="e" aria-label="Stretch width right">↔</button>
    <button class="cc-transform-handle" data-handle="se" aria-label="Scale from bottom right">↘</button>
    <button class="cc-transform-handle" data-handle="s" aria-label="Stretch height downward">↕</button>
    <button class="cc-transform-handle" data-handle="sw" aria-label="Scale from bottom left">↙</button>
    <button class="cc-transform-handle" data-handle="w" aria-label="Stretch width left">↔</button>`;
  container.appendChild(editTransformOverlay);

  editPalette = document.createElement("aside");
  editPalette.id = "cc-editor-palette";
  editPalette.hidden = true;
  editPalette.innerHTML = `
    <h2>Objects</h2>
    <div class="cc-palette-row">
      <label for="cc-object-color">Selection color</label>
      <input id="cc-object-color" type="color" value="#ffd999" disabled>
    </div>
    <div class="cc-palette-row">
      <input id="cc-asset-name" type="text" maxlength="48" placeholder="Asset name">
      <button id="cc-save-asset" class="cc-palette-button" type="button" disabled>Save asset</button>
    </div>
    <h3>Add object</h3>
    <div id="cc-object-list"></div>
    <div class="cc-palette-hint">New objects appear at the center cursor and become selected.</div>`;
  container.appendChild(editPalette);

  // ── CAMERA ────────────────────────────────────────────────

  camera = new THREE.PerspectiveCamera(
    72,
    window.innerWidth / window.innerHeight,
    0.1,
    300
  );

  // ── WORLD ─────────────────────────────────────────────────

  if (world) world.dispose();

  world = new World();

  const params = new URLSearchParams(window.location.search);
  const shotName = params.get("shot");

  const planningShot =
    shotName === "map_overview" ||
    shotName === "hospital_hero" ||
    shotName === "station_platform" ||
    shotName === "boardwalk_scale";

  // The authored location blockouts are now the world layout, not disposable
  // debug overlays. Show them by default; P remains a declutter/inspection toggle.
  world.setPlanningVisible(params.get("planning") !== "0");

  // Jojo is now an authored town-square resident seated at the fountain, not
  // a review-only scale mannequin. Keep her visible in normal play and shots.
  world.setScaleReferenceVisible(true);

  // Labels are useful in authored overhead/review shots, but should not float
  // across the normal game view. L reveals them on demand.
  if (!shotName) {
    world.toggleBlockoutLabels();
  }

  if (labelToggleHandler) {
    window.removeEventListener(
      "keydown",
      labelToggleHandler
    );
  }

  labelToggleHandler = (
    event: KeyboardEvent
  ): void => {
    if (event.code === "KeyL") {
      world?.toggleBlockoutLabels();
    }
  };

  window.addEventListener(
    "keydown",
    labelToggleHandler
  );

  if (planningToggleHandler) {
    window.removeEventListener(
      "keydown",
      planningToggleHandler
    );
  }

  planningToggleHandler = (
    event: KeyboardEvent
  ): void => {
    // P keeps its original Planning behavior.
    if (event.code === "KeyP") {
      world?.togglePlanningVisible();
      return;
    }

    // M opens/closes the navigation map.
    if (event.code !== "KeyM" || !world || !player) return;

    mapOpen = !mapOpen;

    if (mapOpen) {
      world.setPlanningVisible(true);

      if (!frameLiveMap()) {
        mapOpen = false;
        return;
      }

      document.body.classList.add("cc-map-open");
      showMapNavigation();
    } else {
      closeMap();

      // Hand camera control back to PlayerController.
      player.update(0.016);
    }
  };

  window.addEventListener(
    "keydown",
    planningToggleHandler
  );

  if (interiorToggleHandler) {
    window.removeEventListener(
      "keydown",
      interiorToggleHandler
    );
  }

  interiorToggleHandler = (
    event: KeyboardEvent
  ): void => {
    if (event.code === "KeyI") {
      world?.toggleClinicCutaway();
    }
  };

  window.addEventListener(
    "keydown",
    interiorToggleHandler
  );

  // ── PLAYER ────────────────────────────────────────────────

  player = new PlayerController(camera);
  player.update(0.016);

  // ── BUILDING ENTRY ────────────────────────────────────────
  //
  // Single click remains available for pointer lock / mouse look.
  // Double-clicking an actual door enters that building.

  buildingDoorDoubleClickHandler = (
    event: MouseEvent
  ): void => {
    if (!world || !player) return;

    const rect =
      renderer.domElement.getBoundingClientRect();

    const x =
      ((event.clientX - rect.left) /
        rect.width) *
        2 -
      1;

    const y =
      -(
        (event.clientY - rect.top) /
        rect.height
      ) *
        2 +
      1;

    const entrance =
      world.enterClinicDoor(
        camera,
        x,
        y
      );

    if (!entrance) return;

    player.enterBuilding(
      entrance.doorPosition,
      entrance.buildingCenter
    );

    renderer.render(
      world.sceneObj,
      camera
    );
  };

  renderer.domElement.addEventListener(
    "dblclick",
    buildingDoorDoubleClickHandler
  );

  // ── HANDS-ON EDIT MODE ─────────────────────────────────────

  const editIdFor = (obj: THREE.Object3D): string => {
    const parts: string[] = [];
    let current: THREE.Object3D | null = obj;

    while (current && current !== world?.sceneObj) {
      if (typeof current.userData.ccAssetInstanceId === "string") {
        parts.push(`AssetInstance:${current.userData.ccAssetInstanceId}`);
        break;
      }

      const parent: THREE.Object3D | null = current.parent;
      const index = parent
        ? parent.children.indexOf(current)
        : 0;

      parts.push(
        `${current.name || current.type}:${index}`
      );

      current = parent;
    }

    return parts.reverse().join("/");
  };

  const editableCharacterRootFor = (
    obj: THREE.Object3D
  ): THREE.Object3D | null => {
    let current: THREE.Object3D | null = obj;

    while (current && current !== world?.sceneObj) {
      if (current.userData.ccEditableRoot === true) {
        return current;
      }
      current = current.parent;
    }

    return null;
  };

  const editableObjects = (): THREE.Object3D[] => {
    if (!world) return [];

    const objects: THREE.Object3D[] = [];
    const seen = new Set<THREE.Object3D>();

    const add = (obj: THREE.Object3D): void => {
      if (seen.has(obj)) return;
      seen.add(obj);
      objects.push(obj);
    };

    // Ordinary editable geometry.
    world.sceneObj.traverse((obj) => {
      if (
        obj instanceof THREE.Mesh &&
        !editSelectionBoxes.some(box => box === (obj as THREE.Object3D))
      ) {
        add(obj);
      }

      if (typeof obj.userData.ccAssetInstanceId === "string") {
        add(obj);
      }
    });

    // Whole-location transforms live on Group/root objects rather
    // than their child meshes. These roots must therefore be part
    // of snapshots for Undo/Redo and Save/Load to preserve moves.
    for (const name of LOCATION_ROOT_NAMES) {
      const root = world.sceneObj.getObjectByName(name);
      if (root) add(root);
    }

    // Clinic is authored separately and has no Blockout root name.
    // Resolve its actual selectable location root from its real door.
    const clinicDoor =
      world.sceneObj.getObjectByName("hospitalDoorTarget");

    if (clinicDoor) {
      const clinicRoot = locationRootFor(clinicDoor);
      if (clinicRoot) add(clinicRoot);
    }

    const jojo = world.sceneObj.getObjectByName("Character — Jojo");
    if (jojo) add(jojo);

    return objects;
  };

  const snapshotEditState = (): EditSnapshot =>
    editableObjects().map((obj) => ({
      id: editIdFor(obj),
      position: [
        obj.position.x,
        obj.position.y,
        obj.position.z,
      ],
      rotation: [
        obj.rotation.x,
        obj.rotation.y,
        obj.rotation.z,
      ],
      scale: [
        obj.scale.x,
        obj.scale.y,
        obj.scale.z,
      ],
      visible: obj.visible,
      materialColors:
        obj instanceof THREE.Mesh
          ? (Array.isArray(obj.material) ? obj.material : [obj.material])
              .map((material) =>
                material instanceof THREE.MeshStandardMaterial ||
                material instanceof THREE.MeshBasicMaterial ||
                material instanceof THREE.MeshPhongMaterial ||
                material instanceof THREE.MeshLambertMaterial
                  ? `#${material.color.getHexString()}`
                  : ""
              )
          : undefined,
    }));

  const restoreEditState = (
    snapshot: EditSnapshot
  ): void => {
    if (!world) return;

    const byId = new Map<string, THREE.Object3D>();

    for (const obj of editableObjects()) {
      byId.set(editIdFor(obj), obj);
    }

    for (const state of snapshot) {
      const obj = byId.get(state.id);
      if (!obj) continue;

      obj.position.set(...state.position);
      obj.rotation.set(...state.rotation);
      obj.scale.set(...state.scale);
      obj.visible = state.visible;

      if (obj instanceof THREE.Mesh && state.materialColors) {
        const materials = Array.isArray(obj.material) ? obj.material : [obj.material];
        materials.forEach((material, index) => {
          const color = state.materialColors?.[index];
          if (
            color &&
            (material instanceof THREE.MeshStandardMaterial ||
              material instanceof THREE.MeshBasicMaterial ||
              material instanceof THREE.MeshPhongMaterial ||
              material instanceof THREE.MeshLambertMaterial)
          ) {
            material.color.set(color);
          }
        });
      }
      obj.updateMatrixWorld(true);
    }

  };

  const pushUndo = (): void => {
    editUndoStack.push(snapshotEditState());

    if (editUndoStack.length > 100) {
      editUndoStack.shift();
    }

    editRedoStack.length = 0;
  };

  let editorAssetLibrary = structuredClone(
    permanentEditorAssets
  ) as unknown as EditorAssetLibrary;
  let canonicalAssetInstances = structuredClone(
    (permanentEditorAssets as unknown as EditorAssetLibrary).instances
  );

  const recoveredAssets = localStorage.getItem(EDITOR_ASSET_STORAGE_KEY);
  if (recoveredAssets) {
    try {
      const parsed = JSON.parse(recoveredAssets) as EditorAssetLibrary;
      if (Array.isArray(parsed.assets) && Array.isArray(parsed.instances)) {
        editorAssetLibrary = parsed;

        // Canonical definitions win over recovery copies with the same id;
        // localStorage still contributes unsaved assets and placements.
        for (const canonicalAsset of (permanentEditorAssets as unknown as EditorAssetLibrary).assets) {
          const localIndex = editorAssetLibrary.assets.findIndex((asset) => asset.id === canonicalAsset.id);
          if (localIndex >= 0) {
            editorAssetLibrary.assets[localIndex] = canonicalAsset;
          } else {
            editorAssetLibrary.assets.push(canonicalAsset);
          }
        }
      }
    } catch (error) {
      console.warn("[edit] saved asset library invalid", error);
    }
  }

  const saveEditorAssetLibrary = async (
    includeCurrentInstances = true
  ): Promise<void> => {
    localStorage.setItem(
      EDITOR_ASSET_STORAGE_KEY,
      JSON.stringify(editorAssetLibrary)
    );

    if (includeCurrentInstances) {
      canonicalAssetInstances = structuredClone(editorAssetLibrary.instances);
    }

    const canonicalPayload: EditorAssetLibrary = {
      assets: editorAssetLibrary.assets,
      instances: includeCurrentInstances
        ? editorAssetLibrary.instances
        : canonicalAssetInstances,
    };

    try {
      const response = await fetch("/__cc/save-editor-assets", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(canonicalPayload),
      });

      if (!response.ok) {
        throw new Error(`asset save endpoint returned ${response.status}`);
      }
    } catch (error) {
      console.error("[edit] canonical asset save failed; localStorage backup preserved", error);
    }
  };

  const makePrimitiveAsset = (kind: string): THREE.Object3D => {
    const material = new THREE.MeshStandardMaterial({
      color: "#ffd999",
      roughness: 0.84,
      metalness: 0,
    });
    let geometry: THREE.BufferGeometry;

    switch (kind) {
      case "Sphere":
        geometry = new THREE.SphereGeometry(0.5, 20, 14);
        break;
      case "Cylinder":
        geometry = new THREE.CylinderGeometry(0.45, 0.45, 1, 18);
        break;
      case "Cone":
        geometry = new THREE.ConeGeometry(0.5, 1, 18);
        break;
      default:
        geometry = new THREE.BoxGeometry(1, 1, 1);
        break;
    }

    const mesh = new THREE.Mesh(geometry, material);
    mesh.castShadow = true;
    mesh.receiveShadow = true;
    mesh.name = kind;
    return mesh;
  };

  const ensurePrimitiveDefinition = (kind: string): EditorAssetDefinition => {
    const id = `builtin-${kind.toLowerCase()}`;
    const existing = editorAssetLibrary.assets.find((asset) => asset.id === id);
    if (existing) return existing;

    const definition: EditorAssetDefinition = {
      id,
      name: kind,
      object: makePrimitiveAsset(kind).toJSON(),
    };
    editorAssetLibrary.assets.push(definition);
    return definition;
  };

  const clearAssetIdentity = (root: THREE.Object3D): void => {
    root.traverse((obj) => {
      delete obj.userData.ccAssetInstanceId;
      delete obj.userData.ccEditableRoot;
    });
  };

  const assetObjectFromSelection = (source: THREE.Object3D): THREE.Object3D => {
    const clone = source.clone(true);
    clearAssetIdentity(clone);
    clone.position.set(0, 0, 0);
    clone.updateMatrixWorld(true);
    return clone;
  };

  const instantiateAsset = (
    definition: EditorAssetDefinition,
    instanceId: string
  ): THREE.Object3D | null => {
    if (!world) return null;

    const parsed = new THREE.ObjectLoader().parse(
      definition.object as unknown as object
    );
    parsed.name = definition.name;
    parsed.userData.ccEditableRoot = true;
    parsed.userData.ccAssetInstanceId = instanceId;
    world.sceneObj.add(parsed);
    return parsed;
  };

  const placeNewAsset = (definition: EditorAssetDefinition): void => {
    if (!world) return;
    const instanceId = crypto.randomUUID();
    const object = instantiateAsset(definition, instanceId);
    if (!object) return;

    object.visible = false;
    pushUndo();

    editRaycaster.setFromCamera(new THREE.Vector2(0, 0), camera);
    const hit = editRaycaster.intersectObjects(
      snapCandidates().filter((candidate) => !isSelectedOrChild(candidate)),
      true
    )[0];

    if (hit) {
      object.position.copy(hit.point);
      const normal = hit.face?.normal.clone().transformDirection(hit.object.matrixWorld);
      if (normal) object.position.addScaledVector(normal, 0.04);
    } else {
      const forward = camera.getWorldDirection(new THREE.Vector3());
      const target = camera.position.clone().addScaledVector(forward, 3);
      target.y = terrainHeight(target.x, target.z);
      object.position.copy(target);
    }

    object.visible = true;
    object.updateMatrixWorld(true);

    const bounds = new THREE.Box3().setFromObject(object);
    if (hit && hit.face && hit.face.normal.y > 0.5 && !bounds.isEmpty()) {
      object.position.y += hit.point.y - bounds.min.y;
      object.updateMatrixWorld(true);
    }

    editorAssetLibrary.instances.push({ id: instanceId, assetId: definition.id });
    editSelected = [object];
    rebuildSelectionBoxes();
    updateEditHud();
  };

  const loadEditorAssetInstances = (): void => {
    for (const instance of editorAssetLibrary.instances) {
      const definition = editorAssetLibrary.assets.find((asset) => asset.id === instance.assetId);
      if (definition) instantiateAsset(definition, instance.id);
    }
  };

  const clearSelectionBoxes = (): void => {
    for (const box of editSelectionBoxes) {
      box.removeFromParent();
      box.geometry.dispose();

      if (Array.isArray(box.material)) {
        for (const material of box.material) {
          material.dispose();
        }
      } else {
        box.material.dispose();
      }
    }

    editSelectionBoxes = [];
  };

  let updateEditTransformOverlay = (): void => {};

  const rebuildSelectionBoxes = (): void => {
    if (!world) return;

    clearSelectionBoxes();

    for (const obj of editSelected) {
      const box = new THREE.BoxHelper(obj);
      editSelectionBoxes.push(box);
      world.sceneObj.add(box);
    }
  };

  const clearEditSelection = (): void => {
    clearSelectionBoxes();
    editSelected = [];
    editHeldKeys.clear();
    editTransformGestureActive = false;
    if (editTransformOverlay) editTransformOverlay.hidden = true;
  };

  const refreshSelectionBoxes = (): void => {
    for (const box of editSelectionBoxes) {
      box.update();
    }
    updateEditTransformOverlay();
  };

  const LOCATION_ROOT_NAMES = new Set([
    "Blockout — Town Hall + Bank",
    "Blockout — Cafe",
    "Blockout — Park",
    "Blockout — Library",
    "Blockout — Home",
    "Blockout — Workshop",
    "Blockout — Treehouse",
    "Blockout — Bliss Meadow",
    "Blockout — Train Station",
    "Blockout — Boardwalk",
    "Blockout — Cliff Overlook",
  ]);

  const locationRootFor = (
    obj: THREE.Object3D
  ): THREE.Object3D | null => {
    let current: THREE.Object3D | null = obj;

    while (current && current !== world?.sceneObj) {
      if (current.userData.ccEditableRoot === true) {
        return current;
      }

      if (LOCATION_ROOT_NAMES.has(current.name)) {
        return current;
      }

      /*
       * Clinic is authored separately from WorldBlockout.
       * Its location root is the highest ancestor beneath
       * the scene that contains the real hospital door.
       */
      if (
        current.getObjectByName(
          "hospitalDoorTarget"
        )
      ) {
        let root = current;

        while (
          root.parent &&
          root.parent !== world?.sceneObj
        ) {
          root = root.parent;
        }

        return root;
      }

      current = current.parent;
    }

    return null;
  };

  const TERRAIN_BOUND_LOCATION_NAMES = new Set([
    "Blockout — Town Hall + Bank",
    "Blockout — Cafe",
    "Blockout — Park",
    "Blockout — Library",
    "Blockout — Home",
    "Blockout — Workshop",
    "Blockout — Bliss Meadow",
    "Blockout — Train Station",
    "Blockout — Boardwalk",
    "Blockout — Cliff Overlook",
  ]);

  const isTerrainBoundLocation = (
    obj: THREE.Object3D
  ): boolean => {
    // Clinic is authored separately and has no stable root name.
    // Its local Y=0 is nevertheless its authored ground plane.
    if (
      obj.getObjectByName("hospitalDoorTarget")
    ) {
      return true;
    }

    // Treehouse is deliberately absent.
    // Cloud Land is not a selectable location root here.
    return TERRAIN_BOUND_LOCATION_NAMES.has(obj.name);
  };

  const groundLocationRoot = (
    obj: THREE.Object3D
  ): void => {
    if (!isTerrainBoundLocation(obj)) return;

    const worldPos =
      obj.getWorldPosition(new THREE.Vector3());

    const groundY =
      terrainHeight(worldPos.x, worldPos.z);

    if (!obj.parent) {
      obj.position.y = groundY;
      obj.updateMatrixWorld(true);
      return;
    }

    obj.parent.updateMatrixWorld(true);

    const groundedWorld =
      worldPos.clone();

    groundedWorld.y = groundY;

    obj.position.copy(
      obj.parent.worldToLocal(groundedWorld)
    );

    obj.updateMatrixWorld(true);
  };

  const groundSelectedLocations = (): void => {
    for (const obj of editSelected) {
      groundLocationRoot(obj);
    }
  };

  const selectObject = (
    obj: THREE.Object3D,
    additive: boolean
  ): void => {
    if (!additive) {
      editSelected = [obj];
      rebuildSelectionBoxes();
      return;
    }

    const existing = editSelected.indexOf(obj);

    if (existing >= 0) {
      editSelected.splice(existing, 1);
    } else {
      editSelected.push(obj);
    }

    rebuildSelectionBoxes();
  };

  const selectedColorMeshes = (): THREE.Mesh[] => {
    const meshes: THREE.Mesh[] = [];
    const seen = new Set<THREE.Mesh>();

    for (const selected of editSelected) {
      selected.traverse((obj) => {
        if (!(obj instanceof THREE.Mesh) || obj instanceof THREE.SkinnedMesh || seen.has(obj)) return;
        seen.add(obj);
        meshes.push(obj);
      });
    }

    return meshes;
  };

  let updateEditPalette = (): void => {};
  const escapeHtml = (value: string): string =>
    value.replace(/[&<>"]/g, (character) => ({
      "&": "&amp;",
      "<": "&lt;",
      ">": "&gt;",
      '"': "&quot;",
    })[character] ?? character);

  updateEditPalette = (): void => {
    if (!editPalette) return;
    editPalette.hidden = !editMode;
    if (!editMode) return;

    const colorInput = editPalette.querySelector<HTMLInputElement>("#cc-object-color");
    const nameInput = editPalette.querySelector<HTMLInputElement>("#cc-asset-name");
    const saveButton = editPalette.querySelector<HTMLButtonElement>("#cc-save-asset");
    const list = editPalette.querySelector<HTMLDivElement>("#cc-object-list");
    const meshes = selectedColorMeshes();

    if (colorInput) {
      colorInput.disabled = meshes.length === 0;
      const firstMaterial = meshes[0]
        ? (Array.isArray(meshes[0].material) ? meshes[0].material[0] : meshes[0].material)
        : null;
      if (
        firstMaterial instanceof THREE.MeshStandardMaterial ||
        firstMaterial instanceof THREE.MeshBasicMaterial ||
        firstMaterial instanceof THREE.MeshPhongMaterial ||
        firstMaterial instanceof THREE.MeshLambertMaterial
      ) {
        colorInput.value = `#${firstMaterial.color.getHexString()}`;
      }
    }

    if (saveButton) saveButton.disabled = editSelected.length !== 1;
    if (nameInput && document.activeElement !== nameInput && editSelected.length === 1) {
      nameInput.value = editSelected[0].name || "New Object";
    }

    if (list) {
      const builtins = ["Cube", "Sphere", "Cylinder", "Cone"]
        .map((name) => `<button class="cc-object-item" type="button" data-primitive="${name}">${name}</button>`)
        .join("");
      const saved = editorAssetLibrary.assets
        .filter((asset) => !asset.id.startsWith("builtin-"))
        .map((asset) => `<button class="cc-object-item is-saved" type="button" data-asset-id="${asset.id}">${escapeHtml(asset.name)}</button>`)
        .join("");
      list.innerHTML = builtins + saved;
    }
  };

  const updateEditHud = (): void => {
    updateEditPalette();
    const hud = document.getElementById("hud");
    if (!hud) return;

    const key = (value: string): string =>
      `<kbd>${value}</kbd>`;

    if (!editMode) {
      hud.classList.remove("is-editing");
      hud.innerHTML = `
        <div class="hud-row">
          <span class="hud-mode">Explore</span>
          <span>${key("WASD")} Move</span>
          <span>${key("QE")} Turn</span>
          <span>${key("ZC")} Look</span>
          <span>${key("RF")} Rise/Fall</span>
          <span>${key("M")} Map</span>
          <span>${key("L")} Labels</span>
          <span>${key("X")} Edit world</span>
        </div>`;
      return;
    }

    hud.classList.add("is-editing");

    if (editSelected.length === 0) {
      hud.innerHTML = `
        <div class="hud-row">
          <span class="hud-mode">Edit world</span>
          <span>Click Object</span>
          <span>Double-click Location</span>
          <span>${key("⇧")} Add Selection</span>
          <span>${key("⌘S")} Save</span>
          <span>${key("X")} Exit Edit</span>
        </div>`;
      return;
    }

    hud.innerHTML = `
      <div class="hud-row">
        <span class="hud-mode">${editSelected.length} selected</span>
        <span>${key("WASD")} Move</span>
        <span>${key("QE")} Turn L/R</span>
        <span>${key("ZC")} Look U/D</span>
        <span>${key("VB")} Lean L/R</span>
        <span>${key("RF")} Rise/Fall</span>
        <span>Drag Sides Stretch</span>
        <span>Drag Corners Scale</span>
        <span>${key("G")} Drop</span>
        <span>${key("⇧")} Move Snap</span>
      </div>
      <div class="hud-row hud-secondary">
        <span>${key("⌘D")} Duplicate</span>
        <span>${key("⌘Z")} Undo</span>
        <span>${key("⇧⌘Z")} Redo</span>
        <span>${key("⌘S")} Save</span>
        <span>${key("Del")} Remove</span>
        <span>${key("Esc")} Deselect</span>
        <span>${key("X")} Exit</span>
      </div>`;
  };

  const selectionBounds = (): THREE.Box3 => {
    const bounds = new THREE.Box3();
    bounds.makeEmpty();

    for (const obj of editSelected) {
      obj.updateMatrixWorld(true);
      bounds.union(
        new THREE.Box3().setFromObject(obj)
      );
    }

    return bounds;
  };

  const localBoundsFor = (root: THREE.Object3D): THREE.Box3 => {
    root.updateWorldMatrix(true, true);
    const inverseRoot = root.matrixWorld.clone().invert();
    const bounds = new THREE.Box3().makeEmpty();
    const corner = new THREE.Vector3();

    root.traverse((child) => {
      if (!(child instanceof THREE.Mesh) || !child.visible) return;
      const geometry = child.geometry;
      if (!geometry.boundingBox) geometry.computeBoundingBox();
      if (!geometry.boundingBox) return;

      const box = geometry.boundingBox;
      const childToRoot = inverseRoot.clone().multiply(child.matrixWorld);

      for (const x of [box.min.x, box.max.x]) {
        for (const y of [box.min.y, box.max.y]) {
          for (const z of [box.min.z, box.max.z]) {
            corner.set(x, y, z).applyMatrix4(childToRoot);
            bounds.expandByPoint(corner);
          }
        }
      }
    });

    return bounds;
  };

  updateEditTransformOverlay = (): void => {
    if (!editTransformOverlay) return;
    if (!editMode || editSelected.length === 0) {
      editTransformOverlay.hidden = true;
      return;
    }

    const bounds = selectionBounds();
    if (bounds.isEmpty()) {
      editTransformOverlay.hidden = true;
      return;
    }

    const rect = renderer.domElement.getBoundingClientRect();
    let minX = Infinity;
    let minY = Infinity;
    let maxX = -Infinity;
    let maxY = -Infinity;
    let visibleCorner = false;
    const point = new THREE.Vector3();

    for (const x of [bounds.min.x, bounds.max.x]) {
      for (const y of [bounds.min.y, bounds.max.y]) {
        for (const z of [bounds.min.z, bounds.max.z]) {
          point.set(x, y, z).project(camera);
          if (point.z < -1 || point.z > 1) continue;
          visibleCorner = true;
          const px = rect.left + (point.x + 1) * rect.width * 0.5;
          const py = rect.top + (1 - point.y) * rect.height * 0.5;
          minX = Math.min(minX, px);
          minY = Math.min(minY, py);
          maxX = Math.max(maxX, px);
          maxY = Math.max(maxY, py);
        }
      }
    }

    if (!visibleCorner || maxX < rect.left || minX > rect.right || maxY < rect.top || minY > rect.bottom) {
      editTransformOverlay.hidden = true;
      return;
    }

    const padding = 5;
    minX = Math.max(rect.left + 14, minX - padding);
    minY = Math.max(rect.top + 14, minY - padding);
    maxX = Math.min(rect.right - 14, maxX + padding);
    maxY = Math.min(rect.bottom - 14, maxY + padding);

    editTransformOverlay.hidden = false;
    editTransformOverlay.style.left = `${minX}px`;
    editTransformOverlay.style.top = `${minY}px`;
    editTransformOverlay.style.width = `${Math.max(28, maxX - minX)}px`;
    editTransformOverlay.style.height = `${Math.max(28, maxY - minY)}px`;
  };

  type ScaleHandle = "n" | "ne" | "e" | "se" | "s" | "sw" | "w" | "nw";
  type ScaleStart = {
    obj: THREE.Object3D;
    scale: THREE.Vector3;
    worldPosition: THREE.Vector3;
    halfXWorld: THREE.Vector3;
    halfYWorld: THREE.Vector3;
  };

  const beginScaleDrag = (event: PointerEvent): void => {
    const button = (event.target as HTMLElement).closest<HTMLButtonElement>(".cc-transform-handle");
    if (!button || !editTransformOverlay || editSelected.length === 0) return;

    event.preventDefault();
    event.stopPropagation();
    button.setPointerCapture(event.pointerId);
    pushUndo();

    const handle = button.dataset.handle as ScaleHandle;
    const startX = event.clientX;
    const startY = event.clientY;
    const overlayRect = editTransformOverlay.getBoundingClientRect();
    const starts: ScaleStart[] = editSelected.map((obj) => {
      const localBounds = localBoundsFor(obj);
      const size = localBounds.isEmpty()
        ? new THREE.Vector3(1, 1, 1)
        : localBounds.getSize(new THREE.Vector3());
      const matrix3 = new THREE.Matrix3().setFromMatrix4(obj.matrixWorld);

      return {
        obj,
        scale: obj.scale.clone(),
        worldPosition: obj.getWorldPosition(new THREE.Vector3()),
        halfXWorld: new THREE.Vector3(size.x * 0.5, 0, 0).applyMatrix3(matrix3),
        halfYWorld: new THREE.Vector3(0, size.y * 0.5, 0).applyMatrix3(matrix3),
      };
    });

    const onMove = (moveEvent: PointerEvent): void => {
      moveEvent.preventDefault();
      moveEvent.stopPropagation();
      const dx = moveEvent.clientX - startX;
      const dy = moveEvent.clientY - startY;
      const hasX = handle.includes("e") || handle.includes("w");
      const hasY = handle.includes("n") || handle.includes("s");
      const signX = handle.includes("e") ? 1 : handle.includes("w") ? -1 : 0;
      const signY = handle.includes("n") ? 1 : handle.includes("s") ? -1 : 0;
      const outwardX = signX === 1 ? dx : -dx;
      const outwardY = signY === 1 ? -dy : dy;
      const factorX = THREE.MathUtils.clamp(1 + outwardX / Math.max(40, overlayRect.width), 0.08, 20);
      const factorY = THREE.MathUtils.clamp(1 + outwardY / Math.max(40, overlayRect.height), 0.08, 20);
      const uniform = THREE.MathUtils.clamp((factorX + factorY) * 0.5, 0.08, 20);

      for (const start of starts) {
        const { obj } = start;
        const nextScale = start.scale.clone();
        const shift = new THREE.Vector3();

        if (hasX && hasY) {
          nextScale.multiplyScalar(uniform);
          shift.addScaledVector(start.halfXWorld, signX * (uniform - 1));
          shift.addScaledVector(start.halfYWorld, signY * (uniform - 1));
        } else if (hasX) {
          nextScale.x *= factorX;
          shift.addScaledVector(start.halfXWorld, signX * (factorX - 1));
        } else if (hasY) {
          nextScale.y *= factorY;
          shift.addScaledVector(start.halfYWorld, signY * (factorY - 1));
        }

        obj.scale.copy(nextScale);
        if (obj.parent) {
          obj.parent.updateWorldMatrix(true, false);
          obj.position.copy(obj.parent.worldToLocal(start.worldPosition.clone().add(shift)));
        } else {
          obj.position.copy(start.worldPosition).add(shift);
        }
        obj.updateMatrixWorld(true);
      }

      refreshSelectionBoxes();
    };

    const onEnd = (endEvent: PointerEvent): void => {
      endEvent.preventDefault();
      endEvent.stopPropagation();
      button.releasePointerCapture(endEvent.pointerId);
      button.removeEventListener("pointermove", onMove);
      button.removeEventListener("pointerup", onEnd);
      button.removeEventListener("pointercancel", onEnd);
      refreshSelectionBoxes();
    };

    button.addEventListener("pointermove", onMove);
    button.addEventListener("pointerup", onEnd);
    button.addEventListener("pointercancel", onEnd);
  };

  editTransformOverlay.addEventListener("pointerdown", beginScaleDrag);

  const colorInput = editPalette.querySelector<HTMLInputElement>("#cc-object-color")!;
  const assetNameInput = editPalette.querySelector<HTMLInputElement>("#cc-asset-name")!;
  const saveAssetButton = editPalette.querySelector<HTMLButtonElement>("#cc-save-asset")!;
  const objectList = editPalette.querySelector<HTMLDivElement>("#cc-object-list")!;
  let colorGestureActive = false;

  const beginColorGesture = (): void => {
    if (colorGestureActive || selectedColorMeshes().length === 0) return;
    pushUndo();
    colorGestureActive = true;

    for (const mesh of selectedColorMeshes()) {
      mesh.material = Array.isArray(mesh.material)
        ? mesh.material.map((material) => material.clone())
        : mesh.material.clone();
    }
  };

  colorInput.addEventListener("pointerdown", beginColorGesture);
  colorInput.addEventListener("input", () => {
    beginColorGesture();

    for (const mesh of selectedColorMeshes()) {
      const materials = Array.isArray(mesh.material) ? mesh.material : [mesh.material];
      for (const material of materials) {
        if (
          material instanceof THREE.MeshStandardMaterial ||
          material instanceof THREE.MeshBasicMaterial ||
          material instanceof THREE.MeshPhongMaterial ||
          material instanceof THREE.MeshLambertMaterial
        ) {
          material.color.set(colorInput.value);
          material.needsUpdate = true;
        }
      }
    }
  });
  colorInput.addEventListener("change", () => {
    colorGestureActive = false;
    refreshSelectionBoxes();
  });
  colorInput.addEventListener("blur", () => {
    colorGestureActive = false;
  });

  saveAssetButton.addEventListener("click", async () => {
    if (editSelected.length !== 1) return;
    const name = assetNameInput.value.trim() || editSelected[0].name || "New Object";
    const definition: EditorAssetDefinition = {
      id: crypto.randomUUID(),
      name,
      object: assetObjectFromSelection(editSelected[0]).toJSON(),
    };
    editorAssetLibrary.assets.push(definition);
    await saveEditorAssetLibrary(false);
    updateEditPalette();
    saveAssetButton.textContent = "Saved";
    window.setTimeout(() => {
      saveAssetButton.textContent = "Save asset";
    }, 900);
  });

  objectList.addEventListener("click", (event) => {
    const button = (event.target as HTMLElement).closest<HTMLButtonElement>(".cc-object-item");
    if (!button) return;

    const primitive = button.dataset.primitive;
    const definition = primitive
      ? ensurePrimitiveDefinition(primitive)
      : editorAssetLibrary.assets.find((asset) => asset.id === button.dataset.assetId);

    if (definition) placeNewAsset(definition);
  });

  const moveSelectionWorld = (
    delta: THREE.Vector3
  ): void => {
    for (const obj of editSelected) {
      if (!obj.parent) {
        obj.position.add(delta);
        continue;
      }

      obj.parent.updateMatrixWorld(true);

      const worldStart =
        obj.getWorldPosition(new THREE.Vector3());

      const worldEnd =
        worldStart.clone().add(delta);

      obj.position.copy(
        obj.parent.worldToLocal(worldEnd)
      );

      obj.updateMatrixWorld(true);
    }

    refreshSelectionBoxes();
  };

  const rotateSelectionLocal = (
    localAxis: THREE.Vector3,
    angle: number
  ): void => {
    if (editSelected.length === 0) return;

    const q =
      new THREE.Quaternion().setFromAxisAngle(
        localAxis,
        angle
      );

    for (const obj of editSelected) {
      obj.quaternion.multiply(q).normalize();
      obj.updateMatrixWorld(true);
    }

    refreshSelectionBoxes();
  };

  const isSelectedOrChild = (
    obj: THREE.Object3D
  ): boolean => {
    let current: THREE.Object3D | null = obj;

    while (current) {
      if (editSelected.includes(current)) {
        return true;
      }
      current = current.parent;
    }

    return false;
  };

  const snapCandidates = (): THREE.Mesh[] => {
    if (!world) return [];

    const candidates: THREE.Mesh[] = [];

    world.sceneObj.traverse((obj) => {
      if (!(obj instanceof THREE.Mesh)) return;
      if (!obj.visible) return;
      if (isSelectedOrChild(obj)) return;

      candidates.push(obj);
    });

    return candidates;
  };

  const dropSelectionToSurface = (): void => {
    if (!world || editSelected.length === 0) {
      return;
    }

    const bounds = selectionBounds();
    const center =
      bounds.getCenter(new THREE.Vector3());

    /*
     * Start just above the selection's bottom.
     * Ray straight downward.
     *
     * If geometry exists below, land on it.
     * Otherwise fall back to terrainHeight().
     */
    const ray = new THREE.Raycaster(
      new THREE.Vector3(
        center.x,
        bounds.min.y + 0.05,
        center.z
      ),
      new THREE.Vector3(0, -1, 0),
      0,
      200
    );

    const hit =
      ray.intersectObjects(
        snapCandidates(),
        true
      )[0];

    const targetY = hit
      ? hit.point.y
      : terrainHeight(center.x, center.z);

    pushUndo();

    moveSelectionWorld(
      new THREE.Vector3(
        0,
        targetY - bounds.min.y,
        0
      )
    );
  };

  const snapSelectionToNearbySurface = (): void => {
    if (!world || editSelected.length === 0) {
      return;
    }

    const selectedBounds = selectionBounds();

    if (selectedBounds.isEmpty()) return;

    const candidates = snapCandidates();

    /*
     * Surface snapping uses world-space bounding boxes.
     *
     * We look for another object's face near one of our faces.
     * The two objects must overlap on the OTHER two axes,
     * which prevents a nearby-but-unrelated object from
     * pulling the selection sideways across the room.
     */
    const threshold = 0.28;

    let bestDistance = threshold;
    let bestDelta: THREE.Vector3 | null = null;

    const overlaps = (
      aMin: number,
      aMax: number,
      bMin: number,
      bMax: number,
      padding = 0.08
    ): boolean =>
      aMax >= bMin - padding &&
      aMin <= bMax + padding;

    for (const candidate of candidates) {
      candidate.updateMatrixWorld(true);

      const target =
        new THREE.Box3().setFromObject(candidate);

      if (target.isEmpty()) continue;

      const overlapX = overlaps(
        selectedBounds.min.x,
        selectedBounds.max.x,
        target.min.x,
        target.max.x
      );

      const overlapY = overlaps(
        selectedBounds.min.y,
        selectedBounds.max.y,
        target.min.y,
        target.max.y
      );

      const overlapZ = overlaps(
        selectedBounds.min.z,
        selectedBounds.max.z,
        target.min.z,
        target.max.z
      );

      // Selected bottom -> target top.
      if (overlapX && overlapZ) {
        const d =
          Math.abs(
            selectedBounds.min.y -
            target.max.y
          );

        if (d < bestDistance) {
          bestDistance = d;
          bestDelta = new THREE.Vector3(
            0,
            target.max.y -
              selectedBounds.min.y,
            0
          );
        }
      }

      // Selected top -> target bottom.
      if (overlapX && overlapZ) {
        const d =
          Math.abs(
            selectedBounds.max.y -
            target.min.y
          );

        if (d < bestDistance) {
          bestDistance = d;
          bestDelta = new THREE.Vector3(
            0,
            target.min.y -
              selectedBounds.max.y,
            0
          );
        }
      }

      // Selected right -> target left.
      if (overlapY && overlapZ) {
        const d =
          Math.abs(
            selectedBounds.max.x -
            target.min.x
          );

        if (d < bestDistance) {
          bestDistance = d;
          bestDelta = new THREE.Vector3(
            target.min.x -
              selectedBounds.max.x,
            0,
            0
          );
        }
      }

      // Selected left -> target right.
      if (overlapY && overlapZ) {
        const d =
          Math.abs(
            selectedBounds.min.x -
            target.max.x
          );

        if (d < bestDistance) {
          bestDistance = d;
          bestDelta = new THREE.Vector3(
            target.max.x -
              selectedBounds.min.x,
            0,
            0
          );
        }
      }

      // Selected front/back faces along Z.
      if (overlapX && overlapY) {
        const d1 =
          Math.abs(
            selectedBounds.max.z -
            target.min.z
          );

        if (d1 < bestDistance) {
          bestDistance = d1;
          bestDelta = new THREE.Vector3(
            0,
            0,
            target.min.z -
              selectedBounds.max.z
          );
        }

        const d2 =
          Math.abs(
            selectedBounds.min.z -
            target.max.z
          );

        if (d2 < bestDistance) {
          bestDistance = d2;
          bestDelta = new THREE.Vector3(
            0,
            0,
            target.max.z -
              selectedBounds.min.z
          );
        }
      }
    }

    if (bestDelta) {
      moveSelectionWorld(bestDelta);
    }
  };

  const saveEdits = async (): Promise<void> => {
    const snapshot = snapshotEditState();

    // Keep browser storage as an immediate recovery copy.
    localStorage.setItem(
      EDIT_STORAGE_KEY,
      JSON.stringify(snapshot)
    );
    localStorage.setItem(
      EDITOR_ASSET_STORAGE_KEY,
      JSON.stringify(editorAssetLibrary)
    );
    canonicalAssetInstances = structuredClone(editorAssetLibrary.instances);

    try {
      const response = await fetch(
        "/__cc/save-scene-edits",
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
          },
          body: JSON.stringify({
            sceneEdits: snapshot,
            editorAssets: editorAssetLibrary,
          }),
        }
      );

      if (!response.ok) {
        throw new Error(
          `save endpoint returned ${response.status}`
        );
      }

      const result = await response.json();

      console.log(
        "[edit] canonical scene saved",
        result.objects,
        "objects"
      );
    } catch (error) {
      console.error(
        "[edit] canonical save failed; localStorage backup preserved",
        error
      );
    }

  };

  const loadSavedEdits = (): void => {
    restoreEditState(permanentSceneEdits as EditSnapshot);

    const raw = localStorage.getItem(EDIT_STORAGE_KEY);
    if (!raw) return;

    try {
      restoreEditState(JSON.parse(raw) as EditSnapshot);
    } catch (error) {
      console.warn("[edit] saved layout invalid", error);
    }
  };

  const undoEdit = (): void => {
    const previous = editUndoStack.pop();
    if (!previous) return;

    editRedoStack.push(
      snapshotEditState()
    );

    restoreEditState(previous);
    refreshSelectionBoxes();
  };

  const redoEdit = (): void => {
    const next = editRedoStack.pop();
    if (!next) return;

    editUndoStack.push(
      snapshotEditState()
    );

    restoreEditState(next);
    refreshSelectionBoxes();
  };

  const installClones = (
    sources: THREE.Object3D[]
  ): void => {
    if (!world || sources.length === 0) {
      return;
    }

    pushUndo();

    const clones: THREE.Object3D[] = [];

    for (const source of sources) {
      const clone = source.clone(true);

      clone.position.x += 0.35;
      clone.position.z += 0.35;

      if (source.parent) {
        source.parent.add(clone);
      } else {
        world.sceneObj.add(clone);
      }

      clones.push(clone);
    }

    editSelected = clones;
    rebuildSelectionBoxes();
    updateEditHud();
  };

  editClickHandler = (
    event: MouseEvent
  ): void => {
    if (!editMode || !world) return;

    // A double-click is handled separately as a whole-location
    // selection so the normal single-click path remains precise.
    if (event.detail > 1) return;

    event.preventDefault();
    event.stopPropagation();

    const rect =
      renderer.domElement
        .getBoundingClientRect();

    editPointer.x =
      ((event.clientX - rect.left) /
        rect.width) *
        2 -
      1;

    editPointer.y =
      -((event.clientY - rect.top) /
        rect.height) *
        2 +
      1;

    editRaycaster.setFromCamera(
      editPointer,
      camera
    );

    const hits =
      editRaycaster.intersectObjects(
        world.sceneObj.children,
        true
      );

    const hit = hits.find(
      (candidate) =>
        candidate.object.visible &&
        candidate.object instanceof THREE.Mesh &&
        !editSelectionBoxes.some(box => box === candidate.object)
    );

    if (!hit) {
      if (!event.shiftKey) {
        clearEditSelection();
      }

      updateEditHud();
      return;
    }

    selectObject(
      editableCharacterRootFor(hit.object) ?? hit.object,
      event.shiftKey
    );

    updateEditHud();
  };

  editKeyHandler = (
    event: KeyboardEvent
  ): void => {
    const command =
      event.metaKey || event.ctrlKey;

    if (
      event.target instanceof HTMLInputElement &&
      !(command && event.code === "KeyS")
    ) {
      return;
    }

    if (
      event.code === "KeyX" &&
      !command
    ) {
      editMode = !editMode;
      clearEditSelection();
      updateEditHud();
      return;
    }

    // World save is global. Edit mode controls manipulation,
    // not whether the current world can be saved.
    if (
      command &&
      event.code === "KeyS"
    ) {
      event.preventDefault();
      event.stopImmediatePropagation();
      saveEdits();
      return;
    }

    if (!editMode) return;

    if (event.code === "Escape") {
      clearEditSelection();
      updateEditHud();
      return;
    }

    if (
      command &&
      event.code === "KeyZ"
    ) {
      event.preventDefault();
      event.stopImmediatePropagation();

      if (event.shiftKey) {
        redoEdit();
      } else {
        undoEdit();
      }

      return;
    }

    if (
      command &&
      event.code === "KeyC" &&
      editSelected.length > 0
    ) {
      event.preventDefault();
      event.stopImmediatePropagation();

      editClipboard =
        editSelected.map(
          (obj) => obj.clone(true)
        );

      return;
    }

    if (
      command &&
      event.code === "KeyV" &&
      editClipboard.length > 0
    ) {
      event.preventDefault();
      event.stopImmediatePropagation();

      installClones(editClipboard);
      return;
    }

    if (
      command &&
      event.code === "KeyD" &&
      editSelected.length > 0
    ) {
      event.preventDefault();
      event.stopImmediatePropagation();

      installClones(editSelected);
      return;
    }

    if (editSelected.length === 0) {
      return;
    }

    if (
      event.code === "ShiftLeft" ||
      event.code === "ShiftRight"
    ) {
      editHeldKeys.add(event.code);
      return;
    }

    const controlled = new Set([
      "KeyW",
      "KeyA",
      "KeyS",
      "KeyD",
      "KeyQ",
      "KeyE",
      "KeyZ",
      "KeyC",
      "KeyV",
      "KeyB",
      "KeyR",
      "KeyF",
    ]);

    if (controlled.has(event.code)) {
      event.preventDefault();
      event.stopImmediatePropagation();

      if (!editTransformGestureActive) {
        pushUndo();
        editTransformGestureActive = true;
      }

      editHeldKeys.add(event.code);
      return;
    }

    if (event.code === "KeyG") {
      event.preventDefault();
      event.stopImmediatePropagation();
      dropSelectionToSurface();
      return;
    }

    if (
      event.code === "Delete" ||
      event.code === "Backspace"
    ) {
      event.preventDefault();
      event.stopImmediatePropagation();

      pushUndo();

      for (const obj of editSelected) {
        obj.visible = false;
      }

      clearEditSelection();
      updateEditHud();
    }
  };

  editKeyUpHandler = (
    event: KeyboardEvent
  ): void => {
    editHeldKeys.delete(event.code);

    const transformCodes = [
      "KeyW",
      "KeyA",
      "KeyS",
      "KeyD",
      "KeyQ",
      "KeyE",
      "KeyZ",
      "KeyC",
      "KeyV",
      "KeyB",
      "KeyR",
      "KeyF",
    ];

    const transformStillHeld =
      transformCodes.some(
        (code) => editHeldKeys.has(code)
      );

    if (!transformStillHeld) {
      editTransformGestureActive = false;
    }
  };

  const editLocationDoubleClickHandler = (
    event: MouseEvent
  ): void => {
    if (!editMode || !world) return;

    event.preventDefault();
    event.stopImmediatePropagation();

    const rect =
      renderer.domElement.getBoundingClientRect();

    editPointer.x =
      ((event.clientX - rect.left) /
        rect.width) *
        2 -
      1;

    editPointer.y =
      -((event.clientY - rect.top) /
        rect.height) *
        2 +
      1;

    editRaycaster.setFromCamera(
      editPointer,
      camera
    );

    const hits =
      editRaycaster.intersectObjects(
        world.sceneObj.children,
        true
      );

    const hit = hits.find(
      (candidate) =>
        candidate.object.visible &&
        candidate.object instanceof THREE.Mesh &&
        !editSelectionBoxes.some(box => box === candidate.object)
    );

    if (!hit) {
      if (!event.shiftKey) {
        clearEditSelection();
      }

      updateEditHud();
      return;
    }

    const location =
      locationRootFor(hit.object);

    if (!location) {
      // Not part of a known location: retain ordinary
      // object-level selection behavior.
      selectObject(
        hit.object,
        event.shiftKey
      );
      updateEditHud();
      return;
    }

    selectObject(
      location,
      event.shiftKey
    );

    updateEditHud();

    console.log(
      "[edit] selected whole location",
      location.name || "Clinic"
    );
  };

  renderer.domElement.addEventListener(
    "click",
    editClickHandler
  );

  renderer.domElement.addEventListener(
    "dblclick",
    editLocationDoubleClickHandler,
    true
  );

  window.addEventListener(
    "keydown",
    editKeyHandler,
    true
  );

  window.addEventListener(
    "keyup",
    editKeyUpHandler,
    true
  );

  loadEditorAssetInstances();
  loadSavedEdits();

  if (!editMotionStarted) {
    editMotionStarted = true;

    const editMotionTick = (): void => {
      const now = performance.now();

      const dt = Math.min(
        (now - editLastFrame) / 1000,
        0.05
      );

      editLastFrame = now;

      if (
        editMode &&
        editSelected.length > 0 &&
        editHeldKeys.size > 0
      ) {
        const moveSpeed = 2.5;
        const verticalSpeed = 2.0;
        const rotateSpeed =
          THREE.MathUtils.degToRad(75);

        const delta =
          new THREE.Vector3();

        const viewerForward =
          new THREE.Vector3();

        camera.getWorldDirection(
          viewerForward
        );

        viewerForward.y = 0;

        if (viewerForward.lengthSq() < 0.0001) {
          viewerForward.set(0, 0, -1);
        } else {
          viewerForward.normalize();
        }

        const viewerRight =
          new THREE.Vector3()
            .crossVectors(
              viewerForward,
              new THREE.Vector3(0, 1, 0)
            )
            .normalize();

        if (editHeldKeys.has("KeyW")) {
          delta.addScaledVector(
            viewerForward,
            moveSpeed * dt
          );
        }

        if (editHeldKeys.has("KeyS")) {
          delta.addScaledVector(
            viewerForward,
            -moveSpeed * dt
          );
        }

        if (editHeldKeys.has("KeyA")) {
          delta.addScaledVector(
            viewerRight,
            -moveSpeed * dt
          );
        }

        if (editHeldKeys.has("KeyD")) {
          delta.addScaledVector(
            viewerRight,
            moveSpeed * dt
          );
        }

        if (editHeldKeys.has("KeyR")) {
          delta.y += verticalSpeed * dt;
        }

        if (editHeldKeys.has("KeyF")) {
          delta.y -= verticalSpeed * dt;
        }

        if (delta.lengthSq() > 0) {
          moveSelectionWorld(delta);

          /*
           * Shift while moving = attachment mode.
           * Once a selected face gets close to a compatible
           * surface, make the two faces flush.
           */
          if (
            editHeldKeys.has("ShiftLeft") ||
            editHeldKeys.has("ShiftRight")
          ) {
            snapSelectionToNearbySurface();
          }
        }

        let yaw = 0;
        let pitch = 0;
        let roll = 0;

        if (editHeldKeys.has("KeyQ")) {
          yaw += rotateSpeed * dt;
        }

        if (editHeldKeys.has("KeyE")) {
          yaw -= rotateSpeed * dt;
        }

        if (editHeldKeys.has("KeyZ")) {
          pitch += rotateSpeed * dt;
        }

        if (editHeldKeys.has("KeyC")) {
          pitch -= rotateSpeed * dt;
        }

        if (editHeldKeys.has("KeyV")) {
          roll -= rotateSpeed * dt;
        }

        if (editHeldKeys.has("KeyB")) {
          roll += rotateSpeed * dt;
        }

        if (yaw) {
          rotateSelectionLocal(
            new THREE.Vector3(0, 1, 0),
            yaw
          );
        }

        if (pitch) {
          rotateSelectionLocal(
            new THREE.Vector3(1, 0, 0),
            pitch
          );
        }

        if (roll) {
          rotateSelectionLocal(
            new THREE.Vector3(0, 0, -1),
            roll
          );
        }
      }

      requestAnimationFrame(
        editMotionTick
      );
    };

    requestAnimationFrame(
      editMotionTick
    );
  }

  // ── HUD ───────────────────────────────────────────────────

  updateEditHud();

  // ── REVIEW SHOT MODE (?shot=name) ─────────────────────────

  if (shotName) {
    const shots = buildReviewShots();

    const shot = shots.find(
      (s) => s.name === shotName
    );

    if (shot) {
      if (shotName === "clinic_interior") {
        world.setClinicCutawayVisible(true);
      }

      applyReviewShot(camera, shot);

      player.dispose();
      player = null;

      renderOnce(container);
      return;
    }
  }

  // ── RENDER LOOP ───────────────────────────────────────────

  running = true;
  let firstFrame = true;

  window._ccRenderLoop = () => {
    if (!running) {
      window._ccRenderLoop = undefined;
      return;
    }

    const dt = 1 / 60;

    if (player && !mapOpen) {
      player.update(dt);
    }

    const ocean = (world as any)?.ocean;

    if (
      ocean &&
      ocean.isDisposed === false
    ) {
      ocean.update(dt);
    }

    // Update Mae relative to the player each frame.
    if (player) {
      world!.updateMae(
        dt,
        player.pose.position,
        player.pose.yaw,
        camera.position
      );
    }

    updateEditTransformOverlay();

    renderer.render(
      world!.sceneObj,
      camera
    );

    if (firstFrame) {
      firstFrame = false;

      (window as any).__CC_METRICS__ = {
        drawCalls:
          renderer.info.render.calls,
        triangles:
          renderer.info.render.triangles,
        geometries:
          renderer.info.memory.geometries,
        textures:
          renderer.info.memory.textures,
      };

      (window as any).__CC_READY__ = true;
    }

    if (window._ccRenderLoop) {
      requestAnimationFrame(
        window._ccRenderLoop
      );
    }
  };

  requestAnimationFrame(
    window._ccRenderLoop
  );
}

function renderOnce(
  container: HTMLElement
): void {
  renderer.setAnimationLoop(null);

  renderer.render(
    world!.sceneObj,
    camera
  );

  const info = renderer.info;

  const metrics = {
    drawCalls: info.render.calls,
    triangles: info.render.triangles,
    geometries: info.memory.geometries,
    textures: info.memory.textures,
  };

  (window as any).__CC_METRICS__ =
    metrics;

  (window as any).__CC_READY__ = true;
}

// ── PUBLIC API (used by capture.mjs) ───────────────────────

export function captureFrame(): Promise<Blob> {
  renderer.render(
    world!.sceneObj,
    camera
  );

  return new Promise((resolve) => {
    renderer.domElement.toBlob(
      (blob) => resolve(blob!),
      "image/png"
    );
  });
}

export function setCamera(
  pos: number[],
  look: number[],
  fov: number
): void {
  camera.position.set(
    pos[0],
    pos[1],
    pos[2]
  );

  camera.lookAt(
    new THREE.Vector3(
      look[0],
      look[1],
      look[2]
    )
  );

  camera.fov = fov;
  camera.updateProjectionMatrix();
}

export function resize(
  w: number,
  h: number
): void {
  renderer.setPixelRatio(1);
  renderer.setSize(w, h);

  camera.aspect = w / h;
  camera.updateProjectionMatrix();
}

export function dispose(): void {
  running = false;

  if (player) {
    player.dispose();
    player = null;
  }

  if (world) {
    world.dispose();
    world = null;
  }

  Materials.disposeAll();

  if (labelToggleHandler) {
    window.removeEventListener(
      "keydown",
      labelToggleHandler
    );

    labelToggleHandler = null;
  }

  if (planningToggleHandler) {
    window.removeEventListener(
      "keydown",
      planningToggleHandler
    );

    planningToggleHandler = null;
  }

  if (interiorToggleHandler) {
    window.removeEventListener(
      "keydown",
      interiorToggleHandler
    );

    interiorToggleHandler = null;
  }

  if (
    buildingDoorDoubleClickHandler
  ) {
    renderer.domElement.removeEventListener(
      "dblclick",
      buildingDoorDoubleClickHandler
    );

    buildingDoorDoubleClickHandler =
      null;
  }

  renderer.dispose();
  renderer.domElement.remove();
}

window.addEventListener(
  "resize",
  () => {
    if (
      renderer &&
      world &&
      player
    ) {
      camera.aspect =
        window.innerWidth /
        window.innerHeight;

      camera.updateProjectionMatrix();

      renderer.setSize(
        window.innerWidth,
        window.innerHeight
      );
    }
  }
);

declare global {
  interface Window {
    _ccRenderLoop?: () => void;
    __CC_METRICS__?: Record<
      string,
      number
    >;
    __CC_READY__?: boolean;
    __CC_INIT__?: () => void;
    __CC_CAPTURE__?: () => Promise<Blob>;
    __CC_SET_CAM__?: (
      pos: number[],
      look: number[],
      fov: number
    ) => void;
    __CC_RESIZE__?: (
      w: number,
      h: number
    ) => void;
    __CC_DISPOSE__?: () => void;
  }
}

window.__CC_INIT__ = init;
window.__CC_CAPTURE__ = captureFrame;
window.__CC_SET_CAM__ = setCamera;
window.__CC_RESIZE__ = resize;
window.__CC_DISPOSE__ = dispose;

// ── BOOT ────────────────────────────────────────────────────

function hideLoading(): void {
  const l =
    document.getElementById("loading");

  if (l) {
    (l as HTMLElement).style.opacity =
      "0";

    setTimeout(() => {
      l.remove();
    }, 400);
  }
}

function boot(): void {
  try {
    init();
    hideLoading();
  } catch (err) {
    const l =
      document.getElementById(
        "loading"
      );

    if (l) {
      l.textContent =
        "Failed to start rendering: " +
        (err as Error).message;
    }

    throw err;
  }
}

if (
  document.readyState === "loading"
) {
  document.addEventListener(
    "DOMContentLoaded",
    boot
  );
} else {
  boot();
}

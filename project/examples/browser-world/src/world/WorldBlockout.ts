/** Readable planning geometry for unfinished Critter Comforts districts. */
import * as THREE from "three";
import { terrainHeight } from "./Terrain";

type Kind = "building" | "care" | "park" | "station";
interface District { name: string; x: number; z: number; w: number; d: number; h: number; color: number; kind: Kind; rotation?: number }
interface Point { x: number; z: number }

// The first five entries form the compact civic/check-in ring around the square.
// Outer destinations are placed by use: home/workshop uphill, play at the coast,
// meadow and treehouse in quieter landscape pockets.
const DISTRICTS: District[] = [
  { name: "Town Hall + Bank", x: -7.4, z: 14.8, w: 6.4, d: 5.5, h: 4.7, color: 0xf39a9f, kind: "building", rotation: 8 },
  { name: "Cafe", x: -6.3, z: 8.0, w: 5.5, d: 4.8, h: 3.6, color: 0xf4b879, kind: "building", rotation: -12 },
  { name: "Park", x: 6.4, z: 8.2, w: 6.5, d: 5.5, h: 0.25, color: 0x89cc99, kind: "park" },
  { name: "Hospital", x: 7.5, z: 15.0, w: 6.8, d: 5.7, h: 4.5, color: 0x82ced0, kind: "care", rotation: -8 },
  { name: "Library", x: 0.2, z: 21.1, w: 7.2, d: 5.6, h: 5.4, color: 0xb8a8df, kind: "building" },
  { name: "Home", x: 12.8, z: 24.0, w: 6.2, d: 5.4, h: 4.0, color: 0xe8acc9, kind: "building", rotation: 12 },
  { name: "Workshop", x: -11.5, z: 24.2, w: 7.0, d: 5.8, h: 4.5, color: 0x91b9dc, kind: "building", rotation: -10 },
  { name: "Treehouse", x: -16.0, z: 12.0, w: 7.5, d: 7.5, h: 5.2, color: 0x82bd86, kind: "park" },
  { name: "Bliss Meadow", x: 4.5, z: 29.2, w: 12.0, d: 8.5, h: 0.2, color: 0x8fd876, kind: "park" },
  { name: "Train Station", x: -3.5, z: -6.5, w: 7.5, d: 5.2, h: 3.7, color: 0xee8a82, kind: "station" },
  { name: "Boardwalk", x: 19.2, z: 5.0, w: 4.4, d: 12.0, h: 1.4, color: 0xf0ce72, kind: "building", rotation: 0 },
  { name: "Cliff Overlook", x: 17.0, z: 17.5, w: 5.5, d: 5.0, h: 2.0, color: 0xb8cb9c, kind: "park", rotation: 10 },
];

const ROUTES: Array<{ points: Point[]; color: number; width?: number }> = [
  { points: [{ x: 0.2, z: -4.8 }, { x: -1.5, z: -1 }, { x: 0.4, z: 5 }, { x: -0.8, z: 9.5 }], color: 0xe88749, width: 1.35 },
  // short civic spokes; every check-in visibly connects to the fountain
  { points: [{ x: -1.7, z: 13.4 }, { x: -4.0, z: 14.0 }, { x: -6.0, z: 14.6 }], color: 0xd8b47a },
  { points: [{ x: -1.2, z: 11.5 }, { x: -3.5, z: 9.8 }, { x: -5.2, z: 8.6 }], color: 0xd8b47a },
  { points: [{ x: 2.2, z: 11.3 }, { x: 4.2, z: 9.5 }, { x: 5.5, z: 8.7 }], color: 0xd8b47a },
  { points: [{ x: 2.4, z: 13.8 }, { x: 4.7, z: 14.5 }, { x: 6.3, z: 14.8 }], color: 0xd8b47a },
  { points: [{ x: 0.5, z: 16.3 }, { x: 0.3, z: 18.4 }, { x: 0.2, z: 20.0 }], color: 0xd8b47a },
  // outer scenic loop
  { points: [{ x: -6.5, z: 15.5 }, { x: -10.0, z: 18.0 }, { x: -11.5, z: 23.0 }], color: 0x7da99a },
  { points: [{ x: 6.7, z: 15.8 }, { x: 10.0, z: 19.0 }, { x: 12.4, z: 23.0 }], color: 0xd7a83e },
  { points: [{ x: 0.2, z: 21.5 }, { x: 2.0, z: 25.0 }, { x: 4.0, z: 28.0 }], color: 0x8aa774, width: 1.0 },
  { points: [{ x: -6.3, z: 8.0 }, { x: -10.0, z: 9.0 }, { x: -14.5, z: 11.5 }], color: 0x83aa78, width: 1.0 },
  { points: [{ x: 6.2, z: 8.0 }, { x: 11.0, z: 6.0 }, { x: 17.5, z: 5.0 }], color: 0xd7a83e, width: 1.0 },
];

function mat(color: number, opacity = 0.58): THREE.MeshStandardMaterial {
  return new THREE.MeshStandardMaterial({
    color,
    emissive: new THREE.Color(color),
    // A tiny color lift keeps the pastel palette readable without flattening
    // the forms into glowing translucent blocks.
    emissiveIntensity: 0.025,
    roughness: 0.9,
    metalness: 0,
    transparent: opacity < 1,
    opacity,
    depthWrite: opacity >= 1,
    flatShading: true,
  });
}

function makeLabel(text: string, color: number, maxWidth = 2.35): THREE.Sprite {
  const canvas = document.createElement("canvas");
  const ctx = canvas.getContext("2d")!;
  const fontSize = 38;
  ctx.font = `600 ${fontSize}px system-ui, sans-serif`;
  canvas.width = Math.ceil(ctx.measureText(text).width) + 36;
  canvas.height = fontSize + 20;
  ctx.fillStyle = "rgba(20,29,39,.82)";
  ctx.fillRect(1, 1, canvas.width - 2, canvas.height - 2);
  ctx.strokeStyle = new THREE.Color(color).getStyle();
  ctx.lineWidth = 3;
  ctx.strokeRect(1, 1, canvas.width - 2, canvas.height - 2);
  ctx.fillStyle = "white";
  ctx.font = `600 ${fontSize}px system-ui, sans-serif`;
  ctx.textAlign = "center";
  ctx.textBaseline = "middle";
  ctx.fillText(text, canvas.width / 2, canvas.height / 2);
  const texture = new THREE.CanvasTexture(canvas);
  texture.colorSpace = THREE.SRGBColorSpace;
  const sprite = new THREE.Sprite(new THREE.SpriteMaterial({ map: texture, transparent: true, depthTest: true, depthWrite: false }));
  const width = Math.min(maxWidth, canvas.width / 105);
  sprite.scale.set(width, width * canvas.height / canvas.width, 1);
  return sprite;
}

export class WorldBlockout {
  private group = new THREE.Group();
  private geometries: THREE.BufferGeometry[] = [];
  private materials: THREE.Material[] = [];
  private textures: THREE.Texture[] = [];
  private labelsVisible = true;

  constructor() {
    this.group.name = "WorldBlockout";
    this.buildDistricts();
    // ROUTES remain authored layout data, but the old box-per-sample preview
    // broke apart on slopes and polluted the ground. Keep it out of the
    // presentation until branches use the terrain-following path mesh system.
    this.buildRail();
    this.buildFerrisWheel();
    this.buildDestinations();
  }

  private add(geometry: THREE.BufferGeometry, material: THREE.Material | THREE.Material[], parent = this.group): THREE.Mesh {
    const mesh = new THREE.Mesh(geometry, material);
    mesh.castShadow = false;
    mesh.receiveShadow = false;
    parent.add(mesh);
    this.geometries.push(geometry);
    if (Array.isArray(material)) this.materials.push(...material);
    else this.materials.push(material);
    return mesh;
  }

  private tag(parent: THREE.Group, text: string, color: number, y: number, maxWidth = 2.35): void {
    const tag = makeLabel(text, color, maxWidth);
    tag.position.y = y;
    parent.add(tag);
    const material = tag.material as THREE.SpriteMaterial;
    if (material.map) this.textures.push(material.map);
    this.materials.push(material);
  }

  private buildDistricts(): void {
    for (const d of DISTRICTS) {
      // The authored, enterable Clinic owns this location now. Do not place the
      // old solid care blockout on top of it.
      if (d.name === "Hospital") continue;
      const root = new THREE.Group();
      root.name = `Blockout — ${d.name}`;
      root.position.set(d.x, terrainHeight(d.x, d.z), d.z);
      const civic = ["Town Hall + Bank", "Cafe", "Hospital", "Library"].includes(d.name);
      root.rotation.y = civic
        ? Math.atan2(0.5 - d.x, 13.5 - d.z)
        : THREE.MathUtils.degToRad(d.rotation ?? 0);
      this.group.add(root);
      // Buildings sit directly in the landscape. Only give enclosed locations
      // a modest front apron instead of a giant rectangular district slab.
      if (d.kind !== "park" && d.name !== "Boardwalk") {
        const apron = this.add(
          new THREE.BoxGeometry(Math.min(2.8, d.w * 0.45), 0.045, 1.15),
          mat(0xd8ccb7, 1),
          root
        );
        apron.position.set(0, 0.04, d.d * 0.4 + 0.56);
      }
      d.kind === "park" ? this.park(root, d) : this.building(root, d);
      this.tag(root, d.name, d.color, Math.max(1.5, d.h + 0.8));
    }
  }

  private building(root: THREE.Group, d: District): void {
    if (d.name === "Boardwalk") {
      this.boardwalk(root, d);
      return;
    }
    if (d.name === "Library") {
      this.library(root, d);
      return;
    }
    const bh = d.h * 0.72;
    const bodyColor = new THREE.Color(d.color).lerp(new THREE.Color(0xfff4df), 0.18).getHex();
    const body = this.add(new THREE.BoxGeometry(d.w * 0.82, bh, d.d * 0.78), mat(bodyColor, 1), root);
    body.position.y = bh / 2 + 0.1;
    body.castShadow = true;
    body.receiveShadow = true;
    const rh = Math.min(1.25, d.h * 0.28);
    const roofColor = new THREE.Color(d.color).offsetHSL(0, 0, -0.12).getHex();
    const roof = this.add(new THREE.ConeGeometry(Math.max(d.w, d.d) * 0.52, rh, 4), mat(roofColor, 1), root);
    roof.position.y = bh + rh / 2 + 0.1;
    roof.rotation.y = Math.PI / 4;
    roof.castShadow = true;
    const door = this.add(new THREE.BoxGeometry(0.75, 1.65, 0.12), mat(0x27384a, 0.82), root);
    door.position.set(0, 0.93, d.d * 0.4 + 0.08);
    // Calm dollhouse detailing: warm windows, a soft trim rail, and a porch.
    for (const side of [-1, 1]) {
      const window = this.add(new THREE.BoxGeometry(1.15, 1.05, 0.09), mat(0xffe7ad, 1), root);
      window.position.set(side * d.w * 0.23, Math.max(1.25, bh * 0.55), d.d * 0.4 + 0.1);
    }
    const trim = this.add(new THREE.BoxGeometry(d.w * 0.76, 0.14, 0.12), mat(0xfff1da, 1), root);
    trim.position.set(0, Math.max(2.05, bh * 0.76), d.d * 0.4 + 0.12);
    const porch = this.add(new THREE.CylinderGeometry(1.4, 1.55, 0.18, 12), mat(0xe8d6bd, 1), root);
    porch.position.set(0, 0.12, d.d * 0.48 + 0.6);
    if (d.kind === "care") {
      // Friendly neighborhood hospital: cream badge, coral cross, rounded
      // canopy, small side wings, and mint planters rather than an office box.
      const badge = this.add(new THREE.CylinderGeometry(0.72, 0.72, 0.12, 24), mat(0xfff7e8, 0.98), root);
      badge.rotation.x = Math.PI / 2;
      badge.position.set(0, bh * 0.68, d.d * 0.4 + 0.17);
      const a = this.add(new THREE.BoxGeometry(0.82, 0.2, 0.15), mat(0xf27f83, 0.98), root);
      const b = this.add(new THREE.BoxGeometry(0.2, 0.82, 0.15), mat(0xf27f83, 0.98), root);
      a.position.set(0, bh * 0.68, d.d * 0.4 + 0.25);
      b.position.copy(a.position);
      const canopy = this.add(new THREE.CapsuleGeometry(0.34, 2.1, 6, 12), mat(0xffd19a, 0.94), root);
      canopy.rotation.z = Math.PI / 2;
      canopy.rotation.x = Math.PI / 2;
      canopy.position.set(0, 2.18, d.d * 0.44 + 0.66);
      for (const side of [-1, 1]) {
        const wing = this.add(new THREE.BoxGeometry(1.45, bh * 0.64, d.d * 0.58), mat(side < 0 ? 0xa9ded1 : 0xb7e3db, 1), root);
        wing.position.set(side * d.w * 0.37, bh * 0.32 + 0.1, -0.05);
        const planter = this.add(new THREE.CylinderGeometry(0.42, 0.34, 0.45, 10), mat(0xf4b6c8, 0.94), root);
        planter.position.set(side * 1.45, 0.3, d.d * 0.5 + 0.72);
        const shrub = this.add(new THREE.IcosahedronGeometry(0.48, 1), mat(0x88cf8b, 0.96), root);
        shrub.position.set(side * 1.45, 0.82, d.d * 0.5 + 0.72);
      }
    }
    if (d.kind === "station") {
      const canopy = this.add(new THREE.BoxGeometry(d.w * 0.9, 0.12, 1.15), mat(0xf0d39a, 0.75), root);
      canopy.position.set(0, 2.25, d.d * 0.25);
    }
    this.addLocationIdentity(root, d, bh);
  }

  private library(root: THREE.Group, d: District): void {
    const front = d.d * 0.39 + 0.08;
    const bodyH = 4.25;
    const lavender = mat(0xb9acd9, 1);
    const lavenderDeep = mat(0x7769a9, 1);
    const cream = mat(0xffefd2, 1);
    const windowGlow = mat(0xffdda0, 1);
    const wood = mat(0x5e4656, 1);

    const openFront = new THREE.MeshBasicMaterial({
      transparent: true,
      opacity: 0,
      depthWrite: false,
      colorWrite: false,
    });
    // Preserve the original body mesh and hierarchy id while opening only its
    // local +Z face, creating a readable dollhouse cutaway into the shop.
    const body = this.add(
      new THREE.BoxGeometry(6.35, bodyH, 4.25),
      [lavender, lavender, lavender, lavender, openFront, lavender],
      root
    );
    body.position.y = bodyH / 2 + 0.1;
    body.castShadow = true;
    body.receiveShadow = true;

    // Tall central gable creates a recognizable civic silhouette behind the fountain.
    const gable = this.add(new THREE.ConeGeometry(3.7, 1.65, 4), lavenderDeep, root);
    gable.position.set(0, bodyH + 0.92, 0);
    gable.rotation.y = Math.PI / 4;
    gable.castShadow = true;

    // Rounded reading-room bays sit proud of the facade instead of hiding behind it.
    for (const side of [-1, 1]) {
      const bay = this.add(new THREE.CylinderGeometry(0.72, 0.78, 3.45, 12), lavender, root);
      bay.position.set(side * 2.65, 1.82, front - 0.08);
      bay.castShadow = true;
      const cap = this.add(new THREE.ConeGeometry(0.92, 0.92, 12), lavenderDeep, root);
      cap.position.set(side * 2.65, 4.0, front - 0.08);

      const tallWindow = this.add(new THREE.CapsuleGeometry(0.32, 0.9, 8, 14), windowGlow, root);
      tallWindow.position.set(side * 2.65, 2.02, front + 0.72);
      tallWindow.scale.z = 0.16;

      const planter = this.add(new THREE.CylinderGeometry(0.3, 0.24, 0.34, 10), mat(0xd9919f, 1), root);
      planter.position.set(side * 1.25, 0.22, front + 0.76);
      const plant = this.add(new THREE.IcosahedronGeometry(0.32, 1), mat(0x82bd86, 1), root);
      plant.position.set(side * 1.25, 0.62, front + 0.76);
    }

    // Warm arched doorway with a small projecting reading-room porch.
    const door = this.add(new THREE.BoxGeometry(1.28, 2.25, 0.14), wood, root);
    door.position.set(0, 1.23, front + 0.08);
    const arch = this.add(new THREE.TorusGeometry(0.64, 0.11, 8, 24, Math.PI), cream, root);
    arch.position.set(0, 2.34, front + 0.17);
    const canopy = this.add(new THREE.BoxGeometry(2.25, 0.16, 0.82), cream, root);
    canopy.position.set(0, 2.76, front + 0.46);

    for (const side of [-1, 1]) {
      const window = this.add(new THREE.CapsuleGeometry(0.38, 1.05, 8, 14), windowGlow, root);
      window.position.set(side * 1.55, 2.25, front + 0.1);
      window.scale.z = 0.16;
    }

    // Book-spine frieze makes the function readable without a floating label.
    const spineColors = [0xf0a3a7, 0x8fc7c2, 0xf4cf83, 0x8da9d4, 0xd8a8cb];
    for (let i = 0; i < 9; i++) {
      const spine = this.add(new THREE.BoxGeometry(0.32, 0.52 + (i % 3) * 0.08, 0.1), mat(spineColors[i % spineColors.length], 1), root);
      spine.position.set(-1.35 + i * 0.34, 3.55, front + 0.12);
    }

    const step = this.add(new THREE.CylinderGeometry(1.35, 1.48, 0.18, 16), mat(0xe5d3b9, 1), root);
    step.position.set(0, 0.12, front + 0.78);

    // Finish the entrance last so existing editor child indices stay stable.
    // The former single dark slab read as an empty hole from the square;
    // paired warm panels and small glazed lights make it feel welcoming.
    const doorFace = front + 0.165;
    for (const side of [-1, 1]) {
      const panel = this.add(
        new THREE.BoxGeometry(0.54, 1.62, 0.055),
        mat(side < 0 ? 0x9a6570 : 0x8b5c69, 1),
        root
      );
      panel.position.set(side * 0.29, 1.12, doorFace);

      const glass = this.add(
        new THREE.BoxGeometry(0.34, 0.46, 0.035),
        windowGlow,
        root
      );
      glass.position.set(side * 0.29, 1.55, doorFace + 0.04);

      const handle = this.add(
        new THREE.SphereGeometry(0.055, 10, 8),
        mat(0xf2c66d, 1),
        root
      );
      handle.position.set(side * 0.1, 1.02, doorFace + 0.075);
    }

    this.buildLibraryInterior(root);
  }

  private buildLibraryInterior(root: THREE.Group): void {
    const wall = mat(0xb9acd9, 1);
    const darkWood = mat(0x4d342d, 0.94);
    const warmWood = mat(0x8b6047, 0.94);
    const shelfBack = mat(0x6f4b3b, 0.98);
    const cream = mat(0xffefd2, 0.98);
    const brass = mat(0xd9ad55, 0.82);
    const carpet = mat(0x426b59, 0.96);
    const headphone = mat(0x34333f, 0.72);
    const bookColors = [0xb95f68, 0xd39a56, 0x668d87, 0x72679b, 0xd7bd75];
    const movieColors = [0x365b76, 0x8f4e69, 0xc88c4e, 0x5e7656];

    // Rebuild the shell as real wall segments around the interior. The legacy
    // body mesh remains in its original hierarchy slot but has an invisible
    // front; these appended walls preserve that stable id while restoring a
    // complete building with one intentional doorway opening.
    const backWall = this.add(new THREE.BoxGeometry(6.35, 4.25, 0.18), wall, root);
    backWall.position.set(0, 2.225, -2.08);
    for (const side of [-1, 1]) {
      const sideWall = this.add(new THREE.BoxGeometry(0.18, 4.25, 4.16), wall, root);
      sideWall.position.set(side * 3.085, 2.225, 0);

      const frontWall = this.add(new THREE.BoxGeometry(2.38, 4.25, 0.18), wall, root);
      frontWall.position.set(side * 1.985, 2.225, 2.08);
    }
    const doorwayHeader = this.add(new THREE.BoxGeometry(1.58, 1.62, 0.18), wall, root);
    doorwayHeader.position.set(0, 3.54, 2.08);
    const ceiling = this.add(new THREE.BoxGeometry(6.35, 0.16, 4.16), cream, root);
    ceiling.position.set(0, 4.27, 0);

    // Warm bookstore foundation and a green central browsing runner.
    const floor = this.add(new THREE.BoxGeometry(5.85, 0.1, 3.72), warmWood, root);
    floor.position.set(0, 0.16, -0.08);
    const runner = this.add(new THREE.BoxGeometry(1.25, 0.025, 3.15), carpet, root);
    runner.position.set(0, 0.225, -0.02);

    // BOOKS — three substantial back-wall cases with varied, readable spines.
    for (const caseX of [-1.9, 0, 1.9]) {
      const shelf = this.add(new THREE.BoxGeometry(1.62, 2.65, 0.26), darkWood, root);
      shelf.position.set(caseX, 1.5, -1.83);
      for (const shelfY of [0.62, 1.28, 1.94, 2.58]) {
        const ledge = this.add(new THREE.BoxGeometry(1.5, 0.08, 0.4), shelfBack, root);
        ledge.position.set(caseX, shelfY, -1.63);
        for (let i = 0; i < 7; i++) {
          const height = 0.36 + ((i + Math.round(caseX)) % 3) * 0.06;
          const book = this.add(
            new THREE.BoxGeometry(0.14, height, 0.16),
            mat(bookColors[(i + Math.round((caseX + 2) * 2)) % bookColors.length], 0.9),
            root
          );
          book.position.set(caseX - 0.58 + i * 0.19, shelfY + 0.04 + height / 2, -1.58);
          book.rotation.z = i === 5 ? -0.09 : 0;
        }
      }
    }

    // MOVIES — lower face-out media racks along the right-hand wall.
    for (const z of [-0.95, 0.05, 1.05]) {
      const rack = this.add(new THREE.BoxGeometry(0.34, 1.65, 0.82), darkWood, root);
      rack.position.set(2.7, 1.03, z);
      for (let row = 0; row < 3; row++) {
        for (let i = 0; i < 3; i++) {
          const movie = this.add(
            new THREE.BoxGeometry(0.07, 0.34, 0.21),
            mat(movieColors[(row + i) % movieColors.length], 0.86),
            root
          );
          movie.position.set(2.49, 0.5 + row * 0.48, z - 0.23 + i * 0.23);
        }
      }
    }

    // HEADPHONES — a friendly listening bar with three over-ear stations.
    const listeningBar = this.add(new THREE.BoxGeometry(1.75, 0.16, 0.62), cream, root);
    listeningBar.position.set(-1.75, 1.02, 0.55);
    for (const z of [0.2, 0.55, 0.9]) {
      const stand = this.add(new THREE.CylinderGeometry(0.025, 0.04, 0.62, 8), brass, root);
      stand.position.set(-1.75, 1.38, z);
      const band = this.add(new THREE.TorusGeometry(0.16, 0.035, 7, 14, Math.PI), headphone, root);
      band.position.set(-1.75, 1.69, z);
      band.rotation.y = Math.PI / 2;
      const leftCup = this.add(new THREE.SphereGeometry(0.07, 8, 6), headphone, root);
      const rightCup = this.add(new THREE.SphereGeometry(0.07, 8, 6), headphone, root);
      leftCup.position.set(-1.75, 1.56, z - 0.16);
      rightCup.position.set(-1.75, 1.56, z + 0.16);
    }

    // Section headers evoke the hand-lettered department signs of an old
    // bookstore without relying on copyrighted branding.
    const addHeader = (text: string, x: number, y: number, z: number): void => {
      const sign = makeLabel(text, 0x4d342d, 1.35);
      sign.position.set(x, y, z);
      sign.scale.multiplyScalar(0.72);
      root.add(sign);
      const material = sign.material as THREE.SpriteMaterial;
      if (material.map) this.textures.push(material.map);
      this.materials.push(material);
    };
    addHeader("BOOKS", 0, 3.05, -1.52);
    addHeader("MOVIES", 2.38, 2.08, 0.05);
    addHeader("LISTEN", -1.75, 2.1, 0.55);

    // Cozy central recommendation table and a tiny stack of staff picks.
    const table = this.add(new THREE.CylinderGeometry(0.58, 0.48, 0.72, 12), darkWood, root);
    table.position.set(0.55, 0.58, 0.48);
    for (let i = 0; i < 4; i++) {
      const pick = this.add(
        new THREE.BoxGeometry(0.42, 0.055, 0.28),
        mat(bookColors[i], 0.88),
        root
      );
      pick.position.set(0.55, 0.97 + i * 0.06, 0.48);
      pick.rotation.y = i * 0.18;
    }

    const lamp = new THREE.PointLight(0xffdca8, 1.35, 8, 2);
    lamp.position.set(0, 3.35, 0);
    root.add(lamp);
  }

  private boardwalk(root: THREE.Group, d: District): void {
    // ===== 2.1 Deck surface: 7 visible plank sections with alternating warm colors =====
    const plankWidth = d.w / 7;
    const plankColors = [0xddb878, 0xc9a36a, 0xd4b573, 0xbb955d, 0xd0a768, 0xc49e63, 0xd9ae72];
    for (let i = 0; i < 7; i++) {
      const plank = this.add(
        new THREE.BoxGeometry(plankWidth - 0.03, 0.24, d.d),
        mat(plankColors[i], 0.98),
        root
      );
      plank.position.set(-d.w / 2 + plankWidth / 2 + i * plankWidth, 0.2, 0);
    }
    // Plank seams in dark wood
    for (let i = 1; i < 7; i++) {
      const seam = this.add(
        new THREE.BoxGeometry(0.03, 0.025, d.d - 0.1),
        mat(0x8b5e3c, 0.72),
        root
      );
      seam.position.set(-d.w / 2 + i * plankWidth, 0.335, 0);
    }

    // ===== 2.2 Deck edge: 12cm wooden posts along both long edges at 2m intervals =====
    const edgePostMat = mat(0x6b4226, 0.98);
    for (const side of [-1, 1]) {
      for (let z = -d.d / 2 + 1; z < d.d / 2; z += 2) {
        const post = this.add(
          new THREE.CylinderGeometry(0.06, 0.06, 0.15, 6),
          edgePostMat,
          root
        );
        post.position.set(side * (d.w / 2), 0.285, z);
      }
    }

    // ===== 2.3 Enhanced railings: thicker posts, dual top rails, cross-bracing every 4th pair =====
    const railPostMat = mat(0x765038, 0.98);
    const railPositions: number[] = [];
    for (let z = -d.d / 2 + 0.7; z <= d.d / 2 - 0.7; z += 1.4) {
      railPositions.push(z);
    }
    for (const side of [-1, 1]) {
      // Thicker posts
      for (const z of railPositions) {
        const post = this.add(
          new THREE.CylinderGeometry(0.065, 0.085, 0.78, 7),
          railPostMat,
          root
        );
        post.position.set(side * (d.w / 2 - 0.12), 0.72, z);
      }
      // Dual top rails
      for (const y of [0.82, 0.94]) {
        const rail = this.add(
          new THREE.BoxGeometry(0.1, 0.07, d.d - 0.55),
          railPostMat,
          root
        );
        rail.position.set(side * (d.w / 2 - 0.12), y, 0);
      }
      // Cross-bracing every 4th pair
      const idx = railPositions.indexOf(side === -1 ? railPositions[Math.floor(railPositions.length / 4) * (railPositions.length % 4 === 0 ? 4 : Math.floor(railPositions.length / 4))] : -1);
      if (railPositions.length >= 4) {
        const z1 = railPositions[0];
        const z2 = railPositions[3];
        const span = z2 - z1;
        const len = Math.hypot(0, span);
        const brace = this.add(
          new THREE.CylinderGeometry(0.025, 0.025, len, 5),
          railPostMat,
          root
        );
        brace.position.set(side * (d.w / 2 - 0.12), 0.58, (z1 + z2) / 2);
        brace.rotation.x = Math.PI / 2;
      }
    }

    // ===== 2.4 Enhanced stalls (3 existing) with full detail =====
    const stallPositions = [-3.6, 0, 3.6];
    const stallColors = [0x78c8d2, 0xf39ca8, 0xf4cf83];
    const awningColors = [0xffdf79, 0xff9a9e, 0xa8e6cf];
    for (let si = 0; si < 3; si++) {
      const sz = stallPositions[si];
      const stallColor = stallColors[si];
      const awningColor = awningColors[si];

      // Stall body
      const stall = this.add(
        new THREE.BoxGeometry(1.45, 1.45, 1.55),
        mat(stallColor, 0.98),
        root
      );
      stall.position.set(-0.9, 1.05, sz);

      // Corner posts (4)
      const cpMat = mat(0x5e4630, 0.98);
      for (const [cx, cz] of [[-0.65, -0.7], [-0.65, 0.7], [0.65, -0.7], [0.65, 0.7]]) {
        const cornerPost = this.add(
          new THREE.BoxGeometry(0.08, 1.6, 0.08),
          cpMat,
          root
        );
        cornerPost.position.set(-0.9 + cx, 1.85, sz + cz);
      }

      // Window glass on front face
      const windowGlow = mat(0xffe7ad, 0.75);
      windowGlow.transparent = true;
      windowGlow.opacity = 0.75;
      const windowPane = this.add(
        new THREE.BoxGeometry(0.9, 0.6, 0.03),
        windowGlow,
        root
      );
      windowPane.position.set(-0.9, 1.25, sz + 0.79);

      // Extended counter
      const counter = this.add(
        new THREE.BoxGeometry(1.55, 0.1, 0.45),
        mat(0x8b6f47, 0.98),
        root
      );
      counter.position.set(-0.9, 0.85, sz + 0.95);

      // Scalloped awning (base) + scallop bumps
      const awningBase = this.add(
        new THREE.ConeGeometry(1.0, 0.25, 4),
        mat(awningColor, 0.98),
        root
      );
      awningBase.position.set(-0.9, 2.0, sz + 0.65);
      awningBase.rotation.y = Math.PI / 4;

      // Scalloped edge bumps along awning front
      for (let si2 = 0; si2 < 5; si2++) {
        const scallop = this.add(
          new THREE.SphereGeometry(0.12, 6, 4),
          mat(awningColor, 0.98),
          root
        );
        scallop.scale.y = 0.5;
        scallop.position.set(-0.55 + si2 * 0.28, 1.82, sz + 1.15);
      }

      // Hanging sign
      const signPost = this.add(
        new THREE.BoxGeometry(0.04, 0.4, 0.04),
        mat(0x5e4630, 0.98),
        root
      );
      signPost.position.set(-0.9, 2.15, sz + 1.0);
      const signBoard = this.add(
        new THREE.BoxGeometry(0.5, 0.28, 0.04),
        mat(0xfff4e0, 0.98),
        root
      );
      signBoard.position.set(-0.9, 1.95, sz + 1.2);

      // Interior glow (small emissive box inside stall)
      const glowMat = mat(0xffd080, 0.6);
      (glowMat as THREE.MeshStandardMaterial).emissive = new THREE.Color(0xffd080);
      (glowMat as THREE.MeshStandardMaterial).emissiveIntensity = 0.3;
      this.add(
        new THREE.BoxGeometry(0.05, 0.05, 0.05),
        glowMat,
        root
      ).position.set(-0.9, 1.3, sz);
    }

    // ===== 2.5 Lamps (6 total): gold posts with emissive glow, every ~3m =====
    const goldMat = new THREE.MeshStandardMaterial({
      color: 0xf2c66d,
      emissive: new THREE.Color(0xffd080),
      emissiveIntensity: 0.5,
      roughness: 0.4,
      metalness: 0.6,
      flatShading: true,
    });
    const lampPositions = [-3.5, -0.5, 2.5];
    for (const side of [-1, 1]) {
      for (const z of lampPositions) {
        const lampPost = new THREE.Group();
        // Post
        const post = this.add(
          new THREE.CylinderGeometry(0.03, 0.035, 2.2, 6),
          mat(0xf2c66d, 0.98),
          root
        );
        post.position.set(side * (d.w / 2 - 0.3), 1.3, z);
        // Lamp head (emissive sphere)
        const lampHead = this.add(
          new THREE.SphereGeometry(0.15, 8, 6),
          mat(0xffe8a0, 0.9),
          root
        );
        lampHead.position.set(side * (d.w / 2 - 0.3), 2.45, z);
        (lampHead.material as THREE.MeshStandardMaterial).emissive = new THREE.Color(0xffd080);
        (lampHead.material as THREE.MeshStandardMaterial).emissiveIntensity = 0.8;
      }
    }

    // ===== 2.6 Planters + Bollards: 4 corner planters with flowers, 4 rope bollards at front =====
    // Corner planters
    const planterPositions = [[-d.w / 2 + 0.4, -d.d / 2 + 0.4], [-d.w / 2 + 0.4, d.d / 2 - 0.4], [d.w / 2 - 0.4, -d.d / 2 + 0.4], [d.w / 2 - 0.4, d.d / 2 - 0.4]];
    for (const [px, pz] of planterPositions) {
      const planter = this.add(
        new THREE.CylinderGeometry(0.25, 0.2, 0.3, 8),
        mat(0xc4836a, 0.98),
        root
      );
      planter.position.set(px, 0.35, pz);
      // Flowers (small colored spheres)
      const flowerColors = [0xff6b8a, 0xffa07a, 0xdb7093, 0xffd700, 0xff69b4, 0xee82ee];
      for (let fi = 0; fi < 6; fi++) {
        const angle = fi * Math.PI / 3;
        const flower = this.add(
          new THREE.SphereGeometry(0.06, 6, 4),
          mat(flowerColors[fi % flowerColors.length], 0.98),
          root
        );
        flower.position.set(px + Math.cos(angle) * 0.12, 0.58 + Math.random() * 0.15, pz + Math.sin(angle) * 0.12);
      }
    }
    // Rope bollards at front edge
    const bollardMats = mat(0x8b5e3c, 0.98);
    for (let bi = 0; bi < 4; bi++) {
      const bx = -d.w / 2 + 1.2 + bi * 2.5;
      const bollard = this.add(
        new THREE.CylinderGeometry(0.06, 0.07, 0.5, 6),
        bollardMats,
        root
      );
      bollard.position.set(bx, 0.47, d.d / 2 - 0.3);
      // Rope between bollards
      if (bi < 3) {
        const dx = 2.5;
        const rope = this.add(
          new THREE.CylinderGeometry(0.015, 0.015, dx, 4),
          mat(0xd4a76a, 0.98),
          root
        );
        rope.position.set(bx + 1.25, 0.65, d.d / 2 - 0.3);
        rope.rotation.z = Math.atan2(0, dx);
      }
    }
  }

  private addLocationIdentity(root: THREE.Group, d: District, bh: number): void {
    const front = d.d * 0.4 + 0.24;
    if (d.name.startsWith("Town Hall")) {
      // Friendly civic face: paired service doors and a round clock crest.
      for (const x of [-0.62, 0.62]) {
        const door = this.add(new THREE.BoxGeometry(0.72, 1.55, 0.14), mat(0x6389a6, 0.98), root);
        door.position.set(x, 0.88, front);
      }
      const clock = this.add(new THREE.CylinderGeometry(0.48, 0.48, 0.12, 24), mat(0xfff4c9, 0.98), root);
      clock.rotation.x = Math.PI / 2;
      clock.position.set(0, bh * 0.76, front + 0.02);
    } else if (d.name === "Cafe") {
      const awning = this.add(new THREE.BoxGeometry(d.w * 0.68, 0.18, 0.8), mat(0xf08f86, 0.98), root);
      awning.position.set(0, 2.15, front + 0.36);
      const mug = this.add(new THREE.TorusGeometry(0.26, 0.08, 8, 16, Math.PI * 1.55), mat(0xfff1d2, 0.98), root);
      mug.position.set(0, 2.75, front + 0.08);
      mug.rotation.z = -0.35;
    } else if (d.name === "Library") {
      for (const x of [-d.w * 0.29, d.w * 0.29]) {
        const turret = this.add(new THREE.CylinderGeometry(0.6, 0.7, bh * 0.82, 10), mat(0xa99ad3, 0.98), root);
        turret.position.set(x, bh * 0.41, 0.08);
        const cap = this.add(new THREE.ConeGeometry(0.78, 0.8, 10), mat(0x7769a9, 0.98), root);
        cap.position.set(x, bh * 0.82 + 0.4, 0.08);
      }
      const book = this.add(new THREE.BoxGeometry(1.3, 0.82, 0.13), mat(0xffdf9a, 0.98), root);
      book.position.set(0, bh * 0.62, front + 0.03);
    } else if (d.name === "Home") {
      const chimney = this.add(new THREE.BoxGeometry(0.55, 1.55, 0.55), mat(0xb86f70, 0.98), root);
      chimney.position.set(-d.w * 0.23, bh + 0.62, -0.35);
      const stoop = this.add(new THREE.BoxGeometry(2.1, 0.16, 1.0), mat(0xe8c7a8, 0.98), root);
      stoop.position.set(0, 0.13, front + 0.45);
    } else if (d.name === "Workshop") {
      for (const x of [-0.85, 0, 0.85]) {
        const tooth = this.add(new THREE.ConeGeometry(0.72, 0.8, 4), mat(0x577fa7, 0.98), root);
        tooth.position.set(x, bh + 0.74, 0);
        tooth.rotation.y = Math.PI / 4;
      }
      const lamp = this.add(new THREE.SphereGeometry(0.24, 12, 8), mat(0x9ff4ef, 0.98), root);
      lamp.position.set(0, 2.7, front + 0.08);
    } else if (d.name === "Train Station") {
      const clock = this.add(new THREE.CylinderGeometry(0.38, 0.38, 0.12, 20), mat(0xfff2c2, 0.98), root);
      clock.rotation.x = Math.PI / 2;
      clock.position.set(0, 2.82, front + 0.04);
      const platform = this.add(new THREE.BoxGeometry(d.w * 1.05, 0.18, 1.5), mat(0xd7c2a1, 0.98), root);
      platform.position.set(0, 0.1, front + 0.72);
    }
  }

  private park(root: THREE.Group, d: District): void {
    const spots = d.name === "Park" ? [[-1.2, -0.7, 0.9], [1.1, 0.4, 1.05], [-0.2, 1.1, 0.75]] : [[0, 0, 1.45]];
    for (const [x, z, scale] of spots) {
      const trunk = this.add(new THREE.CylinderGeometry(0.12 * scale, 0.17 * scale, 1.3 * scale, 7), mat(0x6d4b31, 0.86), root);
      trunk.position.set(x, 0.65 * scale, z);
      const crown = this.add(new THREE.IcosahedronGeometry(0.75 * scale, 1), mat(d.color, 0.68), root);
      crown.position.set(x, 1.6 * scale, z);
    }
  }

  private buildRoutes(): void {
    for (const route of ROUTES) for (let i = 0; i < route.points.length - 1; i++) this.route(route.points[i], route.points[i + 1], route.color, route.width ?? 1.15);
  }

  private route(a: Point, b: Point, _color: number, width: number): void {
    const dx = b.x - a.x;
    const dz = b.z - a.z;
    const length = Math.hypot(dx, dz);
    const count = Math.max(1, Math.ceil(length / 0.75));
    for (let i = 0; i < count; i++) {
      const t = (i + 0.5) / count;
      const x = THREE.MathUtils.lerp(a.x, b.x, t);
      const z = THREE.MathUtils.lerp(a.z, b.z, t);
      const stone = this.add(
        new THREE.BoxGeometry(width, 0.025, length / count * 1.08),
        mat(0xd7c69f, 1)
      );
      stone.position.set(x, terrainHeight(x, z) + 0.045, z);
      stone.rotation.y = Math.atan2(dx, dz);
    }
  }

  private buildRail(): void {
    for (let z = -31; z <= -6; z += 1.4) {
      const tie = this.add(new THREE.BoxGeometry(3, 0.07, 0.18), mat(0x69482e, 0.88));
      tie.position.set(0.2, terrainHeight(0.2, z) + 0.06, z);
    }
    for (const x of [-0.52, 0.92]) {
      const rail = this.add(new THREE.BoxGeometry(0.09, 0.1, 25), mat(0x727b83, 0.9));
      rail.position.set(x, terrainHeight(x, -18.5) + 0.12, -18.5);
    }
  }

  private buildFerrisWheel(): void {
    const root = new THREE.Group();
    root.position.set(19.0, terrainHeight(19.0, 8.2), 8.2);
    this.group.add(root);

    // ========== PALETTE ==========
    const timberMat = mat(0x8B6914, 0.75);
    const gondolaColors = [0xF17779, 0xFFE487, 0x69B6C4, 0xC0A9DD, 0xFA9FB6, 0x83D47E, 0xF5EFE0, 0xFF9966];
    const canopyMat = mat(0xFFF6EE, 0.75);
    const scallopMat = mat(0xF17779, 0.75);
    const glassMat = mat(0xBFE2F0, 0.75);
    glassMat.transparent = true;
    glassMat.opacity = 0.75;
    const wheelCenterY = 3.25;
    const rimRadius = 2.7;
    const gondolaRing = 4.1;
    const faceColor = 0x333333;

    // ========== 1. Main wheel ring (KEPT AS-IS) ==========
    this.add(
      new THREE.TorusGeometry(2.7, 0.22, 10, 32),
      mat(0xA8805A, 0.78),
      root
    ).position.y = wheelCenterY;

    // ========== 2. Hub structure ==========
    this.add(
      new THREE.CylinderGeometry(0.35, 0.35, 0.3, 8),
      mat(0x7A5C3E, 0.75),
      root
    ).position.set(0, wheelCenterY, 0);

    const innerHub = this.add(
      new THREE.CylinderGeometry(0.15, 0.15, 0.6, 6),
      mat(0x7A5C3E, 0.75),
      root
    );
    innerHub.position.set(0, wheelCenterY, 0);
    innerHub.rotation.z = Math.PI / 2;

    // Crown / top cap (kept current)
    this.add(
      new THREE.ConeGeometry(0.28, 0.5, 6),
      mat(0xF17779, 0.75),
      root
    ).position.set(0, 3.85, 0);

    // 4 small gold decorations around crown base
    for (let i = 0; i < 4; i++) {
      const a = i * Math.PI / 2;
      this.add(
        new THREE.SphereGeometry(0.06, 6, 4),
        mat(0xFFE487, 0.8),
        root
      ).position.set(Math.sin(a) * 0.2, 3.78, Math.cos(a) * 0.2);
    }

    // ========== 3. A-frame support structure ==========
    // Left A-frame: inner and outer legs, angled inward to hub
    // Inner leg (from x=-0.3 at bottom to x=-0.8 at top)
    const leftInner = this.add(
      new THREE.CylinderGeometry(0.08, 0.10, 4.2, 6),
      timberMat,
      root
    );
    leftInner.position.set(-0.3, 2.1, 0);
    leftInner.rotation.z = Math.atan2(0.5, 4.2);

    // Outer leg (from x=-1.0 at bottom to x=-0.8 at top)
    const leftOuter = this.add(
      new THREE.CylinderGeometry(0.08, 0.10, 4.2, 6),
      timberMat,
      root
    );
    leftOuter.position.set(-1.0, 2.1, 0);
    leftOuter.rotation.z = -Math.atan2(0.2, 4.2);

    // Right A-frame: mirrored
    const rightInner = this.add(
      new THREE.CylinderGeometry(0.08, 0.10, 4.2, 6),
      timberMat,
      root
    );
    rightInner.position.set(0.3, 2.1, 0);
    rightInner.rotation.z = -Math.atan2(0.5, 4.2);

    const rightOuter = this.add(
      new THREE.CylinderGeometry(0.08, 0.10, 4.2, 6),
      timberMat,
      root
    );
    rightOuter.position.set(1.0, 2.1, 0);
    rightOuter.rotation.z = Math.atan2(0.2, 4.2);

    // Cross-beams at y=1.8 and y=2.8
    for (const cy of [1.8, 2.8]) {
      const beamLen = 2.0;
      this.add(
        new THREE.CylinderGeometry(0.06, 0.06, beamLen, 6),
        timberMat,
        root
      ).position.set(0, cy, 0);
    }

    // X-braces (thickened to 0.07 radius)
    for (const sign of [-1, 1]) {
      const brace = this.add(
        new THREE.CylinderGeometry(0.07, 0.07, 3.5, 6),
        timberMat,
        root
      );
      brace.position.set(sign * 0.5, 1.75, 0);
      brace.rotation.z = sign * 0.55;
    }

    // ========== 4. Enlarged base platform ==========
    // Main disk
    this.add(
      new THREE.CylinderGeometry(2.0, 1.8, 0.25, 10),
      mat(0xA8A090, 0.82),
      root
    ).position.y = 0.125;

    // Pink torus edge trim
    this.add(
      new THREE.TorusGeometry(1.9, 0.08, 6, 16),
      mat(0xF17779, 0.75),
      root
    ).position.y = 0.25;
    root.children[root.children.length - 1].rotation.x = Math.PI / 2;

    // 3 steps on front
    for (const [sx, sy] of [[2.2, 0.075], [1.8, 0.225], [1.4, 0.375]]) {
      this.add(
        new THREE.BoxGeometry(sx, 0.15, 0.5),
        mat(0x9B7A50, 0.85),
        root
      ).position.set(0, sy, 1.2);
    }

    // ========== 5. Eight detailed gondola cabins at r=3.3 ==========
    for (let i = 0; i < 8; i++) {
      const angle = i * Math.PI / 4;
      const gx = Math.sin(angle) * gondolaRing;
      const gz = Math.cos(angle) * gondolaRing;

      // Connector rod: from rim (r=2.7) to gondola ring (r=3.3)
      const rimX = Math.sin(angle) * rimRadius;
      const rimZ = Math.cos(angle) * rimRadius;
      const rodLen = Math.hypot(gx - rimX, 3.25 - wheelCenterY + Math.cos(angle) * (gondolaRing - rimRadius));
      // Simplified: rod extends from rim edge outward to gondola ring
      const rodVertical = 3.25 - wheelCenterY;
      const rodLen2 = Math.sqrt((gondolaRing - rimRadius) * (gondolaRing - rimRadius) + rodVertical * rodVertical);
      const rod = this.add(
        new THREE.CylinderGeometry(0.04, 0.04, 0.8, 5),
        mat(0x7A5C3E, 0.75),
        root
      );
      rod.position.set((gx + rimX) / 2, 3.25 - 0.4, (gz + rimZ) / 2);
      const rodAngle = Math.atan2(gx - rimX, 3.25 - (wheelCenterY + rodVertical));
      // Rod goes from rim to gondola
      rod.position.set(gx * 0.8 + rimX * 0.2, 3.25 - 0.35, gz * 0.8 + rimZ * 0.2);

      // Gondola cabin body
      const gondolaY = 3.4;
      const body = this.add(
        new THREE.BoxGeometry(0.5, 0.45, 0.5),
        mat(gondolaColors[i], 0.75),
        root
      );
      body.position.set(gx, gondolaY, gz);

      // Interior back wall (white)
      this.add(
        new THREE.BoxGeometry(0.42, 0.3, 0.03),
        mat(0xFFFFFF, 0.75),
        root
      ).position.set(gx, gondolaY, gz - 0.25);

      // Windows on front face (2 panes, wider at 0.12x0.1)
      this.add(
        new THREE.BoxGeometry(0.12, 0.1, 0.02),
        glassMat,
        root
      ).position.set(gx - 0.12, gondolaY + 0.05, gz + 0.26);

      this.add(
        new THREE.BoxGeometry(0.12, 0.1, 0.02),
        glassMat,
        root
      ).position.set(gx + 0.12, gondolaY + 0.05, gz + 0.26);

      // Door on back face
      this.add(
        new THREE.BoxGeometry(0.18, 0.25, 0.02),
        mat(0xF17779, 0.75),
        root
      ).position.set(gx, gondolaY - 0.05, gz - 0.26);

      // Canopy (pillow-top sphere, half-height via scale)
      const canopyY = gondolaY + 0.45 / 2 + 0.12;
      const canopyMesh = this.add(
        new THREE.SphereGeometry(0.32, 8, 6),
        canopyMat,
        root
      );
      canopyMesh.position.set(gx, canopyY, gz);
      canopyMesh.scale.set(1, 0.55, 1);

      // 8 scalloped edge spheres on canopy perimeter
      for (let s = 0; s < 8; s++) {
        const sa = s * Math.PI / 4;
        this.add(
          new THREE.SphereGeometry(0.04, 4, 4),
          scallopMat,
          root
        ).position.set(gx + Math.sin(sa) * 0.3, canopyY, gz + Math.cos(sa) * 0.3);
      }

      // Cute face on front: 2 eyes + mouth
      const faceY = gondolaY + 0.05;
      const faceZ = gz + 0.26;
      this.add(
        new THREE.SphereGeometry(0.035, 6, 4),
        mat(faceColor, 0.9),
        root
      ).position.set(gx - 0.08, faceY + 0.04, faceZ + 0.01);
      this.add(
        new THREE.SphereGeometry(0.035, 6, 4),
        mat(faceColor, 0.9),
        root
      ).position.set(gx + 0.08, faceY + 0.04, faceZ + 0.01);
      this.add(
        new THREE.SphereGeometry(0.04, 6, 4),
        mat(faceColor, 0.9),
        root
      ).position.set(gx, faceY - 0.02, faceZ + 0.01);
    }

    // ========== 6. String lights along top rim arc (~12 warm spheres) ==========
    for (let i = 0; i < 12; i++) {
      const la = Math.PI / 2 + (i / 11) * (Math.PI * 0.9);
      const lx = Math.sin(la) * (rimRadius + 0.15);
      const lz = Math.cos(la) * (rimRadius + 0.15);
      const ly = wheelCenterY + Math.cos(la - Math.PI / 2) * rimRadius * 0.1;
      const light = this.add(
        new THREE.SphereGeometry(0.06, 6, 5),
        mat(0xFFD080, 0.5),
        root
      );
      light.position.set(lx, 3.25 + Math.sin(la) * 2.7 * 0.1 + 0.3, lz);
      (light.material as THREE.MeshStandardMaterial).emissive = new THREE.Color(0xFFD080);
      (light.material as THREE.MeshStandardMaterial).emissiveIntensity = 0.5;
    }

    // Lights along A-frame posts
    for (const sign of [-1, 1]) {
      for (let py = 0.5; py < 3.0; py += 0.5) {
        const lx = sign * (0.3 + (3.25 - py) * 0.1);
        const light = this.add(
          new THREE.SphereGeometry(0.04, 6, 4),
          mat(0xFFE487, 0.5),
          root
        );
        light.position.set(lx, py, 0);
        (light.material as THREE.MeshStandardMaterial).emissive = new THREE.Color(0xFFE487);
        (light.material as THREE.MeshStandardMaterial).emissiveIntensity = 0.3;
      }
    }

    // ========== 7. Enhanced ticket booth ==========
    // Thicker base
    this.add(
      new THREE.BoxGeometry(1.0, 0.7, 0.8),
      mat(0xF17779, 0.75),
      root
    ).position.set(0.7, 0.5, 0.5);

    // Counter window opening (recessed)
    this.add(
      new THREE.BoxGeometry(0.5, 0.3, 0.05),
      mat(0xBFE2F0, 0.75),
      root
    ).position.set(0.7, 0.55, 0.925);

    // Awning above counter
    this.add(
      new THREE.BoxGeometry(0.7, 0.05, 0.5),
      mat(0xFFE487, 0.75),
      root
    ).position.set(0.7, 0.875, 0.65);

    // Flag/sail on top
    const pole = this.add(
      new THREE.CylinderGeometry(0.02, 0.02, 0.4, 6),
      mat(0x888888, 0.8),
      root
    );
    pole.position.set(0.7, 1.05, 0.5);

    const flagCloth = this.add(
      new THREE.BoxGeometry(0.3, 0.2, 0.02),
      mat(0xF17779, 0.8),
      root
    );
    flagCloth.position.set(0.85, 1.1, 0.5);

    // Tag label above the wheel
    this.tag(root, "Ferris Wheel", 0xf0c34f, 6.7);
  }

  private buildDestinations(): void {
    this.gateway(-11.5, 8, "West Beach", 0x7db8c7);
    this.gateway(11.5, -4.5, "East Beach", 0x7db8c7);
    this.gateway(-4.5, -6, "South Beach", 0x7db8c7);
    this.gateway(7.8, -9.5, "Map Travel", 0x73a4d7);
    this.gateway(4.5, 25.8, "Meadow Trail", 0x79bb6c);
    const cloud = new THREE.Group();
    cloud.position.set(15, terrainHeight(15, 29) + 7.2, 29);
    this.group.add(cloud);
    for (const [x, y, z, s] of [[0, 0, 0, 1.4], [-1.2, -0.15, 0.2, 0.9], [1.25, -0.1, 0.1, 1.0]]) {
      const puff = this.add(new THREE.IcosahedronGeometry(s, 1), mat(0xb8d8ef, 0.58), cloud);
      puff.position.set(x, y, z);
    }
    this.tag(cloud, "Cloud Land", 0x8ab8da, 2.0);
  }

  private gateway(x: number, z: number, name: string, color: number): void {
    const root = new THREE.Group();
    root.position.set(x, terrainHeight(x, z), z);
    this.group.add(root);
    for (const side of [-1, 1]) {
      const post = this.add(new THREE.BoxGeometry(0.18, 2, 0.18), mat(color, 0.72), root);
      post.position.set(side * 1.1, 1, 0);
    }
    const lintel = this.add(new THREE.BoxGeometry(2.4, 0.18, 0.18), mat(color, 0.72), root);
    lintel.position.y = 2;
    this.tag(root, name, color, 2.55, 1.55);
  }

  get object(): THREE.Object3D { return this.group; }
  toggleLabels(): boolean {
    this.labelsVisible = !this.labelsVisible;
    this.group.traverse((child) => {
      if (child instanceof THREE.Sprite) child.visible = this.labelsVisible;
    });
    return this.labelsVisible;
  }
  dispose(): void {
    this.geometries.forEach((g) => g.dispose());
    this.textures.forEach((t) => t.dispose());
    this.materials.forEach((m) => m.dispose());
    this.group.clear();
  }
}

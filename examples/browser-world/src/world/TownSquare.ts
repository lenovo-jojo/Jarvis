/**
 * TownSquare.ts — owns the clearing, gathering feature, and signage.
 */
import * as THREE from "three";
import { mat } from "../core/Materials";
import { TOWN_SQUARE } from "../core/WorldConfig";
import { terrainHeight } from "./Terrain";
import { SeededRng } from "../core/SeededNoise";

export class TownSquare {
  private group = new THREE.Group();
  private fountainTime = 0;
  private fountainStreams: THREE.Mesh[] = [];
  private fountainRipples: THREE.Mesh[] = [];
  private fountainTopWater: THREE.Mesh | null = null;
  private signBoard: THREE.Mesh | null = null;

  constructor() {
    const { center } = TOWN_SQUARE;
    const h = terrainHeight(center.x, center.z);
    // Lift the whole square assembly onto its foundation. This guarantees the
    // plaza, furniture, and fountain remain above terrain and path geometry.
    this.group.position.set(center.x, h + 0.08, center.z);

    this.buildStonePlaza();
    this.buildFountain();
    this.buildSignBoard();
    this.buildBenches();
    this.buildLamps();
    // Animated effects are appended after every authored object so their
    // presence cannot shift the stable child indices used by scene-edits.json.
    this.buildFountainWaterEffects();
    this.buildSignBoardDetails();
  }

  private buildStonePlaza(): void {
    const paving = new THREE.MeshStandardMaterial({
      color: "#D8CBB5",
      roughness: 0.94,
      metalness: 0,
    });
    const seamMaterial = new THREE.MeshStandardMaterial({
      color: "#B8AA94",
      roughness: 1,
      metalness: 0,
    });

    // Nearly flush with the ground: this should feel paved into the village,
    // not like a platform placed on top of it.
    const plaza = new THREE.Mesh(
      new THREE.CylinderGeometry(5.75, 5.9, 0.16, 64),
      paving
    );
    // With the group raised 8 cm, this foundation runs from terrain level to
    // 16 cm above it—safely over the 9 cm path ribbon.
    plaza.position.y = 0;
    plaza.receiveShadow = true;
    this.group.add(plaza);

    // Quiet masonry joints across the full civic clearing.
    // Keeping them close in value prevents the plaza reading as a target.
    for (let i = 0; i < 10; i++) {
      const angle = (i / 10) * Math.PI * 2;
      const joint = new THREE.Mesh(
        new THREE.BoxGeometry(0.025, 0.008, 2.8),
        seamMaterial
      );
      joint.position.set(Math.sin(angle) * 4.1, 0.085, Math.cos(angle) * 4.1);
      joint.rotation.y = angle;
      this.group.add(joint);
    }
    for (const radius of [2.2, 3.9, 5.55]) {
      const ring = new THREE.Mesh(
        new THREE.TorusGeometry(radius, 0.018, 5, 64),
        seamMaterial
      );
      ring.rotation.x = Math.PI / 2;
      ring.position.y = 0.086;
      this.group.add(ring);
    }
  }

  private buildFountain(): void {
    const warmStone = new THREE.MeshStandardMaterial({ color: "#E6D8C1", roughness: 0.9 });
    const stoneShade = new THREE.MeshStandardMaterial({ color: "#B9A994", roughness: 0.95 });
    const waterMat = new THREE.MeshStandardMaterial({
      color: "#72CCE0",
      emissive: "#397E99",
      emissiveIntensity: 0.08,
      roughness: 0.18,
      metalness: 0,
      transparent: true,
      opacity: 0.9,
    });

    // Compact low basin leaves circulation room and reads as a friendly
    // village fountain rather than a concrete monument.
    const baseGeo = new THREE.CylinderGeometry(1.45, 1.58, 0.18, 24);
    const base = new THREE.Mesh(baseGeo, warmStone);
    base.position.y = 0.14;
    base.castShadow = true;
    base.receiveShadow = true;
    this.group.add(base);

    const basinGeo = new THREE.TorusGeometry(1.18, 0.17, 10, 32);
    const basin = new THREE.Mesh(basinGeo, warmStone);
    basin.rotation.x = Math.PI / 2;
    basin.position.y = 0.34;
    basin.castShadow = true;
    this.group.add(basin);

    const waterGeo = new THREE.CircleGeometry(1.12, 32);
    const water = new THREE.Mesh(waterGeo, waterMat);
    water.rotation.x = -Math.PI / 2;
    water.position.y = 0.35;
    this.group.add(water);

    const pillarGeo = new THREE.CylinderGeometry(0.13, 0.22, 0.92, 12);
    const pillar = new THREE.Mesh(pillarGeo, stoneShade);
    pillar.position.y = 0.79;
    pillar.castShadow = true;
    this.group.add(pillar);

    const topBowlGeo = new THREE.CylinderGeometry(0.42, 0.3, 0.16, 16);
    const topBowl = new THREE.Mesh(topBowlGeo, warmStone);
    topBowl.position.y = 1.25;
    topBowl.castShadow = true;
    this.group.add(topBowl);

    const finial = new THREE.Mesh(new THREE.SphereGeometry(0.16, 16, 12), warmStone);
    finial.position.y = 1.48;
    finial.castShadow = true;
    this.group.add(finial);

    // Preserve the three original droplet slots so every later authored Town
    // Square object keeps the same persisted hierarchy id.
    for (let i = 0; i < 3; i++) this.group.add(new THREE.Group());
  }

  private buildFountainWaterEffects(): void {
    const waterMat = new THREE.MeshStandardMaterial({
      color: "#72CCE0",
      emissive: "#397E99",
      emissiveIntensity: 0.08,
      roughness: 0.18,
      metalness: 0,
      transparent: true,
      opacity: 0.9,
    });

    this.fountainTopWater = new THREE.Mesh(
      new THREE.CircleGeometry(0.29, 24),
      waterMat
    );
    this.fountainTopWater.rotation.x = -Math.PI / 2;
    this.fountainTopWater.position.y = 1.335;
    this.group.add(this.fountainTopWater);

    // The upper bowl visibly holds water, which spills in four continuous,
    // softly curved streams into the lower basin.
    for (let stream = 0; stream < 4; stream++) {
      const angle = (stream / 4) * Math.PI * 2 + Math.PI / 4;
      const direction = new THREE.Vector3(
        Math.cos(angle),
        0,
        Math.sin(angle)
      );
      const curve = new THREE.QuadraticBezierCurve3(
        direction.clone().multiplyScalar(0.3).setY(1.32),
        direction.clone().multiplyScalar(0.48).setY(0.98),
        direction.clone().multiplyScalar(0.7).setY(0.4)
      );
      const streamMaterial = waterMat.clone();
      streamMaterial.opacity = 0.74;
      const waterStream = new THREE.Mesh(
        new THREE.TubeGeometry(curve, 14, 0.032, 6, false),
        streamMaterial
      );
      this.group.add(waterStream);
      this.fountainStreams.push(waterStream);
    }

    for (let i = 0; i < 3; i++) {
      const rippleMaterial = new THREE.MeshBasicMaterial({
        color: "#C8F4F7",
        transparent: true,
        opacity: 0,
        depthWrite: false,
      });
      const ripple = new THREE.Mesh(
        new THREE.TorusGeometry(0.28, 0.012, 5, 28),
        rippleMaterial
      );
      ripple.rotation.x = Math.PI / 2;
      ripple.position.y = 0.365;
      this.group.add(ripple);
      this.fountainRipples.push(ripple);
    }
  }

  update(dt: number): void {
    this.fountainTime += Math.min(dt, 0.05);

    if (this.fountainTopWater) {
      const pulse = 1 + Math.sin(this.fountainTime * 2.2) * 0.018;
      this.fountainTopWater.scale.setScalar(pulse);
      this.fountainTopWater.rotation.z = this.fountainTime * 0.08;
    }

    for (let i = 0; i < this.fountainStreams.length; i++) {
      const stream = this.fountainStreams[i];
      (stream.material as THREE.MeshStandardMaterial).opacity =
        0.7 + Math.sin(this.fountainTime * 3.2 + i * 1.4) * 0.08;
    }

    for (let i = 0; i < this.fountainRipples.length; i++) {
      const ripple = this.fountainRipples[i];
      const progress = (this.fountainTime * 0.42 + i / 3) % 1;
      const scale = 0.65 + progress * 2.35;
      ripple.scale.setScalar(scale);
      (ripple.material as THREE.MeshBasicMaterial).opacity =
        Math.sin(progress * Math.PI) * 0.34;
    }
  }

  private buildSignBoard(): void {
    const { signPos } = TOWN_SQUARE;
    const localX = signPos[0] - TOWN_SQUARE.center.x;
    const localZ = signPos[1] - TOWN_SQUARE.center.z;
    const lh = terrainHeight(signPos[0], signPos[1]) - this.group.position.y;

    // Posts
    const postGeo = new THREE.CylinderGeometry(0.05, 0.07, 1.45, 6);
    for (const side of [-1, 1]) {
      const post = new THREE.Mesh(postGeo, mat("woodDark"));
      post.position.set(localX + side * 0.62, lh + 0.73, localZ);
      post.castShadow = true;
      this.group.add(post);
    }

    // Board
    const boardGeo = new THREE.BoxGeometry(1.4, 0.72, 0.08);
    const board = new THREE.Mesh(boardGeo, mat("woodMed"));
    board.position.set(localX, lh + 1.12, localZ);
    board.castShadow = true;
    this.group.add(board);
    this.signBoard = board;

    // Board trim (top rail)
    const trimGeo = new THREE.BoxGeometry(1.5, 0.09, 0.1);
    const trim = new THREE.Mesh(trimGeo, mat("woodDark"));
    trim.position.set(localX, lh + 1.52, localZ);
    this.group.add(trim);
  }

  private buildSignBoardDetails(): void {
    if (!this.signBoard) return;
    const paper = new THREE.MeshStandardMaterial({
      color: "#F7E8C4",
      roughness: 0.92,
      side: THREE.DoubleSide,
    });
    const noteColors = ["#ECA3A8", "#F2C96D", "#91C9B9", "#AFA0D5"];

    // Give both sides a finished noticeboard face so the board never reads as
    // a blank brown obstruction when the player approaches from town square.
    // These are children of the persisted board mesh so saved editor moves,
    // rotations, and scales carry the notice details with it.
    for (const facing of [-1, 1]) {
      const inset = new THREE.Mesh(new THREE.PlaneGeometry(1.18, 0.54), paper);
      inset.position.set(0, 0, facing * 0.041);
      inset.rotation.y = facing < 0 ? Math.PI : 0;
      this.signBoard.add(inset);

      for (let i = 0; i < 4; i++) {
        const note = new THREE.Mesh(
          new THREE.PlaneGeometry(0.22, i % 2 === 0 ? 0.2 : 0.16),
          new THREE.MeshStandardMaterial({
            color: noteColors[i],
            roughness: 0.9,
            side: THREE.DoubleSide,
          })
        );
        note.position.set(
          -0.39 + (i % 3) * 0.38,
          -0.05 + (i > 2 ? 0.13 : -0.05),
          facing * 0.046
        );
        note.rotation.y = facing < 0 ? Math.PI : 0;
        note.rotation.z = (i % 2 === 0 ? -1 : 1) * 0.055;
        this.signBoard.add(note);
      }

      const header = new THREE.Mesh(
        new THREE.PlaneGeometry(0.74, 0.1),
        new THREE.MeshStandardMaterial({
          color: "#6F4B3B",
          roughness: 0.95,
          side: THREE.DoubleSide,
        })
      );
      header.position.set(0, 0.24, facing * 0.047);
      header.rotation.y = facing < 0 ? Math.PI : 0;
      this.signBoard.add(header);
    }
  }

  private buildBenches(): void {
    const benchGeo = new THREE.BoxGeometry(1.6, 0.12, 0.5);
    const legGeo = new THREE.BoxGeometry(0.08, 0.4, 0.4);
    const benchMat = mat("woodMed");

    const positions: [number, number][] = [
      [Math.PI * 0.25, 2.8],
      [Math.PI * 0.75, 2.8],
    ];
    for (const [angle, radius] of positions) {
      const x = Math.cos(angle) * radius;
      const z = Math.sin(angle) * radius;
      const bench = new THREE.Mesh(benchGeo, benchMat);
      bench.position.set(x, 0.5, z);
      bench.rotation.y = -angle + Math.PI / 2;
      bench.castShadow = true;
      this.group.add(bench);
      // Legs
      for (const side of [-1, 1]) {
        const leg = new THREE.Mesh(legGeo, mat("woodDark"));
        leg.position.set(x + side * 0.6 * Math.cos(angle), 0.25, z + side * 0.6 * Math.sin(angle));
        this.group.add(leg);
      }
    }
  }

  private buildLamps(): void {
    const lampGeo = new THREE.CylinderGeometry(0.05, 0.05, 1.8, 6);
    const globeGeo = new THREE.SphereGeometry(0.15, 8, 6);
    const globeMat = new THREE.MeshStandardMaterial({
      color: new THREE.Color("#FFF5E0"),
      emissive: new THREE.Color("#FFD700"),
      emissiveIntensity: 0.6,
      roughness: 0.3,
    });

    const positions: [number, number][] = [
      [2.5, -2.0],
      [-2.5, -1.5],
      [2.0, 2.5],
    ];
    for (const [x, z] of positions) {
      const lamp = new THREE.Mesh(lampGeo, mat("stoneDark"));
      lamp.position.set(x, 0.9, z);
      lamp.castShadow = true;
      this.group.add(lamp);
      const globe = new THREE.Mesh(globeGeo, globeMat);
      globe.position.set(x, 1.85, z);
      this.group.add(globe);
    }
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

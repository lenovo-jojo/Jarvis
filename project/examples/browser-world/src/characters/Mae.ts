/**
 * Mae.ts — town companion character (web approximation of Unity Mae).
 * spec: mae-design.md. Primitive body; no rigged GLB.
 * Imports only 'three' and core Palette/Materials.
 */
import * as THREE from "three";
import { DAY } from "../core/Palette";
import { mat } from "../core/Materials";

export type MaeState = "idle" | "walk" | "run" | "hover" | "land";

const WALK_SPEED = 6;      // spec: State transitions — walk in at 6 m/s
const ARRIVE_RADIUS = 0.42; // spec: State transitions — arrival within 0.42 m
const TELEPORT_DIST = 18;  // spec: State transitions — teleport if >18 m

export class Mae extends THREE.Group {
  state: MaeState = "idle";
  summoned = false;
  speaking = false;
  speechBubble: THREE.Sprite;

  private target = new THREE.Vector3();
  private elapsed = 0;
  private ambientTimer = 0; // counts down to next look-at
  private ambientPhase: "idle" | "look" = "idle";
  private ambientDuration = 0;
  private legs: THREE.Object3D[] = [];
  private mouth: THREE.Mesh;
  private bodyRoot: THREE.Group;
  private headTop = 0;

  constructor() {
    super();

    // spec: Pose notes — height normalized to 1.62 m
    const H = 1.62;
    this.bodyRoot = new THREE.Group();
    this.add(this.bodyRoot);

    const bodyMat = mat("woodMed");
    const headMat = mat("wallCream");

    // legs (two cylinders) — spec: States — primitive approximation
    const legH = H * 0.42;
    const legGeo = new THREE.CylinderGeometry(0.05, 0.06, legH, 10);
    for (const x of [-0.09, 0.09]) {
      const leg = new THREE.Mesh(legGeo, bodyMat);
      const pivot = new THREE.Group();
      pivot.position.set(x, legH, 0); // rotate about hip
      leg.position.y = -legH / 2;
      pivot.add(leg);
      this.bodyRoot.add(pivot);
      this.legs.push(pivot);
    }

    // torso capsule
    const torsoH = H * 0.38;
    const torso = new THREE.Mesh(new THREE.CapsuleGeometry(0.14, torsoH, 6, 12), bodyMat);
    torso.position.y = legH + torsoH / 2 + 0.03;
    this.bodyRoot.add(torso);

    // head sphere
    const headR = 0.16;
    const head = new THREE.Mesh(new THREE.SphereGeometry(headR, 20, 16), headMat);
    const neckY = legH + torsoH + 0.03;
    head.position.y = neckY + headR * 0.9;
    this.bodyRoot.add(head);
    this.headTop = head.position.y + headR;

    // mouth — pulses while speaking — spec: Speech bubbles
    this.mouth = new THREE.Mesh(new THREE.BoxGeometry(0.05, 0.02, 0.02), mat("crossRed"));
    this.mouth.position.set(0, neckY + headR * 0.25, headR * 0.95);
    this.bodyRoot.add(this.mouth);

    // speech bubble — spec: Speech bubbles — 0.32 m above renderer bounds
    const canvas = document.createElement("canvas");
    canvas.width = 128; canvas.height = 64;
    const ctx = canvas.getContext("2d")!;
    ctx.fillStyle = "rgba(245,239,224,0.95)";
    ctx.beginPath();
    if (ctx.roundRect) ctx.roundRect(4, 4, 120, 56, 18); else ctx.rect(4, 4, 120, 56);
    ctx.fill();
    ctx.fillStyle = DAY.woodDark; // spec: Palette woodDark ink
    ctx.font = "bold 22px sans-serif";
    ctx.textAlign = "center"; ctx.textBaseline = "middle";
    ctx.fillText("…", 64, 32);
    const tex = new THREE.CanvasTexture(canvas);
    const sprite = new THREE.Sprite(new THREE.SpriteMaterial({ map: tex, transparent: true }));
    sprite.scale.set(0.4, 0.2, 1);
    sprite.position.set(0, this.headTop + 0.32, 0);
    this.speechBubble = sprite;
    this.speechBubble.visible = false;
    this.add(this.speechBubble);

    this.ambientTimer = 18; // spec: State transitions — 18–42 s ambient loop
  }

  getState(): MaeState {
    return this.state;
  }

  // spec: States — per-state visual approximation, no model swap
  setPose(state: MaeState): void {
    this.state = state;
    const sw = state === "walk" ? 0.35 : state === "run" ? 0.6 : 0;
    for (let i = 0; i < this.legs.length; i++) {
      this.legs[i].rotation.x = sw * (i === 0 ? 1 : -1);
    }
    if (state === "hover") {
      this.bodyRoot.position.y = 0.18; // spec: Open questions — hover = float offset
    } else if (state === "land") {
      this.bodyRoot.position.y = -0.06; // sink on landing
    } else {
      this.bodyRoot.position.y = 0;
    }
  }

  tick(dt: number): void {
    this.elapsed += dt;

    // mouth pulse — spec: Speech bubbles — sin(t*18)*0.12
    if (this.speaking) {
      this.mouth.scale.y = 1 + Math.sin(this.elapsed * 18) * 0.12;
    }

    if (this.summoned) {
      const to = new THREE.Vector3().subVectors(this.target, this.position);
      to.y = 0;
      const d = to.length();
      if (d <= ARRIVE_RADIUS) {
        this.summoned = false;
        this.lookAtPlayer();
        this.setPose("idle");
      } else {
        const step = Math.min(WALK_SPEED * dt, d);
        this.position.addScaledVector(to.normalize(), step);
        this.legs.forEach((l, i) => {
          l.rotation.x = Math.sin(this.elapsed * 10) * 0.5 * (i === 0 ? 1 : -1);
        });
      }
      return;
    }

    // spec: State transitions — idle loop 18–42 s, look-at 2.5–5 s
    this.ambientTimer -= dt;
    if (this.ambientTimer <= 0) {
      if (this.ambientPhase === "idle") {
        this.ambientPhase = "look";
        this.ambientDuration = 2.5 + Math.random() * 2.5;
        this.ambientTimer = this.ambientDuration;
        this.lookAtPlayer();
      } else {
        this.ambientPhase = "idle";
        this.ambientTimer = 18 + Math.random() * 24;
      }
    }
  }

  // spec: State transitions — Summon
  summon(playerPos: THREE.Vector3, dist: number): void {
    this.summoned = true;
    this.target.copy(playerPos);
    if (dist > TELEPORT_DIST) {
      // snap 7 m behind player (±1.4 m sideways) — spec: State transitions
      const side = (Math.random() * 2 - 1) * 1.4;
      this.position.copy(playerPos);
      this.position.x += side;
      this.position.z += 7;
    }
  }

  setSpeaking(b: boolean): void {
    this.speaking = b;
    this.speechBubble.visible = b;
    if (!b) this.mouth.scale.y = 1;
  }

  // spec: State transitions — fly → hover
  enterFly(): void {
    this.setPose("hover");
  }

  // spec: State transitions — landing → idle_land → idle
  land(): void {
    this.setPose("land");
    setTimeout(() => this.setPose("idle"), 120);
  }

  // spec: State transitions — face player on arrival
  private lookAtPlayer(): void {
    // face -Z toward target player position (simple yaw)
    const dx = this.target.x - this.position.x;
    const dz = this.target.z - this.position.z;
    if (dx !== 0 || dz !== 0) {
      this.rotation.y = Math.atan2(dx, dz);
    }
  }
}

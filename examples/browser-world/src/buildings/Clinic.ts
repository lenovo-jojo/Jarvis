/**
 * Clinic.ts — owns the hero building.
 * Solid-box → hollow room with real doorway, furnished interior, interior lighting.
 * Exterior silhouette (roof, tower, awning, cross sign, windows, flower pots, steps) unchanged.
 */
import * as THREE from "three";
import { mat, type MaterialKey } from "../core/Materials";
import { CLINIC } from "../core/WorldConfig";
import { terrainHeight } from "../world/Terrain";

export class Clinic {
  private group = new THREE.Group();
  private cutawayParts: THREE.Object3D[] = [];
  private cutawayVisible = false;

  constructor() {
    const { pos, rotY, wallWidth, wallHeight, wallDepth } = CLINIC;
    const h = terrainHeight(pos.x, pos.z);
    this.group.position.set(pos.x, h, pos.z);
    this.group.rotation.y = rotY;

    // ── HOLLOW ROOM (interior) ───────────────────────────────────
    this.buildHollowRoom(wallWidth, wallHeight, wallDepth);

    // The Clinic and Hospital are one place. Preserve the furnished room, but
    // give it the calm teal mid-century exterior established in the blockout.
    this.buildMidCenturyExterior(wallWidth, wallHeight, wallDepth);

    // ── FLOWER POTS ──────────────────────────────────────────────
    this.buildFlowerPots(wallWidth, wallDepth);

    // ── ENTRANCE STEPS ───────────────────────────────────────────
    this.buildSteps(wallDepth);
  }

  private buildMidCenturyExterior(wallW: number, wallH: number, wallD: number): void {
    const teal = new THREE.MeshStandardMaterial({ color: 0x72bdb8, roughness: 0.78 });
    const tealDark = new THREE.MeshStandardMaterial({ color: 0x4f918b, roughness: 0.82 });
    const mint = new THREE.MeshStandardMaterial({ color: 0xa9ded1, roughness: 0.8 });
    const cream = new THREE.MeshStandardMaterial({ color: 0xfff1d7, roughness: 0.76 });
    const coral = new THREE.MeshStandardMaterial({ color: 0xf27f83, roughness: 0.72 });
    const warmWindow = new THREE.MeshStandardMaterial({ color: 0xffe7ad, emissive: 0x7a5c24, emissiveIntensity: 0.22, roughness: 0.55 });
    const front = -wallD / 2 - 0.03;
    const doorW = 1.6;
    const sideW = (wallW - doorW) / 2;

    // Thin teal outer skin keeps the warm interior materials intact.
    const back = new THREE.Mesh(new THREE.BoxGeometry(wallW, wallH, 0.1), teal);
    back.position.set(0, wallH / 2, wallD / 2 + 0.03);
    this.group.add(back);
    for (const side of [-1, 1]) {
      const sideWall = new THREE.Mesh(new THREE.BoxGeometry(0.1, wallH, wallD), teal);
      sideWall.position.set(side * (wallW / 2 + 0.03), wallH / 2, 0);
      this.group.add(sideWall);

      const facade = new THREE.Mesh(new THREE.BoxGeometry(sideW, wallH, 0.1), teal);
      facade.position.set(side * (doorW / 2 + sideW / 2), wallH / 2, front);
      this.group.add(facade);
      this.cutawayParts.push(facade);

      const window = new THREE.Mesh(new THREE.BoxGeometry(1.05, 1.05, 0.08), warmWindow);
      window.position.set(side * 1.75, 1.35, front - 0.07);
      this.group.add(window);
      this.cutawayParts.push(window);

      const wing = new THREE.Mesh(new THREE.BoxGeometry(1.25, wallH * 0.68, wallD * 0.7), mint);
      wing.position.set(side * (wallW / 2 + 0.5), wallH * 0.34, 0.15);
      this.group.add(wing);
    }

    const lintel = new THREE.Mesh(new THREE.BoxGeometry(doorW, 0.35, 0.1), teal);
    lintel.position.set(0, wallH - 0.18, front);
    this.group.add(lintel);
    this.cutawayParts.push(lintel);

    const roof = new THREE.Mesh(new THREE.BoxGeometry(wallW + 0.85, 0.22, wallD + 0.55), tealDark);
    roof.position.set(0, wallH + 0.13, 0.08);
    roof.rotation.z = -0.025;
    roof.castShadow = true;
    this.group.add(roof);
    this.cutawayParts.push(roof);

    const trim = new THREE.Mesh(new THREE.BoxGeometry(wallW + 0.55, 0.1, 0.12), cream);
    trim.position.set(0, wallH - 0.05, front - 0.08);
    this.group.add(trim);
    this.cutawayParts.push(trim);

    const badge = new THREE.Mesh(new THREE.CylinderGeometry(0.58, 0.58, 0.11, 24), cream);
    badge.rotation.x = Math.PI / 2;
    badge.position.set(0, wallH - 0.34, front - 0.12);
    this.group.add(badge);
    this.cutawayParts.push(badge);
    for (const geometry of [new THREE.BoxGeometry(0.66, 0.18, 0.1), new THREE.BoxGeometry(0.18, 0.66, 0.1)]) {
      const cross = new THREE.Mesh(geometry, coral);
      cross.position.set(0, wallH - 0.34, front - 0.2);
      this.group.add(cross);
      this.cutawayParts.push(cross);
    }

    const canopy = new THREE.Mesh(new THREE.CapsuleGeometry(0.22, 1.55, 6, 16), cream);
    canopy.rotation.z = Math.PI / 2;
    canopy.rotation.x = Math.PI / 2;
    canopy.position.set(0, 1.95, front - 0.62);
    this.group.add(canopy);
    this.cutawayParts.push(canopy);

    // Clearly readable double entry doors. Only these visible panels are
    // clickable; adjacent wall and trim must never trigger interior mode.
    const doorMaterial = new THREE.MeshStandardMaterial({ color: 0x2f6670, roughness: 0.62 });
    for (const side of [-1, 1]) {
      const door = new THREE.Mesh(new THREE.BoxGeometry(0.72, 2.12, 0.11), doorMaterial);
      door.name = "hospitalDoorTarget";
      door.position.set(side * 0.39, 1.08, front - 0.16);
      door.castShadow = true;
      this.group.add(door);
      this.cutawayParts.push(door);

      const glass = new THREE.Mesh(new THREE.BoxGeometry(0.44, 0.72, 0.035), warmWindow);
      glass.position.set(side * 0.39, 1.52, front - 0.23);
      this.group.add(glass);
      this.cutawayParts.push(glass);

      const handle = new THREE.Mesh(new THREE.SphereGeometry(0.055, 10, 8), cream);
      handle.position.set(side * 0.12, 1.03, front - 0.25);
      this.group.add(handle);
      this.cutawayParts.push(handle);
    }
  }

  private makeRoundedBox(w: number, h: number, d: number, _bevel: number): THREE.BufferGeometry {
    return new THREE.BoxGeometry(w, h, d);
  }

  // ══════════════════════════════════════════════════════════════════════
  // HOLLOW ROOM with doorway, furnished interior, interior lighting
  // ══════════════════════════════════════════════════════════════════════

  private buildHollowRoom(wallW: number, wallH: number, wallD: number): void {
    // Interior dimensions (slightly smaller than exterior)
    const t = 0.25; // wall thickness
    const iW = wallW - t * 2;
    const iH = wallH - t * 2;
    const iD = wallD - t * 2;

    const wallInterior = mat("wallInterior");
    const floorMat = mat("floorTile");

    // ── CEILING ─────────────────────────────────────────────────
    const ceilingGeo = new THREE.BoxGeometry(iW, t, iD);
    const ceiling = new THREE.Mesh(ceilingGeo, wallInterior);
    ceiling.position.y = wallH - t / 2;
    ceiling.receiveShadow = true;
    this.group.add(ceiling);
    this.cutawayParts.push(ceiling);

    // ── FLOOR ───────────────────────────────────────────────────
    const floorGeo = new THREE.BoxGeometry(iW, t, iD);
    const floor = new THREE.Mesh(floorGeo, floorMat);
    floor.position.y = t / 2;
    floor.receiveShadow = true;
    this.group.add(floor);

    // Floor tile grid lines (subtle)
    const gridMat = new THREE.MeshBasicMaterial({ color: "#B8C8D0" });
    const lineH = new THREE.BoxGeometry(iW * 0.67, 0.005, 0.005);
    const lineV = new THREE.BoxGeometry(0.005, 0.005, iD * 0.67);
    for (let i = -1; i <= 1; i++) {
      const hl = new THREE.Mesh(lineH, gridMat);
      hl.position.set(0, t + 0.003, i * (iD * 0.33));
      this.group.add(hl);
      const vl = new THREE.Mesh(lineV, gridMat);
      vl.position.set(i * (iW * 0.33), t + 0.003, 0);
      this.group.add(vl);
    }

    // ── BACK WALL ───────────────────────────────────────────────
    const backWallGeo = new THREE.BoxGeometry(iW, iH, t);
    const backWall = new THREE.Mesh(backWallGeo, wallInterior);
    backWall.position.set(0, wallH / 2, wallD / 2 - t / 2);
    backWall.receiveShadow = true;
    this.group.add(backWall);

    // ── LEFT WALL ───────────────────────────────────────────────
    const sideWallGeo = new THREE.BoxGeometry(t, iH, iD);
    const leftWall = new THREE.Mesh(sideWallGeo, wallInterior);
    leftWall.position.set(-wallW / 2 + t / 2, wallH / 2, 0);
    leftWall.receiveShadow = true;
    this.group.add(leftWall);

    // ── RIGHT WALL ──────────────────────────────────────────────
    const rightWall = new THREE.Mesh(sideWallGeo, wallInterior);
    rightWall.position.set(wallW / 2 - t / 2, wallH / 2, 0);
    rightWall.receiveShadow = true;
    this.group.add(rightWall);

    // ── FRONT WALL (with doorway opening) ───────────────────────
    // Doorway: 1.6 wide × 2.4 tall, centered at x=0
    const doorW = 1.6;
    const doorH = 2.4;
    const doorSillH = 0.1; // threshold strip

    // Front wall: LEFT section (door frame + wall)
    const frontLeftW = (wallW - doorW) / 2 - 0.15; // gap for frame
    if (frontLeftW > 0) {
      const frontLeftGeo = new THREE.BoxGeometry(frontLeftW, wallH, t);
      const frontLeft = new THREE.Mesh(frontLeftGeo, mat("wallCreamHi"));
      frontLeft.position.set(-(doorW / 2 + frontLeftW / 2 + 0.075), wallH / 2, -wallD / 2 + t / 2);
      frontLeft.castShadow = true;
      frontLeft.receiveShadow = true;
      this.group.add(frontLeft);
      this.cutawayParts.push(frontLeft);
    }

    // Front wall: RIGHT section
    if (frontLeftW > 0) {
      const frontRightGeo = new THREE.BoxGeometry(frontLeftW, wallH, t);
      const frontRight = new THREE.Mesh(frontRightGeo, mat("wallCreamHi"));
      frontRight.position.set((doorW / 2 + frontLeftW / 2 + 0.075), wallH / 2, -wallD / 2 + t / 2);
      frontRight.castShadow = true;
      frontRight.receiveShadow = true;
      this.group.add(frontRight);
      this.cutawayParts.push(frontRight);
    }

    // Front wall: TOP section (above doorway)
    const topH = wallH - doorH - 0.3; // 0.3 = top of door arch
    if (topH > 0) {
      const frontTopGeo = new THREE.BoxGeometry(doorW + 0.3, topH, t);
      const frontTop = new THREE.Mesh(frontTopGeo, mat("wallCreamHi"));
      frontTop.position.set(0, doorH + 0.15 + topH / 2, -wallD / 2 + t / 2);
      frontTop.castShadow = true;
      frontTop.receiveShadow = true;
      this.group.add(frontTop);
      this.cutawayParts.push(frontTop);
    }

    // ── DOOR FRAME (on the opening) ─────────────────────────────
    const frameT = 0.12;
    const frameW = doorW + 0.3;
    // Left frame piece
    const lfGeo = new THREE.BoxGeometry(frameT, doorH, frameT);
    const lf = new THREE.Mesh(lfGeo, mat("windowFrame"));
    lf.position.set(-(doorW / 2 + frameT / 2 - 0.05), doorH / 2 - 0.05, -wallD / 2 - frameT / 2 + 0.05);
    this.group.add(lf);
    this.cutawayParts.push(lf);
    // Right frame piece
    const rf = new THREE.Mesh(lfGeo, mat("windowFrame"));
    rf.position.set((doorW / 2 + frameT / 2 - 0.05), doorH / 2 - 0.05, -wallD / 2 - frameT / 2 + 0.05);
    this.group.add(rf);
    this.cutawayParts.push(rf);
    // Top frame piece (arch)
    const tfGeo = new THREE.BoxGeometry(frameT, frameT, frameW);
    const tf = new THREE.Mesh(tfGeo, mat("windowFrame"));
    tf.position.set(0, doorH - 0.05, -wallD / 2 - frameT / 2 + 0.05);
    this.group.add(tf);
    this.cutawayParts.push(tf);

    // Door arch trim
    const archGeo = new THREE.TorusGeometry(doorW / 2 + 0.05, 0.06, 8, 16, Math.PI);
    const arch = new THREE.Mesh(archGeo, mat("windowFrame"));
    arch.position.set(0, doorH, -wallD / 2 - frameT / 2 + 0.05);
    this.group.add(arch);
    this.cutawayParts.push(arch);

    // The visible teal double doors are created by buildMidCenturyExterior().
    // Do not also place the legacy hinged brown door here: it floated outside
    // the entrance and visually contradicted the closed facade.

    // ── THRESHOLD / ENTRY STEP TRANSITION ────────────────────────
    const sillGeo = new THREE.BoxGeometry(doorW + 0.2, doorSillH, 0.35);
    const sill = new THREE.Mesh(sillGeo, mat("woodDark"));
    sill.position.set(0, doorSillH / 2, -wallD / 2 + 0.18);
    sill.castShadow = true;
    sill.receiveShadow = true;
    this.group.add(sill);

    // Threshold edge strip (visual transition from outside to inside)
    const threshEdgeGeo = new THREE.BoxGeometry(doorW + 0.3, 0.02, 0.04);
    const threshEdge = new THREE.Mesh(threshEdgeGeo, mat("woodDark"));
    threshEdge.position.set(0, doorSillH + 0.01, -wallD / 2 + 0.33);
    this.group.add(threshEdge);

    // ═══════════════════ CORRIDOR ════════════════════════════════════════
    // Corridor extends from the doorway inward into the building
    // Corridor: at least 1.0m wide × 2.4m tall, side walls matching interior wall style
    const corridorW = 1.4;    // corridor width (x-direction) — ≥1.0m
    const corridorL = 1.85;   // corridor length (z-direction, extending inward)
    const corridorH = 2.4;    // corridor height — ≥2.4m
    const corridorWallT = 0.08; // corridor wall thickness

    // Corridor floor (tiled, extending from doorway inward)
    const corridorFloorGeo = new THREE.BoxGeometry(corridorW, 0.02, corridorL);
    const corridorFloor = new THREE.Mesh(corridorFloorGeo, mat("floorTile"));
    const corridorFloorZ = -wallD / 2 + t + doorSillH + corridorL / 2 - 0.1;
    corridorFloor.position.set(0, doorSillH + 0.01, corridorFloorZ);
    corridorFloor.receiveShadow = true;
    this.group.add(corridorFloor);

    // Corridor floor tile grid lines (matching interior style)
    const gridMat2 = new THREE.MeshBasicMaterial({ color: "#B8C8D0" });
    for (let cx = -0.5; cx <= 0.5; cx += 0.5) {
      const lineGeo = new THREE.BoxGeometry(0.003, 0.003, corridorL - 0.1);
      const line = new THREE.Mesh(lineGeo, gridMat2);
      line.position.set(cx, doorSillH + 0.025, corridorFloorZ);
      this.group.add(line);
    }
    for (let cz = 0.2; cz < corridorL - 0.1; cz += 0.5) {
      const lineGeo = new THREE.BoxGeometry(corridorW - 0.2, 0.003, 0.003);
      const line = new THREE.Mesh(lineGeo, gridMat2);
      line.position.set(0, doorSillH + 0.025, corridorFloorZ - corridorL / 2 + cz);
      this.group.add(line);
    }

    // Corridor ceiling (continuation of interior ceiling)
    const corridorCeilingGeo = new THREE.BoxGeometry(corridorW, 0.05, corridorL);
    const corridorCeiling = new THREE.Mesh(corridorCeilingGeo, wallInterior);
    corridorCeiling.position.set(0, (doorH - 0.1) - 0.025, corridorFloorZ);
    corridorCeiling.receiveShadow = true;
    this.group.add(corridorCeiling);

    // Corridor ceiling light panel (over corridor area)
    const cLightGeo = new THREE.CylinderGeometry(0.25, 0.22, 0.04, 12);
    const cLightMat = new THREE.MeshStandardMaterial({
      color: "#FFF8E0",
      emissive: "#FFE090",
      emissiveIntensity: 2.0,
      roughness: 0.3,
      metalness: 0.1,
    });
    const corridorCeilingLight = new THREE.Mesh(cLightGeo, cLightMat);
    corridorCeilingLight.position.set(0, (doorH - 0.1) - 0.04, corridorFloorZ);
    this.group.add(corridorCeilingLight);

    // Corridor side walls (left and right of the corridor path)
    const cWallLeft = new THREE.Mesh(
      new THREE.BoxGeometry(corridorWallT, corridorH - doorSillH, corridorL),
      wallInterior
    );
    cWallLeft.position.set(-corridorW / 2 + corridorWallT / 2, doorSillH + (corridorH - doorSillH) / 2, corridorFloorZ);
    cWallLeft.castShadow = true;
    cWallLeft.receiveShadow = true;
    this.group.add(cWallLeft);

    const cWallRight = new THREE.Mesh(
      new THREE.BoxGeometry(corridorWallT, corridorH - doorSillH, corridorL),
      wallInterior
    );
    cWallRight.position.set(corridorW / 2 - corridorWallT / 2, doorSillH + (corridorH - doorSillH) / 2, corridorFloorZ);
    cWallRight.castShadow = true;
    cWallRight.receiveShadow = true;
    this.group.add(cWallRight);

    // Corridor wall sconces (wall-mounted lights on each side wall)
    const sconceBrackGeo = new THREE.BoxGeometry(0.06, 0.04, 0.06);
    const sconceGlassGeo = new THREE.CylinderGeometry(0.08, 0.1, 0.14, 8);
    const sconceGlassMat = new THREE.MeshStandardMaterial({
      color: "#FFF8E0",
      emissive: "#FFE090",
      emissiveIntensity: 3.0,
      roughness: 0.2,
      metalness: 0.1,
      transparent: true,
      opacity: 0.85,
    });
    const sconceMetalMat = mat("windowFrame");
    const sconcePositions = [0.4, 1.1, 1.6].map((zOff) => corridorFloorZ - corridorL / 2 + zOff);
    for (const sz of sconcePositions) {
      // Left sconce
      const leftBrack = new THREE.Mesh(sconceBrackGeo, sconceMetalMat);
      leftBrack.position.set(-corridorW / 2 + 0.01, doorSillH + 1.8, sz);
      this.group.add(leftBrack);
      const leftGlass = new THREE.Mesh(sconceGlassGeo, sconceGlassMat);
      leftGlass.position.set(-corridorW / 2 + corridorWallT / 2 + 0.05, doorSillH + 1.8, sz);
      this.group.add(leftGlass);
      const leftLight = new THREE.PointLight("#FFD080", 3, 3, 2);
      leftLight.position.set(-corridorW / 2 + corridorWallT + 0.15, doorSillH + 1.7, sz);
      this.group.add(leftLight);

      // Right sconce (mirror)
      const rightBrack = new THREE.Mesh(sconceBrackGeo, sconceMetalMat);
      rightBrack.position.set(corridorW / 2 - 0.01, doorSillH + 1.8, sz);
      this.group.add(rightBrack);
      const rightGlass = new THREE.Mesh(sconceGlassGeo, sconceGlassMat);
      rightGlass.position.set(corridorW / 2 - corridorWallT / 2 - 0.05, doorSillH + 1.8, sz);
      this.group.add(rightGlass);
      const rightLight = new THREE.PointLight("#FFD080", 3, 3, 2);
      rightLight.position.set(corridorW / 2 - corridorWallT - 0.15, doorSillH + 1.7, sz);
      this.group.add(rightLight);
    }

    // Corridor end wall (partial partition — opening on each side for corridor→main room)
    // Top portion above doorway height line
    const corEndTopH = wallH - corridorH - 0.3;
    const corEndTopW = corridorW + 0.3;
    if (corEndTopH > 0) {
      const corEndTopGeo = new THREE.BoxGeometry(corEndTopW, corEndTopH, corridorWallT);
      const corEndTop = new THREE.Mesh(corEndTopGeo, mat("wallCreamHi"));
      corEndTop.position.set(
        0,
        corridorH + corEndTopH / 2 + doorSillH,
        corridorFloorZ - corridorL / 2 + corridorWallT / 2
      );
      corEndTop.castShadow = true;
      corEndTop.receiveShadow = true;
      this.group.add(corEndTop);
    }
    // Bottom portion below threshold height  
    const corEndBotH = doorSillH + 0.02;
    const corEndBotGeo = new THREE.BoxGeometry(corridorW, corEndBotH, corridorWallT);
    const corEndBot = new THREE.Mesh(corEndBotGeo, mat("wallCreamHi"));
    corEndBot.position.set(
      0,
      corEndBotH / 2,
      corridorFloorZ - corridorL / 2 + corridorWallT / 2
    );
    corEndBot.castShadow = true;
    corEndBot.receiveShadow = true;
    this.group.add(corEndBot);
    // Left and right pillars at corridor end (leaving center passage open)
    const corEndPillarW = 0.12;
    for (const side of [-1, 1]) {
      const pillarGeo = new THREE.BoxGeometry(corridorW / 3, corridorH - doorSillH - 0.3, corridorWallT);
      const pillar = new THREE.Mesh(pillarGeo, mat("wallCreamHi"));
      pillar.position.set(
        side * corridorW / 4,
        doorSillH + (corridorH - doorSillH - 0.3) / 2,
        corridorFloorZ - corridorL / 2 + corridorWallT / 2
      );
      pillar.castShadow = true;
      pillar.receiveShadow = true;
      this.group.add(pillar);
    }

    // ── INTERIOR FURNITURE ──────────────────────────────────────
    const woodDarkMat = mat("woodDark");
    const woodLightMat = mat("woodLight");
    const wallCreamMed = mat("wallCream");

    // === RECEPTION DESK (near entrance, just inside door) ===
    // Desk counter
    const deskW = 2.0;
    const deskH = 1.1;
    const deskD = 0.7;
    const deskGeo = new THREE.BoxGeometry(deskW, deskH, deskD);
    const desk = new THREE.Mesh(deskGeo, woodLightMat);
    desk.position.set(0, deskH / 2, -wallD / 2 + 1.2);
    desk.castShadow = true;
    desk.receiveShadow = true;
    this.group.add(desk);
    // Desk top surface
    const deskTopGeo = new THREE.BoxGeometry(deskW + 0.1, 0.06, deskD + 0.2);
    const deskTop = new THREE.Mesh(deskTopGeo, mat("woodMed"));
    deskTop.position.set(0, deskH + 0.03, -wallD / 2 + 1.2);
    deskTop.castShadow = true;
    this.group.add(deskTop);
    // Desk front panel (patient-facing side)
    const deskFrontGeo = new THREE.BoxGeometry(deskW + 0.1, 0.04, deskD + 0.15);
    const deskFront = new THREE.Mesh(deskFrontGeo, mat("wallCream"));
    deskFront.position.set(0, deskH - 0.02, -wallD / 2 + 1.2 - deskD / 2 - 0.05);
    this.group.add(deskFront);
    // Cash register / computer monitor on desk
    const monitorGeo = new THREE.BoxGeometry(0.5, 0.35, 0.04);
    const monitor = new THREE.Mesh(monitorGeo, new THREE.MeshStandardMaterial({
      color: "#2A2A2A",
      roughness: 0.3,
      metalness: 0.2,
    }));
    monitor.position.set(-0.4, deskH + 0.23, -wallD / 2 + 1.1);
    monitor.castShadow = true;
    this.group.add(monitor);
    // Monitor stand
    const standGeo = new THREE.CylinderGeometry(0.04, 0.06, 0.18, 6);
    const stand = new THREE.Mesh(standGeo, new THREE.MeshStandardMaterial({
      color: "#3A3A3A", roughness: 0.4, metalness: 0.3,
    }));
    stand.position.set(-0.4, deskH + 0.12, -wallD / 2 + 1.1);
    this.group.add(stand);

    // === EXAM TABLE (against back wall, center) ===
    const tableW = 1.2;
    const tableH = 0.75;
    const tableD = 0.6;
    const tableTopGeo = new THREE.BoxGeometry(tableW, 0.06, tableD);
    const tableTop = new THREE.Mesh(tableTopGeo, mat("woodLight"));
    tableTop.position.set(0, tableH, wallD / 2 - tableD / 2 - 0.3);
    tableTop.castShadow = true;
    this.group.add(tableTop);
    // Table legs
    const legGeo = new THREE.CylinderGeometry(0.03, 0.03, tableH - 0.03, 6);
    const legMat = mat("woodDark");
    for (const lx of [-1, 1]) {
      for (const lz of [-1, 1]) {
        const leg = new THREE.Mesh(legGeo, legMat);
        leg.position.set(
          lx * (tableW / 2 - 0.08),
          (tableH - 0.03) / 2,
          wallD / 2 - tableD / 2 - 0.3 + lz * (tableD / 2 - 0.08)
        );
        leg.castShadow = true;
        this.group.add(leg);
      }
    }
    // Exam table padding (blue)
    const padGeo = new THREE.BoxGeometry(tableW - 0.1, 0.08, tableD - 0.1);
    const pad = new THREE.Mesh(padGeo, new THREE.MeshStandardMaterial({
      color: "#5A9AC0",
      roughness: 0.7,
      metalness: 0.0,
    }));
    pad.position.set(0, tableH + 0.04, wallD / 2 - tableD / 2 - 0.3);
    this.group.add(pad);

    // === CHAIRS (one on each side of exam table) ===
    const chairPositions = [
      { x: -1.2, z: wallD / 2 - tableD / 2 - 0.3 },
      { x: 1.2, z: wallD / 2 - tableD / 2 - 0.3 },
    ];
    for (const cp of chairPositions) {
      this.buildChair(cp.x, cp.z, wallH);
    }

    // === WALL ACCENT: Medical cross on back wall ===
    const crossBoardGeo = new THREE.BoxGeometry(1.0, 0.7, 0.04);
    const crossBoard = new THREE.Mesh(crossBoardGeo, mat("crossWhite"));
    crossBoard.position.set(0, wallH - 1.0, wallD / 2 - t - 0.04);
    crossBoard.castShadow = true;
    this.group.add(crossBoard);
    // Cross bars on board
    const cbH = new THREE.BoxGeometry(0.4, 0.12, 0.02);
    const cbV = new THREE.BoxGeometry(0.12, 0.4, 0.02);
    const crossRedMat = mat("crossRed");
    for (const piece of [cbH, cbV]) {
      const c = new THREE.Mesh(piece, crossRedMat);
      c.position.set(0, wallH - 1.0, wallD / 2 - t - 0.02);
      this.group.add(c);
    }

    // === WALL SHELF with small plants (left side interior wall) ===
    const shelfW = 1.0;
    const shelfD = 0.2;
    const shelfH = 0.04;
    const shelfY = 2.0;
    const shelfGeo = new THREE.BoxGeometry(shelfW, shelfH, shelfD);
    const shelf = new THREE.Mesh(shelfGeo, mat("woodLight"));
    shelf.position.set(-wallW / 2 + t + shelfW / 2 + 0.15, shelfH / 2, 0.5);
    shelf.castShadow = true;
    this.group.add(shelf);
    // Shelf brackets
    const bracketGeo = new THREE.BoxGeometry(0.04, shelfY - shelfH, 0.04);
    const bracketMat = mat("windowFrame");
    for (const bz of [0.3, 0.7]) {
      const bracket = new THREE.Mesh(bracketGeo, bracketMat);
      bracket.position.set(-wallW / 2 + t + 0.02, (shelfY - shelfH) / 2, 0.5 + bz - shelfD / 2);
      this.group.add(bracket);
    }
    // Small potted plant on shelf
    const potGeo = new THREE.CylinderGeometry(0.08, 0.06, 0.12, 8);
    const pot = new THREE.Mesh(potGeo, mat("potter"));
    pot.position.set(-wallW / 2 + t + shelfW / 2, shelfH + 0.06, 0.5);
    this.group.add(pot);
    // Leaves
    const leafGeo = new THREE.SphereGeometry(0.1, 6, 4);
    const leaf = new THREE.Mesh(leafGeo, mat("leafGreen"));
    leaf.position.set(-wallW / 2 + t + shelfW / 2, shelfH + 0.2, 0.5);
    leaf.scale.set(1, 0.8, 1);
    this.group.add(leaf);

    // === SECOND SMALL plant on sill ===
    const pot2Geo = new THREE.CylinderGeometry(0.06, 0.045, 0.1, 8);
    const pot2 = new THREE.Mesh(pot2Geo, mat("potter"));
    pot2.position.set(wallW / 2 - t - 0.8, shelfH + 0.05, 0.5);
    this.group.add(pot2);
    const leaf2Geo = new THREE.SphereGeometry(0.08, 6, 4);
    const leaf2 = new THREE.Mesh(leaf2Geo, mat("leafDark"));
    leaf2.position.set(wallW / 2 - t - 0.8, shelfH + 0.18, 0.5);
    leaf2.scale.set(1, 0.8, 1);
    this.group.add(leaf2);

    // ═══ INTERIOR LIGHTING ════════════════════════════════════════
    // Warm ceiling-mounted point lights
    const ceilingLightMat = new THREE.MeshStandardMaterial({
      color: "#FFF8E0",
      emissive: "#FFE090",
      emissiveIntensity: 2.5,
      roughness: 0.3,
      metalness: 0.1,
    });

    // Ceiling light fixture 1
    const lightPanelGeo = new THREE.CylinderGeometry(0.35, 0.3, 0.05, 16);
    const lightPanel1 = new THREE.Mesh(lightPanelGeo, ceilingLightMat);
    lightPanel1.position.set(-0.6, wallH - t - 0.025, -0.3);
    this.group.add(lightPanel1);

    // Ceiling light fixture 2
    const lightPanel2 = new THREE.Mesh(lightPanelGeo, ceilingLightMat);
    lightPanel2.position.set(0.6, wallH - t - 0.025, -0.3);
    this.group.add(lightPanel2);

    // Interior point lights (warm)
    const iLight1 = new THREE.PointLight("#FFD080", 8, 6, 2);
    iLight1.position.set(-0.6, wallH - 0.4, -0.3);
    this.group.add(iLight1);

    const iLight2 = new THREE.PointLight("#FFD080", 8, 6, 2);
    iLight2.position.set(0.6, wallH - 0.4, -0.3);
    this.group.add(iLight2);

    // Small warm fill light at reception
    const iLight3 = new THREE.PointLight("#FFC860", 4, 5, 2);
    iLight3.position.set(0, wallH - 1.0, -wallD / 2 + 1.5);
    this.group.add(iLight3);
  }

  /** Build a simple chair */
  private buildChair(x: number, z: number, wallH: number): void {
    const woodDarkMat = mat("woodDark");
    const cushionGeo = new THREE.BoxGeometry(0.4, 0.06, 0.4);
    const cushion = new THREE.Mesh(cushionGeo, new THREE.MeshStandardMaterial({
      color: "#5A9AC0", roughness: 0.7, metalness: 0.0,
    }));
    cushion.position.set(x, 0.48, z);
    cushion.castShadow = true;
    this.group.add(cushion);
    // Seat back
    const backGeo = new THREE.BoxGeometry(0.4, 0.45, 0.04);
    const back = new THREE.Mesh(backGeo, woodDarkMat);
    back.position.set(x, 0.73, z + (z < 0 ? -0.18 : 0.18));
    back.castShadow = true;
    this.group.add(back);
    // Legs
    const legGeo = new THREE.CylinderGeometry(0.02, 0.02, 0.45, 6);
    for (const lx of [-1, 1]) {
      for (const lz of [-1, 1]) {
        const leg = new THREE.Mesh(legGeo, woodDarkMat);
        leg.position.set(x + lx * 0.16, 0.225, z + lz * 0.16);
        this.group.add(leg);
      }
    }
  }

  // ══════════════════════════════════════════════════════════════════════
  // EXTERIOR METHODS (unchanged from original — kept for completeness)
  // ══════════════════════════════════════════════════════════════════════

  private buildTower(xOffset: number, height: number, wallHeight: number): void {
    const towerW = 2.2;
    const tGeo = new THREE.CylinderGeometry(1.1, 1.3, height, 12);
    const tower = new THREE.Mesh(tGeo, mat("wallCreamHi"));
    tower.position.set(xOffset, wallHeight + height / 2, 0.5);
    tower.castShadow = true;
    this.group.add(tower);

    const capGeo = new THREE.ConeGeometry(1.5, 1.2, 12);
    const cap = new THREE.Mesh(capGeo, mat("roofBlue"));
    cap.position.set(xOffset, wallHeight + height + 0.6, 0.5);
    cap.castShadow = true;
    this.group.add(cap);

    const twGeo = new THREE.PlaneGeometry(0.5, 1.0);
    const tw = new THREE.Mesh(twGeo, mat("windowGlass"));
    tw.position.set(xOffset, wallHeight + height * 0.6, 0.5 - 1.32);
    tw.rotation.y = Math.PI;
    this.group.add(tw);

    // ── Clock face (front of tower, below cone cap) ────────────
    const clockY = wallHeight + height * 0.35;
    const clockRadius = 0.8;

    // White face (circular)
    const faceGeo = new THREE.CircleGeometry(clockRadius, 32);
    const face = new THREE.Mesh(faceGeo, mat("crossWhite"));
    face.position.set(xOffset, clockY, 0.5 - 1.32);
    face.rotation.y = Math.PI;
    face.receiveShadow = true;
    this.group.add(face);

    // Outer ring
    const ringGeo = new THREE.TorusGeometry(clockRadius + 0.02, 0.03, 8, 32);
    const ring = new THREE.Mesh(ringGeo, mat("windowFrame"));
    ring.position.set(xOffset, clockY, 0.5 - 1.32 + 0.01);
    ring.rotation.y = Math.PI;
    this.group.add(ring);

    // 4 tick marks (12, 3, 6, 9 o'clock)
    const tickLen = 0.15;
    const tickThick = 0.035;
    const outerTickR = clockRadius - 0.05;
    for (const angle of [0, Math.PI / 2, Math.PI, Math.PI * 1.5]) {
      const tickW = new THREE.BoxGeometry(tickThick, tickLen, 0.02);
      const tick = new THREE.Mesh(tickW, mat("windowFrame"));
      const tx = xOffset + Math.cos(angle) * outerTickR;
      const ty = clockY + Math.sin(angle) * outerTickR;
      tick.position.set(tx, ty, 0.5 - 1.32 + 0.01);
      tick.rotation.z = angle - Math.PI / 2;
      tick.rotation.y = Math.PI;
      this.group.add(tick);
    }

    // Hour hand
    const hourHandW = new THREE.BoxGeometry(tickThick * 0.8, clockRadius * 0.5, 0.02);
    const hourHand = new THREE.Mesh(hourHandW, mat("windowFrame"));
    hourHand.position.set(xOffset, clockY + clockRadius * 0.25, 0.5 - 1.32 + 0.02);
    hourHand.rotation.z = -Math.PI / 4;
    hourHand.rotation.y = Math.PI;
    this.group.add(hourHand);
  }

  private buildDoor(wallDepth: number): void {
    // No-op: door elements are now built inside buildHollowRoom
    // Kept for API compatibility if called elsewhere
  }

  private buildWindows(wallW: number, wallH: number, wallD: number): void {
    const winW = 1.0;
    const winH = 1.2;
    const winY = 1.5;
    const winZ = -wallD / 2 - 0.02;

    for (const side of [-1, 1]) {
      const wx = side * (wallW / 2 - 1.0);

      const fGeo = new THREE.BoxGeometry(winW + 0.25, winH + 0.25, 0.15);
      const f = new THREE.Mesh(fGeo, mat("windowFrame"));
      f.position.set(wx, winY, winZ);
      this.group.add(f);

      const gGeo = new THREE.PlaneGeometry(winW, winH);
      const g = new THREE.Mesh(gGeo, mat("windowGlass"));
      g.position.set(wx, winY, winZ - 0.06);
      g.rotation.y = Math.PI;
      this.group.add(g);

      const sGeo = new THREE.BoxGeometry(winW + 0.35, 0.1, 0.2);
      const s = new THREE.Mesh(sGeo, mat("woodLight"));
      s.position.set(wx, winY - winH / 2 - 0.05, winZ - 0.05);
      this.group.add(s);

      if (side === 1) {
        this.buildWindowFlowerBox(wx, winY - winH / 2 - 0.15, winZ - 0.1);
      } else if (side === -1) {
        this.buildWindowFlowerBox(wx, winY - winH / 2 - 0.15, winZ - 0.1);
      }
    }

    for (let i = 0; i < 2; i++) {
      const sz = (i - 0.5) * 2.0;
      const wx = -wallW / 2 - 0.02;
      const winY2 = 1.8;

      const fGeo = new THREE.BoxGeometry(0.15, winH, winW);
      const f = new THREE.Mesh(fGeo, mat("windowFrame"));
      f.position.set(wx, winY2, sz);
      this.group.add(f);

      const gGeo = new THREE.PlaneGeometry(winW, winH);
      const g = new THREE.Mesh(gGeo, mat("windowGlass"));
      g.position.set(wx - 0.06, winY2, sz);
      g.rotation.y = -Math.PI / 2;
      this.group.add(g);
    }
  }

  private buildAwning(wallW: number, wallD: number): void {
    const awningW = 3.0;
    const awningD = 1.2;
    const awningH = 0.15;

    const geo = new THREE.BoxGeometry(awningW, awningH, awningD);
    const awning = new THREE.Mesh(geo, mat("awning"));
    awning.position.set(0, CLINIC.wallHeight - 0.3, -wallD / 2 - awningD / 2);
    awning.rotation.x = 0.15;
    awning.castShadow = true;
    this.group.add(awning);
    this.cutawayParts.push(awning);

    const scallopGeo = new THREE.TorusGeometry(0.25, 0.06, 8, 16, Math.PI);
    const scallops = 6;
    for (let i = 0; i < scallops; i++) {
      const sx = -(awningW / 2) + (awningW / scallops) * (i + 0.5);
      const s = new THREE.Mesh(scallopGeo, mat("awning"));
      s.position.set(sx, awning.position.y - 0.05, -wallD / 2 - awningD);
      s.rotation.y = Math.PI / 2;
      this.group.add(s);
      this.cutawayParts.push(s);
    }

    const postGeo = new THREE.CylinderGeometry(0.06, 0.06, 2.2, 6);
    for (const side of [-1, 1]) {
      const post = new THREE.Mesh(postGeo, mat("woodDark"));
      post.position.set(side * (awningW / 2 - 0.2), 1.1, -wallD / 2 - awningD + 0.1);
      this.group.add(post);
      this.cutawayParts.push(post);
    }
  }

  private buildFlowerPots(wallW: number, wallD: number): void {
    for (const side of [-1, 1]) {
      const px = side * (wallW / 2 - 1.2);
      const pz = -wallD / 2 - 0.5;

      const potGeo = new THREE.CylinderGeometry(0.35, 0.25, 0.4, 8);
      const pot = new THREE.Mesh(potGeo, mat("potter"));
      pot.position.set(px, 0.2, pz);
      pot.castShadow = true;
      this.group.add(pot);

      const flowerColors: MaterialKey[] = ["petal0", "petal2", "petal", "petal0"];
      for (let i = 0; i < 5; i++) {
        const angle = (i / 5) * Math.PI * 2;
        const r = 0.2;
        const fx = px + Math.cos(angle) * r;
        const fz = pz + Math.sin(angle) * r;
        const fGeo = new THREE.SphereGeometry(0.1, 6, 4);
        const f = new THREE.Mesh(fGeo, mat(flowerColors[i % 4]));
        f.position.set(fx, 0.45, fz);
        this.group.add(f);
      }

      const budGeo = new THREE.SphereGeometry(0.12, 8, 6);
      const bud = new THREE.Mesh(budGeo, mat("petal0"));
      bud.position.set(px, 0.5, pz);
      this.group.add(bud);
    }
  }

  private buildWindowFlowerBox(wx: number, wy: number, wz: number): void {
    const boxGeo = new THREE.BoxGeometry(1.2, 0.25, 0.25);
    const box = new THREE.Mesh(boxGeo, mat("woodDark"));
    box.position.set(wx, wy, wz);
    this.group.add(box);

    const petalKeys: MaterialKey[] = ["petal0", "petal2", "petal", "petal0"];
    for (let i = 0; i < 6; i++) {
      const fx = wx - 0.5 + (i / 5) * 1.0;
      const fGeo = new THREE.SphereGeometry(0.08, 6, 4);
      const f = new THREE.Mesh(fGeo, mat(petalKeys[i % 4]));
      f.position.set(fx, wy + 0.15, wz);
      this.group.add(f);
    }
  }

  private buildCrossSign(wallH: number, wallD: number): void {
    const poleGeo = new THREE.CylinderGeometry(0.06, 0.08, 2.0, 6);
    const pole = new THREE.Mesh(poleGeo, mat("windowFrame"));
    pole.position.set(0, wallH + 1.0, -wallD / 2 - 1.0);
    this.group.add(pole);

    const signGeo = new THREE.CylinderGeometry(0.6, 0.6, 0.12, 16);
    const sign = new THREE.Mesh(signGeo, mat("crossWhite"));
    sign.position.set(0, wallH + 1.8, -wallD / 2 - 1.0);
    sign.castShadow = true;
    this.group.add(sign);

    const crossH = new THREE.BoxGeometry(0.7, 0.2, 0.1);
    const crossV = new THREE.BoxGeometry(0.2, 0.7, 0.1);
    for (const piece of [crossH, crossV]) {
      const m = new THREE.Mesh(piece, mat("crossRed"));
      m.position.copy(sign.position);
      m.position.z -= 0.08;
      this.group.add(m);
    }
  }

  private buildSteps(wallD: number): void {
    const stepGeo = new THREE.BoxGeometry(2.5, 0.15, 0.8);
    for (let i = 0; i < 3; i++) {
      const step = new THREE.Mesh(stepGeo, mat("stoneLight"));
      step.position.set(
        0,
        0.075 + i * 0.1,
        -wallD / 2 - 0.5 - i * 0.7
      );
      step.castShadow = true;
      step.receiveShadow = true;
      this.group.add(step);
    }
  }

  get object(): THREE.Object3D {
    return this.group;
  }

  get doorWorldPos(): THREE.Vector3 {
    const local = new THREE.Vector3(0, 1.2, -CLINIC.wallDepth / 2);
    return local.applyMatrix4(this.group.matrixWorld);
  }

  setCutawayVisible(visible: boolean): void {
    this.cutawayVisible = visible;
    for (const part of this.cutawayParts) part.visible = !visible;
  }

  toggleCutaway(): boolean {
    this.setCutawayVisible(!this.cutawayVisible);
    return this.cutawayVisible;
  }

  isDoorTarget(object: THREE.Object3D): boolean {
    let current: THREE.Object3D | null = object;
    while (current && current !== this.group) {
      if (current.name === "hospitalDoorTarget") return true;
      current = current.parent;
    }
    return false;
  }

  dispose(): void {
    // Only dispose materials created inline (not the shared cached ones from mat())
    // The shared cache in Materials.ts owns all mat() materials.
    // We only need to dispose geometries here.
    this.group.traverse((o) => {
      if (o instanceof THREE.Mesh && o.geometry) {
        o.geometry.dispose();
      }
    });
  }
}

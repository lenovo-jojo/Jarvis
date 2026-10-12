/**
 * Paths.ts — owns path splines and terrain-conforming path geometry.
 * No other module should create path mesh.
 */
import * as THREE from "three";
import { mat } from "../core/Materials";
import { PATH, VEGETATION } from "../core/WorldConfig";
import { terrainHeight } from "./Terrain";
import { SeededRng } from "../core/SeededNoise";

export interface PathPoint {
  pos: THREE.Vector3;
  tangent: THREE.Vector3;
}

export class Paths {
  private group = new THREE.Group();

  /** Path spline control points in XZ. Y computed from terrain. */
  private controlPoints: [number, number][] = PATH.controlPoints.map((p) => [p[0], p[1]]);
  private curve: THREE.CatmullRomCurve3;

  constructor() {
    this.curve = new THREE.CatmullRomCurve3(
      this.controlPoints.map(([x, z]) => new THREE.Vector3(x, 0, z)),
      false,
      "centripetal",
      0.45
    );
    this.buildRibbon();
  }

  private buildRibbon(): void {
    const matPath = mat("path");
    const matEdge = mat("pathEdge");

    // Sample the spline densely
    const samples = 120;
    const pts: THREE.Vector3[] = [];
    for (let i = 0; i <= samples; i++) {
      const t = i / samples;
      const p = this.splinePos(t);
      pts.push(p);
    }

    // Build ribbon geometry: two parallel lines offset by width/2
    const verts: number[] = [];
    const uvs: number[] = [];
    const indices: number[] = [];

    for (let i = 0; i < pts.length; i++) {
      const p = pts[i];
      const next = i < pts.length - 1 ? pts[i + 1] : p.clone().sub(p.clone().sub(pts[i - 1] || p));
      const prev = i > 0 ? pts[i - 1] : p;
      const tangent = new THREE.Vector3().subVectors(next, prev).normalize();
      // Perpendicular in XZ plane
      const perp = new THREE.Vector3(-tangent.z, 0, tangent.x).normalize();

      const left = p.clone().addScaledVector(perp, PATH.width / 2);
      const right = p.clone().addScaledVector(perp, -PATH.width / 2);

      // Slightly below terrain to avoid z-fighting
      // Keep the ribbon safely above interpolated terrain triangles. A 3 cm
      // offset still allowed the plaza plateau transition to cut through it.
      left.y = terrainHeight(left.x, left.z) + 0.09;
      right.y = terrainHeight(right.x, right.z) + 0.09;

      verts.push(left.x, left.y, left.z, right.x, right.y, right.z);
      const v = i / (pts.length - 1);
      uvs.push(0, v, 1, v);
    }

    for (let i = 0; i < pts.length - 1; i++) {
      const a = i * 2, b = i * 2 + 1, c = (i + 1) * 2, d = (i + 1) * 2 + 1;
      indices.push(a, c, b, b, c, d);
    }

    const geo = new THREE.BufferGeometry();
    geo.setAttribute("position", new THREE.Float32BufferAttribute(verts, 3));
    geo.setAttribute("uv", new THREE.Float32BufferAttribute(uvs, 2));
    geo.setIndex(indices);
    geo.computeVertexNormals();

    const ribbon = new THREE.Mesh(geo, matPath);
    ribbon.receiveShadow = true;
    ribbon.name = "PathRibbon";
    this.group.add(ribbon);

    // ── PATH EDGES (darker border strips) ────────────────────────
    const edgeGeo = this.buildEdgeGeo(pts, PATH.width / 2, PATH.width / 2 + 0.25);
    if (edgeGeo) {
      const edge = new THREE.Mesh(edgeGeo, matEdge);
      edge.receiveShadow = true;
      edge.name = "PathEdge";
      this.group.add(edge);
    }

    // ── PATH POSTS (small wooden markers along the path) ─────────
    this.addPathPosts();

    // ── PATH EDGE LIGHTS (warm glow posts) ──────────────────────
    this.addPathEdgeLights(pts);
  }

  /** Spline helper — returns per-point perp vector for edge offset. */
  private makePerpFn(pts: THREE.Vector3[]) {
    return (i: number): THREE.Vector3 => {
      const p = pts[i];
      const next = i < pts.length - 1 ? pts[i + 1] : p.clone().sub(p.clone().sub(pts[i - 1] || p));
      const prev = i > 0 ? pts[i - 1] : p;
      const tangent = new THREE.Vector3().subVectors(next, prev).normalize();
      return new THREE.Vector3(-tangent.z, 0, tangent.x).normalize();
    };
  }

  private addPathEdgeLights(pts: THREE.Vector3[]): void {
    const edgeLightMat = new THREE.MeshStandardMaterial({
      color: new THREE.Color("#FFD080"),
      emissive: new THREE.Color("#FFD080"),
      emissiveIntensity: 1.2,
      roughness: 0.3,
    });
    const postGeo = new THREE.CylinderGeometry(0.03, 0.04, 0.35, 6);
    const glowGeo = new THREE.SphereGeometry(0.06, 6, 4);
    const baseGeo = new THREE.CylinderGeometry(0.06, 0.07, 0.04, 6);

    // Space lights ~8 units apart along the path
    const spacing = 8;
    let prevEnd: THREE.Vector3 | null = null;
    // Sample a dense set of points from the path to find evenly-spaced light positions
    const denseSamples = 200;
    const densePts: THREE.Vector3[] = [];
    for (let i = 0; i <= denseSamples; i++) {
      const t = i / denseSamples;
      densePts.push(this.splinePos(t));
    }

    let accumulatedDist = 0;
    const nextLightDist = spacing / 2; // first light at half spacing from start

    for (let i = 1; i < densePts.length; i++) {
      const d = densePts[i].distanceTo(densePts[i - 1]);
      accumulatedDist += d;
      if (accumulatedDist < nextLightDist - 0.01) continue;

      // Reset when we find the next position
      if (prevEnd) {
        const distFromPrev = densePts[i].distanceTo(prevEnd);
        if (distFromPrev > spacing - 0.3) {
          // Place light on the right edge (facing toward the beach)
          const perp = this.makePerpFn(densePts)(i);
          const rightEdge = densePts[i].clone().addScaledVector(perp, -(PATH.width / 2 + 0.25) + 0.15);
          const ly = terrainHeight(rightEdge.x, rightEdge.z) + 0.3;

          const postGroup = new THREE.Group();

          // Post pole
          const post = new THREE.Mesh(postGeo, mat("woodDark"));
          post.position.y = ly - 0.17;
          post.castShadow = true;
          postGroup.add(post);

          // Light sphere on top
          const glow = new THREE.Mesh(glowGeo, edgeLightMat);
          glow.position.y = ly;
          postGroup.add(glow);

          // Small base plate
          const base = new THREE.Mesh(baseGeo, mat("woodMed"));
          base.position.y = ly - 0.35;
          postGroup.add(base);

          // Subtle point light
          const pl = new THREE.PointLight(0xFFD080, 0.8, 5);
          pl.position.y = ly + 0.05;
          postGroup.add(pl);

          postGroup.position.set(rightEdge.x, ly - 0.17, rightEdge.z);
          this.group.add(postGroup);

          prevEnd = densePts[i];
          // Next light spacing
          // Reset to find next one
          accumulatedDist = 0; // reset to keep going
          break;
        }
      } else if (i === Math.floor(denseSamples * 0.15)) {
        // First light
        const perp = this.makePerpFn(densePts)(i);
        const rightEdge = densePts[i].clone().addScaledVector(perp, -(PATH.width / 2 + 0.25) + 0.15);
        const ly = terrainHeight(rightEdge.x, rightEdge.z) + 0.3;

        const postGroup = new THREE.Group();
        const post = new THREE.Mesh(postGeo, mat("woodDark"));
        post.position.y = ly - 0.17;
        post.castShadow = true;
        postGroup.add(post);
        const glow = new THREE.Mesh(glowGeo, edgeLightMat);
        glow.position.y = ly;
        postGroup.add(glow);
        const base = new THREE.Mesh(baseGeo, mat("woodMed"));
        base.position.y = ly - 0.35;
        postGroup.add(base);
        const pl = new THREE.PointLight(0xFFD080, 0.8, 5);
        pl.position.y = ly + 0.05;
        postGroup.add(pl);
        postGroup.position.set(rightEdge.x, ly - 0.17, rightEdge.z);
        this.group.add(postGroup);

        prevEnd = densePts[i];
      }
    }

    // If we haven't placed enough lights, ensure at least 3-4 evenly spaced
    if (!prevEnd) {
      // Fallback: place lights at fixed t positions
      for (const tVal of [0.15, 0.45, 0.75]) {
        const idx = Math.floor(tVal * densePts.length);
        const pt = densePts[idx];
        const perp = this.makePerpFn(densePts)(idx);
        const rightEdge = pt.clone().addScaledVector(perp, -(PATH.width / 2 + 0.25) + 0.15);
        const ly = terrainHeight(rightEdge.x, rightEdge.z) + 0.3;

        const postGroup = new THREE.Group();
        const post = new THREE.Mesh(postGeo, mat("woodDark"));
        post.position.y = ly - 0.17;
        post.castShadow = true;
        postGroup.add(post);
        const glow = new THREE.Mesh(glowGeo, edgeLightMat);
        glow.position.y = ly;
        postGroup.add(glow);
        const base = new THREE.Mesh(baseGeo, mat("woodMed"));
        base.position.y = ly - 0.35;
        postGroup.add(base);
        const pl = new THREE.PointLight(0xFFD080, 0.8, 5);
        pl.position.y = ly + 0.05;
        postGroup.add(pl);
        postGroup.position.set(rightEdge.x, ly - 0.17, rightEdge.z);
        this.group.add(postGroup);
      }
    }
  }

  private splinePos(t: number): THREE.Vector3 {
    const point = this.curve.getPoint(THREE.MathUtils.clamp(t, 0, 1));
    point.y = terrainHeight(point.x, point.z);
    return point;
  }

  private addPathPosts(): void {
    const rng = new SeededRng(0xCAF3);
    const postGeo = new THREE.CylinderGeometry(0.08, 0.1, 0.5, 6);
    const postMat = mat("woodDark");

    for (let i = 0; i < PATH.controlPoints.length - 1; i++) {
      const [x0, z0] = PATH.controlPoints[i];
      const [x1, z1] = PATH.controlPoints[i + 1];
      // Place posts at fractional positions along each segment
      for (let f = 0.15; f < 0.9; f += 0.3) {
        const x = x0 + (x1 - x0) * f;
        const z = z0 + (z1 - z0) * f;
        // Only place if not in beach zone
        if (z < 1.5) continue;
        const offset = (rng.next() - 0.5) * 2.5;
        const px = x + offset;
        const pz = z;
        if (Math.abs(px - x) < 1.5) { // skip if too close to path center
          continue;
        }
        const post = new THREE.Mesh(postGeo, postMat);
        post.position.set(px, terrainHeight(px, pz) + 0.25, pz);
        post.rotation.y = rng.next() * Math.PI * 2;
        post.castShadow = true;
        this.group.add(post);
      }
    }
  }

  private buildEdgeGeo(
    pts: THREE.Vector3[],
    innerR: number,
    outerR: number
  ): THREE.BufferGeometry | null {
    // Real edge strips — walk each segment and create low flat boxes at ribbon edges.
    const verts: number[] = [];
    const indices: number[] = [];
    const stripWidth = 0.18; // narrow strip width

    for (let i = 0; i < pts.length - 1; i++) {
      const p = pts[i];
      const n = i < pts.length - 1 ? pts[i + 1] : p.clone().sub(p.clone().sub(pts[i - 1] || p));
      const tangent = new THREE.Vector3().subVectors(n, p).normalize();
      const perp = new THREE.Vector3(-tangent.z, 0, tangent.x).normalize();

      const innerOffset = innerR;
      const outerOffset = outerR;

      for (const side of [-1, 1]) {
        const innerPt = p.clone().addScaledVector(perp, innerOffset * side);
        const outerPt = p.clone().addScaledVector(perp, outerOffset * side);
        const innerNext = n.clone().addScaledVector(perp, innerOffset * side);
        const outerNext = n.clone().addScaledVector(perp, outerOffset * side);

        // Clamp edges slightly inward so strips meet the ribbon smoothly
        const edgeInnerOffset = innerR * 0.95 * side;
        const edgeOuterOffset = (innerR + stripWidth) * 0.95 * side;
        const innerEdgePt = p.clone().addScaledVector(perp, edgeInnerOffset);
        const outerEdgePt = p.clone().addScaledVector(perp, edgeOuterOffset);
        const innerEdgeNext = n.clone().addScaledVector(perp, edgeInnerOffset);
        const outerEdgeNext = n.clone().addScaledVector(perp, edgeOuterOffset);

        const iy = terrainHeight(innerEdgePt.x, innerEdgePt.z) + 0.08;
        const oy = terrainHeight(outerEdgePt.x, outerEdgePt.z) + 0.08;
        const iny = terrainHeight(innerEdgeNext.x, innerEdgeNext.z) + 0.08;
        const ony = terrainHeight(outerEdgeNext.x, outerEdgeNext.z) + 0.08;

        const a = verts.length / 3;
        verts.push(innerEdgePt.x, iy, innerEdgePt.z, outerEdgePt.x, oy, outerEdgePt.z);
        verts.push(innerEdgeNext.x, iny, innerEdgeNext.z, outerEdgeNext.x, ony, outerEdgeNext.z);

        const a0 = a, a1 = a + 1, a2 = a + 2, a3 = a + 3;
        indices.push(a0, a2, a1, a1, a2, a3);
      }
    }

    if (verts.length === 0) return null;

    const geo = new THREE.BufferGeometry();
    geo.setAttribute("position", new THREE.Float32BufferAttribute(verts, 3));
    geo.setIndex(indices);
    geo.computeVertexNormals();
    return geo;
  }

  /** Get the path height at a point (for player snap) */
  pathPointAt(t: number): THREE.Vector3 {
    return this.splinePos(t);
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

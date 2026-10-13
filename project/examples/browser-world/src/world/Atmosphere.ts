/**
 * Atmosphere.ts — owns outdoor lights, sky, and fog.
 * The ONLY module that creates DirectionalLight, HemisphereLight, or Fog.
 * No other module may create outdoor light sources.
 */
import * as THREE from "three";
import { DAY, TWILIGHT } from "../core/Palette";
import { RENDER } from "../core/WorldConfig";

export interface AtmosphereState {
  dayFactor: number; // 0 = full night, 1 = full day
}

export class Atmosphere {
  readonly sun: THREE.DirectionalLight;
  readonly hemi: THREE.HemisphereLight;
  readonly sky: THREE.Mesh;
  private fog: THREE.Fog;
  private skyTopColor = new THREE.Color(DAY.skyTop);
  private skyHorizonColor = new THREE.Color(DAY.skyHorizon);
  private state: AtmosphereState = { dayFactor: 1.0 };

  constructor() {
    // ── FOG ──────────────────────────────────────────────────────
    this.fog = new THREE.Fog(new THREE.Color(DAY.fogDay), 20, 90);

    // ── SUN (single directional) ─────────────────────────────────
    this.sun = new THREE.DirectionalLight(new THREE.Color("#FFF8E8"), 1.4);
    this.sun.position.set(8, 12, -6); // south-west, elevated ~52°
    this.sun.castShadow = true;
    this.sun.shadow.mapSize.set(RENDER.shadowMapSize, RENDER.shadowMapSize);
    this.sun.shadow.camera.left = -20;
    this.sun.shadow.camera.right = 20;
    this.sun.shadow.camera.top = 20;
    this.sun.shadow.camera.bottom = -20;
    this.sun.shadow.camera.near = 0.5;
    this.sun.shadow.camera.far = 60;
    this.sun.shadow.bias = -0.0005;
    this.sun.shadow.normalBias = 0.02;
    this.sun.shadow.camera.updateProjectionMatrix();

    // ── HEMISPHERE (sky/ground ambient) ──────────────────────────
    this.hemi = new THREE.HemisphereLight(
      new THREE.Color(DAY.skyTop),
      new THREE.Color(DAY.grassLight),
      0.92
    );

    // ── SKY DOME ─────────────────────────────────────────────────
    // Full sphere keeps elevated overview cameras inside the sky. The previous
    // upper hemisphere exposed the renderer's black clear area below its rim.
    const skyGeo = new THREE.SphereGeometry(100, 32, 24);
    const skyMat = new THREE.ShaderMaterial({
      side: THREE.BackSide,
      depthWrite: false,
      uniforms: {
        topColor: { value: new THREE.Color(DAY.skyTop) },
      horizonColor: { value: new THREE.Color(DAY.skyHorizon) },
      offset: { value: 10 },
      exponent: { value: 0.6 },
    },
    vertexShader: `
      varying vec3 vWorldPosition;
      void main() {
        vec4 worldPos = modelMatrix * vec4(position, 1.0);
        vWorldPosition = worldPos.xyz;
        gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
      }
    `,
    fragmentShader: `
      uniform vec3 topColor;
      uniform vec3 horizonColor;
      uniform float offset;
      uniform float exponent;
      varying vec3 vWorldPosition;
      void main() {
        float h = normalize(vWorldPosition + vec3(0.0, offset, 0.0)).y;
        float t = pow(max(h, 0.0), exponent);
        gl_FragColor = vec4(mix(horizonColor, topColor, t), 1.0);
      }
    `,
  });
    this.sky = new THREE.Mesh(skyGeo, skyMat);
    this.sky.name = "SkyDome";
  }

  get object(): THREE.Object3D {
    // Return a group containing all atmosphere objects
    const g = new THREE.Group();
    g.add(this.sun, this.hemi, this.sky);
    return g;
  }

  /** Apply fog to the scene (must be called from World) */
  applyFog(scene: THREE.Scene): void {
    scene.fog = this.fog;
    scene.background = new THREE.Color(DAY.skyHorizon);
  }

  /** Update per-frame (sun position, fog color, sky color) */
  update(state: AtmosphereState): void {
    this.state = state;
    const d = state.dayFactor;

    // Lerp sky colors day↔night
    const lerpC = (day: string, night: string, factor: number) =>
      new THREE.Color(day).lerp(new THREE.Color(night), 1 - factor);

    const skyTop = lerpC(DAY.skyTop, TWILIGHT.skyTop, d);
    const horizon = lerpC(DAY.skyHorizon, TWILIGHT.skyHorizon, d);

    (this.sky.material as THREE.ShaderMaterial).uniforms["topColor"].value = skyTop;
    (this.sky.material as THREE.ShaderMaterial).uniforms["horizonColor"].value = horizon;

    this.fog.color.copy(horizon).lerp(new THREE.Color(DAY.fogDay), d);
    this.fog.near = THREE.MathUtils.lerp(15, 35, d);
    this.fog.far = THREE.MathUtils.lerp(120, 60, d);

    this.hemi.color.copy(skyTop);
    this.hemi.intensity = THREE.MathUtils.lerp(0.22, 0.92, d);

    this.sun.intensity = THREE.MathUtils.lerp(0.2, 1.4, d);
  }

  dispose(): void {
    this.sky.geometry.dispose();
    (this.sky.material as THREE.Material).dispose();
  }
}

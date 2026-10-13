/**
 * TextureLab.ts — procedural Canvas2D textures (deterministic, seeded).
 * No external image files needed. All colors come from Palette or explicit RGB.
 */
import * as THREE from "three";
import { hexToRgb } from "./Palette";
import { SeededRng, ValueNoise2D } from "./SeededNoise";

export interface TexOpts {
  size?: number;
  seed?: number;
}

/**
 * Per-pixel noise texture around a base RGB.
 * `variation` = max per-channel deviation (0–100).
 */
export function noiseTexture(
  base: { r: number; g: number; b: number } | string,
  variation: number,
  opts: TexOpts = {}
): THREE.CanvasTexture {
  const size = opts.size ?? 256;
  const seed = opts.seed ?? 42;

  let br: number, bg: number, bb: number;
  if (typeof base === "string") {
    const c = hexToRgb(base);
    br = c.r; bg = c.g; bb = c.b;
  } else {
    br = base.r; bg = base.g; bb = base.b;
  }

  const canvas = document.createElement("canvas");
  canvas.width = size;
  canvas.height = size;
  const ctx = canvas.getContext("2d")!;
  const img = ctx.createImageData(size, size);
  const noise = new ValueNoise2D(seed);
  const rng = new SeededRng(seed);

  for (let y = 0; y < size; y++) {
    for (let x = 0; x < size; x++) {
      const n = noise.fbm(x * 0.08, y * 0.08, 3) - 0.5;
      const micro = (rng.next() - 0.5) * 0.35;
      const delta = (n + micro) * variation;
      const i = (y * size + x) * 4;
      img.data[i]     = Math.max(0, Math.min(255, br + delta));
      img.data[i + 1] = Math.max(0, Math.min(255, bg + delta));
      img.data[i + 2] = Math.max(0, Math.min(255, bb + delta));
      img.data[i + 3] = 255;
    }
  }
  ctx.putImageData(img, 0, 0);

  const tex = new THREE.CanvasTexture(canvas);
  tex.wrapS = THREE.RepeatWrapping;
  tex.wrapT = THREE.RepeatWrapping;
  tex.colorSpace = THREE.SRGBColorSpace;
  return tex;
}

/** Mottled grass texture (blend between two greens). */
export function grassTexture(opts: TexOpts = {}): THREE.CanvasTexture {
  const size = opts.size ?? 256;
  const seed = opts.seed ?? 7;
  const canvas = document.createElement("canvas");
  canvas.width = size;
  canvas.height = size;
  const ctx = canvas.getContext("2d")!;
  const noise = new ValueNoise2D(seed);

  for (let y = 0; y < size; y++) {
    for (let x = 0; x < size; x++) {
      const n = noise.fbm(x * 0.06, y * 0.06, 4);
      const r = 75 + n * 68;
      const g = 168 + n * 52;
      const b = 112 + n * 24;
      ctx.fillStyle = `rgb(${Math.round(r)},${Math.round(g)},${Math.round(b)})`;
      ctx.fillRect(x, y, 1, 1);
    }
  }

  const tex = new THREE.CanvasTexture(canvas);
  tex.wrapS = THREE.RepeatWrapping;
  tex.wrapT = THREE.RepeatWrapping;
  tex.colorSpace = THREE.SRGBColorSpace;
  return tex;
}

/** Warm sand with speckle. */
export function sandTexture(opts: TexOpts = {}): THREE.CanvasTexture {
  return noiseTexture("#F0E0B8", 22, opts);
}

/** Vertical wood-grain stripes. */
export function woodTexture(opts: TexOpts = {}): THREE.CanvasTexture {
  const size = opts.size ?? 128;
  const seed = opts.seed ?? 33;
  const canvas = document.createElement("canvas");
  canvas.width = size;
  canvas.height = size;
  const ctx = canvas.getContext("2d")!;
  const noise = new ValueNoise2D(seed);

  for (let y = 0; y < size; y++) {
    for (let x = 0; x < size; x++) {
      const grain = noise.noise(x * 0.02, y * 0.25);
      const base = 130 + grain * 40 - 20;
      ctx.fillStyle = `rgb(${Math.round(base * 0.9)},${Math.round(base * 0.7)},${Math.round(base * 0.5)})`;
      ctx.fillRect(x, y, 1, 1);
    }
  }

  const tex = new THREE.CanvasTexture(canvas);
  tex.wrapS = THREE.RepeatWrapping;
  tex.wrapT = THREE.RepeatWrapping;
  tex.colorSpace = THREE.SRGBColorSpace;
  return tex;
}

/** Coral/cream striped awning. */
export function awningTexture(opts: TexOpts = {}): THREE.CanvasTexture {
  const size = opts.size ?? 128;
  const canvas = document.createElement("canvas");
  canvas.width = size;
  canvas.height = size;
  const ctx = canvas.getContext("2d")!;
  const stripeW = size / 8;
  for (let i = 0; i < 8; i++) {
    ctx.fillStyle = i % 2 === 0 ? "#E8846A" : "#F5EFE0";
    ctx.fillRect(i * stripeW, 0, stripeW, size);
  }
  const tex = new THREE.CanvasTexture(canvas);
  tex.wrapS = THREE.RepeatWrapping;
  tex.wrapT = THREE.RepeatWrapping;
  tex.colorSpace = THREE.SRGBColorSpace;
  return tex;
}

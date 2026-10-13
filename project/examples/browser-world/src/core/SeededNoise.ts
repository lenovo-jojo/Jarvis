/**
 * SeededNoise.ts — deterministic hashing, noise, and PRNG.
 * Single source of all seeded randomness in the project.
 * No uncontrolled Math.random() anywhere in world generation.
 */

// ── 32-BIT SEED HASHING (LCG-based, port from palette) ────────────────
export function hash32(seed: number, i: number, j: number): number {
  let h = (seed ^ (i * 374761393) ^ (j * 668265263)) | 0;
  h = Math.imul(h, 1553316887);
  h ^= h >>> 13;
  h = Math.imul(h, 2246822519);
  h ^= h >>> 16;
  return h;
}

// ── SEEDED PRNG (mulberry32 — fast, good distribution) ────────────────
export class SeededRng {
  private state: number;
  constructor(seed: number) {
    this.state = (seed | 0) & 0xffffffff;
    if (this.state === 0) this.state = 0x9e3779b9;
  }
  /** Uniform [0,1) */
  next(): number {
    this.state = (this.state + 0x6d2b79f5) | 0;
    let t = this.state;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  }
  /** Uniform [min,max) */
  range(min: number, max: number): number {
    return min + (max - min) * this.next();
  }
  /** Integer [min, max] inclusive */
  int(min: number, max: number): number {
    return Math.floor(this.range(min, max + 1));
  }
  /** Random unit vector in XY plane */
  dir(): [number, number] {
    const a = this.next() * Math.PI * 2;
    return [Math.cos(a), Math.sin(a)];
  }
  /** Pick a random element from an array */
  pick<T>(arr: T[]): T {
    return arr[Math.floor(this.next() * arr.length)];
  }
}

// ── VALUE NOISE 2-D ───────────────────────────────────────────────────
function valueAt(seed: number, x: number, y: number): number {
  const xi = Math.floor(x);
  const yi = Math.floor(y);
  const xf = x - xi;
  const yf = y - yi;
  const u = xf * xf * (3 - 2 * xf);
  const v = yf * yf * (3 - 2 * yf);
  const a = h01(seed, xi, yi);
  const b = h01(seed, xi + 1, yi);
  const c = h01(seed, xi, yi + 1);
  const d = h01(seed, xi + 1, yi + 1);
  const top = a + (b - a) * u;
  const bot = c + (d - c) * u;
  return top + (bot - top) * v;
}

function h01(seed: number, i: number, j: number): number {
  return (hash32(seed, i, j) >>> 0) / 4294967296;
}

/** 2-D value noise, output [0,1). */
export function value2D(seed: number, x: number, y: number): number {
  return valueAt(seed, x, y);
}

/** Fractal Brownian motion over value2D. */
export function fbm2D(seed: number, x: number, y: number, octaves = 3): number {
  let amp = 0.5;
  let freq = 1;
  let sum = 0;
  let norm = 0;
  for (let o = 0; o < octaves; o++) {
    sum += amp * valueAt(seed + o * 131, x * freq, y * freq);
    norm += amp;
    amp *= 0.5;
    freq *= 2;
  }
  return sum / norm;
}

/**
 * OOP wrapper used by TextureLab / Terrain:
 *   const n = new ValueNoise2D(seed);
 *   n.noise(x, y)      // [0,1)
 *   n.fbm(x, y, oct)   // [0,1)
 */
export class ValueNoise2D {
  private seed: number;
  constructor(seed: number) {
    this.seed = seed | 0;
  }
  noise(x: number, y: number): number {
    return valueAt(this.seed, x, y);
  }
  fbm(x: number, y: number, octaves = 3): number {
    return fbm2D(this.seed, x, y, octaves);
  }
}

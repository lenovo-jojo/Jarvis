/**
 * Palette.ts — single source of all color values.
 * No other file should define a hex color constant.
 * ART_DIRECTION.md §2 is the canonical table.
 *
 * Storybook coastal day: warm cream walls, coral roof, soft greens,
 * blue-violet shadows, pale sky. Saturated but controlled.
 */

export interface RGB {
  r: number; // 0–255
  g: number;
  b: number;
}

const HEX = /^[0-9a-fA-F]{6}$/;

/** Convert hex string → RGB object */
export function hexToRgb(hex: string): RGB {
  const h = hex.replace("#", "");
  return {
    r: parseInt(h.slice(0, 2), 16),
    g: parseInt(h.slice(2, 4), 16),
    b: parseInt(h.slice(4, 6), 16),
  };
}

/** Convert RGB → hex string */
export function rgbToHex(c: RGB): string {
  const toHex = (n: number) => Math.max(0, Math.min(255, Math.round(n))).toString(16).padStart(2, "0");
  return `#${toHex(c.r)}${toHex(c.g)}${toHex(c.b)}`;
}

/**
 * Deterministic color jitter by ±pct% per channel (seeded).
 * Used for per-instance variation so repeated objects never look cloned.
 */
export function jitter(hex: string, pct: number, seed: number): string {
  const c = hexToRgb(hex);
  // small deterministic hash spread (avoid modulo bias from raw seed)
  const h1 = ((seed * 7919) >>> 0) & 0xff;
  const h2 = ((seed * 104729) >>> 0) & 0xff;
  const h3 = ((seed * 15485863) >>> 0) & 0xff;
  const f = (h: number) => (((h - 128) / 128) * pct * 0.01);
  return rgbToHex({
    r: c.r * (1 + f(h1)),
    g: c.g * (1 + f(h2)),
    b: c.b * (1 + f(h3)),
  });
}

// ── DAYTIME PALETTE (default state) ──────────────────────────────
export const DAY = {
  skyTop:        "#9ED9EE",
  skyHorizon:    "#D9F1F7",
  oceanDeep:     "#48B9C8",
  oceanShallow:  "#72D0D5",
  waterFoam:     "#E8F5FA",
  sandLight:     "#F0E0B8",
  sandDark:      "#C8A878",
  grassLight:    "#AFE18A",
  grassDark:     "#83C66D",
  grassMid:      "#9AD67D",
  pathMain:      "#EEDB9F",
  pathEdge:      "#D0AD73",
  wallCream:     "#F5EFE0",
  wallCreamHi:   "#FFF8EE",
  roofCoral:     "#F17779",
  roofCoralHi:   "#F79A92",
  roofBlue:      "#69B6C4",
  roofBlueHi:    "#91D1D5",
  accentLav:     "#C0A9DD",
  woodDark:      "#7A5C3E",
  woodMed:       "#A8805A",
  woodLight:     "#C8A878",
  potter:        "#D4A878",
  petalA:        "#FA9FB6",
  petalB:        "#A9DCEF",
  petalC:        "#FFE487",
  petalD:        "#EAB4DE",
  leafGreen:     "#83D47E",
  leafDark:      "#65B96B",
  treeTrunk:     "#A97852",
  stoneLight:    "#D8D0C0",
  stoneDark:     "#A8A090",
  windowGlass:   "#BFE2F0",
  windowFrame:   "#F8F4E8",
  crossRed:      "#E84A4A",
  crossWhite:    "#FFF6EE",
  fogDay:        "#BEE0EE",
  shadowColor:   "#3A5C8A",
  floorTile:     "#C8D8E0",
  floorTileEmissive: "#D8DCE0",
  wallInterior:  "#EFE8D8",
} as const;

// ── TWILIGHT PALETTE (future state, not default) ─────────────────
export const TWILIGHT = {
  skyTop:     "#2A2E5C",
  skyHorizon: "#4A4E8C",
  oceanDeep:  "#1E2E5A",
  grassLight: "#4A6A8A",
  windowGlow: "#FFD080",
  shadowBlue: "#1A1E4A",
  fog:        "#3A3E70",
} as const;

/** Flat list of all daytime colors (for texture lab / debug) */
export const ALL_COLORS: string[] = (Object.keys(DAY) as Array<keyof typeof DAY>).map(
  (k) => DAY[k]
);

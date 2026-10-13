/**
 * Materials.ts — single owner of all shared materials.
 * One material per surface type; subtle roughness/variation per key.
 * No other module should call new THREE.MeshStandardMaterial for world surfaces.
 */
import * as THREE from "three";
import { hexToRgb, DAY } from "./Palette";
import { noiseTexture } from "./TextureLab";

export type MaterialKey =
  | "wallCream" | "wallCreamHi"
  | "roofCoral" | "roofCoralHi" | "roofBlue" | "roofBlueHi"
  | "grass" | "sand" | "path" | "pathEdge"
  | "stone" | "stoneLight" | "stoneDark"
  | "woodDark" | "woodMed" | "woodLight"
  | "leafGreen" | "leafDark" | "treeTrunk"
  | "ocean" | "oceanFoam"
  | "windowGlass" | "windowFrame"
  | "crossRed" | "crossWhite"
  | "petal" | "petal0" | "petal2" | "potter" | "awning"
  | "stonePillar"
  | "floorTile" | "wallInterior";

interface MatDef {
  color: keyof typeof DAY;
  roughness: number;
  metalness: number;
  texture?: "noise" | "grain" | "plain";
  emissive?: keyof typeof DAY;
  emissiveIntensity?: number;
  opacity?: number;
}

const DEFS: Record<MaterialKey, MatDef> = {
  wallCream:    { color: "wallCream",  roughness: 0.85, metalness: 0.0,  texture: "noise" },
  wallCreamHi:  { color: "wallCreamHi", roughness: 0.8, metalness: 0.0,  texture: "plain" },
  roofCoral:    { color: "roofCoral",  roughness: 0.65, metalness: 0.0,  texture: "noise" },
  roofCoralHi:  { color: "roofCoralHi", roughness: 0.6, metalness: 0.0, texture: "plain" },
  roofBlue:     { color: "roofBlue",   roughness: 0.65, metalness: 0.0,  texture: "noise" },
  roofBlueHi:   { color: "roofBlueHi", roughness: 0.6,  metalness: 0.0,  texture: "plain" },
  grass:        { color: "grassLight", roughness: 0.95, metalness: 0.0,  texture: "noise" },
  sand:         { color: "sandLight",  roughness: 0.9,  metalness: 0.0,  texture: "noise" },
  path:         { color: "pathMain",   roughness: 0.88, metalness: 0.0,  texture: "noise" },
  pathEdge:     { color: "pathEdge",   roughness: 0.9,  metalness: 0.0,  texture: "plain" },
  stone:        { color: "stoneLight", roughness: 0.85, metalness: 0.0,  texture: "noise" },
  stoneLight:   { color: "stoneLight", roughness: 0.85, metalness: 0.0,  texture: "noise" },
  stoneDark:    { color: "stoneDark",  roughness: 0.8,  metalness: 0.0,  texture: "noise" },
  stonePillar:  { color: "stoneDark",  roughness: 0.8,  metalness: 0.0 },
  woodDark:     { color: "woodDark",   roughness: 0.75, metalness: 0.0,  texture: "grain" },
  woodMed:      { color: "woodMed",    roughness: 0.7,  metalness: 0.0,  texture: "grain" },
  woodLight:    { color: "woodLight",  roughness: 0.65, metalness: 0.0,  texture: "grain" },
  leafGreen:    { color: "leafGreen",  roughness: 0.8,  metalness: 0.0,  texture: "noise" },
  leafDark:     { color: "leafDark",   roughness: 0.85, metalness: 0.0,  texture: "noise" },
  treeTrunk:    { color: "treeTrunk",  roughness: 0.9,  metalness: 0.0,  texture: "grain" },
  ocean:        { color: "oceanDeep",  roughness: 0.15, metalness: 0.05, opacity: 0.88 },
  oceanFoam:    { color: "waterFoam",  roughness: 0.3,  metalness: 0.0 },
  windowGlass:  { color: "windowGlass", roughness: 0.1, metalness: 0.1, opacity: 0.55, emissive: "windowGlass", emissiveIntensity: 0.25 },
  windowFrame:  { color: "windowFrame", roughness: 0.5, metalness: 0.15 },
  crossRed:     { color: "crossRed",   roughness: 0.5,  metalness: 0.0,  emissive: "crossRed", emissiveIntensity: 0.15 },
  crossWhite:   { color: "crossWhite", roughness: 0.5,  metalness: 0.0 },
  petal:        { color: "petalA",     roughness: 0.7,  metalness: 0.0 },
  potter:       { color: "potter",     roughness: 0.8,  metalness: 0.0,  texture: "noise" },
  // aliases for repeated flower/awning uses
  petal0:       { color: "petalA",     roughness: 0.7,  metalness: 0.0 },
  petal2:       { color: "petalD",     roughness: 0.7,  metalness: 0.0 },
  awning:       { color: "roofCoral",  roughness: 0.7,  metalness: 0.0,  texture: "noise" },
  floorTile:    { color: "floorTile",   roughness: 0.55, metalness: 0.0, texture: "noise" },
  wallInterior: { color: "wallInterior", roughness: 0.85, metalness: 0.0, texture: "plain" },
};

const cache = new Map<MaterialKey, THREE.Material>();
const dispoables: THREE.Material[] = [];

export function mat(key: MaterialKey): THREE.Material {
  const hit = cache.get(key);
  if (hit) return hit;

  const def = DEFS[key];
  const color = new THREE.Color(DAY[def.color]);
  const m = new THREE.MeshStandardMaterial({
    color,
    roughness: def.roughness,
    metalness: def.metalness,
  });

  if (def.opacity !== undefined) {
    m.transparent = true;
    m.opacity = def.opacity;
  }
  if (def.emissive !== undefined) {
    m.emissive = new THREE.Color(DAY[def.emissive]);
    m.emissiveIntensity = def.emissiveIntensity ?? 0.2;
  }

  // Subtle per-surface variation (determined, not Math.random)
  if (def.texture === "noise") {
    const base = DAY[def.color];
    const tex = noiseTexture(base, 12, { size: 128, seed: key.length * 7 + 3 });
    m.map = tex;
    m.map.colorSpace = THREE.SRGBColorSpace;
  } else if (def.texture === "grain") {
    m.roughness += 0.03;
  }

  cache.set(key, m);
  dispoables.push(m);
  return m;
}

export function disposeAll(): void {
  for (const m of dispoables) m.dispose();
  dispoables.length = 0;
  cache.clear();
}

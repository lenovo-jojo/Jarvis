# Critter Comforts — browser iteration

A recovered source snapshot of the Three.js/TypeScript world prototype. It includes authored world data, player/camera controllers, procedural world modules, and an editor persistence layer.

This source was copied from the existing project on October 12, 2026. Original local assets, reference media, and VRM characters are excluded. The `src/data` files are the authored snapshot, not regenerated defaults. Development can run with `npm install` and `npm run dev` on port 3333; supply permitted assets where requested by the source. Runtime rendering and asset completeness are not verified by a build.

AI assisted substantially with development. The source is included to make this iteration inspectable, not to claim a production-ready game.

Checks: `npm run typecheck` and `npm run build`. Both checks passed locally on October 12, 2026. The publication copy fixes the palette element type, removes an unused constructor argument, clarifies scene-parent typing and selection-box comparisons, and patches a compatible dependency. These changes were not applied to the live project. The large output bundle remains an optimization opportunity.

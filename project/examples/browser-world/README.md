# critter comforts — browser iteration

a recovered source snapshot of the three.js/typescript world prototype. it includes authored world data, player/camera controllers, procedural world modules, and an editor persistence layer.

this source was copied from the existing project on october 12, 2026. original local assets, reference media, and vrm characters are excluded. the `src/data` files are the authored snapshot, not regenerated defaults. development can run with `npm install` and `npm run dev` on port 3333; supply permitted assets where requested by the source. runtime rendering and asset completeness are not verified by a build.

ai assisted substantially with development. the source is included to make this iteration inspectable, not to claim a production-ready game.

checks: `npm test`, `npm run typecheck`, and `npm run build`. save-handler regression tests cover rejected asset payloads leaving both files unchanged and successful scene-only/combined saves. validation finishes before writes begin; multi-file writes are not transactional and concurrent saves are not serialized. both checks passed locally on october 12, 2026. the publication copy fixes the palette element type, removes an unused constructor argument, clarifies scene-parent typing and selection-box comparisons, and patches a compatible dependency. these changes were not applied to the live project. the large output bundle remains an optimization opportunity.

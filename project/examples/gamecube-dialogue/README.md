# GameCube dialogue prototype

Source recovered from my `cat-gpt` project, an experimental adaptation of an earlier community dialogue-mod approach for Animal Crossing in Dolphin. The original upstream attribution still needs reconstruction; this is not a claim that I originated every module. AI assisted the adaptation and development.

The Python source contains a dialogue parser/encoder, conversation state, platform memory readers, model-request integration, and a "Feeling chatty" choice-menu injection. This snapshot excludes keys, game files, scraped datasets, and saved conversation/memory data.

The included tests exercise real parser/encoder and state logic while stubbing emulator, model, and gossip I/O. Run `python3 tests/run_checks.py` from this directory; the check runner needs only Python’s standard library. Passing them does not verify Dolphin memory writes, screenshots, or model dialogue end-to-end.

The archived requirements document the original environment; dependency modernization and an emulator demonstration remain follow-up work. Provide your own supported emulator/game setup and service configuration to explore the prototype. It is not a packaged mod release.

All three recovered assertion checks passed locally on October 12, 2026.

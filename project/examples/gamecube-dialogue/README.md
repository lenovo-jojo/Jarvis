# gamecube dialogue prototype

source recovered from my `cat-gpt` project, an experimental adaptation of an earlier community dialogue-mod approach for animal crossing in dolphin. the original upstream attribution still needs reconstruction; this is not a claim that i originated every module. ai assisted the adaptation and development.

the python source contains a dialogue parser/encoder, conversation state, platform memory readers, model-request integration, and a "feeling chatty" choice-menu injection. this snapshot excludes keys, game files, scraped datasets, and saved conversation/memory data.

the included tests exercise real parser/encoder and state logic while stubbing emulator, model, and gossip i/o. run `python3 tests/run_checks.py` from this directory; the check runner needs only python’s standard library. passing them does not verify dolphin memory writes, screenshots, or model dialogue end-to-end.

the archived requirements document the original environment; dependency modernization and an emulator demonstration remain follow-up work. provide your own supported emulator/game setup and service configuration to explore the prototype. it is not a packaged mod release.

all three recovered assertion checks passed locally on october 12, 2026.

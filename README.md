# critter comforts

> i just want a pet robot lowkey.

a cozy, local-first jarvis project: characters with a home inside the computer, a window into your world, and different ways to help you keep the thread. part virtual companion, part daily-life assistant, part dollhouse operating system.

built in **unity/c#**, with vrm avatars and a roadmap spanning locomotion, eye tracking, ik animation, tts, stt, voice cloning, and an obsidian-backed character hive mind using **ollama, qwen, and hermes**. developed with ai assistance.

## The cast

These are the characters' intended responsibilities, not claims that every listed workflow is complete.

| Character | Their job | Dedicated sprint |
|---|---|---|
| **P.A.** | Personal assistant and companion. Hold unresolved threads, help with to-dos and check-ins, follow up on plans, and support morning startup and wind-down routines. | C01 — Continuity |
| **Chef** | Make eating easier: meal ideas, pantry context, groceries, cooking steps, and useful follow-ups about food. | C02 — Food |
| **Coach** | Help turn movement intentions into manageable exercise, stretching, recovery, and habit check-ins. | C03 — Movement |
| **Librarian** | Organize notes, research, reading, and remembered ideas; retrieve useful context with source links. | C04 — Knowledge |
| **Nurse** | Support user-entered care routines, hydration, reminders, and wellness logs—not diagnosis or medical authority. | C05 — Care |
| **Banker** | Help understand budgets, expenses, and savings goals through read-only summaries—not autonomous financial transactions. | C06 — Money |
| **Secretary** | Organize appointments, calendar context, priorities, and administrative loose ends. Scene identifier: MayorSecretary. | C07 — Planning |
| **Popular Girl / Gossip** | Help with social plans, conversation preparation, relationships, and remembering things the user chose to share. | C08 — Social life |
| **Therapist** | Offer reflection, emotional check-ins, and small grounding steps—not a replacement for professional care. | C09 — Reflection |
| **Tinkerer** | Help with projects, troubleshooting, creative experiments, and making the next build step concrete. | C10 — Making |

Jojo/Stella, Charmy, Spike, and Mae belong to earlier avatar/companion iterations. BMO and Niko are newer asset imports, not completed companions. Each has its own onboarding sprint in the [roadmap](ROADMAP.md); their future duties and redistribution rights must be settled before publication.

## One crew, individual characters

The hive-mind design shares relevant plans, routines, and world context **without flattening everyone into the same personality**.

- **Obsidian:** editable Markdown souls, shared crew context, and user-controlled memory.
- **Character identity:** separate voice, role, memories, and conversational behavior.
- **Ollama:** local model serving, with explicit model selection and endpoint health checks.
- **Hermes:** agent and tool orchestration, kept separate from ordinary dialogue generation; consequential tool actions require scoped permission.
- **Local model pipeline:** private configuration currently uses the alias `qwen3.8-hermes:latest`, referred to here as the Qwen 3.8 profile. The model sprint verifies its actual weights, provider, and capabilities; this repository does not bundle model weights.
- **Grounded initiative:** follow up on real conversation, remembered plans, actual time, known scene state, or tool results.
- **Continuity:** after an absence, ask what happened rather than assuming an old task is still pending.

The full Obsidian, Ollama/Qwen, and Hermes integration is private-development work and a public-port target—not a bundled, verified end-to-end feature of this public snapshot. The public chat core can connect to a configured local OpenAI-compatible endpoint.

## Engineering epics

| Epic | What it brings together |
|---|---|
| E01 — Mind & memory | Ollama/Qwen inference, Hermes orchestration, Obsidian, individual souls, shared context, retrieval, and grounded initiative. |
| E02 — Embodiment | Locomotion, eye tracking, blinking, expressions, living motion, IK, poses, perches, outfits, and camera framing. |
| E03 — Voice & listening | TTS, STT, voice cloning, lip sync, audio-device recovery, and natural multi-message cadence. |
| E04 — Character vertical slices | One dedicated sprint per character, built on common systems while preserving identity and duties. |
| E05 — Daily-life workflows | Routines, food, care, planning, projects, and user-approved integrations. |
| E06 — Trust & delivery | Privacy, permissions, asset rights, regression tests, demonstrations, and honest public documentation. |

The [sprint backlog](ROADMAP.md) breaks these epics into small deliverables with acceptance criteria. Sprint labels are work packages, not dated commitments or completed milestones.

## What is public today?

This is a curated **source edition**, not the latest private environment.

The public baseline contains local AI integration, companion memory, VRM interaction, poses and camera controls, Animalese/optional TTS, and world-space dialogue. The latest selective port adds microphone pre-roll and lifecycle/device handling, transcript filtering, and head-following, camera-relative speech bubbles.

The larger character, Obsidian, Qwen, voice-cloning, and animation roadmap is being ported incrementally. Personal avatars, recordings, conversation logs, workbook data, credentials, private history, and restricted assets are excluded. Import your own VRM and configure your own services.

## Run and verify

1. Open with **Unity 6000.5.4f1** and allow Package Manager to restore dependencies.
2. Open a scene under `Assets/Scenes` and enter Play Mode.
3. Import your VRM and configure the chat, STT, and voice services you use. Local endpoints run separately.

The latest code port compiled in an isolated Unity batch run and passed core pose discovery: **18 poses without private content**. Public runtime microphone quality and visible bubble layout still need verification. Transcript filtering is heuristic and may reject legitimate short utterances. Compilation is not proof that every roadmap feature works.

## Earlier iterations

- [Unity/C# companion systems](Assets/Scripts)
- [Three.js/TypeScript browser world](examples/browser-world)
- [GameCube dialogue prototype](examples/gamecube-dialogue)

[Project evolution case study](https://github.com/lenovo-jojo/portfolio/blob/main/case-studies/companion-evolution.md) · [Portfolio and delivery evidence](https://github.com/lenovo-jojo/portfolio)

An actively developed prototype, with the goal of making useful companionship feel at home.

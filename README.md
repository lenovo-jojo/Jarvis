# critter comforts

> i just want a pet robot lowkey.

a cozy, local-first jarvis project: characters with a home inside the computer, a window into your world, and different ways to help you keep the thread. part virtual companion, part daily-life assistant, part dollhouse operating system.

built in **unity/c#**, with vrm avatars and a roadmap spanning locomotion, eye tracking, ik animation, tts, stt, voice cloning, and an obsidian-backed character hive mind using **ollama, qwen, and hermes**. developed with ai assistance.

## the cast

these are the characters' intended responsibilities, not claims that every listed workflow is complete.

| character | their job | dedicated sprint |
|---|---|---|
| **p.a.** | personal assistant and companion. hold unresolved threads, help with to-dos and check-ins, follow up on plans, and support morning startup and wind-down routines. | c01 — continuity |
| **chef** | make eating easier: meal ideas, pantry context, groceries, cooking steps, and useful follow-ups about food. | c02 — food |
| **coach** | help turn movement intentions into manageable exercise, stretching, recovery, and habit check-ins. | c03 — movement |
| **librarian** | organize notes, research, reading, and remembered ideas; retrieve useful context with source links. | c04 — knowledge |
| **nurse** | support user-entered care routines, hydration, reminders, and wellness logs—not diagnosis or medical authority. | c05 — care |
| **banker** | help understand budgets, expenses, and savings goals through read-only summaries—not autonomous financial transactions. | c06 — money |
| **secretary** | organize appointments, calendar context, priorities, and administrative loose ends. scene identifier: mayorsecretary. | c07 — planning |
| **popular girl / gossip** | help with social plans, conversation preparation, relationships, and remembering things the user chose to share. | c08 — social life |
| **therapist** | offer reflection, emotional check-ins, and small grounding steps—not a replacement for professional care. | c09 — reflection |
| **tinkerer** | help with projects, troubleshooting, creative experiments, and making the next build step concrete. | c10 — making |

jojo/stella, charmy, spike, and mae belong to earlier avatar/companion iterations. bmo and niko are newer asset imports, not completed companions. each has its own onboarding sprint in the [roadmap](ROADMAP.md); their future duties and redistribution rights must be settled before publication.

## one crew, individual characters

the hive-mind design shares relevant plans, routines, and world context **without flattening everyone into the same personality**.

- **obsidian:** editable markdown souls, shared crew context, and user-controlled memory.
- **character identity:** separate voice, role, memories, and conversational behavior.
- **ollama:** local model serving, with explicit model selection and endpoint health checks.
- **hermes:** agent and tool orchestration, kept separate from ordinary dialogue generation; consequential tool actions require scoped permission.
- **local model pipeline:** private configuration currently uses the alias `qwen3.8-hermes:latest`, referred to here as the qwen 3.8 profile. the model sprint verifies its actual weights, provider, and capabilities; this repository does not bundle model weights.
- **grounded initiative:** follow up on real conversation, remembered plans, actual time, known scene state, or tool results.
- **continuity:** after an absence, ask what happened rather than assuming an old task is still pending.

the full obsidian, ollama/qwen, and hermes integration is private-development work and a public-port target—not a bundled, verified end-to-end feature of this public snapshot. the public chat core can connect to a configured local openai-compatible endpoint.

## engineering epics

| epic | what it brings together |
|---|---|
| e01 — mind & memory | ollama/qwen inference, hermes orchestration, obsidian, individual souls, shared context, retrieval, and grounded initiative. |
| e02 — embodiment | locomotion, eye tracking, blinking, expressions, living motion, ik, poses, perches, outfits, and camera framing. |
| e03 — voice & listening | tts, stt, voice cloning, lip sync, audio-device recovery, and natural multi-message cadence. |
| e04 — character vertical slices | one dedicated sprint per character, built on common systems while preserving identity and duties. |
| e05 — daily-life workflows | routines, food, care, planning, projects, and user-approved integrations. |
| e06 — trust & delivery | privacy, permissions, asset rights, regression tests, demonstrations, and honest public documentation. |

the [sprint backlog](ROADMAP.md) breaks these epics into small deliverables with acceptance criteria. sprint labels are work packages, not dated commitments or completed milestones.

## what is public today?

this is a curated **source edition**, not the latest private environment.

the public baseline contains local ai integration, companion memory, vrm interaction, poses and camera controls, animalese/optional tts, and world-space dialogue. the latest selective port adds microphone pre-roll and lifecycle/device handling, transcript filtering, and head-following, camera-relative speech bubbles.

the larger character, obsidian, qwen, voice-cloning, and animation roadmap is being ported incrementally. personal avatars, recordings, conversation logs, workbook data, credentials, private history, and restricted assets are excluded. import your own vrm and configure your own services.

## run and verify

1. open with **unity 6000.5.4f1** and allow package manager to restore dependencies.
2. open a scene under [assets/scenes](Assets/Scenes) and enter play mode.
3. import your vrm and configure the chat, stt, and voice services you use. local endpoints run separately.

the latest code port compiled in an isolated unity batch run and passed core pose discovery: **18 poses without private content**. public runtime microphone quality and visible bubble layout still need verification. transcript filtering is heuristic and may reject legitimate short utterances. compilation is not proof that every roadmap feature works.

## earlier iterations

- [unity/c# companion systems](Assets/Scripts)
- [three.js/typescript browser world](examples/browser-world)
- [gamecube dialogue prototype](examples/gamecube-dialogue)

[project evolution case study](https://github.com/lenovo-jojo/portfolio/blob/main/case-studies/companion-evolution.md) · [portfolio and delivery evidence](https://github.com/lenovo-jojo/portfolio)

an actively developed prototype, with the goal of making useful companionship feel at home.

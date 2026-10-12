# JARVIS / Critter Comforts — Epics & Sprints

A cozy companion world built around dependable shared systems and distinct characters.

This is a **proposed backlog**, not a list of finished features. Sprint IDs describe bounded work packages, not calendar dates. Private prototype work is evidence of exploration; it is not automatically a verified public feature.

## Delivery rules

- Build and verify one small slice at a time.
- Preserve identity, voice, settings, perches, and working animation systems.
- Separate each character's memories from shared crew context.
- Ground proactive dialogue in available context; imaginative play belongs where the conversation calls for it.
- Prefer positive behavioral guidance and inspect the final prompt when identity drifts.
- Keep personal data and restricted assets out of public source.
- Make consequential external actions visible, permissioned, and reversible.
- Test visible and audible behavior; do not equate compilation with completion.

## Epic map

| ID | Epic | Shared foundation sprints | Character delivery |
|---|---|---|---|
| E01 | Mind & memory | F01 model pipeline; F02 Obsidian hive mind | C01–C16 |
| E02 | Embodiment | F03 locomotion/gaze; F04 IK/expression | C01–C16 |
| E03 | Voice & listening | F05 speech pipeline | C01–C16 |
| E04 | Character vertical slices | Shared character contract below | One sprint per character |
| E05 | Daily-life workflows | F06 routines and approved tools | C01–C10 |
| E06 | Trust & delivery | F07 safety and public release | Every sprint |

## Foundation sprints

### F01 — Ollama, Qwen 3.8 & Hermes pipeline

**Epic:** E01. **Outcome:** a measured, configurable local conversation pipeline.

- Document the division of responsibility: Ollama serves local models; Qwen supplies the configured inference profile; Hermes handles agent/tool orchestration; Obsidian supplies editable identity and memory context.
- Verify Ollama endpoint health and installed models; make local service requirements reproducible without bundling weights.
- Keep Hermes tool execution distinct from character dialogue. Expose enabled tools, timeouts, permissions, cancellation, and results; leave external writes opt-in.
- Inspect the private `qwen3.8-hermes:latest` alias and document the actual model weights, provider, context limits, and license. The alias alone is not evidence of an official model release.
- Make the selected model and provider visible; distinguish local inference from optional cloud fallback.
- Measure prompt construction, retrieval, model generation, validation/retry, and first-response latency.
- Keep world context compact and relevant; expose the exact final prompt for diagnosis.
- Give rejected responses a small, specific redirect rather than a growing pile of prohibitions.

**Acceptance:** a reproducible model configuration and timing trace; no silent provider swap; short and long conversations preserve the selected character identity.

### F02 — Obsidian character hive mind

**Epic:** E01. **Depends on:** F01. **Outcome:** shared continuity without shared-personality contamination.

- Use an editable Markdown vault for crew context and separate character souls.
- Separate shared plans/routines from character-specific memory; preserve provenance and timestamps.
- Retrieve only relevant context and treat old plans as context, not commands.
- Let the user inspect, edit, exclude, export, and delete memories.
- Reconcile completed events, changed priorities, and time away before generating follow-ups.
- Keep personal vault contents local; publish only synthetic examples.

**Acceptance:** a synthetic plan can be shared across roles while each keeps its own identity; edits/deletions affect later retrieval; an absence test produces a status question instead of replaying stale instructions.

### F03 — Locomotion & eye tracking

**Epic:** E02. **Outcome:** stable navigation and readable attention.

- Verify forward/backward movement, camera navigation, character locomotion, and navigation recovery.
- Support explicit attention/whistle commands without moving authored perches.
- Use UniVRM look-at integration with bounded conversational gaze offsets.
- Add subtle listening, thinking, and speaking eye motion without near-camera jitter.
- Keep continuous body look-at optional; preserve the stable eyes-only mode.
- Make face, waist-up, and full-body frames follow current poses and perches.

**Acceptance:** navigation never spins on backward input; sitting and standing retain authored positions; gaze and camera framing work at near/far distances without persistent jitter.

### F04 — IK animation, expressions & living motion

**Epic:** E02. **Depends on:** F03. **Outcome:** expressive motion with clear ownership of bones.

- Layer living motion on top of base poses with a documented update order.
- Separate foot/floor, hand/contact, and gesture solvers.
- Use short pose-keyframe tweens where appropriate; limit gestures to their intended limb.
- Preserve hand/prop contact and natural wrist/elbow posture across transitions.
- Blend thinking and speech emotions gently; hold emotions before fading.
- Keep idle mouth closure, blinking, ear motion, hair/garment collision, and iris highlights independently reversible.
- Preserve eyelid occlusion when testing any iris-highlight shader.

**Acceptance:** seated/standing cycles, gestures, and contact tests preserve the root and unrelated limbs; blink, lip sync, gaze, and emotions coexist; no persistent jitter or prop disappearance during a contact phase.

### F05 — TTS, STT, voice cloning & speech cadence

**Epic:** E03. **Outcome:** responsive, intelligible two-way conversation.

- Configure local TTS/STT providers with explicit optional fallbacks.
- Retain speech beginnings through pre-roll and ring-buffer capture.
- Measure transcription reliability; test filtering against legitimate short utterances as well as phantom transcripts.
- Allow per-character voices and consent-based voice cloning using user-supplied recordings.
- Keep clone recordings and embeddings private; provide removal controls.
- Normalize pronunciation for TTS without changing displayed character spelling.
- Test lip sync and Animalese listening reactions alongside speech.
- Support output-device handoffs without pitch drift or losing speech.
- Prefer one bubble when enough; target roughly 8–20 words per message and up to about 60 words per complete response, split at natural boundaries.

**Acceptance:** a recorded test set retains first words; voice changes affect the intended character; device switching survives active speech; every sequenced bubble plays in order; cloning requires consent and private storage.

### F06 — Routines & user-approved tools

**Epic:** E05. **Depends on:** F02. **Outcome:** practical assistance grounded in the user's actual life.

- Represent morning startup, check-in, wind-down, and user-configured recurring routines.
- Use current time, energy, chosen goals, and known completion state.
- Offer done, snooze, not today, easier step, and why-this controls.
- Start calendar, notes, wellness, and finance integrations read-only.
- Require specific permission before calendar edits, messages, purchases, or other consequential external actions.

**Acceptance:** changed intentions override stale plans; reminders can be dismissed; no unsolicited external writes; characters ask when relevant context is missing.

### F07 — Safety, regression & public delivery

**Epic:** E06. **Outcome:** a portfolio edition that is safe and accurately demonstrated.

- Maintain separate public and private repositories; selectively port reviewed changes.
- Check for secrets, personal logs/recordings, private paths, restricted content, and asset licenses.
- Keep external tools opt-in and provide microphone/camera controls.
- Verify compile, core pose discovery, scene playback, audible speech, persistence, and device handoff separately.
- Record short demonstrations with non-sensitive data.
- Label shipped, private-prototype, experimental, and planned work accurately.

**Acceptance:** each publication has an exact file scope, reproducible checks, documented limitations, and no private payload; a demo does not imply untested features are complete.

## Shared character-sprint contract

Each C-series sprint is **one character's vertical slice**, not a fork of the underlying systems. Characters use shared locomotion, eye tracking, IK/expression, TTS/STT, settings, memory, and framing as applicable.

Every character sprint must verify:

1. Correct soul/identity and task scope, with no contamination from another role.
2. Correct scene/perch and stable seated/standing pose.
3. Correct voice and working input/output; clone data is optional and private.
4. Listening, thinking, speaking, and idle reactions.
5. Grounded initiative and a meaningful return-session follow-up.
6. Isolated memory plus relevant, attributed crew context.
7. HUD, outfit, camera-frame, and speech-bubble behavior.
8. A short role-specific test and a recorded demonstration before marking complete.

## Character sprints

All duties below are proposed product responsibilities. Asset presence alone does not satisfy the character contract.

### C01 — P.A.: continuity

**Epics:** E01, E03, E04, E05. **Duty:** personal assistant and companion who keeps the thread.

Track to-dos, unresolved conversations, check-in questions, morning startup, wind-down, and space-care routines. Prioritize useful follow-ups over generic ambient chatter. Support user-configured breaks without judgment or pressure.

**Acceptance:** after time away, P.A. asks about the outcome of the relevant plan; current priorities override old ones; a check-in moves forward without repeating the last message.

### C02 — Chef: food

**Epics:** E03, E04, E05. **Duty:** help the user eat and make food decisions.

Use known pantry/grocery context, preferences, available effort, meal ideas, and cooking steps. Ask useful questions rather than inventing supplies or in-game shops.

**Acceptance:** a low-energy food request produces an achievable next step; an unfinished meal plan gets a relevant follow-up; conversation remains Chef's own voice.

### C03 — Coach: movement

**Epics:** E02, E04, E05. **Duty:** support manageable activity and recovery.

Offer user-chosen exercise, stretching, movement breaks, and habit tracking. Adapt to stated limitations and energy.

**Acceptance:** a movement goal becomes an appropriate small action; demonstrated poses remain stable; the user can rest or decline without a nagging loop.

### C04 — Librarian: knowledge

**Epics:** E01, E04, E05. **Duty:** help organize, retrieve, and understand information.

Support notes, reading lists, research questions, sources, and remembered ideas.

**Acceptance:** retrieve a synthetic saved idea with provenance; distinguish memory from external research; explain when a source is unavailable instead of inventing it.

### C05 — Nurse: care

**Epics:** E01, E04, E05. **Duty:** support user-entered wellness routines.

Track reminders, hydration, appointments, and user-recorded care context. Stay within supportive organization rather than diagnosis or treatment authority.

**Acceptance:** a reminder uses the user's actual schedule and can be snoozed; sensitive notes stay private; clinical uncertainty is made explicit.

### C06 — Banker: money

**Epics:** E01, E04, E05, E06. **Duty:** make financial information easier to understand.

Start with manual imports or narrowly scoped read-only budgets, expenses, and savings goals.

**Acceptance:** summarize a synthetic budget accurately, with provenance; no autonomous trading, transfers, payments, or purchases.

### C07 — Secretary: planning

**Epics:** E01, E04, E05. **Duty:** calendar and administrative continuity.

Organize appointments, priorities, scheduling context, and unfinished administrative tasks. The authored scene currently uses the MayorSecretary identifier; this sprint does not assume a separate Mayor character.

**Acceptance:** identify a scheduling conflict from provided data; propose a next step; request permission before writing an event or sending a message.

### C08 — Popular Girl / Gossip: social life

**Epics:** E01, E04, E05. **Duty:** social preparation and companionship.

Help with plans, conversation starters, relationship context, and things the user chose to remember.

**Acceptance:** use only known interpersonal context; ask about unknown details; avoid fabricated gossip or invented actions by other people.

### C09 — Therapist: reflection

**Epics:** E01, E03, E04, E05. **Duty:** supportive reflection and emotional check-ins.

Offer space to think, a small grounding step, or a useful question. Respect the user's preferred level of support; do not present the character as professional treatment.

**Acceptance:** a difficult moment gets a relevant, brief response rather than a generic greeting; follow-ups reflect actual context; sensitive memory remains user-controlled.

### C10 — Tinkerer: making

**Epics:** E01, E04, E05, E06. **Duty:** projects and troubleshooting.

Track build ideas, decisions, experiments, blockers, and next actions. Separate suggestions from verified tool results.

**Acceptance:** a synthetic project resumes from its real blocker; an experiment has a testable outcome; tool writes require scoped approval.

### C11 — Jojo / Stella: embodied avatar

**Epics:** E02, E04. **Duty:** the earlier self-insert/avatar experience.

Clarify current identity and control ownership; test player movement, camera modes, poses, gaze, and avatar-to-companion boundaries.

**Acceptance:** player input and companion animation do not fight over the same transforms; the avatar's identity is unambiguous.

### C12 — Charmy: companion onboarding

**Epics:** E01, E03, E04. **Duty:** companionship; additional task specialization remains to be chosen.

Revalidate the earlier satellite character against the shared contract without borrowing P.A.'s or Chef's identity.

**Acceptance:** independent soul, voice, memory, attention, and conversation; any specialized duty is explicitly defined before adding task tools.

### C13 — Spike: companion onboarding

**Epics:** E01, E03, E04. **Duty:** companionship; additional task specialization remains to be chosen.

Revalidate calling, arrival, posing, and conversation against the shared contract.

**Acceptance:** arrival does not corrupt position/pose, and memory and voice remain separate from Charmy and other characters.

### C14 — Mae: guest-character slice

**Epics:** E02, E03, E04, E06. **Duty:** guest companionship and dialogue exploration.

Revalidate the earlier non-VRM guest path, walking/resting, and conversation. Document model and character rights before any asset publication.

**Acceptance:** locomotion and dialogue work in a demonstrated slice; unsupported VRM-only features fail gracefully; rights are recorded.

### C15 — BMO: imported-character onboarding

**Epics:** E02, E04, E06. **Duty:** not yet defined; the model is an import, not a finished assistant.

Inspect the GLB rig, animation support, materials, and expressive capabilities; define a role with the user before adding behavior.

**Acceptance:** rights/provenance checked, a stable visible import, a documented rig plan, and an approved duty. No claim of completed conversation capability from import alone.

### C16 — Niko: imported-character onboarding

**Epics:** E02, E04, E06. **Duty:** not yet defined; the model is an import, not a finished assistant.

Inspect the FBX rig, textures, expressions, and locomotion compatibility; define role and voice separately from the source character's intellectual property.

**Acceptance:** rights/provenance checked, a stable visible import, a documented rig plan, and an approved duty.

## Suggested execution order

1. F07 establishes the privacy and regression gates for every subsequent sprint.
2. F01–F02 establish model identity and memory continuity.
3. F03–F05 establish common embodiment and speech; F06 supplies routine context.
4. C01 P.A. and C02 Chef validate the shared character contract.
5. Apply that contract incrementally to C03–C10, then C11–C16 where rig compatibility and rights permit.
6. Publish only verified, reviewed slices with non-sensitive demonstrations.

No character sprint is marked complete here. Completion requires its shared contract and role-specific acceptance test, not merely imported assets or working source compilation.

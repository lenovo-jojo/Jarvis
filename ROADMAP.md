# jarvis / critter comforts — epics & sprints

a cozy companion world built around dependable shared systems and distinct characters.

this is a **proposed backlog**, not a list of finished features. sprint ids describe bounded work packages, not calendar dates. private prototype work is evidence of exploration; it is not automatically a verified public feature.

## delivery rules

- build and verify one small slice at a time.
- preserve identity, voice, settings, perches, and working animation systems.
- separate each character's memories from shared crew context.
- ground proactive dialogue in available context; imaginative play belongs where the conversation calls for it.
- prefer positive behavioral guidance and inspect the final prompt when identity drifts.
- keep personal data and restricted assets out of public source.
- make consequential external actions visible, permissioned, and reversible.
- test visible and audible behavior; do not equate compilation with completion.

## epic map

| id | epic | shared foundation sprints | character delivery |
|---|---|---|---|
| e01 | mind & memory | f01 model pipeline; f02 obsidian hive mind | c01–c16 |
| e02 | embodiment | f03 locomotion/gaze; f04 ik/expression | c01–c16 |
| e03 | voice & listening | f05 speech pipeline | c01–c16 |
| e04 | character vertical slices | shared character contract below | one sprint per character |
| e05 | daily-life workflows | f06 routines and approved tools | c01–c10 |
| e06 | trust & delivery | f07 safety and public release | every sprint |

## foundation sprints

### f01 — ollama, qwen 3.8 & hermes pipeline

**epic:** e01. **outcome:** a measured, configurable local conversation pipeline.

- document the division of responsibility: ollama serves local models; qwen supplies the configured inference profile; hermes handles agent/tool orchestration; obsidian supplies editable identity and memory context.
- verify ollama endpoint health and installed models; make local service requirements reproducible without bundling weights.
- keep hermes tool execution distinct from character dialogue. expose enabled tools, timeouts, permissions, cancellation, and results; leave external writes opt-in.
- inspect the private `qwen3.8-hermes:latest` alias and document the actual model weights, provider, context limits, and license. the alias alone is not evidence of an official model release.
- make the selected model and provider visible; distinguish local inference from optional cloud fallback.
- measure prompt construction, retrieval, model generation, validation/retry, and first-response latency.
- keep world context compact and relevant; expose the exact final prompt for diagnosis.
- give rejected responses a small, specific redirect rather than a growing pile of prohibitions.

**acceptance:** a reproducible model configuration and timing trace; no silent provider swap; short and long conversations preserve the selected character identity.

### f02 — obsidian character hive mind

**epic:** e01. **depends on:** f01. **outcome:** shared continuity without shared-personality contamination.

- use an editable markdown vault for crew context and separate character souls.
- separate shared plans/routines from character-specific memory; preserve provenance and timestamps.
- retrieve only relevant context and treat old plans as context, not commands.
- let the user inspect, edit, exclude, export, and delete memories.
- reconcile completed events, changed priorities, and time away before generating follow-ups.
- keep personal vault contents local; publish only synthetic examples.

**acceptance:** a synthetic plan can be shared across roles while each keeps its own identity; edits/deletions affect later retrieval; an absence test produces a status question instead of replaying stale instructions.

### f03 — locomotion & eye tracking

**epic:** e02. **outcome:** stable navigation and readable attention.

- verify forward/backward movement, camera navigation, character locomotion, and navigation recovery.
- support explicit attention/whistle commands without moving authored perches.
- use univrm look-at integration with bounded conversational gaze offsets.
- add subtle listening, thinking, and speaking eye motion without near-camera jitter.
- keep continuous body look-at optional; preserve the stable eyes-only mode.
- make face, waist-up, and full-body frames follow current poses and perches.

**acceptance:** navigation never spins on backward input; sitting and standing retain authored positions; gaze and camera framing work at near/far distances without persistent jitter.

### f04 — ik animation, expressions & living motion

**epic:** e02. **depends on:** f03. **outcome:** expressive motion with clear ownership of bones.

- layer living motion on top of base poses with a documented update order.
- separate foot/floor, hand/contact, and gesture solvers.
- use short pose-keyframe tweens where appropriate; limit gestures to their intended limb.
- preserve hand/prop contact and natural wrist/elbow posture across transitions.
- blend thinking and speech emotions gently; hold emotions before fading.
- keep idle mouth closure, blinking, ear motion, hair/garment collision, and iris highlights independently reversible.
- preserve eyelid occlusion when testing any iris-highlight shader.

**acceptance:** seated/standing cycles, gestures, and contact tests preserve the root and unrelated limbs; blink, lip sync, gaze, and emotions coexist; no persistent jitter or prop disappearance during a contact phase.

### f05 — tts, stt, voice cloning & speech cadence

**epic:** e03. **outcome:** responsive, intelligible two-way conversation.

- configure local tts/stt providers with explicit optional fallbacks.
- retain speech beginnings through pre-roll and ring-buffer capture.
- measure transcription reliability; test filtering against legitimate short utterances as well as phantom transcripts.
- allow per-character voices and consent-based voice cloning using user-supplied recordings.
- keep clone recordings and embeddings private; provide removal controls.
- normalize pronunciation for tts without changing displayed character spelling.
- test lip sync and animalese listening reactions alongside speech.
- support output-device handoffs without pitch drift or losing speech.
- prefer one bubble when enough; target roughly 8–20 words per message and up to about 60 words per complete response, split at natural boundaries.

**acceptance:** a recorded test set retains first words; voice changes affect the intended character; device switching survives active speech; every sequenced bubble plays in order; cloning requires consent and private storage.

### f06 — routines & user-approved tools

**epic:** e05. **depends on:** f02. **outcome:** practical assistance grounded in the user's actual life.

- represent morning startup, check-in, wind-down, and user-configured recurring routines.
- use current time, energy, chosen goals, and known completion state.
- offer done, snooze, not today, easier step, and why-this controls.
- start calendar, notes, wellness, and finance integrations read-only.
- require specific permission before calendar edits, messages, purchases, or other consequential external actions.

**acceptance:** changed intentions override stale plans; reminders can be dismissed; no unsolicited external writes; characters ask when relevant context is missing.

### f07 — safety, regression & public delivery

**epic:** e06. **outcome:** a portfolio edition that is safe and accurately demonstrated.

- maintain separate public and private repositories; selectively port reviewed changes.
- check for secrets, personal logs/recordings, private paths, restricted content, and asset licenses.
- keep external tools opt-in and provide microphone/camera controls.
- verify compile, core pose discovery, scene playback, audible speech, persistence, and device handoff separately.
- record short demonstrations with non-sensitive data.
- label shipped, private-prototype, experimental, and planned work accurately.

**acceptance:** each publication has an exact file scope, reproducible checks, documented limitations, and no private payload; a demo does not imply untested features are complete.

## shared character-sprint contract

each c-series sprint is **one character's vertical slice**, not a fork of the underlying systems. characters use shared locomotion, eye tracking, ik/expression, tts/stt, settings, memory, and framing as applicable.

every character sprint must verify:

1. correct soul/identity and task scope, with no contamination from another role.
2. correct scene/perch and stable seated/standing pose.
3. correct voice and working input/output; clone data is optional and private.
4. listening, thinking, speaking, and idle reactions.
5. grounded initiative and a meaningful return-session follow-up.
6. isolated memory plus relevant, attributed crew context.
7. hud, outfit, camera-frame, and speech-bubble behavior.
8. a short role-specific test and a recorded demonstration before marking complete.

## character sprints

all duties below are proposed product responsibilities. asset presence alone does not satisfy the character contract.

### c01 — p.a.: continuity

**epics:** e01, e03, e04, e05. **duty:** personal assistant and companion who keeps the thread.

track to-dos, unresolved conversations, check-in questions, morning startup, wind-down, and space-care routines. prioritize useful follow-ups over generic ambient chatter. support user-configured breaks without judgment or pressure.

**acceptance:** after time away, p.a. asks about the outcome of the relevant plan; current priorities override old ones; a check-in moves forward without repeating the last message.

### c02 — chef: food

**epics:** e03, e04, e05. **duty:** help the user eat and make food decisions.

use known pantry/grocery context, preferences, available effort, meal ideas, and cooking steps. ask useful questions rather than inventing supplies or in-game shops.

**acceptance:** a low-energy food request produces an achievable next step; an unfinished meal plan gets a relevant follow-up; conversation remains chef's own voice.

### c03 — coach: movement

**epics:** e02, e04, e05. **duty:** support manageable activity and recovery.

offer user-chosen exercise, stretching, movement breaks, and habit tracking. adapt to stated limitations and energy.

**acceptance:** a movement goal becomes an appropriate small action; demonstrated poses remain stable; the user can rest or decline without a nagging loop.

### c04 — librarian: knowledge

**epics:** e01, e04, e05. **duty:** help organize, retrieve, and understand information.

support notes, reading lists, research questions, sources, and remembered ideas.

**acceptance:** retrieve a synthetic saved idea with provenance; distinguish memory from external research; explain when a source is unavailable instead of inventing it.

### c05 — nurse: care

**epics:** e01, e04, e05. **duty:** support user-entered wellness routines.

track reminders, hydration, appointments, and user-recorded care context. stay within supportive organization rather than diagnosis or treatment authority.

**acceptance:** a reminder uses the user's actual schedule and can be snoozed; sensitive notes stay private; clinical uncertainty is made explicit.

### c06 — banker: money

**epics:** e01, e04, e05, e06. **duty:** make financial information easier to understand.

start with manual imports or narrowly scoped read-only budgets, expenses, and savings goals.

**acceptance:** summarize a synthetic budget accurately, with provenance; no autonomous trading, transfers, payments, or purchases.

### c07 — secretary: planning

**epics:** e01, e04, e05. **duty:** calendar and administrative continuity.

organize appointments, priorities, scheduling context, and unfinished administrative tasks. the authored scene currently uses the mayorsecretary identifier; this sprint does not assume a separate mayor character.

**acceptance:** identify a scheduling conflict from provided data; propose a next step; request permission before writing an event or sending a message.

### c08 — popular girl / gossip: social life

**epics:** e01, e04, e05. **duty:** social preparation and companionship.

help with plans, conversation starters, relationship context, and things the user chose to remember.

**acceptance:** use only known interpersonal context; ask about unknown details; avoid fabricated gossip or invented actions by other people.

### c09 — therapist: reflection

**epics:** e01, e03, e04, e05. **duty:** supportive reflection and emotional check-ins.

offer space to think, a small grounding step, or a useful question. respect the user's preferred level of support; do not present the character as professional treatment.

**acceptance:** a difficult moment gets a relevant, brief response rather than a generic greeting; follow-ups reflect actual context; sensitive memory remains user-controlled.

### c10 — tinkerer: making

**epics:** e01, e04, e05, e06. **duty:** projects and troubleshooting.

track build ideas, decisions, experiments, blockers, and next actions. separate suggestions from verified tool results.

**acceptance:** a synthetic project resumes from its real blocker; an experiment has a testable outcome; tool writes require scoped approval.

### c11 — jojo / stella: embodied avatar

**epics:** e02, e04. **duty:** the earlier self-insert/avatar experience.

clarify current identity and control ownership; test player movement, camera modes, poses, gaze, and avatar-to-companion boundaries.

**acceptance:** player input and companion animation do not fight over the same transforms; the avatar's identity is unambiguous.

### c12 — charmy: companion onboarding

**epics:** e01, e03, e04. **duty:** companionship; additional task specialization remains to be chosen.

revalidate the earlier satellite character against the shared contract without borrowing p.a.'s or chef's identity.

**acceptance:** independent soul, voice, memory, attention, and conversation; any specialized duty is explicitly defined before adding task tools.

### c13 — spike: companion onboarding

**epics:** e01, e03, e04. **duty:** companionship; additional task specialization remains to be chosen.

revalidate calling, arrival, posing, and conversation against the shared contract.

**acceptance:** arrival does not corrupt position/pose, and memory and voice remain separate from charmy and other characters.

### c14 — mae: guest-character slice

**epics:** e02, e03, e04, e06. **duty:** guest companionship and dialogue exploration.

revalidate the earlier non-vrm guest path, walking/resting, and conversation. document model and character rights before any asset publication.

**acceptance:** locomotion and dialogue work in a demonstrated slice; unsupported vrm-only features fail gracefully; rights are recorded.

### c15 — bmo: imported-character onboarding

**epics:** e02, e04, e06. **duty:** not yet defined; the model is an import, not a finished assistant.

inspect the glb rig, animation support, materials, and expressive capabilities; define a role with the user before adding behavior.

**acceptance:** rights/provenance checked, a stable visible import, a documented rig plan, and an approved duty. no claim of completed conversation capability from import alone.

### c16 — niko: imported-character onboarding

**epics:** e02, e04, e06. **duty:** not yet defined; the model is an import, not a finished assistant.

inspect the fbx rig, textures, expressions, and locomotion compatibility; define role and voice separately from the source character's intellectual property.

**acceptance:** rights/provenance checked, a stable visible import, a documented rig plan, and an approved duty.

## suggested execution order

1. f07 establishes the privacy and regression gates for every subsequent sprint.
2. f01–f02 establish model identity and memory continuity.
3. f03–f05 establish common embodiment and speech; f06 supplies routine context.
4. c01 p.a. and c02 chef validate the shared character contract.
5. apply that contract incrementally to c03–c10, then c11–c16 where rig compatibility and rights permit.
6. publish only verified, reviewed slices with non-sensitive demonstrations.

no character sprint is marked complete here. completion requires its shared contract and role-specific acceptance test, not merely imported assets or working source compilation.

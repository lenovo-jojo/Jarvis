# jarvis roadmap

jarvis is being developed as a local-first dollhouse operating system. the
world, characters, assistants, routines, and integrations should share a small
number of dependable foundations instead of becoming disconnected features.

## guiding priorities

1. keep one scene, character loop, chat, movement, and save flow dependable.
2. make every consequential agent action visible, permissioned, and reversible.
3. prefer local services, with clearly labeled cloud fallbacks and usage limits.
4. add real-life assistance before broad world decoration and simulation.
5. keep every character an individual with separate memory and shared context.

## world organization

| place | systems |
|---|---|
| outside world | weather, time, sky, beach, ocean, waves, critters, circus, ferris wheel |
| living room and plaza | character hangouts, phone calls, speech bubbles, chess, gomoku, tic-tac-toe, claw machine |
| office | project rooms, focus mode, work goals, tickets, blockers, and next actions |
| kitchen, bathroom, and bedroom | food, water, medication, bathroom, shower, sleep, stretching, and routines |
| closet | vrm selection, outfits, skin swaps, laundry, and weather-aware planning |
| attic | memory scrapbook, decisions, preferences, recurring problems, and what worked last time |
| control room | models, search, tailscale, skills, permissions, api budgets, history, undo, and emergency shutoff |
| mailroom | email, calendar alerts, packages, bills, messages, and object-based reminders |

## shared foundations

- [ ] action history with undo for every reversible world or agent action
- [ ] safe permissions center with separate read and write approval per skill
- [ ] emergency stop for agents, network calls, microphones, and cameras
- [ ] private mode that prevents new conversation memories from being saved
- [ ] provider hub for local and cloud language, image, voice, and search models
- [ ] provider health checks, fallback order, request limits, and cost visibility
- [ ] shared world state for time, weather, location, routines, and character state
- [ ] common skill manifest describing permissions, network use, status, and undo
- [ ] shared scheduler with quiet hours, snooze, dismissal, and recurring reminders
- [ ] character framework for identity, memory, navigation, speech, activities, and relationships
- [ ] versioned backups for world state, memories, permissions, outfits, and settings
- [ ] accessibility settings for text size, contrast, reduced motion, mute, captions, and key rebinding
- [ ] asset-license registry for imported models, environments, music, and animations

## phase 0: stabilize the current vertical slice

- [ ] finish and verify the visible, scrollable town chat transcript
- [ ] verify mae walking, resting, whistle attention, phone calls, and speech bubbles
- [ ] verify jojo movement, camera modes, minimap, jumping, and fast travel
- [ ] verify character selection without unwanted camera movement
- [ ] add a repeatable save and load test
- [ ] add a small regression checklist for play-mode verification

## phase 1: control room

- [ ] test the configured searxng endpoint and show connection health
- [ ] inspect and configure tailscale for private service access
- [ ] replace free-form model fields with a model selector
- [ ] support local and cloud model profiles with explicit fallback order
- [ ] add an image-generation provider and request limits
- [ ] add a vrm selector with character identity assignment
- [ ] build the permissions center
- [ ] build action history and undo
- [ ] show offline, degraded, local, and cloud states
- [ ] add an autonomy level: manual, suggestive, assistive, or proactive

## phase 2: daily-life loop

- [ ] add a body-needs dashboard for bathroom, water, food, medication, weed, shower, sleep, and stretching
- [ ] add a what now button that returns exactly one next action
- [ ] let what now consider time, calendar, medication, cats, work goals, energy, weather, cost, and urgency
- [ ] add done, snooze, not today, easier action, and why this controls
- [ ] add focus room mode with a timer, tickets, milestones, and side-quest blocking
- [ ] add morning and shutdown routines that adjust the world and queue tasks
- [ ] synchronize real weather and time with the sky and windows
- [ ] add object-based reminders for the cat bowl, medicine cabinet, desk, closet, and mailbox
- [ ] add talk me through it mode with one tiny instruction at a time

## phase 3: life skills

start read-only. enable external changes only after the permissions and history
systems are dependable.

- [ ] calendar reader, planner, birthday reminders, and approved event editing
- [ ] email inbox summaries, drafts, and separately approved sending
- [ ] weather and local alerts
- [ ] news briefings with source links
- [ ] reddit, anime forum, and community summaries
- [ ] alexa or smart-home controls with per-device approval
- [ ] diet tracker
- [ ] pet wellness tracker
- [ ] exercise coach
- [ ] social preparation with people, recent conversations, gifts, and small-talk prompts
- [ ] read-only financial dashboard using manual imports or a narrowly scoped provider

financial skills must never autonomously trade, transfer money, pay bills, or
make purchases.

## phase 4: interactive world

- [ ] add image windows with source metadata and a look-at gizmo
- [ ] add skin swapping for quick character changes
- [ ] add an outfit and closet system tied to plans, weather, and laundry
- [ ] add ambient cats and critters
- [ ] add a beach around the island, an ocean boundary, and waves
- [ ] add chess, gomoku, and tic-tac-toe through one reusable activity system
- [ ] research and prototype a claw-machine mechanic
- [ ] add a circus and ferris wheel
- [ ] add navigation recovery so characters never spawn inside geometry
- [ ] add companion catch-up behavior across fast travel and scene changes

## phase 5: ambient intelligence

- [ ] add a memory scrapbook with search, edit, pin, export, and delete controls
- [ ] save decisions, preferences, project notes, outfit ideas, recurring problems, and what worked last time
- [ ] add an in-world inbox where alerts arrive as physical objects
- [ ] add project rooms with files, status, blockers, and next steps
- [ ] add a weighted decision wheel using energy, cost, time, weather, and urgency
- [ ] add gentle interruption detection for loops, doomscrolling, freezing, and overwork
- [ ] give every interruption quiet, snooze, dismiss, and never ask again controls
- [ ] let characters consider the visible scene, relationships, interests, and available activities
- [ ] let characters suggest places, poses, friendships, items, and world improvements

## implementation notes

- searxng request support exists, but the configured endpoint still needs a
  visible health test.
- the current model setting is a text field, not a validated selector.
- tailscale must be inspected and verified before it is described as configured.
- localhost addresses refer to the client device, so mobile builds require a
  reachable private service address with compatible security and cors settings.
- proactive behavior should remain brief, dismissible, explainable, and bounded
  by quiet hours and the selected autonomy level.
- precise location should be optional; weather may use a manually selected city.
- health features should track and remind without presenting themselves as
  medical authority.

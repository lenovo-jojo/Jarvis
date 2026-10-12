# JARVIS / Critter Comforts

A Unity/C# companion prototype exploring local AI, avatar interaction, camera controls, and character memory. Developed with AI assistance.

**Public source edition:** this repository is a curated prototype snapshot, not the latest dirty local development checkout. Original personal avatars and optional private content are excluded. Supply your own VRM avatar through the character-import controls. No private add-on is needed to inspect or compile the core; optional editor content is discovered under `Assets/OptionalContent` when installed separately.

## Inspect the iterations

- [Unity/C# services](Assets/Scripts): the public core snapshot, with user-supplied avatars.
- [Three.js/TypeScript browser world](examples/browser-world): recovered source, passing typecheck and production build. The publication snapshot includes small type fixes and a compatible dependency patch; it leaves the live project untouched.
- [GameCube dialogue prototype](examples/gamecube-dialogue): recovered Python integration source, with three passing parser/encoder/conversation-state checks using stubbed external I/O.

[Read the evolution case study](https://github.com/lenovo-jojo/portfolio/blob/main/case-studies/companion-evolution.md) · [Portfolio and delivery evidence](https://github.com/lenovo-jojo/portfolio)

**Verification:** the public core imports and compiles in Unity 6000.5.4f1. Batch-mode pose discovery found 18 standard poses without private content and 27 with the optional pose pack installed. These checks do not prove full scene playback or avatar animation; a visible runtime demonstration remains pending. The descriptions below refer to an evolving prototype.

a local-first 3d ai companion world built with unity 6.

jarvis is a shared mind expressed through several individual vrm characters. jojo is stella, the self-insert, while charmy and spike are satellites with their own identities, memory banks, voices, poses, and conversational behavior.

## current scene

the current vertical slice is a picnic scene where the cast can:

- sit together using reusable humanoid poses
- look toward the player, the camera, or whoever is speaking
- speak through character-specific animalese voices and animated mouths
- display optional world-space speech bubbles
- be selected, outlined, posed, and repositioned
- remember character-specific details and shared gossip
- join stella through the phone control

## camera modes

- pov mode places the camera at stella's eye level for embodied movement through the scene
- tv mode provides an orbiting camera for framing and directing the characters
- `r` frames the visible cast at full-body distance in tv mode
- `f` uses a tighter conversational frame or focuses the selected character
- `z` and `c` orbit the tv camera
- `command-z` undoes an accidental character placement

keyboard camera controls are ignored while typing in the chat box.

## interaction

- click a character to select and highlight them
- drag only from the selected character's visible body to reposition them
- press `p` to change the selected character's pose
- click whistle to have the cast face the camera
- use phone to call a satellite to stella
- toggle speech bubbles without changing the chat transcript
- save and load the current scene state from the in-app controls

## requirements

- unity `6000.5.4f1`
- univrm `0.131.2`
- ollama or another openai-compatible chat endpoint
- an optional openai api key for the bounded `gpt-4o-mini` cloud fallback
- an optional speech-to-text endpoint or local whisper setup
- an optional elevenlabs key for full text-to-speech

## setup

1. clone or download this repository.
2. open the project folder in unity hub using unity `6000.5.4f1`.
3. allow unity to restore the project packages.
4. open the sample scene from the project window's scenes folder.
5. enter play mode.
6. open the in-app settings panel and configure the services you want to use.

## default local ai settings

| setting | default |
|---|---|
| llm base url | `http://localhost:11434/v1` |
| chat model | `llava` |
| vision model | `qwen3-vl:4b-instruct` |
| local mode | enabled |
| cloud fallback | `gpt-4o-mini`, three requests per app session |
| local memory | enabled |
| private web search | `http://127.0.0.1:8080` |

the local endpoints must already be running before jarvis can reach them. when local mode is active and the optional cloud key is present, jarvis tries `gpt-4o-mini` only after the local chat request fails. cloud fallback can be disabled in settings. api keys, settings, and scene state are stored in unity's persistent application data directory and should never be committed to the repository.

## main packages

- univrm `0.131.2`
- universal render pipeline `17.5.0`
- input system `1.19.0`
- ai navigation `2.0.13`
- ugui `2.5.0`
- timeline `1.8.12`

## project status

this is an active prototype. the current priority is making the picnic scene, cast behavior, camera controls, memory, posing, and conversation reliable before expanding the world.

## roadmap

the planned dollhouse operating system, control room, daily-life tools, skills,
world activities, and safety foundations are tracked in
[roadmap.md](ROADMAP.md).

# JARVIS / Critter Comforts

A local-first AI companion prototype built with Unity/C#, exploring voice interaction, persistent character memory, expressive VRM avatars, and daily-life assistance. Developed with AI assistance.

## Public source edition

This repository is a curated source snapshot, not the private development environment. Personal avatars, recordings, conversation logs, credentials, and private or purchased content are excluded. Import your own VRM through the character-import controls.

The public core includes character interaction, local AI integration, memory, pose and camera controls, Animalese/optional TTS, and world-space dialogue. Newer private development explores character-specific routines, richer voice and expression systems, living motion, and multi-scene presentation; those systems are being ported incrementally rather than presented as completed public features.

## Latest selective update

- **Microphone capture:** continuous capture with 250 ms of pre-roll retains the start of speech, with wrap-aware sample extraction.
- **Capture lifecycle:** refresh microphone selection after device changes or focus return, and use a cooldown when listening resumes.
- **Transcript filtering:** suppress repeated short transcripts within a bounded window.
- **Local Whisper:** retains the existing optional executable and model settings.
- **Speech bubbles:** follow the evaluated head rather than standing mesh bounds, stay screen-aligned, and adapt their size/position to the camera frame.

These changes are isolated from avatar assets, external-tool permissions, and experimental animation systems.

## Setup

1. Open the project with Unity **6000.5.4f1** and allow Package Manager to restore dependencies.
2. Open a scene under `Assets/Scenes` and enter Play Mode.
3. Import your own VRM and configure the chat, speech-to-text, and voice services you use.

Local endpoints must run separately. Camera, microphone, and cloud services are optional. Keep personal settings and keys outside version control.

## Verification

This smaller update compiled in an independent Unity 6000.5.4f1 batch run and passed core pose discovery: 18 poses without private content. Visible playback, microphone quality, and speech-bubble layout still need runtime verification in the public checkout. Transcript suppression uses heuristics and may reject legitimate short utterances; it is not a guarantee of transcription accuracy. Source checks do not prove the full private experience is reproduced here.

## Project evolution

- [Unity/C# companion systems](Assets/Scripts)
- [Three.js/TypeScript browser world](examples/browser-world)
- [GameCube dialogue prototype](examples/gamecube-dialogue)

[Read the evolution case study](https://github.com/lenovo-jojo/portfolio/blob/main/case-studies/companion-evolution.md) · [Portfolio and delivery evidence](https://github.com/lenovo-jojo/portfolio)

See [ROADMAP.md](ROADMAP.md) for the longer-term direction. This remains an active prototype.

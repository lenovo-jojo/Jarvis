# setup and controls

this guide separates the current development project from the downloadable public source edition. controls below are checked against public source code; this documentation change does not establish fresh play-mode verification.

## current development and public availability

current development source includes character-specific chat configuration, local qwen speech, and hermes tool orchestration. its declared defaults include `dolphin3:latest` for general chat, `qwen3.8-hermes:latest` for jojo, and a local speech endpoint at `http://127.0.0.1:8020`. these are source defaults, not a reading of saved user settings or proof that a service is running. model aliases do not establish the underlying model weights.

the public source edition has not received that complete configuration or integration. its chat default is `llava`; the newer qwen/hermes work remains a selective-port target in the [roadmap](../ROADMAP.md). changing the model name alone would not bring over the missing integration.

## open the public source edition

1. clone or download this repository and open its project folder in unity hub.
2. use unity `6000.5.4f1`, as recorded in the [project version](../ProjectSettings/ProjectVersion.txt).
3. allow package manager to restore dependencies. the [manifest](../Packages/manifest.json) includes univrm `0.131.2`, universal render pipeline `17.5.0`, and input system `1.19.0`.
4. start with the [sample scene](../Assets/Scenes/SampleScene.unity) and enter play mode. personal avatars are excluded; supply a vrm you have permission to use through the settings/import control.
5. configure the services you intend to use in the settings panel. local endpoints must run separately; this repository does not install model weights or start those services.

scene bindings and imported assets affect which interactions are available. this is a source prototype, not a self-contained installer.

## public configuration defaults

these values come from the public [service configuration](../Assets/Scripts/Core/ServiceConfig.cs). previously saved settings may override them.

| setting | declared default |
|---|---|
| chat endpoint | `http://localhost:11434/v1` |
| chat model | `llava` |
| vision model | `qwen3-vl:4b-instruct` |
| local mode | enabled |
| cloud fallback | `gpt-4o-mini`, up to three requests per session |
| local memory | enabled |
| search endpoint | `http://127.0.0.1:8080` |

cloud fallback requires a configured cloud key and can be disabled. speech-to-text and full text-to-speech require their own configuration. settings are saved under unity's persistent application data directory; keep personal configuration and credentials out of git.

## character interaction

these behaviors are implemented in the public vrm 1.0 service. other avatar service implementations do not necessarily support the same controls.

| action | control |
|---|---|
| select a character | click its visible body |
| reposition the selected character | drag from its visible body after selecting it |
| cycle the pose | press `p` outside the chat input |
| undo a placement | press `command-z` outside the chat input |
| ask characters to face the camera | use the whistle control |
| call an available satellite | use the phone control; requires a loaded character |
| show or hide world-space dialogue | use the speech-bubble control |
| save the configured frame/state | use save or `command-s` / `ctrl-s` |
| load the saved frame/state | use load or `command-l` / `ctrl-l` |

frame saving is implemented separately from the browser world's scene-edit endpoint. it should not be described as saving every possible world or editor change.

## camera controls

| action | control |
|---|---|
| reset to full-body framing | `r`; tv mode can frame the visible cast |
| conversational framing | `f` in tv mode |
| switch pov/shoulder view | `v` or `f` in explore mode when a player avatar is available |
| orbit the tv camera | hold `z` or `c` |

camera shortcuts are guarded while typing into a text field. framing depends on the loaded avatars, selected pose, and camera mode.

## what has been checked

- the guide's unity/package versions, defaults, and shortcuts were checked against the current public source.
- current development defaults were read from the separate development checkout without modifying it.
- earlier public-port checks established compilation and discovery of 18 core poses; they did not establish full scene playback, microphone quality, or bubble layout.
- a fresh visual walkthrough remains a verification task. the roadmap records broader integration work separately.

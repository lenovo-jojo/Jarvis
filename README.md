# Jarvis — AI Companion (Unity 6)

A 3D anime AI companion app built with Unity 6. Talk to her via text or voice, she responds with speech and facial expressions, and you can load custom VRM avatars.

---

## Requirements

- **Unity 6000.5.4f1** — install via [Unity Hub](https://unity.com/download)
- **Ollama** (for local AI) — [ollama.com](https://ollama.com) — run `ollama pull dolphin-mistral` after installing
- **ElevenLabs API key** (for voice) — [elevenlabs.io](https://elevenlabs.io) — free tier works

---

## Setup

1. Download or clone this repo
2. Open **Unity Hub** → click **Open** → select the project folder
3. Unity will auto-download all packages on first open (takes a few minutes — normal)
4. Open the scene: `Assets/Scenes/` → double-click the main scene
5. Press **Play**

---

## Configuration (in-app)

Click the **Settings** button in the app to configure:

| Field | Value |
|---|---|
| LLM Base URL | `http://localhost:11434/v1` (Ollama default) |
| LLM Model Name | `dolphin-mistral` (or any model you have pulled) |
| LLM API Key | Leave blank for Ollama; paste OpenAI key for cloud mode |
| ElevenLabs API Key | Your ElevenLabs key |
| ElevenLabs Voice ID | Your chosen voice ID (default is pre-filled) |
| Local Mode | Toggle ON for Ollama, OFF for OpenAI |

Settings are saved automatically to your device.

---

## Features

- 💬 **Text chat** with memory (last 10 messages kept)
- 🎤 **Voice input** — auto-detects when you speak via mic (Whisper API)
- 🔊 **Voice output** — ElevenLabs TTS with lip sync
- 😊 **Facial expressions** — reacts to happy/sad/thinking keywords
- 📷 **Webcam context** — AI can see your camera feed
- 🖥️ **Screen context** — AI can see your screen
- 🧍 **VRM avatar support** — import any VRM 1.0 file
- 🎭 **Pose system** — loads poses from `Assets/OptionalContent/Solo Scenes/`
- 🖱️ **Click and drag** the avatar around the screen

---

## Cloud Mode (OpenAI)

Toggle **Local Mode OFF** in Settings and paste your OpenAI API key. Uses `gpt-4o-mini` with vision.

---

## Package Dependencies (auto-installed by Unity)

- [UniVRM 0.128.1](https://github.com/vrm-c/UniVRM) via OpenUPM
- Universal Render Pipeline (URP)
- Input System
- TextMeshPro

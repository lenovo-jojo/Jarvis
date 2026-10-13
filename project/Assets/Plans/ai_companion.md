# Project Overview
- **Game Title**: Local AI Companion (Vision & VRM)
- **High-Level Concept**: A highly interactive, local-first AI companion app that features a fully animated 3D VRM avatar, voice output via ElevenLabs, and real-time vision context (camera/screen capture) with the ability to switch dynamically between local uncensored models and cloud search engines.
- **Players**: Single player (interactive AI-human companionship)
- **Inspiration / Reference Games**: Otherhalf, Grok Ani, Virtual Streamer companions
- **Tone / Art Direction**: Modern, futuristic, clean UI with a highly customizable 3D anime/stylized character avatar.
- **Target Platform**: Windows, macOS, Android, iOS.
- **Screen Orientation / Resolution**: Landscape (1920x1080) for Desktop, Portrait (1080x1920) for Mobile. Layouts will be responsive to handle both orientations.
- **Render Pipeline**: Universal Render Pipeline (URP) with high-fidelity post-processing.

---

# Architecture Recommendation: Local API Client (Ollama / LM Studio)
The user is unsure whether an *Embedded Engine* or *Local API Client* is better. We strongly recommend a **Local API Client** architecture for the following reasons:
1. **Performance**: Embedding a full 7B or 8B parameter LLM inside Unity on mobile results in huge app footprints (4-8 GB), excessive RAM usage (often causing mobile OS out-of-memory crashes), and heavy battery drain. Connecting to an external local/home server (like Ollama or LM Studio) allows high-speed execution even on low-end devices.
2. **Switching Flexibility**: An OpenAI-compatible API client architecture makes it trivial to switch between local uncensored models (e.g. local Ollama URL `http://localhost:11434/v1`) and cloud-based models with web search (e.g. OpenRouter, OpenAI, Perplexity). It's simply a matter of swapping the base URL and API Key in settings.
3. **Platform Compatibility**: Running native C++ plugins for llama.cpp requires complex per-platform native builds for Windows, macOS (Apple Silicon + Intel), iOS, and Android. An HTTP-based local API client is 100% C# and works instantly across all platforms.

---

# Game Mechanics

## Core Gameplay Loop
1. **Chat & Interact**: The player types or speaks to the 3D companion avatar.
2. **Context-Aware Reasoning**: The player can grant the AI access to their webcam or desktop screen. The AI captures a frame and uses a multimodal model (e.g., LLaVA, Llava-Phi3, or GPT-4o) to "see" and talk about the player's world or what they are doing on their computer/device.
3. **Voice Response**: The AI's text response is sent to ElevenLabs, returning high-fidelity speech.
4. **Dynamic Avatar Animation**: The 3D VRM avatar lip-syncs to the spoken audio and changes facial expressions/animations based on the emotion or context of the conversation.

## Controls and Input Methods
- **Text Input**: Standard input field for typing chat messages.
- **Push-to-Talk / Voice Input**: Direct voice recording and transcription (Whisper or local speech-to-text) to talk directly to the avatar.
- **Context Toggles**: UI buttons to enable/disable camera access and screen capture.
- **File Explorer / Drag-and-Drop**: For importing custom `.vrm` files at runtime.
- **Settings Panel**: Input fields to configure local LLM endpoint (Ollama/LM Studio), API keys (ElevenLabs, OpenRouter, OpenAI), select active models, and choose voice profiles.

---

# UI Design

The UI will be designed with a modern glassmorphic look suitable for both Desktop (Landscape) and Mobile (Portrait).

```
+-------------------------------------------------------------------+
|  [Settings Button]                              [Connection Status]|
|                                                                   |
|             +-----------------------------------------+           |
|             |                                         |           |
|             |          3D VRM AVATAR VIEWPORT         |           |
|             |        (Animated, Lip-Sync, Idle)       |           |
|             |                                         |           |
|             +-----------------------------------------+           |
|                                                                   |
|   +--------------------------+    +---------------------------+   |
|   |  [Camera Feed Preview]   |    |   [Screen Feed Preview]   |   |
|   +--------------------------+    +---------------------------+   |
|                                                                   |
|   +-----------------------------------------------------------+   |
|   | AI: Hello! I can see your screen now. What are you coding?|   |
|   +-----------------------------------------------------------+   |
|                                                                   |
|   +--------------------------------------------------+ +------+   |
|   | [Enter your message here...]                     | | Send |   |
|   +--------------------------------------------------+ +------+   |
|   | [Toggle Cam]  [Toggle Screen]  [Import VRM]      | [Mute] |   |
|   +--------------------------------------------------+ --------+   |
+-------------------------------------------------------------------+
```

---

# Key Asset & Context

We will create a modular codebase with clear interfaces to ensure testability and flexibility:

### 1. Interfaces & Models
- `ILLMService`: Interface for sending chat history + images to the model.
- `IVoiceService`: Interface for text-to-speech with ElevenLabs.
- `IVRMService`: Interface for importing VRM files and controlling expressions/lip-sync.
- `IContextService`: Interface for camera and screen capturing.

### 2. Services Configuration (`ServiceConfig.cs`)
```csharp
[Serializable]
public class ServiceConfig
{
    public string llmBaseUrl = "http://localhost:11434/v1";
    public string llmApiKey = "";
    public string llmModelName = "llava";
    public string elevenLabsApiKey = "";
    public string elevenLabsVoiceId = "21m00Tcm4TlvDq8ikWAM"; // Rachel default
    public bool isLocalMode = true;
}
```

### 3. ElevenLabs Client C# Signature
```csharp
public interface IVoiceService
{
    IEnumerator GenerateSpeechCoroutine(string text, Action<AudioClip> onComplete);
}
```

---

# Implementation Steps

```
+----------------------------------+
| Step 1: Base & Services Architecture |
+-----------------+----------------+
                  |
                  v
+-----------------+----------------+
|      Step 2: UI & UX Layout      |
+-----------------+----------------+
                  |
                  v
+-----------------+----------------+
| Step 3: LLM & Vision Service     |
+-----------------+----------------+
                  |
                  v
+-----------------+----------------+
| Step 4: UniVRM Loader & Control |
+-----------------+----------------+
                  |
                  v
+-----------------+----------------+
| Step 5: ElevenLabs Voice & Sync |
+-----------------+----------------+
                  |
                  v
+-----------------+----------------+
| Step 6: Integration & Final Test |
+----------------------------------+
```

### Step 1: Base Architecture & Configuration
- **Description**: Setup project folder structure (`Scripts/Core`, `Scripts/Services`, `Scripts/UI`, `Scripts/VRM`). Implement Service Locator/Dependency Injection manager to coordinate services, and create global `ServiceConfig` scriptable object or persistent state file.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: No

### Step 2: UI & UX Interface Setup
- **Description**: Build responsive Canvas-based UI featuring Chat Display, Message Input Field, Service Settings Panel (for URLs/API Keys), and previews for camera/screen.
- **Assigned role**: developer
- **Dependencies**: Step 1
- **Parallelizable**: Yes

### Step 3: LLM & Vision Context Service
- **Description**: Implement `OllamaLLMService` supporting local Ollama/LM Studio APIs, and `CloudLLMService` supporting OpenAI/OpenRouter APIs. Create `ContextService` using Unity's `WebCamTexture` for real-world camera feed and `ScreenCapture.CaptureScreenshotAsTexture` / native screen grabber for screen context.
- **Assigned role**: developer
- **Dependencies**: Step 1
- **Parallelizable**: Yes

### Step 4: UniVRM Integration & Avatar Service
- **Description**: Import UniVRM package. Implement `VRMService` to handle runtime loading of `.vrm` files using `RuntimeVRMLoader`. Integrate standard eye-blinking and random idle animations.
- **Assigned role**: developer
- **Dependencies**: Step 1
- **Parallelizable**: Yes

### Step 5: ElevenLabs TTS & Lip-Sync Service
- **Description**: Implement `ElevenLabsVoiceService` sending text requests via `UnityWebRequest` to ElevenLabs API and loading response raw MP3 data into Unity `AudioClip`. Create an audio volume analyzer to drive VRM mouth blendshapes (A, I, U, E, O) for automatic lip-sync.
- **Assigned role**: developer
- **Dependencies**: Step 1 & Step 4
- **Parallelizable**: Yes

### Step 6: System Integration & Polish
- **Description**: Wire all components together in the main controller. If LLM mode is local, configure prompt with camera/screen description. Send response text to ElevenLabs, trigger lipsync, and play voice clip. Add dynamic camera switching and settings saving/loading.
- **Assigned role**: developer
- **Dependencies**: Steps 2, 3, 4, 5
- **Parallelizable**: No

---

# Verification & Testing

### 1. Unit & Integration Tests
- **LLM Connection Test**: Test sending a message to local Ollama (`localhost:11434`) and verifying string responses.
- **ElevenLabs API Test**: Validate sending a small string to ElevenLabs API and successfully playing back the returned AudioClip.
- **VRM Runtime Loader Test**: Verify loading a VRM file from a local disk path, validating that the GameObject is correctly created with eye blinking active.

### 2. Manual Verification Checklist
- Run app on Desktop and load a standard custom `.vrm` avatar.
- Toggle webcam access and verify the camera feed is displayed in the preview panel.
- Ask the AI "What do you see?" and verify the AI describes the screen/camera capture.
- Switch settings from "Local (Ollama)" to "Online (OpenRouter)" and verify seamless switching.
- Verify lipsync animations respond to the voice audio's volume changes.

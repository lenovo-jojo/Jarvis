# project overview
- **game title**: local ai companion (vision & vrm)
- **high-level concept**: a highly interactive, local-first ai companion app that features a fully animated 3d vrm avatar, voice output via elevenlabs, and real-time vision context (camera/screen capture) with the ability to switch dynamically between local uncensored models and cloud search engines.
- **players**: single player (interactive ai-human companionship)
- **inspiration / reference games**: otherhalf, grok ani, virtual streamer companions
- **tone / art direction**: modern, futuristic, clean ui with a highly customizable 3d anime/stylized character avatar.
- **target platform**: windows, macos, android, ios.
- **screen orientation / resolution**: landscape (1920x1080) for desktop, portrait (1080x1920) for mobile. layouts will be responsive to handle both orientations.
- **render pipeline**: universal render pipeline (urp) with high-fidelity post-processing.

---

# architecture recommendation: local api client (ollama / lm studio)
the user is unsure whether an *embedded engine* or *local api client* is better. we strongly recommend a **local api client** architecture for the following reasons:
1. **performance**: embedding a full 7b or 8b parameter llm inside unity on mobile results in huge app footprints (4-8 gb), excessive ram usage (often causing mobile os out-of-memory crashes), and heavy battery drain. connecting to an external local/home server (like ollama or lm studio) allows high-speed execution even on low-end devices.
2. **switching flexibility**: an openai-compatible api client architecture makes it trivial to switch between local uncensored models (e.g. local ollama url `http://localhost:11434/v1`) and cloud-based models with web search (e.g. openrouter, openai, perplexity). it's simply a matter of swapping the base url and api key in settings.
3. **platform compatibility**: running native c++ plugins for llama.cpp requires complex per-platform native builds for windows, macos (apple silicon + intel), ios, and android. an http-based local api client is 100% c# and works instantly across all platforms.

---

# game mechanics

## core gameplay loop
1. **chat & interact**: the player types or speaks to the 3d companion avatar.
2. **context-aware reasoning**: the player can grant the ai access to their webcam or desktop screen. the ai captures a frame and uses a multimodal model (e.g., llava, llava-phi3, or gpt-4o) to "see" and talk about the player's world or what they are doing on their computer/device.
3. **voice response**: the ai's text response is sent to elevenlabs, returning high-fidelity speech.
4. **dynamic avatar animation**: the 3d vrm avatar lip-syncs to the spoken audio and changes facial expressions/animations based on the emotion or context of the conversation.

## controls and input methods
- **text input**: standard input field for typing chat messages.
- **push-to-talk / voice input**: direct voice recording and transcription (whisper or local speech-to-text) to talk directly to the avatar.
- **context toggles**: ui buttons to enable/disable camera access and screen capture.
- **file explorer / drag-and-drop**: for importing custom `.vrm` files at runtime.
- **settings panel**: input fields to configure local llm endpoint (ollama/lm studio), api keys (elevenlabs, openrouter, openai), select active models, and choose voice profiles.

---

# ui design

the ui will be designed with a modern glassmorphic look suitable for both desktop (landscape) and mobile (portrait).

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

# key asset & context

we will create a modular codebase with clear interfaces to ensure testability and flexibility:

### 1. interfaces & models
- `ILLMService`: interface for sending chat history + images to the model.
- `IVoiceService`: interface for text-to-speech with elevenlabs.
- `IVRMService`: interface for importing vrm files and controlling expressions/lip-sync.
- `IContextService`: interface for camera and screen capturing.

### 2. services configuration (`ServiceConfig.cs`)
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

### 3. elevenlabs client c# signature
```csharp
public interface IVoiceService
{
    IEnumerator GenerateSpeechCoroutine(string text, Action<AudioClip> onComplete);
}
```

---

# implementation steps

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

### step 1: base architecture & configuration
- **description**: setup project folder structure (`Scripts/Core`, `Scripts/Services`, `Scripts/UI`, `Scripts/VRM`). implement service locator/dependency injection manager to coordinate services, and create global `ServiceConfig` scriptable object or persistent state file.
- **assigned role**: developer
- **dependencies**: none
- **parallelizable**: no

### step 2: ui & ux interface setup
- **description**: build responsive canvas-based ui featuring chat display, message input field, service settings panel (for urls/api keys), and previews for camera/screen.
- **assigned role**: developer
- **dependencies**: step 1
- **parallelizable**: yes

### step 3: llm & vision context service
- **description**: implement `OllamaLLMService` supporting local ollama/lm studio apis, and `CloudLLMService` supporting openai/openrouter apis. create `ContextService` using unity's `WebCamTexture` for real-world camera feed and `ScreenCapture.CaptureScreenshotAsTexture` / native screen grabber for screen context.
- **assigned role**: developer
- **dependencies**: step 1
- **parallelizable**: yes

### step 4: univrm integration & avatar service
- **description**: import univrm package. implement `VRMService` to handle runtime loading of `.vrm` files using `RuntimeVRMLoader`. integrate standard eye-blinking and random idle animations.
- **assigned role**: developer
- **dependencies**: step 1
- **parallelizable**: yes

### step 5: elevenlabs tts & lip-sync service
- **description**: implement `ElevenLabsVoiceService` sending text requests via `UnityWebRequest` to elevenlabs api and loading response raw mp3 data into unity `AudioClip`. create an audio volume analyzer to drive vrm mouth blendshapes (a, i, u, e, o) for automatic lip-sync.
- **assigned role**: developer
- **dependencies**: step 1 & step 4
- **parallelizable**: yes

### step 6: system integration & polish
- **description**: wire all components together in the main controller. if llm mode is local, configure prompt with camera/screen description. send response text to elevenlabs, trigger lipsync, and play voice clip. add dynamic camera switching and settings saving/loading.
- **assigned role**: developer
- **dependencies**: steps 2, 3, 4, 5
- **parallelizable**: no

---

# verification & testing

### 1. unit & integration tests
- **llm connection test**: test sending a message to local ollama (`localhost:11434`) and verifying string responses.
- **elevenlabs api test**: validate sending a small string to elevenlabs api and successfully playing back the returned audioclip.
- **vrm runtime loader test**: verify loading a vrm file from a local disk path, validating that the gameobject is correctly created with eye blinking active.

### 2. manual verification checklist
- run app on desktop and load a standard custom `.vrm` avatar.
- toggle webcam access and verify the camera feed is displayed in the preview panel.
- ask the ai "what do you see?" and verify the ai describes the screen/camera capture.
- switch settings from "local (ollama)" to "online (openrouter)" and verify seamless switching.
- verify lipsync animations respond to the voice audio's volume changes.

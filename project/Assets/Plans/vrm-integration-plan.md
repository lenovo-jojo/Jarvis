# Project Overview 
- Game Title: Local AI Companion (Vision & VRM)
- High-Level Concept: A highly interactive, local-first AI companion app featuring a fully animated 3D VRM 1.0 avatar, voice output via ElevenLabs, and real-time vision context (camera/screen capture) with dynamic local/cloud LLM models.
- Players: Single player (interactive AI-human companionship)
- Inspiration / Reference Games: Otherhalf, Grok Ani, Virtual Streamer companions
- Tone / Art Direction: Modern, futuristic, clean glassmorphic UI with a highly customizable 3D anime/stylized character avatar.
- Target Platform: macOS, Windows, Android, iOS.
- Screen Orientation / Resolution: Landscape (1920x1080) for Desktop, Portrait (1080x1920) for Mobile.
- Render Pipeline: Universal Render Pipeline (URP).

# Game Mechanics 
## Core Gameplay Loop
1. **Interactive Chat**: Player types or speaks to the 3D companion avatar.
2. **Context-Aware Vision**: AI captures camera/screen frames and speaks about player surroundings or activities.
3. **Voice Synthesis**: Response plays back via high-fidelity ElevenLabs voice.
4. **Dynamic Avatar Animation**: The VRM avatar performs natural procedural eye blinking, looks at the camera, changes facial expressions (Joy, Sorrow, Angry) matching context, and lip-syncs dynamically to sound volume.

## Controls and Input Methods
- **Text & Voice Inputs**: Chat bar, push-to-talk, toggle cam/screen, mute button.
- **Import Custom VRM**: Runtime directory loading or file drag-and-drop of custom `.vrm` files.

# UI
The scene uses a modern overlay Canvas with buttons for toggling context, mute status, and importing custom `.vrm` models.

# Key Asset & Context
- `Assets/Scripts/Services/VRMService.cs`: Manages VRM model importing, eye looking, procedural blinking, dynamic facial expressions, and mouth lipsync.
- `Packages/manifest.json`: Configuration of project package dependencies, including UniVRM packages and local overrides.
- `Packages/com.vrmc.gltf/`: Local overrides of UniGLTF/UniHumanoid to patch the obsolete editor TreeView scripts for Unity 6 compatibility.

# Implementation Steps

### Step 1: Configure manifest.json with Scoped Registries & UniVRM Packages
- **Description**: Add OpenUPM scoped registries and declare the latest stable UniVRM `0.128.1` package.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: No

### Step 2: Establish Local Mutable Copy of `com.vrmc.gltf`
- **Description**: Copy `Library/PackageCache/com.vrmc.gltf@...` folder into `Packages/com.vrmc.gltf/` to override the package as mutable.
- **Assigned role**: developer
- **Dependencies**: Step 1
- **Parallelizable**: No

### Step 3: Patch Unity 6 TreeView Compilation Errors
- **Description**: Comment out or remove the obsolete `MuscleInspectorEditor.cs` script inside `Packages/com.vrmc.gltf/Editor/UniHumanoid/` since this IMGUI TreeView inspector script is obsolete in Unity 6 and not required for runtime VRM importing.
- **Assigned role**: developer
- **Dependencies**: Step 2
- **Parallelizable**: No

### Step 4: Implement Native VRM 1.0 runtime Loader inside VRMService
- **Description**: Rewrite `VRMService.cs` to use the native UniVRM API (`VrmUtility.LoadAsync`, `VRMBlendShapeProxy`, etc.) to import `jdog.vrm` at runtime, set up hair/clothing physics, facial expressions, and accurate lipsync.
- **Assigned role**: developer
- **Dependencies**: Step 3
- **Parallelizable**: No

### Step 5: Test and Verify 
- **Description**: Verify successful compile with no errors and confirm runtime loading of `jdog.vrm`.
- **Assigned role**: developer
- **Dependencies**: Step 4
- **Parallelizable**: No

# Verification & Testing
1. **Compilation Check**: Confirm the Unity 6 Editor compiles successfully with 0 errors.
2. **File Check**: Ensure the custom VRM file is successfully resolved at `/Users/home/Downloads/jdog.vrm` or copied under `Assets/jdog.vrm`.
3. **Runtime Loading**: Enter Play mode, verify that the custom VRM 1.0 avatar loads seamlessly, displays the default textures/materials properly, and has functional lip-sync and blinking.

# project overview 
- game title: local ai companion (vision & vrm)
- high-level concept: a highly interactive, local-first ai companion app featuring a fully animated 3d vrm 1.0 avatar, voice output via elevenlabs, and real-time vision context (camera/screen capture) with dynamic local/cloud llm models.
- players: single player (interactive ai-human companionship)
- inspiration / reference games: otherhalf, grok ani, virtual streamer companions
- tone / art direction: modern, futuristic, clean glassmorphic ui with a highly customizable 3d anime/stylized character avatar.
- target platform: macos, windows, android, ios.
- screen orientation / resolution: landscape (1920x1080) for desktop, portrait (1080x1920) for mobile.
- render pipeline: universal render pipeline (urp).

# game mechanics 
## core gameplay loop
1. **interactive chat**: player types or speaks to the 3d companion avatar.
2. **context-aware vision**: ai captures camera/screen frames and speaks about player surroundings or activities.
3. **voice synthesis**: response plays back via high-fidelity elevenlabs voice.
4. **dynamic avatar animation**: the vrm avatar performs natural procedural eye blinking, looks at the camera, changes facial expressions (joy, sorrow, angry) matching context, and lip-syncs dynamically to sound volume.

## controls and input methods
- **text & voice inputs**: chat bar, push-to-talk, toggle cam/screen, mute button.
- **import custom vrm**: runtime directory loading or file drag-and-drop of custom `.vrm` files.

# ui
the scene uses a modern overlay canvas with buttons for toggling context, mute status, and importing custom `.vrm` models.

# key asset & context
- `Assets/Scripts/Services/VRMService.cs`: manages vrm model importing, eye looking, procedural blinking, dynamic facial expressions, and mouth lipsync.
- `Packages/manifest.json`: configuration of project package dependencies, including univrm packages and local overrides.
- `Packages/com.vrmc.gltf/`: local overrides of unigltf/unihumanoid to patch the obsolete editor treeview scripts for unity 6 compatibility.

# implementation steps

### step 1: configure manifest.json with scoped registries & univrm packages
- **description**: add openupm scoped registries and declare the latest stable univrm `0.128.1` package.
- **assigned role**: developer
- **dependencies**: none
- **parallelizable**: no

### step 2: establish local mutable copy of `com.vrmc.gltf`
- **description**: copy `Library/PackageCache/com.vrmc.gltf@...` folder into `Packages/com.vrmc.gltf/` to override the package as mutable.
- **assigned role**: developer
- **dependencies**: step 1
- **parallelizable**: no

### step 3: patch unity 6 treeview compilation errors
- **description**: comment out or remove the obsolete `MuscleInspectorEditor.cs` script inside `Packages/com.vrmc.gltf/Editor/UniHumanoid/` since this imgui treeview inspector script is obsolete in unity 6 and not required for runtime vrm importing.
- **assigned role**: developer
- **dependencies**: step 2
- **parallelizable**: no

### step 4: implement native vrm 1.0 runtime loader inside vrmservice
- **description**: rewrite `VRMService.cs` to use the native univrm api (`VrmUtility.LoadAsync`, `VRMBlendShapeProxy`, etc.) to import `jdog.vrm` at runtime, set up hair/clothing physics, facial expressions, and accurate lipsync.
- **assigned role**: developer
- **dependencies**: step 3
- **parallelizable**: no

### step 5: test and verify 
- **description**: verify successful compile with no errors and confirm runtime loading of `jdog.vrm`.
- **assigned role**: developer
- **dependencies**: step 4
- **parallelizable**: no

# verification & testing
1. **compilation check**: confirm the unity 6 editor compiles successfully with 0 errors.
2. **file check**: ensure the custom vrm file is successfully resolved at `/Users/home/Downloads/jdog.vrm` or copied under `Assets/jdog.vrm`.
3. **runtime loading**: enter play mode, verify that the custom vrm 1.0 avatar loads seamlessly, displays the default textures/materials properly, and has functional lip-sync and blinking.

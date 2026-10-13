# Project Overview
- Game Title: AI Companion
- High-Level Concept: An AI-driven companion avatar app using VRM models, speech-to-text, LLM, and audio lip-syncing.
- Players: Single player / Companion user
- Inspiration / Reference Games: AI companion apps, VTuber tools
- Tone / Art Direction: Interactive utility
- Target Platform: StandaloneOSX / PC
- Screen Orientation / Resolution: Desktop landscape
- Render Pipeline: URP (PC_RPAsset)

# Game Mechanics
## Core Gameplay Loop
Users interact with a 3D VRM companion avatar that listens to voice or text input, generates responses using an LLM service, speaks back using voice synthesis, analyzes the speech audio for real-time lip-sync, and updates facial expressions / body poses accordingly.
## Controls and Input Methods
- Mouse orbit/zoom controls to inspect and adjust the view of the companion.
- UI toggles and input fields for microphone, screen context, webcam context, and animation posing.

# UI
The existing UI handles messaging and settings toggles. No new UI elements are required for this specific task.

# Key Asset & Context
- `Assets/Scripts/Services/VRMService.cs`: Manages the active VRM avatar and procedural fallback companion. It handles bone caching, custom expressions, lipsync, and procedural animations (idle, sway, breathing, poses) in `LateUpdate()`.

# Implementation Steps
1. **Rotate Character to Face Camera**: 
   - **Description**: Update `VRMService.cs`'s `Update` method to continuously rotate the active companion (`currentAvatarGo`) to face `Camera.main` on the horizontal plane. This ensures the character is always facing the user/camera.
   - **Assigned role**: developer
   - **Dependencies**: None
   - **Parallelizable**: No

2. **Correct Arm Posing in Standing Pose**:
   - **Description**: Modify `VRMService.cs`'s `LateUpdate` method inside the `currentPoseIndex == 0` (Standing) block. Change the left upper arm Z-rotation to `-80f` and the right upper arm Z-rotation to `80f` so that the arms hang downward along the sides of the character rather than sticking outwards/upwards.
   - **Assigned role**: developer
   - **Dependencies**: None
   - **Parallelizable**: No

# Verification & Testing
- Start the Unity scene.
- Verify that the loaded VRM character always rotates smoothly to face the camera even when orbiting around using the mouse drag controls.
- Cycle through the poses and verify that in Pose 0 (Standing), the VRM's arms hang naturally down along its sides.

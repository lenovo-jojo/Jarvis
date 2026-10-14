# project overview
- game title: ai companion
- high-level concept: an ai-driven companion avatar app using vrm models, speech-to-text, llm, and audio lip-syncing.
- players: single player / companion user
- inspiration / reference games: ai companion apps, vtuber tools
- tone / art direction: interactive utility
- target platform: standaloneosx / pc
- screen orientation / resolution: desktop landscape
- render pipeline: urp (pc_rpasset)

# game mechanics
## core gameplay loop
users interact with a 3d vrm companion avatar that listens to voice or text input, generates responses using an llm service, speaks back using voice synthesis, analyzes the speech audio for real-time lip-sync, and updates facial expressions / body poses accordingly.
## controls and input methods
- mouse orbit/zoom controls to inspect and adjust the view of the companion.
- ui toggles and input fields for microphone, screen context, webcam context, and animation posing.

# ui
the existing ui handles messaging and settings toggles. no new ui elements are required for this specific task.

# key asset & context
- `Assets/Scripts/Services/VRMService.cs`: manages the active vrm avatar and procedural fallback companion. it handles bone caching, custom expressions, lipsync, and procedural animations (idle, sway, breathing, poses) in `LateUpdate()`.

# implementation steps
1. **rotate character to face camera**: 
   - **description**: update `VRMService.cs`'s `Update` method to continuously rotate the active companion (`currentAvatarGo`) to face `Camera.main` on the horizontal plane. this ensures the character is always facing the user/camera.
   - **assigned role**: developer
   - **dependencies**: none
   - **parallelizable**: no

2. **correct arm posing in standing pose**:
   - **description**: modify `VRMService.cs`'s `LateUpdate` method inside the `currentPoseIndex == 0` (standing) block. change the left upper arm z-rotation to `-80f` and the right upper arm z-rotation to `80f` so that the arms hang downward along the sides of the character rather than sticking outwards/upwards.
   - **assigned role**: developer
   - **dependencies**: none
   - **parallelizable**: no

# verification & testing
- start the unity scene.
- verify that the loaded vrm character always rotates smoothly to face the camera even when orbiting around using the mouse drag controls.
- cycle through the poses and verify that in pose 0 (standing), the vrm's arms hang naturally down along its sides.

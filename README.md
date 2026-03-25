# Bad Apple — Unity DOTS

Renders any video or live webcam feed as a 3D grid of Unity GameObjects using **Unity DOTS** (Data-Oriented Technology Stack). Each pixel of the video frame is mapped to a GameObject chosen by brightness — creating a real-time Bad Apple effect with thousands of entities.

🎬 **[Watch on YouTube](https://www.youtube.com/watch?v=drZ75RWCZ-M)**

## How it works

Each frame, the script:
1. Reads the current video frame (or webcam feed) as a `Color32` array
2. Converts each pixel to grayscale
3. Maps the grayscale value to one of the pixel prefabs (darkest → brightest)
4. Spawns DOTS entities for each visible pixel via `EntityManager`
5. Destroys all entities from the previous frame

The heavy per-pixel work runs in a **Burst-compiled parallel job** (`RenderJob`) for performance, with entity spawning handled in a second job (`SetPixelPosJob`).

## Features

- Video file playback or live webcam input
- Configurable screen resolution and pixel grid spacing
- Adjustable playback speed
- Skip darkest pixel option (renders only non-black pixels)
- Invert pixel brightness
- Apple count displayed on screen (HUD)
- Inspector buttons: Play, Stop, Start Webcam, Refresh Webcams

## Requirements

> ⚠️ **Unity 2020.3.30f1 only** — uses legacy DOTS APIs (`GameObjectConversionUtility`, `IJobParallelFor` with `EntityManager`) that are not compatible with newer Unity/DOTS versions.

**Required Unity packages:**
- `com.unity.entities`
- `com.unity.jobs`
- `com.unity.burst`
- `com.unity.collections`
- `com.unity.mathematics`
- Universal Render Pipeline (URP)

## Setup

1. Open the project in **Unity 2020.3.30f1**
2. Open the scene: `Assets/Scenes/DotsBadApple.unity`
3. Select the `DotsBadApple` GameObject in the Hierarchy
4. In the Inspector, configure:

| Field | Description |
|-------|-------------|
| `Pixels` | Array of prefabs ordered **darkest to brightest** |
| `Screen Size` | Resolution to sample the video at (e.g. 64×48) |
| `Aspect Ratio` | Spacing between pixels in world units |
| `Video Clip` | Assign `badapple.mp4` or any video file |
| `Skip Darkest Pixel` | Skip spawning entities for the darkest shade |
| `Inverse Pixels` | Invert brightness mapping |
| `Use Jobs` | Enable Burst-compiled parallel processing |
| `Batch Pixels` | Batch size for the parallel job |

5. Press **Play** in the Editor, then click **Play Video** in the Inspector — or press **Start Webcam** for live input.

## Project Structure

```
Assets/
├── Scripts/
│   └── DotsBadApple.cs       # Main script + Jobs + Editor extension
├── Prefabs/Pixels/
│   └── Pixel1–15.prefab      # 15 pixel prefabs (darkest to brightest)
├── 3D Objects/
│   ├── Materials/            # 15 materials (mat 1–15)
│   └── Mesh/                 # Apple mesh (ElmaLow.fbx) + textures
├── Videos/
│   └── badapple.mp4          # Bad Apple video
├── Scenes/
│   └── DotsBadApple.unity    # Main scene
└── URP/                      # Universal Render Pipeline config
```

## Third-party assets

- **LiteFPSCounter** by OmniSAR Technologies — in-scene FPS display

## Credits

- Bad Apple!! — original video by Alstroemeria Records
- Apple mesh: included in `Assets/3D Objects/Mesh/`

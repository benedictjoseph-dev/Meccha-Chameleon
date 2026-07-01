# Chameleon Color & Painting System 🎨

This system provides a highly optimized, zero-allocation texture painting mechanic specifically designed for animated characters (SkinnedMeshRenderers). It allows players to sample colors from the environment and seamlessly paint them onto their character model using a brush.

## Required Files
To migrate or share this system, you only need **two** C# scripts located in the `ColorSystem` folder:

1. **`ColorSamplerController.cs`** - Handles input (Q key) and raycasting against the environment to sample base colors or exact texture pixels.
2. **`PlayerTexturePainter.cs`** - Handles the brush logic, UV projection, and real-time texture modification on the player character.

---

## How It Works (Technical Breakdown)

### 1. Color Sampling (`ColorSamplerController.cs`)
When the player holds **Q**, the system shoots a Raycast from the Camera through the mouse cursor. 
- It first checks if the hit object has a `_BaseColor` or `_Color` property on its material.
- If the object uses a texture, the system mathematically pulls the exact pixel color from the `Texture2D` using UV coordinates. 
- When the player Left-Clicks, it saves that color into a public `LockedColor` variable.

### 2. Animated Mesh Collision (`PlayerTexturePainter.cs`)
Normally in Unity, you cannot get UV coordinates from a raycast hitting a moving `SkinnedMeshRenderer`. 
- To solve this, the script creates a single invisible `MeshCollider` and updates it every frame using `SkinnedMeshRenderer.BakeMesh()`.
- It re-uses the exact same Mesh object in memory, preventing RAM overflow and GC spikes.

### 3. Zero-Allocation Painting (`PlayerTexturePainter.cs`)
The biggest issue with texture painting in Unity is lag caused by copying massive `Color[]` arrays every frame.
- **The Solution:** On start, the script clones your player's texture into a raw `RGBA32` format. It then grabs a `NativeArray<Color32>` which acts as a **direct memory pointer** to the GPU texture.
- When the player drags their mouse, the brush math (calculating the circular radius and soft falloff) modifies the bytes in memory directly without creating *any* Garbage Collection allocations.
- A single `Texture2D.Apply()` is called to sync the frame to the GPU.

---

## Setup Instructions

If you are setting this up in a new scene or project, follow these exact steps:

### Part 1: Player Setup
1. Attach **both** scripts (`ColorSamplerController` and `PlayerTexturePainter`) to your Player GameObject (or a central Manager object).
2. **SkinnedMeshRenderer**: Drag your character's mesh into the `Skinned Mesh Renderer` slot on the Painter script.
3. **Important**: DO NOT assign your Player into the "Player Renderers" array on the `ColorSamplerController`. Leave that array empty so it doesn't accidentally tint your whole material!
4. **Texture Settings**: Find your character's texture image in your Project window. In the Inspector, check **"Read/Write Enabled"** and click Apply. *(If your character doesn't have a texture, the script will automatically generate a blank white canvas for you!)*

### Part 2: Environment Setup
1. Any object in the world that you want to sample colors from **must have a Collider**.
2. If you want to sample exact pixel colors from an object's texture (like wood grain), the object **must use a MeshCollider**. Box and Sphere colliders do not contain the UV data required for pixel sampling.
3. Just like the player, any environment texture you want to sample from must have **Read/Write Enabled** checked in its import settings.

### Part 3: UI & Brush Indicator (Optional but Recommended)
1. **Sample UI**: Create a simple UI Image on your Canvas (e.g., a crosshair or corner swatch). Drag it into the `Color Sampling UI` slot on the Sampler script. It will automatically show the color you are pointing at.
2. **Brush Indicator**: Create a 3D Sphere in your scene. **Remove its Collider**, give it a glowing transparent material, and drag it into the `Brush Indicator` slot on the Painter script. It will project onto your mesh and scale up/down when you press **Numpad + / -**.

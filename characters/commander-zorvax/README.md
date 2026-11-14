# Commander Zorvax

- `commander-zorvax.blend` is the editable Blender 4.5 source. It includes the mesh, materials, 26-bone FK rig, five actions, and the four packed turnaround guides in the hidden **Turnaround references** collection.
- `../../public/assets/characters/commander-zorvax.glb` is the self-contained game export. It has its origin at the feet, uses glTF's Y-up coordinates, and faces +Z. It is about 2.90 scene units tall at scale 1.
- `turnaround/` contains the aligned front, back, and side reference PNGs.

## Preview and animate

In Blender, select `Commander_Zorvax`, open the Dope Sheet in **Action Editor** mode, and select one of the saved actions. Use Pose Mode to key new FK motion. The separate cape bones let you adjust cloth motion by hand.

| Clip | Use |
| --- | --- |
| `Idle` | Looping head, chest, and cape motion |
| `Nod` | One-shot acknowledgment |
| `Welcome` | One-shot greeting with the right arm |
| `Angry` | 2.63-second outburst for an action Zorvax objects to. He scowls, shouts, and raises a fist. |
| `Complain` | 4.70-second reaction to disappointing results. He raises a brow, grumbles, and gestures with both palms. |

The `head` bone's custom properties drive four face shapes. In Pose Mode, use `Scowl`, `Shout`, `Disbelief`, and `Grumble` to key expressions. Both reactions include facial motion and return to the relaxed pose. The mouth movement is silent and works with optional dialogue or subtitles. The rig also covers body, hand, finger, and cape movement.

## Load it in the game

The repository already includes Three.js. Load the bundled asset and update its mixer in the scene's frame loop.

```js
import { AnimationMixer } from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';

const { scene: commander, animations } = await new GLTFLoader()
  .loadAsync('/assets/characters/commander-zorvax.glb');

const mixer = new AnimationMixer(commander);
mixer.clipAction(animations.find((clip) => clip.name === 'Idle')).play();
scene.add(commander);

// In the frame loop, with deltaSeconds expressed in seconds:
mixer.update(deltaSeconds);
```

In React Three Fiber, use `useLoader(GLTFLoader, url)` and advance the mixer with `useFrame`. Apply the character scale on its parent group and light the asset with the game scene's own lights.

Use `LoopOnce` for either reaction. Start it from the relevant game event and return to `Idle` when the mixer emits `finished` for that action.

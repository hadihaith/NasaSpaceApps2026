# Alien Student

`alien-student.blend` contains the editable mesh, materials, a 25-bone FK rig, and the original sprite sheet packed into the **Reference art** collection. Select `Alien_Student` in the Action Editor to preview the clips. In Pose Mode, the rig includes separate bones for hands and digits.

Use `/assets/characters/alien-student.glb` with Three.js `GLTFLoader`. The GLB uses +Y up and faces +Z. It is 2.35 scene units tall at scale 1, with its origin at the feet.

| Clip | Playback |
| --- | --- |
| `Idle` | Loop for the standing NPC |
| `Walk` | In-place loop. Move the parent through the scene while it plays. |
| `Wave` | One-shot greeting |

The facial expression is fixed. The backpack moves with the torso.

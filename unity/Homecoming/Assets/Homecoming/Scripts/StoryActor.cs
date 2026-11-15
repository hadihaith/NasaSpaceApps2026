using System;
using System.Collections.Generic;
using UnityEngine;

namespace Homecoming
{
    public sealed class StoryActor : MonoBehaviour
    {
        public GameObject rig;
        public AnimationClip[] clips;
        public int role;
        Transform head, chest, leftArm, leftForearm, leftHand, rightArm, rightForearm, rightHand;
        Transform[] bones;
        Vector3[] restPositions, restScales, idlePositions, idleScales;
        Quaternion[] restRotations, idleRotations;
        readonly Dictionary<string, AnimationClip> actions = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);
        SkinnedMeshRenderer[] faces;
        float[][] restMorphs, idleMorphs;
        bool initialized;
        public Transform ReportGrip => rightHand;

        public void Initialize()
        {
            if (initialized) return;
            foreach (var animator in rig.GetComponentsInChildren<Animator>()) animator.enabled = false;
            foreach (var animation in rig.GetComponentsInChildren<Animation>()) animation.enabled = false;
            bones = rig.GetComponentsInChildren<Transform>();
            int count = bones.Length;
            restPositions = new Vector3[count]; restRotations = new Quaternion[count]; restScales = new Vector3[count];
            idlePositions = new Vector3[count]; idleRotations = new Quaternion[count]; idleScales = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                var bone = bones[i];
                restPositions[i] = bone.localPosition; restRotations[i] = bone.localRotation; restScales[i] = bone.localScale;
                switch (bone.name)
                {
                    case "head": head = bone; break;
                    case "chest": case "spine": chest = bone; break;
                    case "upper_arm.L": leftArm = bone; break;
                    case "forearm.L": leftForearm = bone; break;
                    case "hand.L": leftHand = bone; break;
                    case "upper_arm.R": rightArm = bone; break;
                    case "forearm.R": rightForearm = bone; break;
                    case "hand.R": rightHand = bone; break;
                }
            }
            foreach (var clip in clips) actions[clip.name] = clip;
            faces = rig.GetComponentsInChildren<SkinnedMeshRenderer>();
            restMorphs = new float[faces.Length][];
            idleMorphs = new float[faces.Length][];
            for (int i = 0; i < faces.Length; i++)
            {
                restMorphs[i] = new float[faces[i].sharedMesh.blendShapeCount];
                idleMorphs[i] = new float[restMorphs[i].Length];
                for (int j = 0; j < restMorphs[i].Length; j++) restMorphs[i][j] = faces[i].GetBlendShapeWeight(j);
            }
            initialized = true;
        }
        AnimationClip Clip(string name)
        {
            if (actions.TryGetValue(name, out var clip)) return clip;
            foreach (var candidate in clips)
                if (candidate.name.EndsWith("_" + name, StringComparison.OrdinalIgnoreCase)) return candidate;
            return null;
        }
        static float Smooth(float from, float to, float t) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(from, to, t));
        static float Beat(float t, float at, float width) => Mathf.Exp(-Mathf.Pow((t - at) / width, 2));
        static float Hash(int i, int n) => Mathf.Repeat(Mathf.Sin((i + 1) * 12.9898f + n * 78.233f) * 43758.5453f, 1);
        static float Envelope(float t, float start, float end) => Smooth(start, start + .22f, t) * (1 - Smooth(end - .32f, end, t));

        // Every pose is evaluated from time, so seeking, pause and replay agree exactly.
        // Blend back through the breathing idle instead of freezing on the last action key.
        void Sample(string actionName, float t, float start, float end, float rate = 1, bool loop = false, float hold = 0)
        {
            for (int i = 0; i < faces.Length; i++)
                for (int j = 0; j < restMorphs[i].Length; j++) faces[i].SetBlendShapeWeight(j, restMorphs[i][j]);
            for (int i = 0; i < bones.Length; i++)
            {
                bones[i].localPosition = restPositions[i]; bones[i].localRotation = restRotations[i]; bones[i].localScale = restScales[i];
            }
            var idle = Clip("Idle");
            if (idle != null) idle.SampleAnimation(rig, Mathf.Repeat(t * .85f + role * .47f, idle.length));
            var action = Clip(actionName);
            if (actionName == "Idle" || action == null) return;
            float weight = Envelope(t, start, end);
            if (weight <= 0) return;
            for (int i = 0; i < bones.Length; i++)
            {
                idlePositions[i] = bones[i].localPosition; idleRotations[i] = bones[i].localRotation; idleScales[i] = bones[i].localScale;
            }
            for (int i = 0; i < faces.Length; i++)
                for (int j = 0; j < idleMorphs[i].Length; j++) idleMorphs[i][j] = faces[i].GetBlendShapeWeight(j);
            float age = Mathf.Max(0, t - start) * rate;
            if (hold > 0)
            {
                float peak = action.length * .5f;
                age = age < peak ? age : age < peak + hold ? peak : age - hold;
            }
            float time = loop ? Mathf.Repeat(age, action.length) : Mathf.Min(age, action.length - .001f);
            action.SampleAnimation(rig, time);
            for (int i = 0; i < bones.Length; i++)
            {
                bones[i].localPosition = Vector3.Lerp(idlePositions[i], bones[i].localPosition, weight);
                bones[i].localRotation = Quaternion.Slerp(idleRotations[i], bones[i].localRotation, weight);
                bones[i].localScale = Vector3.Lerp(idleScales[i], bones[i].localScale, weight);
            }
            for (int i = 0; i < faces.Length; i++)
                for (int j = 0; j < idleMorphs[i].Length; j++)
                    faces[i].SetBlendShapeWeight(j, Mathf.Lerp(idleMorphs[i][j], faces[i].GetBlendShapeWeight(j), weight));
        }
        static void Turn(Transform bone, Vector3 worldAxis, float degrees)
        {
            if (bone != null) bone.rotation = Quaternion.AngleAxis(degrees, worldAxis) * bone.rotation;
        }
        void Face(string expression, float strength)
        {
            foreach (var renderer in faces)
            {
                if (renderer.sharedMesh == null) continue;
                int index = renderer.sharedMesh.GetBlendShapeIndex(expression);
                if (index < 0) continue;
                // glTFast imports full-strength frames at 1, while other Unity meshes use 100.
                // Use the mesh's authored range: a hard-coded percentage explodes the GPU-skinned face.
                var mesh = renderer.sharedMesh;
                float fullWeight = mesh.GetBlendShapeFrameWeight(index, mesh.GetBlendShapeFrameCount(index) - 1);
                float amount = Mathf.Clamp01(strength) * fullWeight;
                renderer.SetBlendShapeWeight(index, Mathf.Max(renderer.GetBlendShapeWeight(index), amount));
            }
        }
        void Reach(Transform upper, Transform lower, Transform hand, Vector3 target, float weight)
        {
            if (upper == null || lower == null || hand == null || weight <= 0) return;
            for (int step = 0; step < 5; step++)
            {
                RotateToward(lower, hand, target, weight * .55f);
                RotateToward(upper, hand, target, weight * .55f);
            }
        }
        static void RotateToward(Transform joint, Transform hand, Vector3 target, float weight)
        {
            Vector3 from = hand.position - joint.position, to = target - joint.position;
            if (from.sqrMagnitude < .00001f || to.sqrMagnitude < .00001f) return;
            joint.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(from, to), weight) * joint.rotation;
        }
        public void Pose(string shot, float t, bool still)
        {
            if (!initialized) Initialize();
            bool visible = shot == "home" || (shot == "recruit" && role == 0) || (shot == "ready" && role > 0) || shot == "epilogue" ||
                (shot == "jam" && role == 2) || shot == "reveal" || shot == "party" || shot == "years" || shot == "return" ||
                shot == "hearing" || shot == "evidence" || shot == "reaction" ||
                (shot == "correction" && role < 2) || (shot == "launch" && role == 0) || (shot == "lab" && role > 0) ||
                CutsceneScript.IsCutsceneShot(shot);
            gameObject.SetActive(visible);
            if (!visible) return;
            string action = "Idle";
            float start = .1f, end = 8, rate = 1, hold = 0;
            bool loop = false;
            switch (shot)
            {
                case "reveal": if (role == 0) { action = "Welcome"; start = 1.04f; end = 4.2f; hold = .5f; } break;
                case "correction":
                    // Nyxen's finger goes up; Zorvax answers "The first of many!" with open arms.
                    action = role == 1 ? "Eureka" : "Welcome";
                    start = role == 1 ? .2f : 1.55f; end = role == 1 ? 3.3f : 3.6f; hold = role == 1 ? .6f : .5f; break;
                case "party": action = role == 0 ? "Welcome" : role == 1 ? "Explain" : "Idle"; start = .72f + role * .2f; end = 3.8f; loop = false; rate = 1.05f; break;
                case "return": action = role == 0 ? "Complain" : "Idle"; start = 1.12f; end = 4.65f; rate = 1.35f; break;
                case "launch": action = "Nod"; start = .08f; end = 2.1f; rate = .8f; break;
                case "lab": if (role == 1) { action = "Explain"; start = .35f; end = 6.5f; rate = .77f; hold = .9f; } break;
                case "hearing":
                    if (role == 0) { action = "Welcome"; start = .08f; end = 5.1f; hold = 2; rate = .9f; }
                    if (role == 2) { action = "Walk"; start = 3.6f; end = 5.1f; loop = true; }
                    break;
                case "evidence":
                    if (role == 0) { action = "Complain"; start = 4.7f; end = 7.5f; }
                    if (role == 1) { action = "Explain"; start = 2.4f; end = 8.4f; hold = 1.1f; }
                    if (role == 2) { action = "Walk"; start = .03f; end = 1.7f; loop = true; }
                    break;
                case "reaction":
                    if (role == 0) { action = "Nod"; start = 6.8f; end = 9.6f; rate = .85f; }
                    if (role == 2) { action = "Walk"; start = .1f; end = 1.25f; loop = true; }
                    break;
                case "recruit": if (t < 3.4f) { action = "Welcome"; start = .1f; end = 3.5f; hold = .6f; } break;
                // Cutscene 1, The Welcome Baskets: only Ibrahim's authored clips are used.
                case "drought-alert": if (role == 1) { action = "Explain"; start = .3f; end = 2.8f; rate = .9f; } break;
                case "drought-welcome": if (role == 0) { action = "Welcome"; start = .15f; end = 2.8f; hold = .7f; } break;
                case "drought-count": if (role == 2) { action = "Wave"; start = .1f; end = 1.95f; rate = 1.2f; } break;
                case "drought-none": if (role == 0) { action = "Nod"; start = .1f; end = 1.5f; } break;
                case "drought-all": if (role == 0) { action = "Complain"; start = .05f; end = 3.5f; rate = 1.2f; } break;
            }
            Sample(action, still ? (t < start ? 0 : start + .64f) : t, start, end, rate, loop, hold);
            float x = role == 0 ? -1.5f : role == 1 ? .35f : 2.1f;
            float z = role == 0 ? .85f : role == 1 ? .12f : -.18f;
            float yaw = role == 0 ? -8 : role == 1 ? -13 : -23;
            float y = 0, roll = 0, headYaw = 0, headPitch = 0, torsoPitch = 0;
            float leftArmTilt = 0, rightArmTilt = 0, torsoRoll = 0;
            float idleSway = still ? 0 : Mathf.Sin(t * 1.2f + role * 2) * .013f;
            x += idleSway;
            if (shot == "home")
            {
                headYaw = still ? 0 : Mathf.Sin(t * .45f + role) * 5;
                if (role == 2) { yaw = -32; headPitch = -3; }
            }
            if (shot == "reveal")
            {
                float take = Smooth(2.65f + role * .17f, 3.44f + role * .17f, t);
                headYaw = role == 2 ? -26 * Smooth(2.06f, 2.7f, t) : 29 * (1 - take) - 25 * take;
            }
            if (shot == "reveal" && role == 0)
            {
                float anticipate = Beat(t, 1.18f, .2f), surge = Smooth(1.22f, 1.65f, t);
                z += .32f * surge - .05f * anticipate;
                y += still ? 0 : Beat(t, 1.58f, .19f) * .11f;
                x -= .16f * surge;
                torsoPitch = -6 * anticipate + 9 * surge;
                headPitch = -7 * surge;
            }
            if (shot == "correction")
            {
                // Zorvax glances toward the cannon, then turns back to declare it.
                headYaw = role == 1 ? -22 : 18 * Smooth(.7f, 1.1f, t) * (1 - Smooth(1.45f, 1.85f, t));
                torsoPitch = role == 1 ? -3 : 4 - 7 * Smooth(1.55f, 1.95f, t);
                if (role == 1) leftArmTilt = -7 * Envelope(t, .4f, 3.3f);
            }
            if (shot == "party")
            {
                float dance = still || role == 2 ? 0 : Smooth(.72f + role * .22f, .94f + role * .22f, t) * (1 - Smooth(2.9f, 3.85f, t));
                float beat = Mathf.Sin((t - .72f) * 7.8f + role * 1.85f);
                float hop = Mathf.Max(0, beat);
                x += Mathf.Sin(t * 4 + role) * .085f * dance;
                y += hop * hop * (role == 2 ? .17f : .09f) * dance;
                roll = beat * (role == 1 ? 2.5f : 4) * dance;
                yaw += Mathf.Sin(t * 3.9f + role) * 7 * dance;
                headPitch = beat * 3 * dance;
                torsoRoll = -beat * 3 * dance;
                rightArmTilt = beat * 7 * dance;
            }
            if ((shot == "party" || shot == "jam") && role == 2)
            {
                x -= .24f; z += .22f; yaw -= 12;
                headPitch = shot == "jam" ? 11 - 5 * Smooth(1.13f, 1.42f, t) : 13 * Smooth(.2f, .56f, t);
                if (shot == "jam") headYaw -= 20 * Smooth(1.22f, 1.6f, t);
            }
            if (shot == "years")
            {
                float lapse = StoryStage.Lapse(t, still);
                y = -.06f * lapse; torsoPitch = (role == 0 ? 8 : 5) * lapse; headPitch = (role == 2 ? 16 : 11) * lapse;
                roll = (role == 2 ? -5 : 2) * lapse; headYaw = (role == 0 ? -9 : 5) * lapse;
                rightArmTilt = (role == 0 ? -12 : -5) * lapse;
                // "Should I take the banner down?": the cadet looks up at it, then back down.
                if (role == 2) headPitch = 16 * lapse - 30 * Smooth(2.9f, 3.4f, t) * (1 - Smooth(4.5f, 5.1f, t));
                // "Not until they get here.": Zorvax straightens, chin up.
                if (role == 0)
                {
                    float resolve = Smooth(4.5f, 5f, t);
                    torsoPitch = 8 * lapse - 9 * resolve; headPitch = 11 * lapse - 15 * resolve; rightArmTilt = -12 * lapse + 8 * resolve;
                }
            }
            if (shot == "return")
            {
                float notice = Smooth(.24f + role * .12f, .85f + role * .12f, t);
                // Zorvax turns only three-quarters toward the broadcast; his head does the rest.
                // Side-on, his flat cape is seen edge-on and reads as a blade cutting through him.
                float protest = role == 0 ? Smooth(1.4f, 2.45f, t) : .3f * Smooth(2, 3, t);
                yaw += notice * (role == 0 ? 45 : role == 1 ? 125 : -90) * (1 - protest);
                headYaw = (role == 0 ? 28 * notice * (1 - protest) : 0) - 24 * protest;
                headPitch = -6 * notice + 4 * protest; torsoPitch = -4 * protest;
                if (role == 0) Face("Disbelief", .7f * Smooth(1.4f, 2, t));
            }
            if (shot == "lab" || shot == "ready")
            {
                x = role == 1 ? -1.75f : 1.9f; z = .3f; yaw = role == 1 ? 24 : -35;
                headYaw = role == 1 ? 13 * (1 - Smooth(3.8f, 4.8f, t)) : -13;
                if (role == 2) headPitch = 9 * Smooth(.6f, 1.5f, t);
            }
            if (shot == "launch")
            {
                x = -.25f; z = .73f; yaw = 20;
                float preparation = Beat(t, .3f, .17f), impact = Beat(t, .75f, .1f);
                torsoPitch = -5 * preparation + 13 * Smooth(.42f, .73f, t) * (1 - Smooth(.95f, 1.65f, t));
                y = -.025f * impact; headPitch = 11 * Smooth(.2f, .6f, t) * (1 - Smooth(1, 1.6f, t));
            }
            if (shot == "hearing")
            {
                if (role == 2) { x += .73f * (1 - Smooth(3.6f, 4.95f, t)); z += .2f * Smooth(3.6f, 4.95f, t); }
                if (role == 0) { headYaw = -16 * Smooth(1.4f, 2.1f, t); torsoPitch = -5 * Beat(t, 1.8f, .8f); }
                float up = Smooth(StoryStage.VictoryLaunch + .20f, 3.9f, t);
                float glance = Smooth(4.10f + role * .10f, 4.65f + role * .10f, t);
                headPitch = Mathf.Lerp(-(role == 0 ? 12 : 18) * up, role == 0 ? 4 : 1, glance);
                if (role == 0)
                {
                    yaw += 15 * glance;
                    headYaw = Mathf.Lerp(headYaw, 25, glance);
                }
                else if (role == 1)
                {
                    float backToZorvax = Smooth(4.93f, 5.46f, t);
                    yaw += glance * Mathf.Lerp(11, -9, backToZorvax);
                    headYaw = glance * Mathf.Lerp(28, -30, backToZorvax);
                }
                else
                {
                    yaw -= 13 * glance;
                    headYaw = -31 * glance;
                }
            }
            if (shot == "evidence")
            {
                float readerOn = Smooth(2.7f, 3.55f, t);
                float lean = Smooth(7.9f, 8.95f, t) * (1 - Smooth(9.4f, 10.65f, t));
                if (role == 0)
                {
                    x -= .18f * readerOn - .69f * lean;
                    z += .18f * lean;
                    headYaw = 33 * readerOn - 12 * Smooth(5.65f, 6.45f, t);
                    headPitch = 12 * readerOn + 18 * lean;
                    torsoPitch = 10 * readerOn + 18 * lean - 9 * Beat(t, 5.65f, .37f);
                    Face("Disbelief", .76f * readerOn * (1 - lean));
                }
                if (role == 1)
                {
                    z += .49f * Smooth(.18f, 1.3f, t) - .24f * Smooth(4.45f, 5.55f, t);
                    x += .22f * readerOn;
                    yaw -= 14 * (1 - readerOn);
                    headYaw = -19 + 49 * Smooth(2.5f, 3.6f, t);
                    headPitch = 9 + 10 * Beat(t, 2.65f, .59f);
                    torsoPitch = 12 * Beat(t, 2.65f, .65f);
                }
                if (role == 2)
                {
                    float approach = Smooth(.08f, 1.7f, t);
                    x -= .55f * approach;
                    z += .65f * approach;
                    headYaw = -24 * approach;
                    headPitch = 7 + 18 * Beat(t, 5.53f, .47f);
                    torsoPitch = 23 * Beat(t, 5.53f, .58f);
                    if (!still) y -= .10f * Beat(t, 5.52f, .38f);
                }
            }
            if (shot == "reaction")
            {
                float toPlayer = Smooth(5.05f, 6.5f, t);
                if (role == 0)
                {
                    x -= .22f * (1 - toPlayer);
                    yaw += 14 - 25 * toPlayer;
                    headYaw = 23 * (1 - toPlayer);
                    headPitch = 11 * (1 - toPlayer);
                    Face("Disbelief", .62f * (1 - toPlayer));
                }
                if (role == 1) { headYaw = -21; headPitch = 5; }
                if (role == 2) { x -= .55f * (1 - Smooth(.15f, 1.25f, t)); z += .65f * (1 - Smooth(.15f, 1.25f, t)); }
            }
            if (CutsceneScript.IsCutsceneShot(shot))
            {
                // Glances: positive head yaw looks toward the television side of the set.
                if (shot == "drought-alert" && role != 1) headYaw = 22 * Smooth(.4f, 1, t);
                if (shot == "drought-welcome" && role > 0) headYaw = role == 1 ? -18 : -24;
                if ((shot == "drought-count" || shot == "drought-how-many") && role == 0) headYaw = 18;
                if (shot == "drought-how-many" && role == 2) { headPitch = -16 * Smooth(.2f, .7f, t); torsoPitch = -4 * Smooth(.2f, .7f, t); }
                if (shot == "drought-all")
                {
                    if (role == 2)
                    {
                        float flinch = Smooth(.9f, 1.15f, t);
                        headPitch = -18 * (1 - flinch) + 6 * flinch;
                        y -= .05f * flinch;
                        leftArmTilt = 22 * flinch; rightArmTilt = -22 * flinch;
                    }
                    if (role == 1) headYaw = 20 * Smooth(1, 1.4f, t);
                }
            }
            transform.localPosition = new Vector3(x, y, z);
            transform.localRotation = Quaternion.Euler(0, yaw, roll);
            Turn(chest, transform.right, torsoPitch);
            Turn(chest, Vector3.forward, torsoRoll);
            Turn(leftArm, Vector3.forward, leftArmTilt);
            Turn(rightArm, Vector3.forward, rightArmTilt);
            Turn(head, Vector3.up, headYaw);
            Turn(head, transform.right, headPitch);
            if (shot == "launch" && role == 0)
            {
                float reach = Smooth(.12f, .5f, t) * (1 - Smooth(.92f, 1.5f, t));
                float press = Smooth(.52f, .74f, t) * (1 - Smooth(.78f, 1.1f, t));
                Reach(leftArm, leftForearm, leftHand, new Vector3(-.05f, Mathf.Lerp(1.42f, 1.13f, press), 1), reach);
                // Turn the palm toward the button so the contact reads in the insert.
                if (leftHand != null)
                    leftHand.rotation = Quaternion.Slerp(leftHand.rotation,
                        Quaternion.FromToRotation(leftHand.TransformDirection(Vector3.up), Vector3.down) * leftHand.rotation,
                        reach * .78f);
            }
            if (shot == "party" && role == 2)
                Reach(rightArm, rightForearm, rightHand, new Vector3(.60f, 1.0f, .91f),
                    Smooth(.16f, .66f, t) * (1 - Smooth(.76f, 1.35f, t)));
            if (shot == "hearing" && role == 0)
            {
                Reach(leftArm, leftForearm, leftHand, new Vector3(.59f, .98f, .91f),
                    .9f * Smooth(.95f, StoryStage.VictoryLaunch, t) * (1 - Smooth(StoryStage.VictoryLaunch + .12f, 3.75f, t)));
            }
            if (shot == "evidence")
            {
                if (role == 0)
                {
                    Reach(leftArm, leftForearm, leftHand, StoryStage.ReaderSwitch, Envelope(t, 8.35f, 9.95f));
                }
                if (role == 1)
                {
                    Reach(leftArm, leftForearm, leftHand, new Vector3(-.24f, 1.43f, .96f), Envelope(t, .14f, 1.15f));
                    Reach(rightArm, rightForearm, rightHand, new Vector3(1.17f, 1.91f, -.27f), Envelope(t, 2.15f, 4.55f));
                }
                if (role == 2)
                {
                    var target = Vector3.Lerp(new Vector3(1.29f, 1.04f, 1.14f), StoryStage.ArchiveSocket, Smooth(4.85f, 5.6f, t));
                    Reach(rightArm, rightForearm, rightHand, target, Envelope(t, 1.45f, 6.12f));
                }
            }
        }
    }
}

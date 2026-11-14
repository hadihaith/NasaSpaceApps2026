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
        float[][] restMorphs;
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
            for (int i = 0; i < faces.Length; i++)
            {
                restMorphs[i] = new float[faces[i].sharedMesh.blendShapeCount];
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
        }
        static void Turn(Transform bone, Vector3 worldAxis, float degrees)
        {
            if (bone != null) bone.rotation = Quaternion.AngleAxis(degrees, worldAxis) * bone.rotation;
        }
        void Face(string expression, float amount)
        {
            foreach (var renderer in faces)
            {
                if (renderer.sharedMesh == null) continue;
                int index = renderer.sharedMesh.GetBlendShapeIndex(expression);
                if (index >= 0) renderer.SetBlendShapeWeight(index, Mathf.Max(renderer.GetBlendShapeWeight(index), amount));
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
        void PosePhotographer(string shot, float t)
        {
            bool party = shot == "party";
            bool visible = party || shot == "home" || shot == "reveal" || shot == "years" || shot == "return";
            gameObject.SetActive(visible);
            if (!visible) return;
            Sample(party ? "Snap" : "Idle", t, .82f, 2.55f);
            transform.localPosition = new Vector3(-2.85f, 0, .55f);
            transform.localRotation = Quaternion.Euler(0, shot == "return" ? 50 : 17, 0);
        }
        public void Pose(string shot, float t, bool still)
        {
            if (!initialized) Initialize();
            if (role == 3)
            {
                PosePhotographer(shot, t);
                return;
            }
            bool visible = shot == "home" || (shot == "recruit" && role == 0) || (shot == "ready" && role > 0) || shot == "epilogue" ||
                shot == "reveal" || shot == "party" || shot == "years" || shot == "return" || shot == "hearing" ||
                (shot == "correction" && role < 2) || (shot == "launch" && role == 0) || (shot == "lab" && role > 0);
            gameObject.SetActive(visible);
            if (!visible) return;
            string action = "Idle";
            float start = .1f, end = 8, rate = 1, hold = 0;
            bool loop = false;
            switch (shot)
            {
                case "reveal": if (role == 0) { action = "Welcome"; start = 1.04f; end = 4.2f; hold = .5f; } break;
                case "correction":
                    action = role == 1 ? "Eureka" : "Nod";
                    start = role == 1 ? .2f : .7f; end = 2.85f; hold = role == 1 ? .44f : .18f; break;
                case "party": action = role == 0 ? "Welcome" : role == 1 ? "Explain" : "Wave"; start = .65f + role * .07f; end = 4.4f; loop = true; rate = 1.05f; break;
                case "return": action = role == 0 ? "Complain" : "Idle"; start = 1.12f; end = 4.65f; rate = 1.35f; break;
                case "launch": action = "Nod"; start = .08f; end = 2.1f; rate = .8f; break;
                case "lab": if (role == 1) { action = "Explain"; start = .35f; end = 6.5f; rate = .77f; hold = .9f; } break;
                case "hearing": action = role == 0 ? "Welcome" : role == 1 ? "Eureka" : "Idle"; start = role == 1 ? 2.05f : .08f; end = 5.6f; hold = role == 0 ? 2 : .5f; rate = .9f; break;
                case "recruit": if (t < 3.4f) { action = "Welcome"; start = .1f; end = 3.5f; hold = .6f; } break;
            }
            Sample(action, t, start, end, rate, loop, hold);
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
                headYaw = role == 1 ? -22 : 18 * Smooth(.7f, 1.1f, t);
                torsoPitch = role == 1 ? -3 : 4;
                if (role == 1) leftArmTilt = -7 * Envelope(t, .4f, 2.65f);
            }
            if (shot == "party")
            {
                float dance = still ? 0 : Smooth(.72f, .94f, t);
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
            if (shot == "years")
            {
                y = -.06f; torsoPitch = role == 0 ? 8 : 5; headPitch = role == 2 ? 16 : 11;
                roll = role == 2 ? -5 : 2; headYaw = role == 0 ? -9 : 5;
                rightArmTilt = role == 0 ? -12 : -5;
            }
            if (shot == "return")
            {
                float notice = Smooth(.24f + role * .12f, .85f + role * .12f, t);
                float protest = role == 0 ? Smooth(1.45f, 2.05f, t) : .3f * Smooth(2, 3, t);
                yaw += notice * (role == 0 ? 100 : role == 1 ? 125 : -90) * (1 - protest);
                headYaw = -24 * protest; headPitch = -6 * notice + 4 * protest; torsoPitch = -4 * protest;
                if (role == 0) Face("Disbelief", 70 * Smooth(1.4f, 2, t));
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
                float interruption = Smooth(2.24f, 2.8f, t);
                if (role == 0) { yaw += 15 * interruption; headYaw = 30 * interruption; headPitch = 9 * interruption; torsoPitch = -4 * interruption; }
                if (role == 1) { headYaw = -20 * interruption; headPitch = 5 * interruption; }
                if (role == 2) { headYaw = -17 * interruption; headPitch = 7 * interruption; }
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
                Reach(leftArm, leftForearm, leftHand, new Vector3(-.05f, Mathf.Lerp(1.42f, 1.16f, Smooth(.52f, .75f, t)), 1), reach);
            }
            if (shot == "hearing" && role == 0)
                Reach(rightArm, rightForearm, rightHand, new Vector3(-.74f, 1.41f, 1.36f), .9f * Smooth(.08f, .4f, t));
        }
    }
}

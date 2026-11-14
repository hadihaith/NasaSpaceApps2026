using System;
using UnityEngine;

namespace Homecoming
{
    public sealed class StoryActor : MonoBehaviour
    {
        public GameObject rig;
        public AnimationClip[] clips;
        public int role;
        Transform head;
        Transform pressArm, pressForearm, pressHand;
        Quaternion headRest;

        public void Initialize()
        {
            // Sampling the original imported clips makes pause and chapter seeking exact.
            foreach (var animator in rig.GetComponentsInChildren<Animator>()) animator.enabled = false;
            foreach (var animation in rig.GetComponentsInChildren<Animation>()) animation.enabled = false;
            foreach (var bone in rig.GetComponentsInChildren<Transform>())
            {
                if (bone.name == "head") { head = bone; headRest = bone.localRotation; }
                if (bone.name == "upper_arm.L") pressArm = bone;
                if (bone.name == "forearm.L") pressForearm = bone;
                if (bone.name == "hand.L") pressHand = bone;
            }
        }

        AnimationClip Clip(string name)
        {
            foreach (var clip in clips)
                if (clip.name.Equals(name, StringComparison.OrdinalIgnoreCase) || clip.name.EndsWith("_" + name)) return clip;
            return clips.Length > 0 ? clips[0] : null;
        }

        public void Pose(string shot, float t, bool still)
        {
            bool visible = shot == "home" || (shot == "recruit" && role == 0) || shot == "ready" || shot == "epilogue" ||
                shot == "reveal" || shot == "party" || shot == "years" || shot == "return" || shot == "hearing" ||
                (shot == "correction" && role < 2) || (shot == "launch" && role == 0) || (shot == "lab" && role > 0);
            gameObject.SetActive(visible);
            if (!visible) return;
            string action = "Idle";
            if (role == 0)
            {
                if (shot == "reveal" || shot == "party" || shot == "hearing" || shot == "recruit") action = "Welcome";
                if (shot == "return") action = "Complain";
                if (shot == "launch" || shot == "correction") action = "Nod";
            }
            if (role == 1)
            {
                if (shot == "correction" || shot == "hearing") action = "Eureka";
                if (shot == "lab" || shot == "party" || shot == "return") action = "Explain";
            }
            if (role == 2 && shot == "party") action = "Wave";
            var clip = Clip(action) ?? Clip("Idle");
            float delay = shot == "reveal" ? 1.35f : shot == "hearing" && role == 1 ? 2.1f : .15f;
            if (clip != null)
            {
                float sample = action == "Idle" ? (t + role * .37f) % clip.length : Mathf.Clamp(t - delay, 0, clip.length - .001f);
                clip.SampleAnimation(rig, sample);
            }
            float bounce = shot == "party" && t > .72f && !still ? Mathf.Sin(t * 8 + role * 1.7f) : 0;
            float tired = shot == "years" ? 1 : 0;
            float x = new[] { -1.5f, .35f, 2.1f }[role];
            float z = new[] { .85f, .12f, -.18f }[role];
            float yaw = new[] { -8f, -13f, -23f }[role];
            if (shot == "lab" || shot == "ready")
            {
                x = role == 1 ? -1.75f : role == 2 ? 1.9f : -3.4f;
                z = .3f;
                yaw = role == 1 ? 24 : -35;
            }
            if (shot == "launch") { x = -.72f; z = .73f; yaw = 20; }
            transform.localPosition = new Vector3(x + bounce * .09f, Mathf.Max(0, bounce) * .13f, z);
            transform.localRotation = Quaternion.Euler(tired * 7, yaw, tired * (role == 2 ? 7 : -2) + bounce * 4);
            if (head != null)
            {
                if (clip == null) head.localRotation = headRest;
                head.localRotation *= Quaternion.Euler(tired * 12, shot == "return" ? Mathf.SmoothStep(0, 32, t / 1.2f) : 0, 0);
            }
            if (shot == "launch" && role == 0 && pressArm != null && pressForearm != null && pressHand != null)
            {
                float reach = Mathf.SmoothStep(0, 1, (t - .1f) / .4f) * (1 - Mathf.SmoothStep(0, 1, (t - 1.05f) / .45f));
                Vector3 target = new Vector3(-.05f, 1.13f - Mathf.SmoothStep(0, .1f, (t - .54f) / .2f), 1);
                for (int step = 0; step < 4; step++)
                    foreach (var joint in new[] { pressForearm, pressArm })
                    {
                        var from = pressHand.position - joint.position;
                        var to = target - joint.position;
                        joint.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(from, to), reach * .65f) * joint.rotation;
                    }
            }
        }
    }
}

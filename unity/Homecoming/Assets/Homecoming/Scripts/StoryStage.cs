using UnityEngine;
using UnityEngine.Video;

namespace Homecoming
{
    // The set is authored into Story.unity by the editor builder. All props remain editable.
    public sealed class StoryStage : MonoBehaviour
    {
        public Camera filmCamera;
        public GameObject bridge, stars, moon, earth, projection, banner, cannon, report, switchProp, balloon;
        public Transform[] confetti;
        public StoryActor[] crew;
        public Renderer screen;
        public TextMesh monitorTitle, monitorSubtitle;
        public VideoPlayer television;
        public Light practical;
        public Material televisionMaterial;
        RenderTexture videoTexture;
        Texture poster;
        string current;
        bool videoFailed;
        float videoPrepareAge;

        public bool VideoFallback => videoFailed;
        public void Initialize()
        {
            foreach (var actor in crew) actor.Initialize();
            poster = Resources.Load<Texture2D>("Film/apollo11");
            // Clone per player; the asset is never modified by playback.
            televisionMaterial = new Material(televisionMaterial);
            screen.sharedMaterial = televisionMaterial;
            videoTexture = new RenderTexture(960, 720, 0, RenderTextureFormat.ARGB32);
            videoTexture.Create();
            television.renderMode = VideoRenderMode.RenderTexture;
            television.targetTexture = videoTexture;
            television.playOnAwake = false;
            television.isLooping = true;
            television.audioOutputMode = VideoAudioOutputMode.Direct;
            television.prepareCompleted += OnVideoPrepared;
            television.errorReceived += OnVideoError;
            television.Prepare();
        }
        void OnVideoPrepared(VideoPlayer player)
        {
            if (player.isActiveAndEnabled && UsesFilm(current))
                player.Play();
        }
        void OnVideoError(VideoPlayer player, string message)
        {
            videoFailed = true;
            Debug.LogWarning("Apollo archival clip unavailable; the bundled still is being shown. " + message);
        }
        public void SetPaused(bool paused)
        {
            if (!television.isActiveAndEnabled) return;
            if (paused) television.Pause();
            else if (UsesFilm(current) && television.isPrepared) television.Play();
        }
        bool UsesFilm(string id) => id == "apollo" || id == "reveal" || id == "correction" || id == "party";
        public void SetShot(string id)
        {
            current = id;
            bridge.SetActive(id != "signal" && id != "earth");
            if (!television.isActiveAndEnabled) return;
            if (UsesFilm(id) && television.isPrepared)
            {
                if (id == "apollo") television.time = 0;
                television.Play();
            }
            else television.Pause();
        }
        public void Evaluate(string id, float t, bool reducedMotion, bool mute)
        {
            if (current != id) SetShot(id);
            bool space = id == "signal" || id == "earth";
            bridge.SetActive(!space);
            stars.SetActive(true);
            moon.SetActive(id == "signal");
            earth.SetActive(id == "earth");
            projection.SetActive(id == "lab" || id == "ready");
            bool old = id == "years" || id == "return" || id == "hearing" || id == "deflate" ||
                id == "home" || id == "recruit" || id == "epilogue";
            banner.SetActive(old || id == "party");
            banner.transform.localRotation = Quaternion.Euler(0, 0, old ? -9 : -2);
            banner.transform.localScale = new Vector3(1, old ? .83f : Mathf.SmoothStep(.02f, 1, (t - .7f) / .5f), 1);
            report.SetActive(id == "hearing");
            switchProp.SetActive(id == "launch");
            balloon.SetActive((old || id == "party") && id != "recruit");
            float deflate = id == "deflate" ? Mathf.SmoothStep(0, 1, (t - .84f) / 1.8f) : 0;
            balloon.transform.localPosition = new Vector3(-.65f + deflate * .52f,
                1.9f - deflate * 1.1f + (reducedMotion ? 0 : Mathf.Sin(t * 1.6f) * .035f), .5f);
            balloon.transform.localScale = Vector3.Scale(new Vector3(.48f, .62f, .48f), Vector3.one * (1 - deflate * .88f));
            balloon.transform.localRotation = Quaternion.Euler(0, 0, -deflate * 74);
            cannon.SetActive(id != "lab" && id != "ready" && id != "apollo" && id != "recruit");
            float recoil = id == "party" ? Pulse(t, .75f, .15f) : id == "jam" ? Pulse(t, .58f, .12f) + Pulse(t, 1.08f, .1f) : 0;
            cannon.transform.localRotation = Quaternion.Euler(-22 - recoil * 14, -18, 0);
            switchProp.transform.localScale = new Vector3(1, 1 - Pulse(t, .74f, .12f) * .15f, 1);
            practical.intensity = old ? .65f : id == "launch" ? Mathf.Lerp(.65f, 3, Mathf.SmoothStep(0, 1, (t - .74f) / .15f)) : 1.5f;
            foreach (var actor in crew) actor.Pose(id, t, reducedMotion);
            for (int i = 0; i < confetti.Length; i++)
            {
                bool flying = id == "party" && t > .72f;
                confetti[i].gameObject.SetActive(flying);
                if (!flying) continue;
                float age = reducedMotion ? 1 : t - .72f;
                float vx = Hash(i, 0) * 5 - 2.5f, vy = 2.6f + Hash(i, 1) * 3.5f;
                confetti[i].localPosition = new Vector3(.8f + vx * age, .9f + vy * age - 2.1f * age * age,
                    .5f + (Hash(i, 2) - .5f) * age * 2);
                confetti[i].localRotation = Quaternion.Euler(age * (80 + i * 7), age * 150, i * 28 + age * 120);
            }
            if (moon.activeSelf) moon.transform.localRotation = Quaternion.Euler(-6, 225 + (reducedMotion ? 0 : t * 7), 8);
            if (earth.activeSelf) earth.transform.localRotation = Quaternion.Euler(0, 228 + (reducedMotion ? 0 : t * 6), 21);
            if (projection.activeSelf)
            {
                float rise = id == "ready" ? 1 : Mathf.SmoothStep(0, 1, (t - .6f) / 1.3f);
                projection.transform.localPosition = new Vector3(0, Mathf.Lerp(.55f, 1.65f, rise), -.8f);
                projection.transform.localScale = Vector3.one * (.04f + .96f * rise);
                projection.transform.localRotation = Quaternion.Euler(0, reducedMotion ? 0 : t * 15, 12);
            }
            if (!television.isPrepared && !videoFailed)
            {
                videoPrepareAge += Time.unscaledDeltaTime;
                if (videoPrepareAge > 12) videoFailed = true;
            }
            bool video = UsesFilm(id) && television.isPrepared && !videoFailed;
            television.SetDirectAudioMute(0, mute);
            television.SetDirectAudioVolume(0, .45f);
            televisionMaterial.mainTexture = video ? (Texture)videoTexture : id == "return" ? null : poster;
            televisionMaterial.color = id == "years" ? new Color(.23f, .23f, .2f) : id == "return" ? new Color(.07f, .11f, .13f) : Color.white;
            monitorTitle.gameObject.SetActive(id == "return" || id == "years");
            monitorSubtitle.gameObject.SetActive(id == "return");
            monitorTitle.text = id == "return" ? "ARTEMIS" : "NO SIGNAL";
            EvaluateCamera(id, t, reducedMotion);
        }
        static float Hash(int i, int n) => Mathf.Repeat(Mathf.Sin((i + 1) * 12.9898f + n * 78.233f) * 43758.5453f, 1);
        static float Pulse(float t, float at, float width) => Mathf.Exp(-Mathf.Pow((t - at) / width, 2));
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        void EvaluateCamera(string id, float t, bool reduced)
        {
            Vector3 from = V(.35f, 2.05f, 8.5f), to = from, look = V(.25f, 1.42f, 0), target = look;
            float duration = 4, fov = 36;
            switch (id)
            {
                case "home": from = to = V(1.6f, 2.15f, 9.8f); look = target = V(1.7f, 1.55f, -.2f); break;
                case "signal": from = V(5, 3, 65); to = V(-.35f, 1.45f, 5.9f); look = target = V(0, 1.2f, 0); duration = 4.5f; break;
                case "apollo": from = to = V(1.95f, 2.05f, 1.4f); look = target = V(1.95f, 2.05f, -1.5f); fov = 32; break;
                case "reveal": from = V(1.95f, 2.05f, 1.4f); to = V(.7f, 2.1f, 8.4f); look = V(1.95f, 2.05f, -1.5f); target = V(.25f, 1.45f, .1f); duration = 2.4f; break;
                case "correction": from = V(-.7f, 1.85f, 4.5f); to = V(-.4f, 1.8f, 4.15f); look = target = V(-.35f, 1.65f, .35f); fov = 34; duration = 2.6f; break;
                case "party": from = V(.2f, 2.2f, 8.4f); to = V(.35f, 2.35f, 9.1f); look = target = V(.2f, 1.6f, 0); break;
                case "jam": from = V(.05f, 1.45f, 3.7f); to = V(.1f, 1.3f, 3.4f); look = target = V(.8f, 1.08f, .5f); duration = 1.7f; break;
                case "years": from = V(.35f, 2.35f, 9.1f); to = V(.15f, 2.1f, 8.8f); look = target = V(.2f, 1.6f, 0); break;
                case "return": from = V(.15f, 2.1f, 8.8f); to = V(.9f, 1.95f, 7.4f); break;
                case "earth": from = V(1.1f, 1.6f, 7.5f); to = V(-.1f, 1.4f, 5.9f); look = target = V(0, 1.2f, 0); break;
                case "launch": from = V(-.2f, 1.5f, 3.7f); to = V(.2f, 1.4f, 3.35f); look = target = V(-.05f, 1.08f, .6f); duration = 1.5f; break;
                case "lab": from = V(-.8f, 2.1f, 8.8f); to = V(.1f, 1.9f, 7.8f); look = target = V(0, 1.45f, -.4f); duration = 6.4f; break;
                case "ready": from = to = V(.1f, 2.1f, 8.6f); look = target = V(0, 1.5f, -.4f); break;
                case "hearing": from = V(-.25f, 2.1f, 8.4f); to = V(-.2f, 2.0f, 7.6f); look = target = V(.05f, 1.4f, .35f); duration = 5.3f; break;
                case "deflate": from = V(-.55f, 1.65f, 3.5f); to = V(-.4f, 1.3f, 4); look = V(-.65f, 1.8f, .5f); target = V(-.3f, 1.1f, .5f); duration = 3.5f; break;
                case "recruit": from = to = V(-2.65f, 1.8f, 5); look = target = V(-2.7f, 1.35f, .8f); break;
            }
            float p = reduced ? 1 : Mathf.SmoothStep(0, 1, t / duration);
            filmCamera.transform.position = Vector3.Lerp(from, to, p);
            filmCamera.transform.LookAt(Vector3.Lerp(look, target, p));
            float aspect = filmCamera.aspect;
            if (aspect < 1.55f && id != "apollo" && id != "jam" && id != "deflate")
                fov = 2 * Mathf.Atan(Mathf.Tan(fov * Mathf.Deg2Rad / 2) * 1.55f / Mathf.Max(.5f, aspect)) * Mathf.Rad2Deg;
            filmCamera.fieldOfView = fov;
        }
        void OnDestroy()
        {
            if (television != null)
            {
                television.prepareCompleted -= OnVideoPrepared;
                television.errorReceived -= OnVideoError;
            }
            if (videoTexture != null) { videoTexture.Release(); Destroy(videoTexture); }
            if (videoTexture != null && televisionMaterial != null) Destroy(televisionMaterial);
        }
    }
}

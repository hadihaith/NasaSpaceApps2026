using UnityEngine;
using UnityEngine.Video;

namespace Homecoming
{
    // The set is authored into Story.unity by the editor builder. All props remain editable.
    public sealed partial class StoryStage : MonoBehaviour
    {
        public const float VictoryLaunch = 3.08f;
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
        Texture2D artemisOut, artemisMoon, artemisHome;
        Transform jammedStreamer, trigger;
        Material streamerMaterial, archiveBackdropMaterial;
        GameObject archiveBackdrop;
        Renderer[] cabinet, hullLettering;
        Vector3 cabinetScreenScale;
        bool paused;
        const double ArchiveStart = 0;
        const double RevealStart = 0;
        const double CorrectionStart = 4.2;
        static readonly Vector3 PartyCamera = new Vector3(.35f, 2.35f, 9.1f);
        static readonly Vector3 PartyLook = new Vector3(.2f, 1.6f, 0);
        string current;
        bool videoFailed;
        float videoPrepareAge;
        Transform switchButton;
        StoryLighting lighting;
        StoryEnvironment environment;
        // Discovery cutscene props, created at runtime so the authored scene stays untouched.
        const int BasketCount = 18;
        Transform basketPile;
        Transform[] basketItems;
        Material wicker, ribbon;
        Color practicalColor;
        bool practicalCaptured;
        float titleSize = -1;

        public bool VideoFallback => videoFailed;
        public StoryEnvironment Environment
        {
            get
            {
                if (environment == null)
                {
                    environment = GetComponent<StoryEnvironment>();
                    if (environment == null) environment = gameObject.AddComponent<StoryEnvironment>();
                    environment.Initialize(this);
                }
                return environment;
            }
        }
        // Created on first use, so editor validation renders get the same lighting as play.
        public StoryLighting Lighting
        {
            get
            {
                if (lighting == null)
                {
                    lighting = GetComponent<StoryLighting>();
                    if (lighting == null) lighting = gameObject.AddComponent<StoryLighting>();
                    lighting.Initialize(this);
                }
                return lighting;
            }
        }
        public void Initialize()
        {
            _ = Lighting;
            _ = Environment;
            foreach (var actor in crew) actor.Initialize();
            BuildBaskets();
            ApplyTypography();
            poster = Resources.Load<Texture2D>("Film/apollo-still");
            // Clone per player; the asset is never modified by playback.
            televisionMaterial = new Material(televisionMaterial);
            screen.sharedMaterial = televisionMaterial;
            videoTexture = new RenderTexture(1280, 720, 0, RenderTextureFormat.ARGB32);
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
        void PrepareStoryProps()
        {
            if (jammedStreamer != null) return;
            cabinet = screen.transform.parent.GetComponentsInChildren<Renderer>(true);
            cabinetScreenScale = screen.transform.localScale;
            var identification = bridge.transform.Find("Hull identification");
            var stencil = bridge.transform.Find("Hull stencil");
            hullLettering = new[] { identification?.GetComponent<Renderer>(), stencil?.GetComponent<Renderer>() };
            switchButton = switchProp.transform.Find("Red pushbutton");
            // Frame the widescreen broadcast against black until the CRT is revealed.
            archiveBackdropMaterial = new Material(televisionMaterial) { name = "Archival picture mask", color = Color.black };
            archiveBackdrop = Part(PrimitiveType.Quad, screen.transform.parent,
                screen.transform.localPosition - Vector3.forward * .015f, V(40, 24, 1), archiveBackdropMaterial);
            archiveBackdrop.transform.localRotation = screen.transform.localRotation;
            artemisOut = Resources.Load<Texture2D>("Film/artemis-out");
            artemisMoon = Resources.Load<Texture2D>("Film/artemis-moon");
            artemisHome = Resources.Load<Texture2D>("Film/artemis-home");
            var barrelMaterial = cannon.transform.Find("Barrel").GetComponent<Renderer>().sharedMaterial;
            var muzzle = cannon.transform.Find("Black muzzle");
            jammedStreamer = new GameObject("Jammed teal streamer").transform;
            jammedStreamer.SetParent(cannon.transform, false);
            jammedStreamer.localPosition = muzzle.localPosition + new Vector3(.035f, .022f, .028f);
            var line = jammedStreamer.gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            streamerMaterial = new Material(barrelMaterial) { name = "Matte teal streamer" };
            streamerMaterial.color = new Color(.16f, .64f, .57f);
            streamerMaterial.SetFloat("_Metallic", 0);
            streamerMaterial.SetFloat("_Glossiness", .18f);
            line.sharedMaterial = streamerMaterial;
            line.startWidth = .069f;
            line.endWidth = .048f;
            line.numCornerVertices = 3;
            Vector3[] bends = { V(0, 0, 0), V(.035f, .11f, .03f), V(.095f, .19f, .02f),
                V(.125f, .24f, -.05f), V(.09f, .30f, -.12f), V(-.01f, .32f, -.09f) };
            line.positionCount = bends.Length;
            line.SetPositions(bends);

            Part(PrimitiveType.Sphere, cannon.transform, V(-.25f, -.06f, .35f),
                V(.18f, .13f, .14f), barrelMaterial);
            trigger = cannon.transform.GetChild(cannon.transform.childCount - 1);
            trigger.name = "Round cannon trigger";

        }
        public void ApplyTypography()
        {
            // Scene lettering uses the same authored typography as the interface.
            foreach (var text in GetComponentsInChildren<TextMesh>(true))
            {
                // The console codes read as noise on screen; the set is cleaner without them.
                if (text.name == "Console stencil") { text.gameObject.SetActive(false); continue; }
                // Raised so the hull lettering clears the television from the camera's eye line.
                if (text.name == "Hull identification") { text.text = "I.A.D."; SetHeight(text.transform, 3.95f); }
                if (text.name == "Hull stencil")
                {
                    // One legible line instead of a tiny two-line stencil.
                    text.text = "INTERGALACTIC AFFECTION DEPARTMENT";
                    SetHeight(text.transform, 3.55f);
                    text.characterSize = .11f * 10 / text.fontSize;
                }
                bool display = text.name == "Welcome lettering" || text.name == "Broadcast title" || text.name == "Report heading" ||
                    text.name == "Hull identification" || text.name == "Hull stencil";
                var font = Resources.Load<Font>(display ? "Fonts/Display" : "Fonts/Mono");
                if (font == null) continue;
                text.font = font;
                text.GetComponent<Renderer>().sharedMaterial = font.material;
                // Rasterise glyphs at twice the size and scale down: crisp lettering, same layout.
                if (text.fontSize < 160)
                {
                    text.characterSize *= text.fontSize / 160f;
                    text.fontSize = 160;
                }
            }
        }
        static void SetHeight(Transform t, float y) => t.localPosition = new Vector3(t.localPosition.x, y, t.localPosition.z);
        void OnVideoPrepared(VideoPlayer player)
        {
            if (!player.isActiveAndEnabled || !UsesFilm(current)) return;
            player.time = FilmOffset(current);
            if (!paused) player.Play();
        }
        void OnVideoError(VideoPlayer player, string message)
        {
            videoFailed = true;
            Debug.LogWarning("Apollo archival clip unavailable; the bundled still is being shown. " + message);
        }
        public void SetPaused(bool paused)
        {
            this.paused = paused;
            if (!television.isActiveAndEnabled) return;
            if (paused) television.Pause();
            else if (UsesFilm(current) && television.isPrepared) television.Play();
        }
        bool UsesFilm(string id) => id == "apollo" || id == "reveal" || id == "correction";
        static double FilmOffset(string id) => id == "reveal" ? RevealStart : id == "correction" ? CorrectionStart : ArchiveStart;
        public void SetShot(string id)
        {
            var previous = current;
            current = id;
            bridge.SetActive(id != "signal" && id != "earth");
            if (!television.isActiveAndEnabled) return;
            if (UsesFilm(id) && television.isPrepared)
            {
                bool continuous = previous == "apollo" && id == "reveal" || previous == "reveal" && id == "correction";
                if (!continuous || System.Math.Abs(television.time - FilmOffset(id)) > .23)
                    television.time = FilmOffset(id);
                if (!paused) television.Play();
            }
            else television.Pause();
        }
        static bool SpaceShot(string id) => id == "signal" || id == "earth";
        public void Evaluate(string id, float t, bool reducedMotion, bool mute, float archiveVolume = .45f,
            HomecomingReport findings = null)
        {
            if (current != id) SetShot(id);
            if (!SpaceShot(id)) PrepareStoryProps();
            bool space = SpaceShot(id);
            bridge.SetActive(!space);
            stars.SetActive(true);
            moon.SetActive(id == "signal");
            earth.SetActive(id == "earth");
            projection.SetActive(id == "lab" || id == "ready");
            bool cutscene = CutsceneScript.IsCutsceneShot(id);
            bool old = id == "years" || id == "return" || id == "hearing" || id == "evidence" || id == "reaction" || id == "deflate" ||
                id == "home" || id == "recruit" || id == "epilogue" || cutscene;
            banner.SetActive(old || id == "party" || id == "jam");
            float unfurl = id == "jam" ? 1 : reducedMotion ? (t >= .72f ? 1 : 0) : Mathf.Clamp01((t - .72f) / .58f);
            float overshoot = reducedMotion ? 0 : Mathf.Sin(unfurl * Mathf.PI * 2) * Mathf.Exp(-unfurl * 3) * .17f;
            // How far the room has aged: 0 in 1969, 1 in 2026, rolling forward during the time-lapse.
            float aged = id == "years" ? Lapse(t, reducedMotion) : old ? 1 : 0;
            banner.transform.localRotation = Quaternion.Euler(0, 0, old ? Mathf.Lerp(-2, -9, aged) : -2 + (reducedMotion ? 0 : Mathf.Sin(t * 5) * Mathf.Exp(-Mathf.Max(0, t - 1.2f) * 2) * 2));
            banner.transform.localScale = new Vector3(1, old ? Mathf.Lerp(1, .83f, aged) : Mathf.Max(.02f, Mathf.SmoothStep(0, 1, unfurl) + overshoot), 1);
            report.SetActive(false);
            switchProp.SetActive(id == "launch");
            balloon.SetActive((old || id == "party") && id != "recruit");
            float deflateAge = id == "deflate" ? Mathf.Max(0, t - .84f) : 0;
            float deflate = reducedMotion ? (deflateAge >= 1.8f ? 1 : 0) : Mathf.SmoothStep(0, 1, deflateAge / 1.8f);
            float flutter = reducedMotion ? 0 : Mathf.Sin(deflateAge * 24) * Mathf.Sin(deflate * Mathf.PI);
            float sag = old ? aged : 0, droop = 1 - .08f * sag;
            balloon.transform.localPosition = new Vector3(-.25f + deflate * .7f + flutter * .07f,
                1.9f - .2f * sag - deflate * 1.15f + (reducedMotion ? 0 : Mathf.Sin(t * 1.6f) * .035f * (1 - sag)), .5f);
            balloon.transform.localScale = new Vector3(.48f * droop * (1 - deflate * .86f) * (1 + flutter * .15f),
                .62f * droop * (1 - deflate * .93f), .48f * droop * (1 - deflate * .86f));
            balloon.transform.localRotation = Quaternion.Euler(0, 0, -deflate * 85 + flutter * 16);
            cannon.SetActive(id != "lab" && id != "ready" && id != "apollo" && id != "recruit");
            bool celebratoryShot = id == "party" || id == "hearing";
            float cannonCue = id == "hearing" ? VictoryLaunch : .72f;
            float recoil = reducedMotion ? 0 : celebratoryShot ? Spring(t - cannonCue, 8, 24) * 19 :
                id == "jam" ? Spring(t - .58f, 14, 36) * 7 + Spring(t - 1.08f, 14, 36) * 5 : 0;
            cannon.transform.localRotation = Quaternion.Euler(-22 - recoil, -18, 0);
            if (jammedStreamer != null)
            {
                jammedStreamer.gameObject.SetActive(id == "jam" || old || id == "party" && t >= 1.32f);
                float bend = id == "jam" && t < 1.08f ? 0 : 29;
                jammedStreamer.localRotation = Quaternion.Euler(18 + bend - 3 * recoil, 0, -27);
            }
            if (trigger != null && celebratoryShot)
                trigger.localPosition = V(-.25f, -.06f, .35f - .045f * Pulse(t, cannonCue, .075f));
            else if (trigger != null) trigger.localPosition = V(-.25f, -.06f, .35f);
            switchProp.transform.localScale = Vector3.one;
            if (switchButton != null) switchButton.localPosition = new Vector3(0, .19f - Pulse(t, .74f, .1f) * .075f, 0);
            Lighting.Evaluate(id, t, reducedMotion);
            foreach (var actor in crew) actor.Pose(id, t, reducedMotion);
            for (int i = 0; i < confetti.Length; i++)
            {
                bool flying = id == "party" && t > .72f;
                bool landed = id == "jam" || (old && id != "recruit") || flying && (reducedMotion || t >= 3.75f);
                confetti[i].gameObject.SetActive(flying || landed);
                if (!flying && !landed) continue;
                float vx = Hash(i, 0) * 5 - 2.5f, vy = 2.9f + Hash(i, 1) * 3.5f;
                float landAt = (vy + Mathf.Sqrt(vy * vy + 4 * 2.6f * .89f)) / (2 * 2.6f);
                float age = landed ? landAt : Mathf.Min(landAt, t - .72f);
                float drag = (1 - Mathf.Exp(-age * .8f)) / .8f;
                confetti[i].localPosition = V(.8f + vx * drag,
                    Mathf.Max(.012f, .9f + vy * age - 2.6f * age * age), .5f + (Hash(i, 2) - .5f) * drag * 2);
                confetti[i].localRotation = age >= landAt ? Quaternion.Euler(90, Hash(i, 4) * 360, 0) :
                    Quaternion.Euler(age * (80 + i * 7), age * 150, i * 28 + age * 120);
            }
            if (moon.activeSelf) moon.transform.localRotation = Quaternion.Euler(-6, 225 + (reducedMotion ? 0 : t * 7), 8);
            if (earth.activeSelf) earth.transform.localRotation = Quaternion.Euler(0, 228 + (reducedMotion ? 0 : t * 6), 21);
            if (projection.activeSelf)
            {
                float rise = id == "ready" ? 1 : reducedMotion ? (t >= .6f ? 1 : 0) : Mathf.SmoothStep(0, 1, (t - .6f) / 1.3f);
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
            television.SetDirectAudioMute(0, mute || id != "apollo");
            television.SetDirectAudioVolume(0, archiveVolume);
            televisionMaterial.mainTexture = id == "return" ? (t >= 3.2f ? artemisHome : t >= 1.5f ? artemisMoon : artemisOut) :
                video ? (Texture)videoTexture : poster;
            bool monitorOff = id == "launch" || id == "lab" || id == "ready" || id == "hearing" || id == "evidence" || id == "reaction" || id == "deflate";
            float brightness = monitorOff ? .015f : id == "years" ? Mathf.Lerp(1, .045f, aged) :
                id == "return" ? Mathf.SmoothStep(.06f, 1, (t - .15f) / .40f) : 1;
            televisionMaterial.color = new Color(brightness, brightness, brightness);
            bool pictureOnly = id == "apollo" || id == "reveal" && !reducedMotion && t < .24f;
            if (archiveBackdrop != null) archiveBackdrop.SetActive(pictureOnly);
            if (cabinet != null)
            {
                bool broadcast = UsesFilm(id) || id == "return";
                screen.transform.localScale = broadcast ? V(cabinetScreenScale.y * 16f / 9f,
                    cabinetScreenScale.y, cabinetScreenScale.z) : cabinetScreenScale;
                foreach (var renderer in cabinet)
                    if (renderer != screen) renderer.enabled = !pictureOnly;
            }
            if (hullLettering != null)
                foreach (var renderer in hullLettering)
                    if (renderer != null) renderer.enabled = !pictureOnly;
            monitorTitle.gameObject.SetActive(false);
            monitorSubtitle.gameObject.SetActive(false);
            if (titleSize < 0) titleSize = monitorTitle.characterSize;
            monitorTitle.characterSize = titleSize;
            // Captured lazily: editor validation evaluates shots without calling Initialize.
            if (!practicalCaptured) { practicalColor = practical.color; practicalCaptured = true; }
            practical.color = practicalColor;
            if (cutscene) EvaluateCutscene(id, t, reducedMotion);
            else if (basketPile != null) basketPile.gameObject.SetActive(false);
            EvaluateCamera(id, t, reducedMotion);
            EvaluateEvidenceReader(id, t, reducedMotion, findings);
            Environment.Evaluate(id, t, reducedMotion);
        }
        // The 2026 time-lapse: 57 years pass between 0.25 s and 2.7 s of the shot.
        public static float Lapse(float t, bool reduced) => reduced ? (t >= 2.7f ? 1 : 0) : Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.25f, 2.7f, t));
        static float Hash(int i, int n) => Mathf.Repeat(Mathf.Sin((i + 1) * 12.9898f + n * 78.233f) * 43758.5453f, 1);
        static float Spring(float age, float damping, float frequency) => age < 0 ? 0 : Mathf.Exp(-age * damping) * Mathf.Sin(age * frequency);
        static float Pulse(float t, float at, float width) => Mathf.Exp(-Mathf.Pow((t - at) / width, 2));
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        void EvaluateCamera(string id, float t, bool reduced)
        {
            Vector3 from = V(.35f, 2.05f, 8.5f), to = from, look = V(.25f, 1.42f, 0), target = look;
            float duration = 4, fov = 36;
            switch (id)
            {
                case "home": from = to = V(1.6f, 2.15f, 9.8f); look = target = V(1.7f, 1.55f, -.2f); break;
                case "signal": from = V(5, 3, 65); to = V(-.35f, 1.45f, 5.9f); look = target = V(0, 1.2f, 0); duration = 3.5f; break;
                // Start back far enough to see the whole 1969 set, then ease toward the screen.
                case "apollo": case "reveal":
                    var screenCenter = screen.transform.position;
                    fov = 34;
                    float height = screen.transform.lossyScale.y * Mathf.Max(1, (4f / 3f) / filmCamera.aspect);
                    float distance = height / (2 * Mathf.Tan(fov * Mathf.Deg2Rad * .5f));
                    from = to = screenCenter + Vector3.forward * distance;
                    look = target = screenCenter;
                    if (id == "reveal")
                    {
                        to = V(.7f, 2.1f, 8.4f); target = V(.25f, 1.45f, .1f); duration = 2.6f;
                    }
                    break;
                case "correction": from = V(-.7f, 1.85f, 4.5f); to = V(-.4f, 1.8f, 4.15f); look = target = V(-.35f, 1.65f, .35f); fov = 34; duration = 3.6f; break;
                case "party": from = V(.2f, 2.2f, 8.4f); to = PartyCamera; look = target = PartyLook; break;
                case "jam": from = V(.05f, 1.45f, 3.7f); to = V(.1f, 1.3f, 3.4f); look = target = V(.8f, 1.08f, .5f); duration = 2.6f; break;
                case "years": from = to = PartyCamera; look = target = PartyLook; break;
                case "return": from = V(.15f, 2.1f, 8.8f); to = V(.9f, 1.95f, 7.4f); duration = 4.8f; break;
                case "earth": from = V(1.1f, 1.6f, 7.5f); to = V(-.1f, 1.4f, 5.9f); look = target = V(0, 1.2f, 0); duration = 5.2f; break;
                case "launch": from = V(.86f, 1.72f, 3.8f); to = V(.74f, 1.62f, 3.45f); look = target = V(-.05f, 1.12f, .8f); duration = 1.5f; break;
                case "lab": from = V(-.8f, 2.1f, 8.8f); to = V(.1f, 1.9f, 7.8f); look = target = V(0, 1.45f, -.4f); duration = 6.4f; break;
                case "ready": from = to = V(.1f, 2.1f, 8.6f); look = target = V(0, 1.5f, -.4f); break;
                case "hearing": from = V(-.25f, 2.1f, 8.4f); to = V(-.6f, 2.0f, 7.6f); look = V(.05f, 1.4f, .35f); target = V(-.31f, 1.75f, .35f); duration = 5.75f; break;
                case "evidence": from = V(.72f, 2.23f, 7.2f); to = V(.23f, 2.0f, 6.7f); look = V(.25f, 1.28f, .35f); target = V(.22f, 1.45f, .6f); duration = 3.55f; break;
                case "reaction": from = V(-.88f, 1.95f, 6.15f); to = V(-1.42f, 1.82f, 4.85f); look = V(-.57f, 1.44f, .6f); target = V(-1.22f, 1.49f, .75f); duration = 8.5f; break;
                case "deflate": from = V(.32f, 1.76f, 4.3f); to = V(.33f, 1.49f, 4.26f); look = V(.35f, 1.45f, .55f); target = V(.42f, 1.04f, .55f); duration = 2.65f; break;
                case "recruit": from = to = V(-2.65f, 1.8f, 5); look = target = V(-2.7f, 1.35f, .8f); break;
                // Cutscene 1, The Welcome Baskets. Crew marks: Zorvax x -1.5, Nyxen x .35, Cadet B x 2.1.
                case "drought-alert": from = V(1.2f, 1.95f, 3.6f); to = V(1.0f, 1.9f, 4.7f); look = V(1.95f, 2.0f, -1.5f); target = V(1.2f, 1.6f, -.4f); duration = 2.6f; fov = 34; break;
                case "drought-welcome": from = V(-.9f, 1.75f, 4.4f); to = V(-1.05f, 1.7f, 3.9f); look = target = V(-1.45f, 1.45f, .85f); duration = 2.6f; fov = 34; break;
                case "drought-count": from = V(1.6f, 1.55f, 3.6f); to = V(1.65f, 1.5f, 3.3f); look = target = V(2.05f, 1.2f, -.1f); duration = 1.9f; fov = 34; break;
                case "drought-none": from = to = V(-1.0f, 1.7f, 3.6f); look = target = V(-1.45f, 1.5f, .85f); fov = 32; break;
                case "drought-how-many": from = V(1.65f, 1.3f, 3.2f); to = V(1.7f, 1.2f, 3.0f); look = V(2.05f, 1.45f, -.1f); target = V(2.05f, 1.9f, -.1f); duration = 1.9f; fov = 34; break;
                case "drought-all": from = V(.55f, 2.0f, 7.2f); to = V(.95f, 1.85f, 6.0f); look = target = V(.75f, 1.2f, .2f); duration = 3.4f; fov = 38; break;
            }
            // Time-lapse convention: the camera holds still while the years pass.
            float hold = id == "years" ? 2.7f : 0;
            float q = Mathf.Clamp01((t - hold) / (duration - hold));
            float p = reduced ? 1 : q * q * q * (q * (6 * q - 15) + 10);
            if (!reduced && id == "reveal") p = Mathf.SmoothStep(0, 1, (t - .12f) / 2.16f);
            filmCamera.transform.position = Vector3.Lerp(from, to, p);
            if (!reduced && id == "signal")
            {
                float distance = Mathf.Exp(Mathf.Lerp(Mathf.Log(from.z), Mathf.Log(to.z), p));
                filmCamera.transform.position = new Vector3(Mathf.Lerp(from.x, to.x, p), Mathf.Lerp(from.y, to.y, p), distance);
            }
            if (!reduced && id == "home")
                filmCamera.transform.position += new Vector3(Mathf.Sin(t * .16f) * .09f, Mathf.Sin(t * .22f) * .025f, 0);
            filmCamera.transform.LookAt(Vector3.Lerp(look, target, p));
            if (id == "evidence" && t >= MonitorChangesPage)
            {
                var center = screen.transform.position;
                float insert = reduced ? 1 : Mathf.SmoothStep(0, 1, Mathf.InverseLerp(MonitorChangesPage, MonitorChangesPage + 1.2f, t));
                filmCamera.transform.position = Vector3.Lerp(filmCamera.transform.position, center + V(0, .32f, 3.95f), insert);
                filmCamera.transform.LookAt(Vector3.Lerp(target, center, insert));
            }
            if (!reduced && id == "launch")
            {
                float impulse = Pulse(t, .77f, .11f);
                filmCamera.transform.position += new Vector3(Mathf.Sin(t * 93) * .025f, Mathf.Cos(t * 77) * .012f, 0) * impulse;
                filmCamera.transform.Rotate(0, 0, Spring(t - .74f, 12, 24) * .7f, Space.Self);
            }
            float aspect = filmCamera.aspect;
            if (aspect < 1.55f && id != "apollo" && id != "reveal" && id != "jam" && id != "deflate")
                fov = 2 * Mathf.Atan(Mathf.Tan(fov * Mathf.Deg2Rad / 2) * 1.55f / Mathf.Max(.5f, aspect)) * Mathf.Rad2Deg;
            filmCamera.fieldOfView = fov;
        }
        // ---- Discovery cutscenes ----
        void EvaluateCutscene(string id, float t, bool reduced)
        {
            // The 1969 television doubles as the ship's alert monitor.
            bool alert = id == "drought-alert";
            float flash = alert && !reduced ? (Mathf.Repeat(t * 2.2f, 1) < .55f ? 1 : .35f) : .55f;
            televisionMaterial.mainTexture = null;
            televisionMaterial.color = new Color(.78f * flash, .07f * flash, .05f * flash, 1);
            monitorTitle.gameObject.SetActive(true);
            monitorSubtitle.gameObject.SetActive(true);
            monitorTitle.text = "SOIL MOISTURE";
            monitorTitle.characterSize = titleSize * .62f;
            monitorSubtitle.text = "DECLINING";
            if (alert) practical.color = Color.Lerp(practicalColor, new Color(1, .15f, .1f), .6f * flash);

            bool pile = id == "drought-all";
            BuildBaskets();
            basketPile.gameObject.SetActive(pile);
            if (!pile) return;
            const float startY = 4.2f, gravity = 9.8f;
            for (int i = 0; i < BasketCount; i++)
            {
                // Four layers stacked over Cadet B's mark; each basket drops a beat after the last.
                int layer = i < 7 ? 0 : i < 12 ? 1 : i < 16 ? 2 : 3;
                int first = layer == 0 ? 0 : layer == 1 ? 7 : layer == 2 ? 12 : 16;
                int count = layer == 0 ? 7 : layer == 1 ? 5 : layer == 2 ? 4 : 2;
                float angle = (i - first) / (float)count * Mathf.PI * 2 + layer * .9f;
                float radius = .44f - layer * .1f;
                var rest = new Vector3(2.1f + Mathf.Cos(angle) * radius, layer * .27f, .08f + Mathf.Sin(angle) * radius * .7f);
                var restRotation = Quaternion.Euler((Hash(i, 4) - .5f) * 24, Hash(i, 5) * 360, (Hash(i, 6) - .5f) * 24);
                float age = reduced ? 10 : t - (1.0f + i * .055f);
                var item = basketItems[i];
                item.gameObject.SetActive(age >= 0);
                if (age < 0) continue;
                float fallTime = Mathf.Sqrt(2 * (startY - rest.y) / gravity);
                float y, settle;
                if (age < fallTime) { y = startY - .5f * gravity * age * age; settle = 0; }
                else
                {
                    float landed = age - fallTime;
                    y = rest.y + Mathf.Abs(Mathf.Sin(landed * 14)) * Mathf.Exp(-landed * 7) * .12f;
                    settle = Mathf.SmoothStep(0, 1, landed / .25f);
                }
                var spin = Quaternion.Euler(age * 210 * (Hash(i, 7) - .5f), age * 140, age * 170 * (Hash(i, 8) - .5f));
                item.localPosition = new Vector3(rest.x, y, rest.z);
                item.localRotation = Quaternion.Slerp(spin, Quaternion.identity, settle) * restRotation;
                item.localScale = Vector3.one * (.9f + Hash(i, 9) * .2f);
            }
        }
        void BuildBaskets()
        {
            if (basketPile != null) return;
            var source = cannon.GetComponentInChildren<Renderer>(true).sharedMaterial;
            wicker = new Material(source) { name = "Wicker (cutscene)", color = new Color(.72f, .52f, .3f) };
            wicker.SetFloat("_Metallic", 0); wicker.SetFloat("_Glossiness", .15f);
            ribbon = new Material(source) { name = "Ribbon (cutscene)", color = new Color(.86f, .3f, .34f) };
            ribbon.SetFloat("_Metallic", 0); ribbon.SetFloat("_Glossiness", .35f);
            basketPile = new GameObject("Welcome baskets (cutscene)").transform;
            basketPile.SetParent(bridge.transform, false);
            basketItems = new Transform[BasketCount];
            for (int i = 0; i < BasketCount; i++)
            {
                var basket = new GameObject("Basket " + i).transform;
                basket.SetParent(basketPile, false);
                Part(PrimitiveType.Cylinder, basket, V(0, .1f, 0), V(.34f, .1f, .34f), wicker);
                Part(PrimitiveType.Cylinder, basket, V(0, .205f, 0), V(.37f, .014f, .37f), wicker);
                Part(PrimitiveType.Cube, basket, V(-.16f, .3f, 0), V(.025f, .2f, .025f), wicker);
                Part(PrimitiveType.Cube, basket, V(.16f, .3f, 0), V(.025f, .2f, .025f), wicker);
                Part(PrimitiveType.Cube, basket, V(0, .4f, 0), V(.345f, .025f, .025f), wicker);
                Part(PrimitiveType.Sphere, basket, V(.09f, .25f, .07f), V(.1f, .1f, .1f), ribbon);
                basketItems[i] = basket;
            }
            basketPile.gameObject.SetActive(false);
        }
        static GameObject Part(PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            var collider = go.GetComponent<Collider>();
            if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        void OnDestroy()
        {
            if (Application.isPlaying)
            {
                Destroy(evidenceLampMaterial);
                Destroy(evidenceMetal);
            }
            else
            {
                DestroyImmediate(evidenceLampMaterial);
                DestroyImmediate(evidenceMetal);
            }
            if (environment != null) environment.Teardown();
            if (wicker != null) Destroy(wicker);
            if (ribbon != null) Destroy(ribbon);
            if (streamerMaterial != null)
            {
                if (Application.isPlaying) Destroy(streamerMaterial); else DestroyImmediate(streamerMaterial);
            }
            if (archiveBackdropMaterial != null)
            {
                if (Application.isPlaying) Destroy(archiveBackdropMaterial); else DestroyImmediate(archiveBackdropMaterial);
            }
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

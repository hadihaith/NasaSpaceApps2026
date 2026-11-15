using System.IO;
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

namespace Homecoming
{
    [RequireComponent(typeof(Camera))]
    public sealed class IntroPlayback : MonoBehaviour
    {
        public const string ScenePath = "Assets/Scenes/Intro.unity";
        public const string VideoFile = "auk-intro.mp4";
        const float FallbackDuration = 3.2f;
        const float StallTimeout = 4.5f;
        const string MuteKey = "homecoming.unity.mute";

        VideoPlayer video;
        AsyncOperation storyLoad;
        Texture2D logo, astronaut, spaceApps;
        float fallbackTime, stalledFor;
        bool fallback, paused, finished, muted;
        bool wasRunningInBackground;
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        string videoPath;
        bool native;
        double previousTime = -1;
        [DllImport("HomecomingIntro")] static extern int HomecomingIntroStart(string path, int muted);
        [DllImport("HomecomingIntro")] static extern int HomecomingIntroState();
        [DllImport("HomecomingIntro")] static extern double HomecomingIntroTime();
        [DllImport("HomecomingIntro")] static extern void HomecomingIntroPause(int paused);
        [DllImport("HomecomingIntro")] static extern void HomecomingIntroMute(int muted);
        [DllImport("HomecomingIntro")] static extern void HomecomingIntroStop();
#else
        long lastFrame = -1;
        Material screen;
#endif

        void Start()
        {
            wasRunningInBackground = Application.runInBackground;
            Application.runInBackground = true;
            muted = PlayerPrefs.GetInt(MuteKey, 0) == 1;
            string source = Path.Combine(Application.streamingAssetsPath, VideoFile);
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
            videoPath = source;
#else
            screen = new Material(Resources.Load<Shader>("Shaders/IntroScreen"));
            PrepareUnityVideo(source);
#endif
        }

#if UNITY_EDITOR || !UNITY_STANDALONE_OSX
        void PrepareUnityVideo(string source)
        {
            video = gameObject.AddComponent<VideoPlayer>();
            video.playOnAwake = false;
            video.renderMode = VideoRenderMode.APIOnly;
            video.audioOutputMode = VideoAudioOutputMode.Direct;
            video.waitForFirstFrame = true;
            video.prepareCompleted += OnPrepared;
            video.loopPointReached += OnEnded;
            video.errorReceived += OnError;

            video.url = source;
            video.Prepare();
        }

        void OnPrepared(VideoPlayer player)
        {
            ApplyMute();
            if (!paused && !finished && !fallback) player.Play();
        }

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            Texture frame = video != null && video.frame >= 0 ? video.texture : null;
            if (frame == null || fallback)
            {
                Graphics.Blit(source, destination);
                return;
            }

            float aspect = (float)source.width * frame.height / (source.height * frame.width);
            screen.SetVector("_Fit", new Vector4(Mathf.Max(1, aspect), Mathf.Max(1, 1 / aspect), 0, 0));
            Graphics.Blit(frame, destination, screen);
        }
#endif

        void ApplyMute()
        {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
            if (native) HomecomingIntroMute(muted ? 1 : 0);
#endif
            if (video != null && video.isPrepared && video.audioTrackCount > 0) video.SetDirectAudioMute(0, muted);
        }

        void OnEnded(VideoPlayer player) => Complete();

        void OnError(VideoPlayer player, string error)
        {
            Debug.LogWarning("AUK intro unavailable. Showing the logo card. " + error);
            ShowStill();
        }

        void ShowStill()
        {
            if (finished || fallback) return;
            fallback = true;
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
            if (native)
            {
                HomecomingIntroStop();
                native = false;
            }
#endif
            if (video != null) video.Stop();
            logo = Resources.Load<Texture2D>("Film/auk");
            astronaut = Resources.Load<Texture2D>("Film/astronaut");
            spaceApps = Resources.Load<Texture2D>("Film/space-apps-kuwait");
            PreloadStory();
        }

        void PreloadStory()
        {
            if (storyLoad != null) return;
            storyLoad = SceneManager.LoadSceneAsync(StorySession.StoryScene, LoadSceneMode.Single);
            storyLoad.allowSceneActivation = finished;
        }

        void Complete()
        {
            if (finished) return;
            finished = true;
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
            if (native) HomecomingIntroPause(1);
#endif
            if (video != null && video.isPrepared) video.Pause();
            PreloadStory();
            storyLoad.allowSceneActivation = true;
        }

        void Update()
        {
            if (finished) return;
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                Complete();
                return;
            }
            if (Input.GetKeyDown(KeyCode.M))
            {
                muted = !muted;
                ApplyMute();
                PlayerPrefs.SetInt(MuteKey, muted ? 1 : 0);
                PlayerPrefs.Save();
            }
            if (Input.GetKeyDown(KeyCode.Space))
            {
                paused = !paused;
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
                if (native) HomecomingIntroPause(paused ? 1 : 0);
#endif
                if (!fallback && video != null && video.isPrepared)
                {
                    if (paused) video.Pause(); else video.Play();
                }
            }
            if (paused) return;
            if (fallback)
            {
                fallbackTime += Time.unscaledDeltaTime;
                if (fallbackTime >= FallbackDuration) Complete();
                return;
            }
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
            UpdateNative();
#else
            long frame = video.frame;
            if (frame >= 0 && frame != lastFrame)
            {
                lastFrame = frame;
                stalledFor = 0;
                // Give the decoder the first frame before starting Unity's background load.
                PreloadStory();
            }
            else if ((stalledFor += Time.unscaledDeltaTime) >= StallTimeout) ShowStill();
#endif
        }

#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        void UpdateNative()
        {
            if (!native)
            {
                try
                {
                    int result = HomecomingIntroStart(videoPath, muted ? 1 : 0);
                    native = result == 1;
                    if (result < 0 || (!native && (stalledFor += Time.unscaledDeltaTime) >= StallTimeout))
                        ShowStill();
                }
                catch (System.DllNotFoundException error)
                {
                    Debug.LogWarning("Native intro unavailable. " + error.Message);
                    ShowStill();
                }
                return;
            }

            int state = HomecomingIntroState();
            if (state == 2) { Complete(); return; }
            if (state < 0) { ShowStill(); return; }
            double time = HomecomingIntroTime();
            if (state == 1 && time > previousTime)
            {
                previousTime = time;
                stalledFor = 0;
                PreloadStory();
            }
            else if ((stalledFor += Time.unscaledDeltaTime) >= StallTimeout) ShowStill();
        }
#endif

        void OnDestroy()
        {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
            if (native) HomecomingIntroStop();
#else
            if (screen != null) Destroy(screen);
#endif
            Application.runInBackground = wasRunningInBackground;
        }

        void OnGUI()
        {
            if (!fallback || Event.current.type != EventType.Repaint) return;
            float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
            float x = (Screen.width - 1600 * scale) * .5f;
            float y = (Screen.height - 900 * scale) * .5f;
            Rect Area(float left, float top, float width, float height) =>
                new Rect(x + left * scale, y + top * scale, width * scale, height * scale);

            if (logo != null) GUI.DrawTexture(Area(1009, 248, 112, 230), logo, ScaleMode.ScaleToFit);
            if (astronaut != null) GUI.DrawTexture(Area(362, 197, 445, 406), astronaut, ScaleMode.ScaleToFit);
            if (spaceApps != null) GUI.DrawTexture(Area(879, 510, 372, 154), spaceApps, ScaleMode.ScaleToFit);
        }
    }
}

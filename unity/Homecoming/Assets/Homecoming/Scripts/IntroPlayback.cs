using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

namespace Homecoming
{
    [RequireComponent(typeof(Camera))]
    public sealed class IntroPlayback : MonoBehaviour
    {
        public const string ScenePath = "Assets/Scenes/Intro.unity";
        public const string VideoPath = "Film/auk-intro";
        const float FallbackDuration = 3.2f;
        const float StallTimeout = 4.5f;
        const string MuteKey = "homecoming.unity.mute";

        VideoPlayer video;
        AsyncOperation storyLoad;
        Texture2D logo, astronaut, spaceApps;
        float fallbackTime, stalledFor;
        long lastFrame = -1;
        bool fallback, paused, finished, muted;

        void Start()
        {
            video = gameObject.AddComponent<VideoPlayer>();
            video.playOnAwake = false;
            video.renderMode = VideoRenderMode.APIOnly;
            video.audioOutputMode = VideoAudioOutputMode.Direct;
            video.waitForFirstFrame = true;
            muted = PlayerPrefs.GetInt(MuteKey, 0) == 1;
            video.prepareCompleted += OnPrepared;
            video.loopPointReached += OnEnded;
            video.errorReceived += OnError;

            var clip = Resources.Load<VideoClip>(VideoPath);
            if (clip == null || PlayerPrefs.GetInt("homecoming.unity.reduced", 0) == 1)
            {
                ShowStill();
                return;
            }
            video.clip = clip;
            video.Prepare();
        }

        void OnPrepared(VideoPlayer player)
        {
            ApplyMute();
            if (!paused && !finished) player.Play();
        }

        void ApplyMute()
        {
            if (video.isPrepared && video.audioTrackCount > 0) video.SetDirectAudioMute(0, muted);
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
            video.Stop();
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
            if (video.isPrepared) video.Pause();
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
                if (!fallback && video.isPrepared)
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
            long frame = video.frame;
            if (frame >= 0 && frame != lastFrame)
            {
                lastFrame = frame;
                stalledFor = 0;
                // Give the decoder the first frame before starting Unity's background load.
                PreloadStory();
            }
            else if ((stalledFor += Time.unscaledDeltaTime) >= StallTimeout) ShowStill();
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
            float x = (Screen.width - 1600 * scale) * .5f;
            float y = (Screen.height - 900 * scale) * .5f;
            Rect Area(float left, float top, float width, float height) =>
                new Rect(x + left * scale, y + top * scale, width * scale, height * scale);

            if (fallback)
            {
                if (logo != null) GUI.DrawTexture(Area(1009, 248, 112, 230), logo, ScaleMode.ScaleToFit);
                if (astronaut != null) GUI.DrawTexture(Area(362, 197, 445, 406), astronaut, ScaleMode.ScaleToFit);
                if (spaceApps != null) GUI.DrawTexture(Area(879, 510, 372, 154), spaceApps, ScaleMode.ScaleToFit);
            }
            else if (video != null && video.frame >= 0 && video.texture != null)
                GUI.DrawTexture(Area(0, 0, 1600, 900), video.texture, ScaleMode.ScaleToFit, false);
        }
    }
}

using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Homecoming
{
    [DefaultExecutionOrder(-10000)]
    public sealed class GameRestart : MonoBehaviour
    {
        bool restarting;
        string restartError;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            var controller = new GameObject("Restart game");
            DontDestroyOnLoad(controller);
            controller.AddComponent<GameRestart>();
        }

        void OnEnable() => SceneManager.sceneLoaded += SceneLoaded;
        void OnDisable() => SceneManager.sceneLoaded -= SceneLoaded;

        void Update()
        {
            if (!restarting && Input.GetKeyDown(KeyCode.R)) Restart();
        }

        internal void Restart()
        {
            if (restarting) return;
            restarting = true;
            restartError = null;
            // LoadScene also completes a Story preload held by the intro video.
            SceneManager.LoadScene(IntroPlayback.ScenePath,LoadSceneMode.Single);
        }

        void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!restarting || scene.path != IntroPlayback.ScenePath) return;
            try
            {
                // Clear after unloading the previous scene so its last frame cannot restore a save.
                if (Directory.Exists(Application.persistentDataPath))
                    Directory.Delete(Application.persistentDataPath,true);
                PlayerPrefs.DeleteAll();
                StorySession.Reset();
                Time.timeScale = 1;
                AudioListener.pause = false;
                AudioListener.volume = 1;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                Input.ResetInputAxes();
                Debug.Log("GAME_RESTART_OK: local data cleared; opening video restarted.");
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                var intro = FindAnyObjectByType<IntroPlayback>();
                if (intro != null) intro.enabled = false;
                restartError = "Could not clear saved data. Press R to retry.";
                Debug.LogError(restartError+" "+error.Message);
            }
            finally { restarting = false; }
        }

        void OnGUI()
        {
            if (restartError == null) return;
            GUI.Box(new Rect(20,20,Screen.width-40,60),restartError);
        }
    }
}

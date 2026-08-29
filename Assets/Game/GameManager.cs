using System;
using System.Collections.Generic;
using Core;
using Core.Events;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game
{
    public enum GameState { Boot, MainMenu, Lobby, Playing, Paused }

    /// <summary>
    /// PURPOSE: Owns overall game state, scene transitions, and pause/time control - the
    ///          single thing other systems react to instead of polling each other
    ///          directly. Pausing uses reference-counted tokens so a pause menu, a
    ///          cutscene, and a dialogue system can each request a freeze without
    ///          stomping each other's resume.
    /// DEPENDENCIES: None.
    /// EVENTS PUBLISHED: OnStateChanged(GameState previous, GameState current); optionally
    ///                    raises the assigned onGamePaused/onGameResumed GameEvent assets
    ///                    for designer-wireable reactions (MenuManager, AudioManager ducking, ...).
    /// PUBLIC API: SetState, LoadScene, Pause(object), Resume(object), ForceResumeAll,
    ///             IsPaused, QuitGame, CurrentState
    /// </summary>
    public class GameManager : Singleton<GameManager>
    {
        [Header("Optional designer-facing hooks")]
        [Tooltip("Raised whenever the game enters/leaves the Paused state.")]
        [SerializeField] private GameEvent onGamePaused;
        [SerializeField] private GameEvent onGameResumed;

        public GameState CurrentState { get; private set; } = GameState.Boot;
        public event Action<GameState, GameState> OnStateChanged;
        public bool IsPaused => _pauseSources.Count > 0;

        public int money = 0;
        public int quota = 200;
        public int roundDeaths = 0;

        private readonly HashSet<object> _pauseSources = new();
        private GameState _stateBeforePause;
        private float _previousTimeScale = 1f;

        public void SetState(GameState next)
        {
            if (CurrentState == next) return;
            
            GameState previous = CurrentState;
            CurrentState = next;
            OnStateChanged?.Invoke(previous, next);
        }

        public void LoadScene(string sceneName) => SceneManager.LoadScene(sceneName);

        /// <summary>
        /// Requests a pause. `source` identifies the requester (commonly `this`) - pass the
        /// SAME reference to Resume(), or the token is never released and the game stays
        /// paused. Calling Pause() twice with the same source is a harmless no-op. Safe to
        /// call alongside other pause sources - the game only returns to its previous state
        /// once every source has called Resume().
        /// </summary>
        public void Pause(object source)
        {
            if (source == null)
            {
                Debug.LogError("[GameManager] Pause() requires a non-null source.");
                return;
            }

            bool wasPaused = IsPaused;
            if (_pauseSources.Add(source) && !wasPaused)
            {
                _previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                _stateBeforePause = CurrentState;
                SetState(GameState.Paused);
                onGamePaused?.Raise();
            }
        }

        /// <summary>Releases one source's pause request. A mismatched or repeated call (a source that never paused, or already released) is a safe no-op.</summary>
        public void Resume(object source)
        {
            if (source == null || !_pauseSources.Remove(source)) return;

            if (!IsPaused)
            {
                Time.timeScale = _previousTimeScale;
                SetState(_stateBeforePause);
                onGameResumed?.Raise();
            }
        }

        /// <summary>Emergency escape hatch that clears every outstanding pause token regardless of source. Prefer matched Pause/Resume calls; reach for this only around things like a hard scene reload.</summary>
        public void ForceResumeAll()
        {
            if (_pauseSources.Count == 0) return;
            
            _pauseSources.Clear();
            Time.timeScale = _previousTimeScale;
            SetState(_stateBeforePause);
            onGameResumed?.Raise();
        }

        public void QuitGame()
        {
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
        }
    }
}
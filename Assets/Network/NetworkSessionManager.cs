using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Events;
using Game;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Network
{
    /// <summary>
    /// PURPOSE: Online session management (create/browse/join/leave/start) via Unity
    ///          Gaming Services' Multiplayer Services SDK. Lives next to the persistent
    ///          NetworkManager rather than inside GameManager, since it owns the online
    ///          connection itself, not general game state. Creating or joining a session
    ///          with .WithRelayNetwork() automatically configures and starts Netcode for
    ///          GameObjects' NetworkManager.
    /// DEPENDENCIES: A linked Unity Gaming Services project with Authentication and
    ///               Multiplayer Services enabled, plus a persistent NetworkManager +
    ///               UnityTransport (both instantiated by Bootstrapper).
    /// EVENTS PUBLISHED: OnSessionJoined(ISession), OnSessionLeft, OnSessionPlayersChanged,
    ///                    OnGameStarted, OnLocalClientEnteredGame, OnSessionError(string);
    ///                    optionally raises the assigned onSessionJoined/onSessionLeft
    ///                    GameEvent assets.
    /// PUBLIC API: CreateSessionAsync, JoinSessionByCodeAsync, JoinSessionByIdAsync,
    ///             QueryAvailableSessionsAsync, StartGame, LeaveSessionAsync,
    ///             CloseSessionAsync, CurrentSession, IsInSession, IsSessionHost,
    ///             HasEnteredGame
    /// </summary>
    public class NetworkSessionManager : Core.Singleton<NetworkSessionManager>
    {
        [Header("Session defaults")]
        [Tooltip("Default max players for a hosted session, if none is specified when creating one.")]
        [SerializeField] private int defaultMaxPlayers = 4;

        [Header("Optional designer-facing hooks")]
        [SerializeField] private GameEvent onSessionJoined;
        [SerializeField] private GameEvent onSessionLeft;

        public ISession CurrentSession { get; private set; }
        public bool IsInSession => CurrentSession != null;
        public bool IsSessionHost => CurrentSession.IsHost;

        /// <summary>True once this client has finished loading into the gameplay scene for
        /// the current session.</summary>
        public bool HasEnteredGame { get; private set; }

        public event Action<ISession> OnSessionJoined;
        public event Action OnSessionLeft;
        public event Action OnSessionPlayersChanged;
        /// <summary>Fires only on the host the moment it calls StartGame().</summary>
        public event Action OnGameStarted;
        /// <summary>Fires on every client (host and joiners alike) once its own local scene
        /// load into the gameplay scene finishes.</summary>
        public event Action OnLocalClientEnteredGame;
        public event Action<string> OnSessionError;

        private Task<bool> _servicesReadyTask;

        private void Start()
        {
            if (IsDuplicate) return;

            // Subscribe to OnLoadComplete as early as the API allows
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientStarted += HandleNetworkStarted;
                NetworkManager.Singleton.OnServerStarted += HandleNetworkStarted;
            }

            _ = EnsureServicesReadyAsync(); // warm up sign-in so the first Create/Join click doesn't have to wait
        }

        private void OnApplicationQuit()
        {
            if (CurrentSession != null)
                _ = CurrentSession.LeaveAsync(); // best-effort - the process may close before this finishes
        }

        private void HandleNetworkStarted()
        {
            if (NetworkManager.Singleton.SceneManager == null) return;

            NetworkManager.Singleton.SceneManager.OnLoadComplete -= HandleSceneLoadComplete;
            NetworkManager.Singleton.SceneManager.OnLoadComplete += HandleSceneLoadComplete;

            NetworkManager.Singleton.SceneManager.OnSynchronizeComplete -= HandleSynchronizeComplete;
            NetworkManager.Singleton.SceneManager.OnSynchronizeComplete += HandleSynchronizeComplete;
        }
        
        private void HandleSceneLoadComplete(ulong clientId, string sceneName, LoadSceneMode mode)
            => TryEnterGame(clientId);

        private void HandleSynchronizeComplete(ulong clientId)
            => TryEnterGame(clientId);

        private void TryEnterGame(ulong clientId)
        {
            if (!IsInSession || clientId != NetworkManager.Singleton.LocalClientId || HasEnteredGame) return;

            GameManager.Instance.SetState(GameState.Playing);
            HasEnteredGame = true;
            OnLocalClientEnteredGame?.Invoke();
        }

        /// <summary>Signs in anonymously and initializes Unity Gaming Services. Safe to call repeatedly.</summary>
        private Task<bool> EnsureServicesReadyAsync() => _servicesReadyTask ??= InitializeServicesAsync();

        private async Task<bool> InitializeServicesAsync()
        {
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();

                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkSessionManager] Failed to initialize Unity Gaming Services: {e}");
                OnSessionError?.Invoke("Couldn't connect to online services - check your internet connection.");
                _servicesReadyTask = null;
                return false;
            }
        }

        /// <summary>Creates and hosts a new session. Starts the Netcode connection automatically once created.</summary>
        public async Task<bool> CreateSessionAsync(string sessionName, int maxPlayers = 0, bool isPrivate = false)
        {
            if (!await EnsureServicesReadyAsync()) return false;

            try
            {
                var options = new SessionOptions
                {
                    Name = string.IsNullOrWhiteSpace(sessionName) ? "New Session" : sessionName,
                    MaxPlayers = maxPlayers > 0 ? maxPlayers : defaultMaxPlayers,
                    IsPrivate = isPrivate,
                }.WithRelayNetwork();

                IHostSession session = await MultiplayerService.Instance.CreateSessionAsync(options);
                BindSession(session);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkSessionManager] CreateSessionAsync failed: {e}");
                OnSessionError?.Invoke("Couldn't create a session - try again in a moment.");
                return false;
            }
        }

        /// <summary>Joins a session using the short join code the host shares (CurrentSession.Code once hosting).</summary>
        public async Task<bool> JoinSessionByCodeAsync(string joinCode)
        {
            if (string.IsNullOrWhiteSpace(joinCode))
            {
                OnSessionError?.Invoke("Enter a join code first.");
                return false;
            }
            if (!await EnsureServicesReadyAsync()) return false;

            try
            {
                ISession session = await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode.Trim());
                BindSession(session);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkSessionManager] JoinSessionByCodeAsync failed: {e}");
                OnSessionError?.Invoke("Couldn't join that session - check the code and try again.");
                return false;
            }
        }

        /// <summary>Joins a session picked from QueryAvailableSessionsAsync's results.</summary>
        public async Task<bool> JoinSessionByIdAsync(string sessionId)
        {
            if (!await EnsureServicesReadyAsync()) return false;

            try
            {
                ISession session = await MultiplayerService.Instance.JoinSessionByIdAsync(sessionId);
                BindSession(session);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkSessionManager] JoinSessionByIdAsync failed: {e}");
                OnSessionError?.Invoke("Couldn't join that session - it may have just closed.");
                return false;
            }
        }

        /// <summary>Public session browser - lists open, non-private sessions. This is the server browser: call it on Refresh, and on Open() if nothing's joined yet.</summary>
        public async Task<IList<ISessionInfo>> QueryAvailableSessionsAsync()
        {
            if (!await EnsureServicesReadyAsync()) return Array.Empty<ISessionInfo>();

            try
            {
                var results = await MultiplayerService.Instance.QuerySessionsAsync(new QuerySessionsOptions());
                return results.Sessions;
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkSessionManager] QueryAvailableSessionsAsync failed: {e}");
                OnSessionError?.Invoke("Couldn't fetch the session list.");
                return Array.Empty<ISessionInfo>();
            }
        }

        /// <summary>Host-only: moves everyone into the gameplay scene through Netcode's scene manager, which propagates the load to every connected client.</summary>
        public void StartGame(string gameplaySceneName)
        {
            if (!IsSessionHost)
            {
                Debug.LogWarning("[NetworkSessionManager] Only the session host can start the game.");
                return;
            }
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                Debug.LogError("[NetworkSessionManager] NetworkManager isn't listening - can't start the game.");
                return;
            }

            NetworkManager.Singleton.SceneManager.LoadScene(gameplaySceneName, LoadSceneMode.Single);
            OnGameStarted?.Invoke();
        }

        public async Task LeaveSessionAsync()
        {
            if (CurrentSession == null) return;

            try { await CurrentSession.LeaveAsync(); }
            catch (Exception e) { Debug.LogError($"[NetworkSessionManager] LeaveSessionAsync failed: {e}"); }
            finally { UnbindSession(); }
        }

        /// <summary>Host-only: closes the session for everyone instead of just leaving it.</summary>
        public async Task CloseSessionAsync()
        {
            if (CurrentSession is not IHostSession hostSession)
            {
                await LeaveSessionAsync();
                return;
            }

            try { await hostSession.DeleteAsync(); }
            catch (Exception e) { Debug.LogError($"[NetworkSessionManager] CloseSessionAsync failed: {e}"); }
            finally { UnbindSession(); }
        }

        private void BindSession(ISession session)
        {
            CurrentSession = session;
            session.PlayerJoined += HandleSessionPlayerJoined;
            session.PlayerLeaving += HandleSessionPlayerLeft;
            session.RemovedFromSession += HandleRemovedFromSession;

            OnSessionJoined?.Invoke(session);
            onSessionJoined?.Raise();
        }

        private void UnbindSession()
        {
            if (CurrentSession == null) return;
            CurrentSession.PlayerJoined -= HandleSessionPlayerJoined;
            CurrentSession.PlayerLeaving -= HandleSessionPlayerLeft;
            CurrentSession.RemovedFromSession -= HandleRemovedFromSession;

            CurrentSession = null;
            HasEnteredGame = false;
            OnSessionLeft?.Invoke();
            onSessionLeft?.Raise();
        }

        private void HandleSessionPlayerJoined(string playerId) => OnSessionPlayersChanged?.Invoke();
        private void HandleSessionPlayerLeft(string playerId) => OnSessionPlayersChanged?.Invoke();

        private void HandleRemovedFromSession()
        {
            UnbindSession();
            OnSessionError?.Invoke("You exited the session.");
        }
    }
}
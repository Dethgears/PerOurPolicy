using System.Collections.Generic;
using Game;
using Unity.Services.Multiplayer;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UI;
using Network;

namespace Menu.Menus
{
    /// <summary>
    /// Create/browse/join sessions and see who's connected before the host starts the
    /// game. NetworkSessionManager owns the connection; GameManager owns game state;
    /// this menu is where the two get bridged for the local player, including closing
    /// itself once this client has actually entered the gameplay scene. Drive it through
    /// MenuManager like any other MenuBase screen - don't call Open()/Close() on it directly.
    /// </summary>
    public class LobbyMenu : MenuBase
    {
        [Header("Create / Join")]
        [SerializeField] private GameObject findSessionPanel;
        [SerializeField] private TMP_InputField sessionNameField;
        [SerializeField] private TMP_InputField joinCodeField;
        [SerializeField] private Button createButton;
        [SerializeField] private Button joinByCodeButton;
        [SerializeField] private Button refreshButton;
        [SerializeField] private TMP_Text statusText;

        [Header("Public session list (the server browser)")] 
        [SerializeField] private Transform sessionListContent;
        [SerializeField] private SessionListItemView sessionListItemPrefab;

        [Header("In-session")]
        [SerializeField] private GameObject inSessionPanel;
        [SerializeField] private TMP_Text sessionCodeText;
        [SerializeField] private Transform playerListContent;
        [SerializeField] private TMP_Text playerListRowPrefab;
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button leaveButton;
        [SerializeField] private string gameplaySceneName = "DefaultScene";

        private readonly List<SessionListItemView> _spawnedListItems = new();
        private readonly List<TMP_Text> _spawnedPlayerRows = new();

        protected override void Awake()
        {
            base.Awake();
            createButton.onClick.AddListener(OnCreateClicked);
            joinByCodeButton.onClick.AddListener(OnJoinByCodeClicked);
            refreshButton.onClick.AddListener(RefreshSessionList);
            startGameButton.onClick.AddListener(OnStartGameClicked);
            leaveButton.onClick.AddListener(OnLeaveClicked);
        }

        public override void Open()
        {
            base.Open();
            NetworkSessionManager.Instance.OnSessionJoined += HandleSessionJoined;
            NetworkSessionManager.Instance.OnSessionLeft += HandleSessionLeft;
            NetworkSessionManager.Instance.OnSessionPlayersChanged += RefreshPlayerList;
            NetworkSessionManager.Instance.OnSessionError += ShowStatus;
            NetworkSessionManager.Instance.OnLocalClientEnteredGame += HandleLocalClientEnteredGame;

            RefreshForCurrentSessionState();
            if (!NetworkSessionManager.Instance.IsInSession) RefreshSessionList();
        }

        public override void Close()
        {
            base.Close();
            NetworkSessionManager.Instance.OnSessionJoined -= HandleSessionJoined;
            NetworkSessionManager.Instance.OnSessionLeft -= HandleSessionLeft;
            NetworkSessionManager.Instance.OnSessionPlayersChanged -= RefreshPlayerList;
            NetworkSessionManager.Instance.OnSessionError -= ShowStatus;
            NetworkSessionManager.Instance.OnLocalClientEnteredGame -= HandleLocalClientEnteredGame;
        }

        /// <summary>Fires locally once THIS client (host or joiner) has actually finished loading into the gameplay scene - see NetworkSessionManager.OnLocalClientEnteredGame.</summary>
        private void HandleLocalClientEnteredGame() => MenuManager.Instance.CloseMenu();

        private async void OnCreateClicked()
        {
            SetButtonsInteractable(false);
            string name = string.IsNullOrWhiteSpace(sessionNameField.text) ? null : sessionNameField.text;
            await NetworkSessionManager.Instance.CreateSessionAsync(name);
            SetButtonsInteractable(true);
        }

        private async void OnJoinByCodeClicked()
        {
            SetButtonsInteractable(false);
            await NetworkSessionManager.Instance.JoinSessionByCodeAsync(joinCodeField.text);
            SetButtonsInteractable(true);
        }

        /// <summary>The server browser: fetches open sessions and spawns one SessionListItemView row per result.</summary>
        private async void RefreshSessionList()
        {
            ClearSessionList();
            ShowStatus("Refreshing...");

            var sessions = await NetworkSessionManager.Instance.QueryAvailableSessionsAsync();
            ShowStatus(sessions.Count == 0 ? "No open sessions found." : string.Empty);

            foreach (var info in sessions)
            {
                var item = Instantiate(sessionListItemPrefab, sessionListContent);
                item.Bind(info, OnSessionListItemJoinClicked);
                _spawnedListItems.Add(item);
            }
        }

        private async void OnSessionListItemJoinClicked(ISessionInfo info)
        {
            SetButtonsInteractable(false);
            await NetworkSessionManager.Instance.JoinSessionByIdAsync(info.Id);
            SetButtonsInteractable(true);
        }

        private void OnStartGameClicked() => NetworkSessionManager.Instance.StartGame(gameplaySceneName);

        private async void OnLeaveClicked()
        {
            SetButtonsInteractable(false);
            await NetworkSessionManager.Instance.LeaveSessionAsync();
            SetButtonsInteractable(true);
        }

        private void HandleSessionJoined(ISession session)
        {
            GameManager.Instance.SetState(GameState.Lobby);
            RefreshForCurrentSessionState();
        }

        private void HandleSessionLeft()
        {
            GameManager.Instance.SetState(GameState.MainMenu);
            RefreshForCurrentSessionState();
        }

        private void RefreshForCurrentSessionState()
        {
            bool inSession = NetworkSessionManager.Instance.IsInSession;
            findSessionPanel.SetActive(!inSession);
            inSessionPanel.SetActive(inSession);
            if (!inSession) return;

            sessionCodeText.text = $"Code: {NetworkSessionManager.Instance.CurrentSession.Code}";
            startGameButton.gameObject.SetActive(NetworkSessionManager.Instance.IsSessionHost);
            RefreshPlayerList();
        }

        private void RefreshPlayerList()
        {
            foreach (var row in _spawnedPlayerRows) Destroy(row.gameObject);
            _spawnedPlayerRows.Clear();

            var session = NetworkSessionManager.Instance.CurrentSession;
            if (session == null) return;

            foreach (var player in session.Players)
            {
                var row = Instantiate(playerListRowPrefab, playerListContent);
                row.text = player.Id == session.CurrentPlayer.Id ? "You" : player.Id;
                _spawnedPlayerRows.Add(row);
            }
        }

        private void ClearSessionList()
        {
            foreach (var item in _spawnedListItems) Destroy(item.gameObject);
            _spawnedListItems.Clear();
        }

        private void ShowStatus(string message) => statusText.text = message;

        private void SetButtonsInteractable(bool interactable)
        {
            createButton.interactable = interactable;
            joinByCodeButton.interactable = interactable;
            refreshButton.interactable = interactable;
        }
    }
}
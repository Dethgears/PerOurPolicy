using System;
using TMPro;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.UI;

namespace Menu
{
    /// <summary>
    /// One row in the public session browser list. Put this on its own small prefab
    /// (name text + player-count text + a Join button) and assign that prefab to
    /// LobbyMenu's Session List Item Prefab field.
    /// </summary>
    public class SessionListItemView : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text playerCountText;
        [SerializeField] private Button joinButton;

        public void Bind(ISessionInfo info, Action<ISessionInfo> onJoinClicked)
        {
            nameText.text = string.IsNullOrEmpty(info.Name) ? "Unnamed session" : info.Name;
            playerCountText.text = $"{info.MaxPlayers-info.AvailableSlots}/{info.MaxPlayers}";

            joinButton.onClick.RemoveAllListeners();
            joinButton.onClick.AddListener(() => onJoinClicked(info));
        }
    }
}
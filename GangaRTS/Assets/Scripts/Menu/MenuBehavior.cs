﻿using UnityEngine;
using Photon.Realtime;
using Photon.Pun;
using System.Collections.Generic;
using PowerUI;
using System.Collections;
using System;
using System.Text.RegularExpressions;

namespace GangaGame
{
    public class GameInfo
    {
        public const string PLAYER_READY = "IsPlayerReady";
        public const string PLAYER_TEAM = "PlayerTeam";
        public const bool PLAYER_LOADED_LEVEL = false;
    }

    public class MenuBehavior : MonoBehaviourPunCallbacks
    {
        string gameVersion = "0.1";

        private Dictionary<string, RoomInfo> cachedRoomList;
        private List<Player> playerListEntries = new List<Player>();

        public TextAsset MenuHTMLFile;
        public TextAsset LobbyHTMLFile;
        public TextAsset RoomHTMLFile;

        private float timeToStart = 10.0f;
        private float timerToStart = 0.0f;
        private float sendMessageTimer = 0.0f;
        private bool timerActive = false;

        private bool isPlayerReady = false;
        private bool gameStarted = false;
        private int playerTeam = 1;

        [PunRPC]
        private void Update()
        {
            if (timerActive && !gameStarted)
            {
                timerToStart -= Time.deltaTime;
                sendMessageTimer -= Time.deltaTime;
                if(sendMessageTimer <= 0)
                {
                    AddMessasgeToChat(String.Format("Game will start in {0:F0}", timerToStart));
                    sendMessageTimer = 1.0f;
                }
                if(timerToStart <= 0)
                {
                    AddMessasgeToChat(String.Format("Game started..."));
                    gameStarted = true;

                    PhotonNetwork.CurrentRoom.IsOpen = false;
                    PhotonNetwork.CurrentRoom.IsVisible = false;

                    PhotonNetwork.LoadLevel("test1");
                }
            }
            string className = "";
            if (PowerUI.CameraPointer.All[0].ActiveOver != null)
                className = PowerUI.CameraPointer.All[0].ActiveOver.className;

            if (UnityEngine.Input.GetMouseButtonUp(0))
                if (className.Contains("multiplayer"))
                    CreateConnectDialog();
                else if (className.Contains("dialogOK") && !PhotonNetwork.IsConnected)
                {
                    string userName = UI.document.getElementsByClassName("inputName")[0].innerText;
                    if (userName.Length <= 0)
                    {
                        UI.document.getElementsByClassName("error")[0].innerText = "Wrong username";
                    }
                    else
                    {
                        DeleteDialog();
                        Connect(userName);
                    }
                }
                else if (className.Contains("backToMenu") && !PhotonNetwork.InRoom)
                {
                    PhotonNetwork.Disconnect();
                    CreateMessage("Disconnecting...");
                }
                else if (className.Contains("refreshMenu"))
                    UpdateLobbyListView();

                else if (className.Contains("createRoomDialog"))
                    CreateRoomMenu();

                else if (className.Contains("deleteDialog"))
                    DeleteDialog();

                else if (className.Contains("dialogOK") && PhotonNetwork.IsConnected)
                {
                    string roomName = UI.document.getElementsByClassName("inputName")[0].innerText;
                    string maxplayers = UI.document.getElementsByClassName("inputMaxplayers")[0].innerText;
                    if (!Regex.Match(maxplayers, @"^[2-9][0-9]*$", RegexOptions.IgnoreCase).Success)
                    {
                        UI.document.getElementsByClassName("error")[0].innerText = "Wrong maxplayers value";
                    }
                    else if (roomName.Length <= 0)
                    {
                        UI.document.getElementsByClassName("error")[0].innerText = "Empty room name";
                    }
                    else
                    {
                        PhotonNetwork.JoinOrCreateRoom(roomName, new RoomOptions { MaxPlayers = (byte)int.Parse(maxplayers) }, TypedLobby.Default);
                        DeleteDialog();
                        CreateMessage("Creating...");
                    }
                }
                else if (PhotonNetwork.InRoom && className.Contains("backToMenu"))
                {
                    PhotonNetwork.LeaveRoom();
                    CreateMessage("Leaving...");
                }
                else if (PhotonNetwork.InRoom && (UnityEngine.Input.GetKeyDown(KeyCode.Return) || className.Contains("chatSend")))
                {
                    string chatInput = UI.document.getElementsByClassName("chatInput")[0].getAttribute("value");
                    if(chatInput.Length > 0)
                    {
                        UI.document.getElementsByClassName("chatInput")[0].setAttribute("value", "");
                        GetComponent<PhotonView>().RPC(
                            "AddMessasgeToChat", PhotonTargets.All, new string[1] { String.Format("{0}: {1}", PhotonNetwork.NickName, chatInput) }
                            );
                    }
                }
                else if (PhotonNetwork.InRoom && className.Contains("setReady"))
                {
                    SetReady(!isPlayerReady);
                }
                else if (PhotonNetwork.InRoom && className.Contains("teamSelect"))
                {
                    int teamSelect = int.Parse(UI.document.getElementsByClassName("teamSelect")[0].getAttribute("value"));
                    SetTeam(teamSelect);
                }
        }

        void CreateConnectDialog()
        {
            UI.document.Run("CreateConnectDialog");
        }
        void CreateRoomMenu()
        {
            UI.document.Run("CreateRoomDialog");
        }
        void DeleteDialog()
        {
            UI.document.getElementsByClassName("crateRoomForm")[0].remove();
        }
        void CreateMessage(string message)
        {
            UI.document.Run("CreateMessage", new string[1] { message });
        }
        void AddInfo(string info)
        {
            UI.document.Run("AddInfo", new string[1] { info });
        }
        void CreateUser(string username, int playerTeam, bool playerReady, bool canEdit)
        {
            if (UI.document.getElementsByClassName("players").length <= 0)
                return;
            
            UI.document.Run("CreateUser", username, playerTeam, playerReady, canEdit);
        }
        [PunRPC]
        public void AddMessasgeToChat(string message)
        {
            if (UI.document.getElementsByClassName("chat").length <= 0)
                return;

            UI.document.Run("CreateMessage", new string[1] { message });
        }
        
        public override void OnConnectedToMaster()
        {
            UI.document.innerHTML = LobbyHTMLFile.text;
            UpdateLobbyListView();
        }
        
        public override void OnDisconnected(DisconnectCause cause)
        {
            gameStarted = false;
            UI.document.innerHTML = MenuHTMLFile.text;
        }

        public override void OnRoomListUpdate(List<RoomInfo> roomList)
        {
            UpdateCachedRoomList(roomList);
        }
        
        public override void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged)
        {
            UpdateRoomView();
        }

        public override void OnJoinedRoom()
        {
            isPlayerReady = false;
            foreach (Player p in PhotonNetwork.PlayerList)
            {
                playerListEntries.Add(p);
            }
            UI.document.innerHTML = RoomHTMLFile.text;
            UpdateRoomView();

            //Hashtable props = new Hashtable
            //{
            //    {AsteroidsGame.PLAYER_LOADED_LEVEL, false}
            //};
            //PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        }

        public override void OnLeftRoom()
        {
            gameStarted = false;
            playerListEntries.Clear();
            UI.document.innerHTML = LobbyHTMLFile.text;
            UpdateLobbyListView();
        }

        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            CreateMessage(string.Format("{0} connected to room", newPlayer.NickName));
            playerListEntries.Add(newPlayer);
            UpdateRoomView();
        }

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            CreateMessage(string.Format("{0} disconnected from room", otherPlayer.NickName));
            playerListEntries.Remove(otherPlayer);
            UpdateRoomView();
        }

        public override void OnJoinRandomFailed(short returnCode, string message)
        {
            Debug.Log("OnJoinRandomFailed: " + message);
        }

        public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
        {
            UpdateRoomView();
        }

        private void UpdateCachedRoomList(List<RoomInfo> roomList)
        {
            Debug.Log("UpdateCachedRoomList: " + roomList.Count);
            foreach (RoomInfo info in roomList)
            {
                // Remove room from cached room list if it got closed, became invisible or was marked as removed
                if (!info.IsOpen || !info.IsVisible || info.RemovedFromList)
                {
                    if (cachedRoomList.ContainsKey(info.Name))
                    {
                        cachedRoomList.Remove(info.Name);
                    }

                    continue;
                }

                // Update cached room info
                if (cachedRoomList.ContainsKey(info.Name))
                {
                    cachedRoomList[info.Name] = info;
                }
                // Add new room info to cache
                else
                {
                    cachedRoomList.Add(info.Name, info);
                }
            }
        }

        private void UpdateRoomView()
        {
            if (UI.document.getElementsByClassName("players").length <= 0)
                return;
            else
                UI.document.getElementsByClassName("players")[0].innerHTML = "";

            bool allPlayersIsReady = true;
            foreach (Player player in playerListEntries)
            {
                object playerReadyObd;
                bool playerReady = false;
                if (player.CustomProperties.TryGetValue(GameInfo.PLAYER_READY, out playerReadyObd))
                    playerReady = (bool)playerReadyObd;

                object playerTeamObd;
                int playerTeam = 1;
                if (player.CustomProperties.TryGetValue(GameInfo.PLAYER_TEAM, out playerTeamObd))
                    playerTeam = (int)playerTeamObd;

                bool canEdit = false;
                if (player == PhotonNetwork.LocalPlayer)
                    canEdit = true;

                CreateUser(player.NickName, playerTeam, playerReady, canEdit);

                if (!playerReady)
                    allPlayersIsReady = false;
            }
            if (allPlayersIsReady)
            {
                timerToStart = timeToStart;
                AddMessasgeToChat(String.Format("All players is ready, game will start in {0:F0} seconds", timerToStart));
                sendMessageTimer = 0.0f;
                timerActive = true;
            }
            else if(timerActive)
            {
                AddMessasgeToChat(String.Format("Timer is stopped"));
                timerActive = false;
            }

            if (UI.document.getElementsByClassName("roomInfo").length <= 0)
                return;
            else
                UI.document.getElementsByClassName("roomInfo")[0].innerHTML = "";

            AddInfo(String.Format("Room name: {0}", PhotonNetwork.CurrentRoom.Name));
            AddInfo(String.Format("Max players: {0}", PhotonNetwork.CurrentRoom.MaxPlayers));
        }

        void SetReady(bool newState)
        {
            isPlayerReady = newState;
            ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable() {
                { GameInfo.PLAYER_READY, isPlayerReady }//,  { GameInfo.PLAYER_LOADED_LEVEL, false}
            };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        }

        void SetTeam(int newTeam)
        {
            playerTeam = newTeam;
            ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable() {
                { GameInfo.PLAYER_TEAM, playerTeam}
            };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        }

        private void UpdateLobbyListView()
        {
            var menuBlock = UI.document.getElementsByClassName("menu")[0];
            menuBlock.innerText = "";

            if(cachedRoomList != null)
                foreach (RoomInfo info in cachedRoomList.Values)
                {
                    var roomBlock = UI.document.createElement("div");
                    roomBlock.className = "menuElement";
                    roomBlock.innerText = info.Name;
                    menuBlock.appendChild(roomBlock);
                }

        }

        void Awake()
        {
            PhotonNetwork.AutomaticallySyncScene = true;
        }
        
        public void Connect(string userName)
        {
            if (PhotonNetwork.IsConnected)
            {
                OnConnectedToMaster();
            }
            else
            {
                PhotonNetwork.GameVersion = gameVersion;
                PhotonNetwork.NickName = userName;
                PhotonNetwork.ConnectUsingSettings();
                CreateMessage("Connecting...");
            }
        }

        private bool CheckPlayersReady()
        {
            if (!PhotonNetwork.IsMasterClient)
            {
                return false;
            }

            foreach (Player player in PhotonNetwork.PlayerList)
            {
                object playerReady;
                if (player.CustomProperties.TryGetValue(GameInfo.PLAYER_READY, out playerReady))
                {
                    if (!(bool)playerReady)
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }

            return true;
        }
    }
}
using System;

/// <summary>
/// Defines all client-side events used for internal communication via the EventBus.
/// These events are not commands to be sent to the server.
/// </summary>
public static class Events
{
    //================ G A M E P L A Y   E V E N T S ================
    
    /// <summary>
    /// Groups events related to the flow of a game hand.
    /// GamePlayController should publish these events after receiving and processing server data.
    /// UI/View layers should listen to these events to update the interface.
    /// </summary>
    public static class Game
    {
        // /// <summary>Fired when the main game information is ready.</summary>
        // public struct OnGameInfoReady { public GameInfo GameInfo; }

        // /// <summary>Fired when a new player joins the table.</summary>
        // public struct OnPlayerJoined { public PlayerInfo NewPlayer; }
        
        // /// <summary>Fired when a player's balance at the table is updated.</summary>
        // public struct OnPlayerBalanceChanged { public PlayerUpdateGoldModel GoldInfo; }
    }


    //================ L O B B Y   E V E N T S ================

    /// <summary>
    /// Groups events related to actions taken in the Lobby.
    /// </summary>
    public static class Lobby
    {
        //================ UI / I N P U T   E V E N T S ================
        /// <summary>
        /// Groups events related to user interface interactions.
        /// </summary>
        public static class UI
        {
            public struct ClickOpenTournamentPopup {}
            // public struct ClickTournamentListItem { public TournamentDetail TournamentDetail; }
        }

        // /// <summary>Request to log in using an access token.</summary>
        // public struct OnLoginWithTokenRequested { public string Token; }

        // /// <summary>Request to create a new room.</summary>
        // public struct OnCreateRoomRequested { public CreateRoomModel RoomData; }

        // /// <summary>Request to join a private game.</summary>
        // public struct OnJoinPrivateGameRequested { public JoinPrivateGameModel GameData; }

        // /// <summary>Request to find a room by its ID.</summary>
        // public struct OnFindRoomByIdRequested { public int RoomId; }
        
        // /// <summary>Request to view the leaderboard for a tournament.</summary>
        // public struct OnTournamentLeaderboardRequested { public TournametLeaderBoardDetailsModel RequestData; }

        // /// <summary>Request to create a new tournament.</summary>
        // public struct OnCreateTournamentRequested { public CreateTournamentModel TournamentData; }
    }
}

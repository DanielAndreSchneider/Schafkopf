using Microsoft.AspNetCore.SignalR;
using Schafkopf.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace Schafkopf.Hubs
{
    public class SchafkopfHub : Hub
    {
        public readonly static Game Game = new Game();
        public async Task SendChatMessage(string message)
        {
            String user = ((Player)Context.Items["player"]).Name;
            foreach (String connectionId in Game.GetPlayersConnectionIds())
            {
                await Clients.Client(connectionId).SendAsync("ReceiveChatMessage", user, message);
            }
        }
        public async Task Announce(bool wantToPlay)
        {
            Player player = (Player)Context.Items["player"];
            if (Game.CurrentGameState == State.Announce && player == Game.PlayingPlayers[Game.ActionPlayer])
            {
                foreach (String connectionId in player.GetConnectionIds())
                {
                    await Clients.Client(connectionId).SendAsync("CloseAnnounceModal");
                }
                await Game.PlayingPlayers[Game.ActionPlayer].Announce(wantToPlay, this, Game);
                Game.ActionPlayer = (Game.ActionPlayer + 1) % 4;
                if (Game.ActionPlayer == Game.StartPlayer)
                {
                    Game.CurrentGameState = State.AnnounceGameType;
                    await Game.SendAskForGameType(this);
                    return;
                }
                await Game.SendAskAnnounce(this);
            }
        }

        public async Task AnnounceGameType(string gameTypeString)
        {
            if (Game.CurrentGameState != State.AnnounceGameType)
            {
                return;
            }
            GameType gameType = GameType.Ramsch;
            switch (gameTypeString)
            {
                case "Sauspiel":
                    gameType = GameType.Sauspiel;
                    break;
                case "Wenz":
                    gameType = GameType.Wenz;
                    break;
                case "Solo":
                    gameType = GameType.Farbsolo;
                    break;
            }
            Player player = (Player)Context.Items["player"];
            if (player == Game.PlayingPlayers[Game.ActionPlayer])
            {
                await Game.PlayingPlayers[Game.ActionPlayer].AnnounceGameType(gameType, this, Game);
                Game.ActionPlayer = (Game.ActionPlayer + 1) % 4;
                await Game.SendAskForGameType(this);
                foreach (String connectionId in player.GetConnectionIds())
                {
                    await Clients.Client(connectionId).SendAsync("CloseAnnounceGameTypeModal");
                }
            }
        }
        public async Task AnnounceGameColor(string gameColorString)
        {
            if (Game.CurrentGameState != State.AnnounceGameColor)
            {
                return;
            }
            Player player = (Player)Context.Items["player"];
            if (player != Game.Leader)
            {
                return;
            }
            Color color = Color.Eichel;
            switch (gameColorString)
            {
                case "Eichel":
                    color = Color.Eichel;
                    break;
                case "Gras":
                    color = Color.Gras;
                    break;
                case "Herz":
                    color = Color.Herz;
                    break;
                case "Schellen":
                    color = Color.Schellen;
                    break;
            }
            Game.Leader.AnnouncedColor = color;
            await Game.StartGame(this);
            foreach (String connectionId in player.GetConnectionIds())
            {
                await Clients.Client(connectionId).SendAsync("CloseGameColorModal");
            }
        }

        public async Task PlayCard(String cardId)
        {
            Color cardColor;
            Enum.TryParse(cardId.Split("-")[0], true, out cardColor);
            int cardNumber = Int16.Parse(cardId.Split("-")[1]);
            await Game.PlayCard((Player)Context.Items["player"], cardColor, cardNumber, this);
        }


        public async Task ReconnectPlayer(string userId)
        {
            Player player = Game.Players.Single(p => p.Id == userId);
            player.AddConnectionId(this);
            await Clients.Caller.SendAsync("ReceiveSystemMessage", $"Willkommen zurück {player.Name}");
            if (Game.CurrentGameState != State.Idle) {
                if (Game.PlayingPlayers.Contains(player))
                {
                    await Game.SendPlayerIsPlayingGameTypeAndColor(this, new List<string>() { Context.ConnectionId });
                    if (Game.Trick.Count == 0)
                    {
                        await Game.SendPlayerIsStartingTheRound(this, new List<string>() { Context.ConnectionId });
                    }
                    await player.SendHand(this);
                    await Game.Trick.SendTrick(this, Game);
                    await Game.SendPlayers(this);
                    // send modals
                    if (Game.CurrentGameState == State.Playing && Game.TrickCount == 8)
                    {
                        await Game.SendEndGameModal(this, new List<String>() { Context.ConnectionId });
                    }
                    foreach (Player p in Game.PlayingPlayers)
                    {
                        if (p.GetConnectionIds().Count == 0)
                        {
                            await Game.SendConnectionToPlayerLostModal(this, new List<String>() { Context.ConnectionId });
                            break;
                        }
                    }
                    if (Game.PlayingPlayers[Game.ActionPlayer] == player)
                    {
                        if (Game.CurrentGameState == State.Announce)
                        {
                            await Game.SendAskAnnounce(this);
                        }
                        else if (Game.CurrentGameState == State.AnnounceGameType)
                        {
                            await Game.SendAskForGameType(this);
                        }
                    }
                    if (Game.Leader == player && Game.CurrentGameState == State.AnnounceGameColor)
                    {
                        await Game.SendAskForGameColor(this);
                    }
                    // check if all players are connected again and close connectionLostModal for the other players
                    await Task.Delay(1000);
                    if (Game.PlayingPlayers.All(p => p.GetConnectionIds().Count > 0))
                    {
                        foreach (String connectionId in Game.GetPlayingPlayersConnectionIds())
                        {
                            await Clients.Client(connectionId).SendAsync("CloseGameOverModal");
                        }
                    }
                } else {
                    // else if is spectating: send hand, trick, modals
                    // else ask if player wants to spectate
                }
            } else
            {
                if (!Game.PlayingPlayers.Contains(player)) {
                    await Clients.Caller.SendAsync("AskWantToPlay");
                }
            }
        }
        public async Task AddPlayer(string userName)
        {
            Player player = new Player(userName, this);
            await Game.AddPlayer(player, this);
            await Clients.Caller.SendAsync("StoreUserId", player.Id);
            // send username
            // if game is running ask if player wants to spectate
        }

        public async Task PlayerWantsToPlay()
        {
            Player player = (Player)Context.Items["player"];
            await Game.PlayerPlaysTheGame(player, this);
            foreach (String connectionId in player.GetConnectionIds())
            {
                await Clients.Client(connectionId).SendAsync("CloseWantToPlayModal");
            }
        }

        public async Task PlayerWantsToPause()
        {
            Player player = (Player)Context.Items["player"];
            await Game.PlayerDoesNotPlayTheGame(player, this);
            foreach (String connectionId in player.GetConnectionIds())
            {
                await Clients.Client(connectionId).SendAsync("CloseWantToPlayModal");
            }
        }

        public async Task ResetGame()
        {
            await Game.Reset(this);
        }

        public async Task UpdatePlayingPlayers()
        {
            foreach (String connectionId in Game.GetPlayersConnectionIds())
            {
                await Clients.Client(connectionId).SendAsync("ReceiveSystemMessage", $"Playing Players: {String.Join(", ", Game.PlayingPlayers.Select(p => p.Name + " (" + p.GetConnectionIds().Count + ")"))}");
            }
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            foreach (Player player in Game.Players)
            {
                if (player.RemoveConnectionId(Context.ConnectionId))
                {
                    if (Game.PlayingPlayers.Contains(player) && player.GetConnectionIds().Count == 0)
                    {
                        if (Game.CurrentGameState == State.Idle)
                        {
                            Task asyncTask = Game.PlayerDoesNotPlayTheGame(player, this);
                        }
                        else
                        {
                            Task asyncTask = Game.SendConnectionToPlayerLostModal(this, Game.GetPlayingPlayersConnectionIds());
                            asyncTask = Game.ResetIfAllConnectionsLost(this);
                        }
                    }
                }
            }
            return base.OnDisconnectedAsync(exception);
        }
    }
}
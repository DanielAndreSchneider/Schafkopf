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
                foreach (String connectionId in player.GetConnectionIdsWithSpectators())
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
                foreach (String connectionId in player.GetConnectionIdsWithSpectators())
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
            foreach (String connectionId in player.GetConnectionIdsWithSpectators())
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
            if (!Game.Players.Any(p => p.Id == userId)) {
                await Clients.Caller.SendAsync("AskUsername");
                return;
            }
            Player player = Game.Players.Single(p => p.Id == userId);
            player.AddConnectionId(this);
            await Clients.Caller.SendAsync("ReceiveSystemMessage", $"Willkommen zurück {player.Name}");
            if (Game.CurrentGameState != State.Idle) {
                if (Game.PlayingPlayers.Contains(player))
                {
                    await Game.SendUpdatedGameState(player, this);
                    // check if all players are connected again and close connectionLostModal for the other players
                    await Task.Delay(1000);
                    if (Game.PlayingPlayers.All(p => p.GetConnectionIds().Count > 0))
                    {
                        foreach (String connectionId in Game.GetPlayingPlayersConnectionIds())
                        {
                            await Clients.Client(connectionId).SendAsync("CloseGameOverModal");
                        }
                    }
                }
                else if (Game.PlayingPlayers.Any(p => p.Spectators.Contains(player)))
                {
                    await Game.SendUpdatedGameState(Game.PlayingPlayers.Single(p => p.Spectators.Contains(player)), this);
                }
                else {
                    await Game.SendAskWantToSpectate(this, new List<string>() {Context.ConnectionId});
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
            await Clients.Caller.SendAsync("StoreUserId", player.Id);
            await Game.AddPlayer(player, this);
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
        public async Task PlayerWantsToSpectate(int playerId)
        {
            Player player = (Player)Context.Items["player"];
            foreach (String connectionId in player.GetConnectionIds())
            {
                await Clients.Client(connectionId).SendAsync("CloseWantToSpectateModal");
            }
            if (playerId >= 0 && playerId <= 4)
            {
                Game.PlayingPlayers[playerId].SpectatorsWaitingForApproval.Enqueue(player);
                await Game.PlayingPlayers[playerId].AskForApprovalToSpectate(this);
            }
        }

        public async Task AllowSpectator(bool allow) {
            Player player = (Player)Context.Items["player"];
            if (player.SpectatorsWaitingForApproval.Count == 0) {
                return;
            }
            Player spectator = player.SpectatorsWaitingForApproval.Dequeue();
            if (allow)
            {
                await player.AddSpectator(spectator, this, Game);
            }
            await player.AskForApprovalToSpectate(this);
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
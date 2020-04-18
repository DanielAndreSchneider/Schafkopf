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
        public static Dictionary<String, Game> Games = new Dictionary<String, Game>();
        public async Task SendChatMessage(string message)
        {
            Game game = ((Game)Context.Items["game"]);
            String user = ((Player)Context.Items["player"]).Name;
            foreach (String connectionId in game.GetPlayersConnectionIds())
            {
                await Clients.Client(connectionId).SendAsync("ReceiveChatMessage", user, message);
            }
        }
        public async Task Announce(bool wantToPlay)
        {
            Game game = ((Game)Context.Items["game"]);
            Player player = (Player)Context.Items["player"];
            if (game.CurrentGameState == State.Announce && player == game.PlayingPlayers[game.ActionPlayer])
            {
                foreach (String connectionId in player.GetConnectionIdsWithSpectators())
                {
                    await Clients.Client(connectionId).SendAsync("CloseAnnounceModal");
                }
                await game.PlayingPlayers[game.ActionPlayer].Announce(wantToPlay, this, game);
                game.ActionPlayer = (game.ActionPlayer + 1) % 4;
                if (game.ActionPlayer == game.StartPlayer)
                {
                    game.CurrentGameState = State.AnnounceGameType;
                    await game.SendAskForGameType(this);
                    return;
                }
                await game.SendAskAnnounce(this);
            }
        }

        public async Task AnnounceGameType(string gameTypeString)
        {
            Game game = ((Game)Context.Items["game"]);
            if (game.CurrentGameState != State.AnnounceGameType)
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
            if (player == game.PlayingPlayers[game.ActionPlayer])
            {
                await game.PlayingPlayers[game.ActionPlayer].AnnounceGameType(gameType, this, game);
                game.ActionPlayer = (game.ActionPlayer + 1) % 4;
                await game.SendAskForGameType(this);
                foreach (String connectionId in player.GetConnectionIdsWithSpectators())
                {
                    await Clients.Client(connectionId).SendAsync("CloseAnnounceGameTypeModal");
                }
            }
        }
        public async Task AnnounceGameColor(string gameColorString)
        {
            Game game = ((Game)Context.Items["game"]);
            if (game.CurrentGameState != State.AnnounceGameColor)
            {
                return;
            }
            Player player = (Player)Context.Items["player"];
            if (player != game.Leader)
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
            game.Leader.AnnouncedColor = color;
            await game.StartGame(this);
            foreach (String connectionId in player.GetConnectionIdsWithSpectators())
            {
                await Clients.Client(connectionId).SendAsync("CloseGameColorModal");
            }
        }

        public async Task PlayCard(String cardId)
        {
            Game game = ((Game)Context.Items["game"]);
            Color cardColor;
            Enum.TryParse(cardId.Split("-")[0], true, out cardColor);
            int cardNumber = Int16.Parse(cardId.Split("-")[1]);
            await game.PlayCard((Player)Context.Items["player"], cardColor, cardNumber, this);
        }


        public async Task ReconnectPlayer(string userId, string gameId)
        {
            if (!Games.Keys.Contains(gameId))
            {
                await Clients.Caller.SendAsync("AskUsername");
                return;
            }
            Game game = Games[gameId];
            if (!game.Players.Any(p => p.Id == userId))
            {
                await Clients.Caller.SendAsync("AskUsername");
                return;
            }
            Context.Items.Add("game", game);
            Player player = game.Players.Single(p => p.Id == userId);
            player.AddConnectionId(this);
            await Clients.Caller.SendAsync("ReceiveSystemMessage", $"Willkommen zurück {player.Name}");
            if (game.CurrentGameState != State.Idle)
            {
                if (game.PlayingPlayers.Contains(player))
                {
                    await game.SendUpdatedGameState(player, this);
                    // check if all players are connected again and close connectionLostModal for the other players
                    await Task.Delay(1000);
                    if (game.PlayingPlayers.All(p => p.GetConnectionIds().Count > 0))
                    {
                        foreach (String connectionId in game.GetPlayingPlayersConnectionIds())
                        {
                            await Clients.Client(connectionId).SendAsync("CloseGameOverModal");
                        }
                    }
                }
                else if (game.PlayingPlayers.Any(p => p.Spectators.Contains(player)))
                {
                    await game.SendUpdatedGameState(game.PlayingPlayers.Single(p => p.Spectators.Contains(player)), this);
                }
                else
                {
                    await game.SendAskWantToSpectate(this, new List<string>() { Context.ConnectionId });
                }
            }
            else
            {
                if (!game.PlayingPlayers.Contains(player))
                {
                    await Clients.Caller.SendAsync("AskWantToPlay");
                }
            }
        }
        public async Task AddPlayer(string userName, string gameId)
        {
            Game game;
            if (Games.Keys.Contains(gameId))
            {
                game = Games[gameId];
            }
            else
            {
                game = new Game();
                Games[gameId] = game;
            }
            Context.Items.Add("game", game);
            Player player = new Player(userName, this);
            await Clients.Caller.SendAsync("StoreUserId", player.Id);
            await game.AddPlayer(player, this);
        }

        public async Task PlayerWantsToPlay()
        {
            Game game = ((Game)Context.Items["game"]);
            Player player = (Player)Context.Items["player"];
            await game.PlayerPlaysTheGame(player, this);
            foreach (String connectionId in player.GetConnectionIds())
            {
                await Clients.Client(connectionId).SendAsync("CloseWantToPlayModal");
            }
        }
        public async Task PlayerWantsToSpectate(int playerId)
        {
            Game game = ((Game)Context.Items["game"]);
            Player player = (Player)Context.Items["player"];
            foreach (String connectionId in player.GetConnectionIds())
            {
                await Clients.Client(connectionId).SendAsync("CloseWantToSpectateModal");
            }
            if (playerId >= 0 && playerId <= 4)
            {
                game.PlayingPlayers[playerId].SpectatorsWaitingForApproval.Enqueue(player);
                await game.PlayingPlayers[playerId].AskForApprovalToSpectate(this);
            }
        }

        public async Task AllowSpectator(bool allow)
        {
            Game game = ((Game)Context.Items["game"]);
            Player player = (Player)Context.Items["player"];
            if (player.SpectatorsWaitingForApproval.Count == 0)
            {
                return;
            }
            Player spectator = player.SpectatorsWaitingForApproval.Dequeue();
            if (allow)
            {
                await player.AddSpectator(spectator, this, game);
            }
            await player.AskForApprovalToSpectate(this);
        }

        public async Task PlayerWantsToPause()
        {
            Game game = ((Game)Context.Items["game"]);
            Player player = (Player)Context.Items["player"];
            await game.PlayerDoesNotPlayTheGame(player, this);
            foreach (String connectionId in player.GetConnectionIds())
            {
                await Clients.Client(connectionId).SendAsync("CloseWantToPlayModal");
            }
        }

        public async Task ResetGame()
        {
            Game game = ((Game)Context.Items["game"]);
            await game.Reset(this);
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            if (Context.Items.Keys.Contains("game") && Context.Items.Keys.Contains("player"))
            {
                Game game = ((Game)Context.Items["game"]);
                foreach (Player player in game.Players)
                {
                    if (player.RemoveConnectionId(Context.ConnectionId))
                    {
                        if (game.PlayingPlayers.Contains(player) && player.GetConnectionIds().Count == 0)
                        {
                            if (game.CurrentGameState == State.Idle)
                            {
                                Task asyncTask = game.PlayerDoesNotPlayTheGame(player, this);
                            }
                            else
                            {
                                Task asyncTask = game.SendConnectionToPlayerLostModal(this, game.GetPlayingPlayersConnectionIds());
                                asyncTask = game.ResetIfAllConnectionsLost(this);
                            }
                        }
                    }
                }
            }
            return base.OnDisconnectedAsync(exception);
        }
    }
}
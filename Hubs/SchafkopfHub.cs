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
            if (message == "/restart")
            {
                await game.Reset(this);
                return;
            }
            String user = ((Player)Context.Items["player"]).Name;
            if (message.StartsWith("/kick"))
            {
                Player playerToKick = game.Players.Single(p => p.Name == message.Split("/kick ")[1]);
                foreach (String connectionId in playerToKick.GetConnectionIds())
                {
                    await Clients.Client(connectionId).SendAsync("ReceiveKicked", user);
                }
                return;
            }
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
                await game.SendPlayers(this);
                if (game.PlayingPlayers.All(p => p.WantToPlayAnswered))
                {
                    game.CurrentGameState = State.AnnounceGameType;
                    await game.SendAskForGameType(this);
                    return;
                }
                await game.SendAskAnnounce(this);
            }
            if (game.CurrentGameState == State.AnnounceHochzeit)
            {
                if (player.HandTrumpCount(GameType.Ramsch, Color.Herz) == 1 && !player.HasBeenAskedToOfferMarriage)
                {
                    foreach (String connectionId in player.GetConnectionIdsWithSpectators())
                    {
                        await Clients.Client(connectionId).SendAsync("CloseAnnounceModal");
                    }
                    player.HasBeenAskedToOfferMarriage = true;
                    if (wantToPlay)
                    {
                        game.AnnouncedGame = GameType.Hochzeit;
                        game.Leader = player;
                        foreach (String connectionId in game.GetPlayingPlayersConnectionIds())
                        {
                            await Clients.Client(connectionId).SendAsync("ReceiveChatMessage", player.Name, "Wer will mich heiraten?");
                        }
                    }
                    await game.SendAskAnnounceHochzeit(this);
                }
                else if (game.AnnouncedGame == GameType.Hochzeit && !player.HasAnsweredMarriageOffer)
                {
                    player.HasAnsweredMarriageOffer = true;
                    if (wantToPlay)
                    {
                        foreach (String connectionId in game.GetPlayingPlayersConnectionIds())
                        {
                            await Clients.Client(connectionId).SendAsync("CloseAnnounceModal");
                            await Clients.Client(connectionId).SendAsync("ReceiveChatMessage", player.Name, "Ich will!");
                            await Clients.Client(connectionId).SendAsync("ReceiveSystemMessage", $"{game.Leader.Name} und {player.Name} haben geheiratet");
                        }
                        game.CurrentGameState = State.HochzeitExchangeCards;
                        game.HusbandWife = player;
                        game.ActionPlayer = game.PlayingPlayers.IndexOf(game.HusbandWife);
                        await game.SendAskExchangeCards(this, game.HusbandWife.GetConnectionIdsWithSpectators());
                    }
                    else
                    {
                        foreach (String connectionId in player.GetConnectionIdsWithSpectators())
                        {
                            await Clients.Client(connectionId).SendAsync("CloseAnnounceModal");
                        }
                        await game.SendAskAnnounceHochzeit(this);
                    }
                }
                await game.SendPlayers(this);
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
                if (gameType == GameType.Sauspiel && !await player.IsSauspielPossible(this))
                {
                    return;
                }
                await game.PlayingPlayers[game.ActionPlayer].AnnounceGameType(gameType, this, game);
                game.ActionPlayer = (game.ActionPlayer + 1) % 4;
                await game.SendPlayers(this);
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
            if (game.AnnouncedGame == GameType.Sauspiel)
            {
                if (!await player.IsSauspielOnColorPossible(color, this))
                {
                    return;
                }
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
            if (player.GetConnectionIds().Count == 1)
            {
                Task asyncTask = game.SendPlayersInfo(this);
            }
            await Clients.Caller.SendAsync("ReceiveSystemMessage", $"Willkommen zurück {player.Name}");
            await game.SendPlayers(this);
            if (game.CurrentGameState != State.Idle)
            {
                if (game.PlayingPlayers.Contains(player))
                {
                    await game.SendUpdatedGameState(player, this, new List<String> { Context.ConnectionId });
                    // check if all players are connected again and close connectionLostModal for the other players
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
                    await game.SendUpdatedGameState(
                        game.PlayingPlayers.Single(p => p.Spectators.Contains(player)),
                        this,
                        new List<String> { Context.ConnectionId });
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
                    if (game.Players.Where((p => p.GetConnectionIds().Count > 0 && p.Playing)).ToList().Count <= 4)
                    {
                        await game.PlayerPlaysTheGame(player, this);
                    }
                    else
                    {
                        await game.SendAskWantToPlay(this, new List<string>() { Context.ConnectionId });
                    }
                }
            }
        }
        public async Task AddPlayer(string userName, string gameId)
        {
            Game game;
            Player player;
            String error = "";
            // rename user
            if (Context.Items.Keys.Contains("player"))
            {
                player = (Player)Context.Items["player"];
                game = (Game)Context.Items["game"];
                if (userName != player.Name && game.Players.Where(p => p.Name == userName).ToList().Count > 0)
                {
                    error = $"Der Name \"{userName}\" ist bereits vergeben!";
                }
                else if (userName.ToLower() == "system")
                {
                    error = "Dein Name darf nicht \"System\" sein!";
                }
                if (error != "")
                {
                    await Clients.Caller.SendAsync("ReceiveSystemMessage", $"Error: {error}");
                    return;
                }
                player.Name = userName;
                await Clients.Caller.SendAsync("StoreUser", player.Id, player.Name);
                await game.SendPlayers(this);
                return;
            }

            if (Games.Keys.Contains(gameId))
            {
                game = Games[gameId];
            }
            else
            {
                game = new Game();
                Games[gameId] = game;
            }
            if (game.Players.Where(p => p.Name == userName).ToList().Count > 0)
            {
                player = game.Players.Single(p => p.Name == userName);
                // Assume identity of existing user if it has no more active connections
                if (player.GetConnectionIds().Count == 0)
                {
                    string newUserId = System.Guid.NewGuid().ToString();
                    player.Id = newUserId;
                    await Clients.Caller.SendAsync("StoreUser", player.Id, player.Name);
                    await ReconnectPlayer(newUserId, gameId);
                    return;
                }
                error = $"Der Name \"{userName}\" ist bereits vergeben!";
            }
            else if (userName.ToLower() == "system")
            {
                error = "Dein Name darf nicht \"System\" sein!";
            }
            if (error != "")
            {
                await Clients.Caller.SendAsync("ReceiveSystemMessage", $"Error: {error}");
                return;
            }
            Context.Items.Add("game", game);
            player = new Player(userName, this);
            await Clients.Caller.SendAsync("StoreUser", player.Id, player.Name);
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
            else
            {
                await game.SendAskWantToSpectate(this, spectator.GetConnectionIds());
                foreach (String connectionId in spectator.GetConnectionIds())
                {
                    await Clients.Client(connectionId).SendAsync(
                        "ReceiveSystemMessage",
                        $"Error: {player.Name} will nicht, dass du zuschaust"
                    );
                }
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

        public async Task ShowLastTrick(bool showLastTrick)
        {
            Game game = ((Game)Context.Items["game"]);
            Player player = (Player)Context.Items["player"];
            if (showLastTrick && game.LastTrick != null)
            {
                await game.LastTrick.SendTrick(this, game, new List<string>() { Context.ConnectionId });
                await game.SendLastTrickButton(this, new List<string>() { Context.ConnectionId }, LastTrickButtonState.hide);
            }
            else
            {
                await game.Trick.SendTrick(this, game, new List<string>() { Context.ConnectionId });
                await game.SendLastTrickButton(this, new List<string>() { Context.ConnectionId }, LastTrickButtonState.show);
            }
        }

        public async Task TakeTrick()
        {
            Game game = ((Game)Context.Items["game"]);
            Player player = (Player)Context.Items["player"];
            if (game.Trick.Count == 4 && game.Trick.GetWinner() == player)
            {
                player.TakeTrick(game.Trick);
                game.TrickCount++;
                if (game.TrickCount == 8)
                {
                    await game.SendEndGameModal(this, game.GetPlayingPlayersConnectionIds());
                }

                game.ActionPlayer = game.PlayingPlayers.FindIndex(p => p == player);
                await game.SendPlayers(this);
                game.LastTrick = game.Trick;
                game.Trick = new Trick(game, game.ActionPlayer);
                await game.Trick.SendTrick(this, game, game.GetPlayingPlayersConnectionIds());
                await game.SendTakeTrickButton(this, game.GetPlayingPlayersConnectionIds());
                await game.SendPlayerIsStartingTheRound(this, game.GetPlayingPlayersConnectionIds());
            }
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            if (Context.Items.Keys.Contains("game") && Context.Items.Keys.Contains("player"))
            {
                Game game = ((Game)Context.Items["game"]);
                foreach (Player player in game.Players)
                {
                    if (player.RemoveConnectionId(Context.ConnectionId) && player.GetConnectionIds().Count == 0)
                    {
                        Task asyncTask = game.SendPlayersInfo(this);
                        asyncTask = game.SendPlayers(this);
                        asyncTask = game.PlayerDoesNotPlayTheGame(player, this);
                        if (game.PlayingPlayers.Contains(player))
                        {
                            if (game.CurrentGameState != State.Idle)
                            {
                                asyncTask = game.SendConnectionToPlayerLostModal(this, game.GetPlayingPlayersConnectionIds());
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
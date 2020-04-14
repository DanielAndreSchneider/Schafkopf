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
            await Clients.All.SendAsync("ReceiveChatMessage", user, message);
        }
        public async Task Announce(bool wantToPlay)
        {
            Player player = (Player)Context.Items["player"];
            if (player == Game.PlayingPlayers[Game.ActionPlayer])
            {
                await Game.PlayingPlayers[Game.ActionPlayer].Announce(wantToPlay, this);
                Game.ActionPlayer = (Game.ActionPlayer + 1) % 4;
                if (Game.ActionPlayer == Game.StartPlayer)
                {
                    await AskForGameType();
                    return;
                }
                foreach (String connectionId in Game.PlayingPlayers[Game.ActionPlayer].GetConnectionIds())
                {
                    await Clients.Client(connectionId).SendAsync("AskAnnounce");
                }
            }
        }

        public async Task AnnounceGameType(string gameTypeString)
        {
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
                await Game.PlayingPlayers[Game.ActionPlayer].AnnounceGameType(gameType, this);
                Game.ActionPlayer = (Game.ActionPlayer + 1) % 4;
                await AskForGameType();
            }
        }
        public async Task AnnounceGameColor(string gameColorString)
        {
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
            if (Game.AnnouncedGame == GameType.Farbsolo)
            {
                await Clients.All.SendAsync(
                    "ReceiveSystemMessage",
                    $"{Game.Leader.Name} spielt {color}-{Game.AnnouncedGame}"
                );
            }
            else if (Game.AnnouncedGame == GameType.Sauspiel)
            {
                await Clients.All.SendAsync(
                    "ReceiveSystemMessage",
                    $"{Game.Leader.Name} spielt auf die {color} Sau"
                );
            }
            await Game.StartGame(this);
        }

        public async Task PlayCard(String cardId)
        {
            Color cardColor;
            Enum.TryParse(cardId.Split("-")[0], true, out cardColor);
            int cardNumber = Int16.Parse(cardId.Split("-")[1]);
            await Game.PlayCard((Player)Context.Items["player"], cardColor, cardNumber, this);
        }

        public async Task AskForGameType()
        {
            for (int i = 0; i < 4; i++)
            {
                if (Game.PlayingPlayers[Game.ActionPlayer].WantToPlay)
                {
                    // game type not anounced
                    if (Game.PlayingPlayers[Game.ActionPlayer].AnnouncedGameType == GameType.Ramsch)
                    {
                        foreach (String connectionId in Game.PlayingPlayers[Game.ActionPlayer].GetConnectionIds())
                        {
                            await Clients.Client(connectionId).SendAsync("AskGameType");
                        }
                    }
                    // game type already anounnced for everyone
                    else
                    {
                        // decide who plays and ask for color
                        Game.DecideWhoIsPlaying();
                        await AskForColor();
                    }
                    return;
                }
                Game.ActionPlayer = (Game.ActionPlayer + 1) % 4;
            }
            // no one wants to play => it's a ramsch
            await Clients.All.SendAsync(
                "ReceiveSystemMessage",
                $"Es wird geramscht!"
            );
            await Game.StartGame(this);
        }
        public async Task AskForColor()
        {
            // Leader has to choose a color he wants to play with or a color to escort his solo
            if (Game.AnnouncedGame == GameType.Sauspiel || Game.AnnouncedGame == GameType.Farbsolo)
            {
                foreach (String connectionId in Game.Leader.GetConnectionIds())
                {
                    await Clients.Client(connectionId).SendAsync("AskColor");
                }
            }
            else if (Game.AnnouncedGame == GameType.Hochzeit) //Hochzeit, announce husband or wife
            {
                //TODO::Wait for somebody to press the Button
            }
            else
            {
                await Clients.All.SendAsync(
                    "ReceiveSystemMessage",
                    $"{Game.Leader.Name} spielt {Game.AnnouncedGame}"
                );
                await Game.StartGame(this);
            }
        }
        public async Task ReconnectPlayer(string userId)
        {
            Player player = Game.Players.Single(p => p.Id == userId);
            player.AddConnectionId(this);
            await Clients.Caller.SendAsync("ReceiveSystemMessage", "Willkommen zurück");
            // send username
            // if is playing send cards, ...
            // if game is idle ask if player wants to join
        }
        public async Task AddPlayer(string userName)
        {
            Player player = new Player(userName, this);
            await Game.AddPlayer(player, this);
            await Clients.Caller.SendAsync("StoreUserId", player.Id);
            // send username
        }

        public async Task PlayerWantsToPlay()
        {
            await Game.PlayerPlaysTheGame((Player)Context.Items["player"], this);
            await UpdatePlayingPlayers();
        }

        public async Task ResetGame()
        {
            await Game.Reset(this);
        }

        public async Task UpdatePlayingPlayers()
        {
            await Clients.All.SendAsync("ReceiveSystemMessage", $"Playing Players: {String.Join(", ", Game.PlayingPlayers.Select(p => p.Name + " (" + p.GetConnectionIds().Count + ")"))}");
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
                            Task asyncTask = Game.PlayerDoesNotPlaysTheGame(player, this);
                        }
                        else
                        {
                            Clients.All.SendAsync(
                                "GameOver",
                                "Verbindung zu Spieler verloren",
                                "Möchtest du neustarten oder auf den anderen Spieler warten?"
                            );
                            Task asyncTask = Game.ResetIfAllConnectionsLost(this);
                        }
                    }
                }
            }
            return base.OnDisconnectedAsync(exception);
        }
    }
}
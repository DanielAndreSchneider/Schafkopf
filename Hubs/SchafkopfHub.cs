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
        private readonly static Game Game = new Game();
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

        public async Task AskForGameType()
        {
            for (int i = 0; i < 4; i++)
            {
                if (Game.PlayingPlayers[Game.ActionPlayer].WantToPlay)
                {
                    if (Game.PlayingPlayers[Game.ActionPlayer].AnnouncedGameType == GameType.Ramsch)
                    {
                        foreach (String connectionId in Game.PlayingPlayers[Game.ActionPlayer].GetConnectionIds())
                        {
                            // await Clients.Client(connectionId).SendAsync("ReceiveSystemMessage", "Was magst du spielen?");
                            await Clients.Client(connectionId).SendAsync("AskGameType");
                        }
                    }
                    else
                    {
                        // decide who plays and ask for color
                    }
                    return;
                }
                Game.ActionPlayer = (Game.ActionPlayer + 1) % 4;
            }
            // no one wants to play => it's a ramsch
        }
        public async Task ReconnectPlayer(string userId)
        {
            Player player = Game.Players.Single(p => p.Id == userId);
            player.AddConnectionId(this);
            // send username
            // send cards, ...
            await UpdatePlayerCountAsync();
        }
        public async Task AddPlayer(string userName)
        {
            Player player = new Player(userName, this);
            await Game.AddPlayer(player, this);
            await Clients.Caller.SendAsync("StoreUserId", player.Id);
            // send username
            // send cards
            await UpdatePlayerCountAsync();
        }

        public async Task PlayerWantsToPlay()
        {
            await Game.PlayerPlaysTheGame((Player)Context.Items["player"], this);
            await UpdatePlayingPlayers();
        }

        private async Task UpdatePlayingPlayers()
        {
            await Clients.All.SendAsync("ReceiveSystemMessage", $"Playing Players: {String.Join(", ", Game.PlayingPlayers.Select(p => p.Name + " (" + p.GetConnectionIds().Count + ")"))}");
        }
        private async Task UpdatePlayerCountAsync()
        {
            await Clients.All.SendAsync("ReceiveSystemMessage", $"Number of players: {Game.Players.Count}");
            await Clients.All.SendAsync("ReceiveSystemMessage", $"Players: {String.Join(", ", Game.Players.Select(p => p.Name + " (" + p.GetConnectionIds().Count + ")"))}");
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            foreach (Player player in Game.Players)
            {
                player.RemoveConnectionId(Context.ConnectionId);
            }
            Task task = UpdatePlayerCountAsync();
            return base.OnDisconnectedAsync(exception);
        }
    }
}
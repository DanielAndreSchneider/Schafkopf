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
        private readonly static List<string> _connections = new List<string>();
        private readonly static Game game = new Game();
        public async Task SendChatMessage(string user, string message)
        {
            await Clients.All.SendAsync("ReceiveChatMessage", user, message);
        }
        public async Task DealCards()
        {
            if (_connections.Count >= 4)
            {
                game.DealCards();
                for (int i = 0; i < 4; i++)
                {
                    await Clients.Client(_connections[i]).SendAsync(
                        "ReceiveHand",
                        game.PlayingPlayers[i].HandCards.Select(card => card.ToString())
                    );
                }
            }
            else
            {
                await Clients.All.SendAsync("ReceiveSystemMessage", "Error: not enough players");
            }
        }

        private async Task UpdatePlayerCountAsync()
        {
            await Clients.All.SendAsync("ReceiveSystemMessage", $"Number of players: {_connections.Count}");
        }

        public override Task OnConnectedAsync()
        {
            _connections.Add(Context.ConnectionId);
            game.PlayingPlayers.Add(new Player(Context.ConnectionId));
            Task task = UpdatePlayerCountAsync();
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            _connections.Remove(Context.ConnectionId);
            Task task = UpdatePlayerCountAsync();
            return base.OnDisconnectedAsync(exception);
        }
    }
}
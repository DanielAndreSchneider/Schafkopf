using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Schafkopf.Hubs
{
    public class SchafkopfHub : Hub
    {
        private readonly static List<string> _connections = new List<string>();
        string[] cards = {
            "Eichel-A",
            "Eichel-10",
            "Eichel-K",
            "Eichel-O",
            "Eichel-U",
            "Eichel-9",
            "Eichel-8",
            "Eichel-7",
            "Gras-A",
            "Gras-10",
            "Gras-K",
            "Gras-O",
            "Gras-U",
            "Gras-9",
            "Gras-8",
            "Gras-7",
            "Herz-A",
            "Herz-10",
            "Herz-K",
            "Herz-O",
            "Herz-U",
            "Herz-9",
            "Herz-8",
            "Herz-7",
            "Schellen-A",
            "Schellen-10",
            "Schellen-K",
            "Schellen-O",
            "Schellen-U",
            "Schellen-9",
            "Schellen-8",
            "Schellen-7",
        };
        public async Task SendChatMessage(string user, string message)
        {
            await Clients.All.SendAsync("ReceiveChatMessage", user, message);
        }
        public async Task DealCards()
        {
            ShuffleCards();
            if (_connections.Count >= 4)
            {
                for (int i = 0; i < 4; i++)
                {
                    await Clients.Client(_connections[i]).SendAsync("ReceiveHand", new ArraySegment<string>(cards, i * 8, 8));
                }
            }
            else
            {
                await Clients.All.SendAsync("ReceiveSystemMessage", "Error: not enough players");
            }
        }

        private void ShuffleCards()
        {
            Random rnd = new Random();

            int n = cards.Length;
            while (n > 1)
            {
                int k = rnd.Next(n--);
                string temp = cards[n];
                cards[n] = cards[k];
                cards[k] = temp;
            }
        }

        private async Task UpdatePlayerCountAsync()
        {
            await Clients.All.SendAsync("ReceiveSystemMessage", $"Number of players: {_connections.Count}");
        }

        public override Task OnConnectedAsync()
        {
            _connections.Add(Context.ConnectionId);
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
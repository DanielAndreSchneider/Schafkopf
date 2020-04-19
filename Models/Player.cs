using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Schafkopf.Hubs;

namespace Schafkopf.Models
{
    public class Player
    {
        public List<Card> HandCards = new List<Card>();
        public int Balance = 0;
        public String Name = "";
        public String Id = "";
        private readonly List<String> _connectionIds = new List<String>();
        public Boolean Playing = false;
        public Boolean WantToPlay = false;
        public GameType AnnouncedGameType = GameType.Ramsch;
        public Color AnnouncedColor = Color.None;
        public List<Player> Spectators = new List<Player>();
        public Queue<Player> SpectatorsWaitingForApproval = new Queue<Player>();


        public Player(String name, SchafkopfHub hub)
        {
            Name = name;
            AddConnectionId(hub);
            Id = System.Guid.NewGuid().ToString();
        }

        public void Reset() {
            HandCards = new List<Card>();
            Balance = 0;
            Playing = false;
            WantToPlay = false;
            AnnouncedGameType = GameType.Ramsch;
            AnnouncedColor = Color.None;
            Spectators = new List<Player>();
            SpectatorsWaitingForApproval = new Queue<Player>();
        }

        //-------------------------------------------------
        // Player plays a card
        // Card will be removed from the hand-cards
        // Throw exception in case that a card has been played twice
        //-------------------------------------------------
        public async Task<Card> PlayCard(Color cardColor, int cardNumber, SchafkopfHub hub, Game game)
        {
            foreach (Card card in HandCards) {
                if (card.Color == cardColor && card.Number == cardNumber)
                {
                    HandCards.Remove(card);
                    await SendHand(hub, game.AnnouncedGame, game.Trick.Trump);
                    return card;
                }
            }
            throw new Exception("There is something wrong, the card is not on the hand.");
        }

        //-------------------------------------------------
        // Player takes the trick and add its points to his own balance
        //-------------------------------------------------
        public void TakeTrick(Trick trick)
        {
            int points = trick.Cards[0].getPoints() + trick.Cards[1].getPoints() + trick.Cards[2].getPoints() + trick.Cards[3].getPoints();
            Balance += points;
        }

        public async Task Announce(bool wantToPlay, SchafkopfHub hub, Game game)
        {
            //Message about the players actions
            WantToPlay = wantToPlay;
            foreach (String connectionId in game.GetPlayingPlayersConnectionIds())
            {
                if (WantToPlay)
                {
                    await hub.Clients.Client(connectionId).SendAsync("ReceiveChatMessage", Name, "ich mag spielen");
                }
                else
                {
                    await hub.Clients.Client(connectionId).SendAsync("ReceiveChatMessage", Name, "ich mag nicht spielen");
                }
            }
        }

        //-------------------------------------------------
        // Player can decide whether he is leading a game or not
        //-------------------------------------------------
        public void Leading()
        {
            WantToPlay = true;
        }
        public void Following()
        {
            WantToPlay = false;
        }

        //-------------------------------------------------
        // Player can decide what type of Game he is playing
        //-------------------------------------------------
        public void DecideGameType(GameType gameTyp)
        {
            AnnouncedGameType = gameTyp;
        }
        internal async Task AnnounceGameType(GameType gameType, SchafkopfHub hub, Game game)
        {
            AnnouncedGameType = gameType;
            //Message about the players actions
            foreach (String connectionId in game.GetPlayingPlayersConnectionIds()) {
                await hub.Clients.Client(connectionId).SendAsync("ReceiveChatMessage", Name, $"Ich hätte ein {gameType}");
            }
        }

        public void AddConnectionId(SchafkopfHub hub) {
            hub.Context.Items.Add("player", this);
            lock(_connectionIds) {
                _connectionIds.Add(hub.Context.ConnectionId);
            }
        }
        public bool RemoveConnectionId(String id) {
            lock(_connectionIds) {
                return _connectionIds.Remove(id);
            }
        }
        public List<String> GetConnectionIds() {
            return _connectionIds;
        }
        public List<String> GetConnectionIdsWithSpectators()
        {
            return GetSpectatorConnectionIds().Concat(GetConnectionIds()).ToList();
        }

        public async Task SendHand(SchafkopfHub hub, GameType gameType = GameType.Ramsch, Color trump = Color.Herz) {
            foreach (String connectionId in GetConnectionIdsWithSpectators())
            {
                await hub.Clients.Client(connectionId).SendAsync(
                    "ReceiveHand",
                    HandCards.OrderByDescending(c => c.GetValue(gameType, trump)).Select(card => card.ToString())
                );
            }
        }

        public List<String> GetSpectatorConnectionIds()
        {
            return Spectators.Aggregate(new List<String>(), (acc, x) => acc.Concat(x.GetConnectionIds()).ToList());
        }

        public async Task AddSpectator(Player player, SchafkopfHub hub, Game game) {
            foreach (String connectionId in game.GetPlayingPlayersConnectionIds()) {
                await hub.Clients.Client(connectionId).SendAsync("ReceiveSystemMessage", $"{player.Name} schaut jetzt bei {Name} zu");
            }
            Spectators.Add(player);
            await game.SendUpdatedGameState(this, hub);
        }

        public async Task AskForApprovalToSpectate(SchafkopfHub hub)
        {
            if (SpectatorsWaitingForApproval.Count == 0) {
                foreach (String connectionId in GetConnectionIds())
                {
                    await hub.Clients.Client(connectionId).SendAsync("CloseAllowSpectatorModal");
                }
                return;
            }
            foreach (String connectionId in GetConnectionIds()) {
                await hub.Clients.Client(connectionId).SendAsync("AskAllowSpectator", SpectatorsWaitingForApproval.Peek().Name);
            }
        }
    }
}

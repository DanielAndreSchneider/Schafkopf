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
        public Card[] HandCards = new Card[8];
        public int Balance = 0;
        public String Name = "";
        public String Id = "";
        private readonly List<String> _connectionIds = new List<String>();
        public Boolean Playing = false;
        public Boolean WantToPlay = false;
        public GameType AnnouncedGameType = GameType.Ramsch;
        public Color AnnouncedColor = Color.Schellen;


        public Player(String name, SchafkopfHub hub)
        {
            Name = name;
            AddConnectionId(hub);
            Id = System.Guid.NewGuid().ToString();
        }

        //-------------------------------------------------
        // Player plays a card
        // Card will be removed from the hand-cards
        // Throw exception in case that a card has been played twice
        //-------------------------------------------------
        public Card PlayCard(int card)
        {
            Card playedCard = HandCards[card];
            HandCards[card] = null;
            //TODO::Portray own card deck with less cards
            if(playedCard == null)
            {
                throw new Exception("There is something wrong, the card was already been played.");
            }
            return playedCard;
        }

        //-------------------------------------------------
        // Player takes the trick and add its points to his own balance
        //-------------------------------------------------
        public void TakeTrick(Trick trick)
        {
            int points = trick.Cards[0].Number + trick.Cards[1].Number + trick.Cards[2].Number + trick.Cards[3].Number;
            Balance += points;
        }

        public async Task Announce(bool wantToPlay, SchafkopfHub hub)
        {
            //Message about the players actions
            WantToPlay = wantToPlay;
            if (WantToPlay) {
                await hub.Clients.All.SendAsync("ReceiveChatMessage", Name, "ich mag spielen");
            } else {
                await hub.Clients.All.SendAsync("ReceiveChatMessage", Name, "ich mag nicht spielen");
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
        internal async Task AnnounceGameType(GameType gameType, SchafkopfHub hub)
        {
            AnnouncedGameType = gameType;
            //Message about the players actions
            await hub.Clients.All.SendAsync("ReceiveChatMessage", Name, $"Ich hätte ein {gameType}");
        }

        public void AnnounceColor()
        {
            //TODO::Wait for the player to choose its color
        }

        public override bool Equals(object obj)
        {
            Player secondPlayer = obj as Player;

            if(this.Name.Equals(secondPlayer.Name))
            {
                return true;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public void AddConnectionId(SchafkopfHub hub) {
            hub.Context.Items.Add("player", this);
            lock(_connectionIds) {
                _connectionIds.Add(hub.Context.ConnectionId);
            }
        }
        public void RemoveConnectionId(String id) {
            lock(_connectionIds) {
                _connectionIds.Remove(id);
            }
        }
        public List<String> GetConnectionIds() {
            return _connectionIds;
        }
    }
}

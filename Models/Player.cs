using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Schafkopf.Models
{
    public class Player
    {
        public Card[] HandCards = new Card[8];
        public int Balance = 0;
        public String Name = "";
        public Boolean Playing = false;
        public Boolean AnnounceLeading = false;
        public GameType AnnouncedGameType = GameType.Ramsch;
        public Color AnnouncedColor = Color.Schellen;


        public Player(String name)
        {
            Name = name;
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

        public void Announce()
        {
            //TODO::Wait player's decision
            //Message about the players actions
            if(AnnounceLeading)
            {
                //Player leads
            } else
            {
                //Next
            }
        }

        //-------------------------------------------------
        // Player can decide whether he is leading a game or not
        //-------------------------------------------------
        public void Leading()
        {
            AnnounceLeading = true;
        }
        public void Following()
        {
            AnnounceLeading = false;
        }

        //-------------------------------------------------
        // Player can decide what type of Game he is playing
        //-------------------------------------------------
        public void DecideGameType(GameType gameTyp)
        {
            AnnouncedGameType = gameTyp;
        }
        internal void AnnounceGameType()
        {
            //TODO::Wait player's decision about the game type

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
    }
}

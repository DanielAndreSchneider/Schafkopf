using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Schafkopf.Hubs;

namespace Schafkopf.Models
{
    public class Trick
    {
        public Card[] Cards = new Card[4];
        public Player[] Player = new Player[4];
        public Card FirstCard;
        public int Count = 0;
        public GameType GameType = GameType.Ramsch;
        public Color Trump = Color.Herz;
        public int Winner = 0;

        public Trick(Game game)
        {
            GameType = game.AnnouncedGame;
            DetermineTrumpf(game);
        }

        //-------------------------------------------------
        // Card is added to the trick
        // in case that there are too many cards in one trick, an exception is thrown
        //-------------------------------------------------
        public async Task AddCard(Card card, Player player, SchafkopfHub hub, Game game)
        {
            if (Count >= 4)
            {
                throw new Exception("There are too many Cards in the trick.");
            }
            Cards[Count] = card;
            Player[Count] = player;

            await SendTrick(hub, game);

            //Determine the winner of the Trick
            if(Count > 0)
            {
                DetermineWinnerCard(card);
            } else
            {
                FirstCard = card;
                FirstCard.TrickValue = card.GetValue(GameType, Trump);
            }
            Count++;
        }

        //-------------------------------------------------
        // FirstCard
        // WinnerCard
        // NewCard
        //-------------------------------------------------
        private void DetermineWinnerCard(Card newCard)
        {
            newCard.TrickValue = newCard.GetValue(GameType, Trump, FirstCard);
            //Check which one is higher
            if (newCard.TrickValue > Cards[Winner].TrickValue)
            {
                Winner = Count;
            }
        }

        public Player GetWinner()
        {
            return Player[Winner];
        }

        private void DetermineTrumpf(Game game) {
            switch (game.AnnouncedGame)
            {
                case GameType.Ramsch:
                case GameType.Sauspiel:
                case GameType.Hochzeit:
                    Trump = Color.Herz;
                    break;
                case GameType.Farbsolo:
                case GameType.FarbsoloTout:
                    Trump = game.Leader.AnnouncedColor;
                    break;
                case GameType.Wenz:
                case GameType.WenzTout:
                    Trump = Color.None;
                    break;
            }
        }

        public async Task SendTrick(SchafkopfHub hub, Game game)
        {
            for (int i = 0; i < 4; i++)
            {
                Card[] permutedCards = new Card[4];
                for (int j = 0; j < 4; j++)
                {
                    permutedCards[j] = Cards[(j + i) % 4];
                }
                Player player = game.PlayingPlayers[(game.ActionPlayer - Count + i + 4) % 4];
                foreach (String connectionId in player.GetConnectionIdsWithSpectators())
                {
                    await hub.Clients.Client(connectionId).SendAsync(
                        "ReceiveTrick",
                        permutedCards.Select(card => card == null ? "" : card.ToString())
                    );
                }
            }
        }
    }
}

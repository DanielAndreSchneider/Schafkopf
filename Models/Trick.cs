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
        public Color Trumpf = Color.Herz;
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
                switch (this.GameType)
                {
                    //Highest card looses
                    case GameType.Ramsch:
                    case GameType.Hochzeit:
                    case GameType.Sauspiel:
                        {
                            //Determine value
                            if (card.Number == 2 || card.Number == 3)
                            {
                                card.Value = 100 * card.Number + (int)card.Color;
                            }
                            else if (card.Color == Trumpf && card.Number == 4)
                            {
                                card.Value = 29;
                            }
                            else if (card.Color == Trumpf)
                            {
                                card.Value = 3 * card.Number;
                            }
                            else if (card.Number == 4)
                            {
                                card.Value = 19;
                            }
                            else
                            {
                                card.Value = 2 * card.Number;
                            }
                        }
                        break;
                    case GameType.Wenz:
                    case GameType.WenzTout:
                        {
                            //Determine value
                            if (card.Number == 2)
                            {
                                card.Value = 100 * card.Number + (int)card.Color;
                            }
                            else if (card.Number == 4)
                            {
                                card.Value = 29;
                            }
                            else if (card.Number == 3)
                            {
                                card.Value = 28;
                            }
                            else
                            {
                                card.Value = 3 * card.Number;
                            }
                        }
                        break;
                    case GameType.Farbsolo:
                    case GameType.FarbsoloTout:
                        {
                            //Determine value
                            if (card.Number == 2 || card.Number == 3)
                            {
                                card.Value = 100 * card.Number + (int)card.Color;
                            }
                            else if (card.Color == Trumpf && card.Number == 4)
                            {
                                card.Value = 29;
                            }
                            else if (card.Color == Trumpf)
                            {
                                card.Value = 3 * card.Number;
                            }
                            else if (card.Number == 4)
                            {
                                card.Value = 19;
                            }
                            else
                            {
                                card.Value = 2 * card.Number;
                            }
                        }
                        break;
                }
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
            switch (this.GameType)
            {
                //Highest card looses
                case GameType.Ramsch:
                case GameType.Hochzeit:
                case GameType.Sauspiel:
                    {
                        //Determine value
                        if(newCard.Number == 2 || newCard.Number == 3)
                        {
                            newCard.Value = 100 * newCard.Number + (int)newCard.Color;
                        } else if(newCard.Color == Trumpf && newCard.Number == 4)
                        {
                            newCard.Value = 29;
                        } else if(newCard.Color == Trumpf)
                        {
                            newCard.Value = 3 * newCard.Number;
                        } else if(newCard.Color != FirstCard.Color)
                        {
                            newCard.Value = 0;
                        } else if(newCard.Number == 4)
                        {
                            newCard.Value = 19;
                        } else
                        {
                            newCard.Value = 2 * newCard.Number;
                        }

                        //Check which one is higher
                        if(newCard.Value > Cards[Winner].Value)
                        {
                            Winner = Count;
                        }
                    }
                    break;
                case GameType.Wenz:
                case GameType.WenzTout:
                    {
                        //Determine value
                        if (newCard.Number == 2)
                        {
                            newCard.Value = 100 * newCard.Number + (int)newCard.Color;
                        }
                        else if (newCard.Color != FirstCard.Color)
                        {
                            newCard.Value = 0;
                        }
                        else if (newCard.Number == 4)
                        {
                            newCard.Value = 29;
                        }
                        else if (newCard.Number == 3)
                        {
                            newCard.Value = 28;
                        }
                        else
                        {
                            newCard.Value = 3 * newCard.Number;
                        }

                        //Check which one is higher
                        if (newCard.Value > Cards[Winner].Value)
                        {
                            Winner = Count;
                        }
                    }
                    break;
                case GameType.Farbsolo:
                case GameType.FarbsoloTout:
                    {
                        //Determine value
                        if (newCard.Number == 2 || newCard.Number == 3)
                        {
                            newCard.Value = 100 * newCard.Number + (int)newCard.Color;
                        }
                        else if (newCard.Color == Trumpf && newCard.Number == 4)
                        {
                            newCard.Value = 29;
                        }
                        else if (newCard.Color == Trumpf)
                        {
                            newCard.Value = 3 * newCard.Number;
                        }
                        else if (newCard.Color != FirstCard.Color)
                        {
                            newCard.Value = 0;
                        }
                        else if (newCard.Number == 4)
                        {
                            newCard.Value = 19;
                        }
                        else
                        {
                            newCard.Value = 2 * newCard.Number;
                        }

                        //Check which one is higher
                        if (newCard.Value > Cards[Winner].Value)
                        {
                            Winner = Count;
                        }
                    }
                    break;
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
                    Trumpf = Color.Herz;
                    break;
                case GameType.Farbsolo:
                case GameType.FarbsoloTout:
                    Trumpf = game.Leader.AnnouncedColor;
                    break;
                case GameType.Wenz:
                case GameType.WenzTout:
                    Trumpf = Color.None;
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

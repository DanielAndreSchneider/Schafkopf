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
        public Boolean Playing = true;
        public Boolean WantToPlay = false;
        public Boolean WantToPlayAnswered = false;
        public GameType AnnouncedGameType = GameType.Ramsch;
        public Color AnnouncedColor = Color.None;
        public List<Player> Spectators = new List<Player>();
        public Queue<Player> SpectatorsWaitingForApproval = new Queue<Player>();
        public bool HasBeenAskedToOfferMarriage = false;
        public bool HasAnsweredMarriageOffer = false;
        private bool IsRunaway = false;


        public Player(String name, String connectionId)
        {
            Name = name;
            AddConnectionId(connectionId);
            Id = System.Guid.NewGuid().ToString();
        }

        public void Reset()
        {
            HandCards = new List<Card>();
            Balance = 0;
            Playing = true;
            WantToPlay = false;
            WantToPlayAnswered = false;
            AnnouncedGameType = GameType.Ramsch;
            AnnouncedColor = Color.None;
            Spectators = new List<Player>();
            SpectatorsWaitingForApproval = new Queue<Player>();
            IsRunaway = false;
            HasBeenAskedToOfferMarriage = false;
            HasAnsweredMarriageOffer = false;
        }

        //-------------------------------------------------
        // Player plays a card
        // Card will be removed from the hand-cards
        // Throw exception in case that a card has been played twice
        //-------------------------------------------------
        public async Task<Card> PlayCard(Color cardColor, int cardNumber, SchafkopfHub hub, Game game)
        {
            foreach (Card card in HandCards)
            {
                if (card.Color == cardColor && card.Number == cardNumber)
                {
                    if (!CanCardBePlayed(game, card))
                    {
                        foreach (String connectionId in GetConnectionIds())
                        {
                            await hub.Clients.Client(connectionId).SendAsync("ReceiveSystemMessage", "Die Karte kannst du gerade nicht spielen!");
                        }
                        return null;
                    }
                    HandCards.Remove(card);
                    await SendHand(hub, game.GameState.AnnouncedGame, game.GameState.Trick.Trump);
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
            WantToPlayAnswered = true;
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
        // Player can decide what type of Game he is playing
        //-------------------------------------------------
        public async Task AnnounceGameType(GameType gameType, SchafkopfHub hub, Game game)
        {
            AnnouncedGameType = gameType;
            //Message about the players actions
            foreach (String connectionId in game.GetPlayingPlayersConnectionIds())
            {
                await hub.Clients.Client(connectionId).SendAsync("ReceiveChatMessage", Name, $"Ich hätte ein {gameType}");
            }
        }

        public void AddConnectionId(String connectionId)
        {
            lock (_connectionIds)
            {
                _connectionIds.Add(connectionId);
            }
        }
        public bool RemoveConnectionId(String id)
        {
            lock (_connectionIds)
            {
                return _connectionIds.Remove(id);
            }
        }
        public List<String> GetConnectionIds()
        {
            return _connectionIds;
        }
        public List<String> GetConnectionIdsWithSpectators()
        {
            return GetSpectatorConnectionIds().Concat(GetConnectionIds()).ToList();
        }

        public async Task SendHand(SchafkopfHub hub, GameType gameType = GameType.Ramsch, Color trump = Color.Herz)
        {
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

        public async Task AddSpectator(Player player, SchafkopfHub hub, Game game)
        {
            foreach (String connectionId in game.GetPlayingPlayersConnectionIds())
            {
                await hub.Clients.Client(connectionId).SendAsync("ReceiveSystemMessage", $"{player.Name} schaut jetzt bei {Name} zu");
            }
            Spectators.Add(player);
            await game.SendUpdatedGameState(this, hub, player.GetConnectionIds());
        }

        public async Task AskForApprovalToSpectate(SchafkopfHub hub)
        {
            if (SpectatorsWaitingForApproval.Count == 0)
            {
                foreach (String connectionId in GetConnectionIds())
                {
                    await hub.Clients.Client(connectionId).SendAsync("CloseAllowSpectatorModal");
                }
                return;
            }
            foreach (String connectionId in GetConnectionIds())
            {
                await hub.Clients.Client(connectionId).SendAsync("AskAllowSpectator", SpectatorsWaitingForApproval.Peek().Name);
            }
        }

        private bool CanCardBePlayed(Game game, Card card)
        {
            if (game.GameState.Trick.FirstCard == null)
            {
                if (game.GameState.AnnouncedGame == GameType.Sauspiel &&
                    HandContainsSearchedSau(game.GameState.Leader.AnnouncedColor) &&
                    !IsRunaway)
                {
                    // Davonlaufen
                    if (HandColorCount(game.GameState.Leader.AnnouncedColor, game.GameState.AnnouncedGame, game.GameState.Trick.Trump) >= 4)
                    {
                        IsRunaway = true;
                        return true;
                    }
                    return card.IsTrump(game.GameState.AnnouncedGame, game.GameState.Trick.Trump) || card.Color != game.GameState.Leader.AnnouncedColor || card.Number == 11;
                }
                return true;
            }
            else if (game.GameState.Trick.FirstCard.IsTrump(game.GameState.AnnouncedGame, game.GameState.Trick.Trump))
            {
                if (HandContainsTrump(game))
                {
                    return card.IsTrump(game.GameState.AnnouncedGame, game.GameState.Trick.Trump);
                }
                if (
                    game.GameState.AnnouncedGame == GameType.Sauspiel &&
                    HandContainsSearchedSau(game.GameState.Leader.AnnouncedColor) &&
                    !IsRunaway
                )
                {
                    if (game.GameState.TrickCount < 6)
                    {
                        return card.Color != game.GameState.Leader.AnnouncedColor || card.Number != 11;
                    }
                }
                return true;
            }
            else if (HandContainsColor(game.GameState.Trick.FirstCard.Color, game.GameState.AnnouncedGame, game.GameState.Trick.Trump))
            {
                if (
                    game.GameState.AnnouncedGame == GameType.Sauspiel &&
                    game.GameState.Trick.FirstCard.Color == game.GameState.Leader.AnnouncedColor &&
                    HandContainsSearchedSau(game.GameState.Leader.AnnouncedColor)
                )
                {
                    return card.Color == game.GameState.Trick.FirstCard.Color && card.Number == 11;
                }
                return !card.IsTrump(game.GameState.AnnouncedGame, game.GameState.Trick.Trump) && card.Color == game.GameState.Trick.FirstCard.Color;
            }
            else if (
                game.GameState.AnnouncedGame == GameType.Sauspiel &&
                HandContainsSearchedSau(game.GameState.Leader.AnnouncedColor) &&
                !IsRunaway
            )
            {
                if (game.GameState.TrickCount < 6)
                {
                    return card.Color != game.GameState.Leader.AnnouncedColor || card.Number != 11;
                }
            }
            return true;
        }

        private int HandColorCount(Color color, GameType gameType, Color trump)
        {
            return HandCards.Where(
                        c => !c.IsTrump(gameType, trump) &&
                        c.Color == color
                    ).ToList().Count;
        }

        public int HandTrumpCount(GameType gameType, Color trump)
        {
            return HandCards.Where(c => c.IsTrump(gameType, trump)).ToList().Count;
        }

        private bool HandContainsColor(Color color, GameType gameType, Color trump)
        {
            return HandCards.Any(c => !c.IsTrump(gameType, trump) && c.Color == color);
        }

        private bool HandContainsTrump(Game game)
        {
            return HandCards.Any(c => c.IsTrump(game.GameState.AnnouncedGame, game.GameState.Trick.Trump));
        }

        private bool HandContainsSearchedSau(Color searchedColor)
        {
            return HandCards.Any(c => c.Color == searchedColor && c.Number == 11);
        }

        public async Task<bool> IsSauspielPossible(SchafkopfHub hub)
        {
            foreach (Color searchedColor in new List<Color>() { Color.Eichel, Color.Gras, Color.Schellen })
            {
                if (
                    HandContainsColor(searchedColor, GameType.Sauspiel, Color.Herz) &&
                    !HandContainsSearchedSau(searchedColor)
                )
                {
                    return true;
                }
            }
            foreach (String connectionId in GetConnectionIds())
            {
                await hub.Clients.Client(connectionId).SendAsync("ReceiveSystemMessage", "Du bist gesperrt!");
            }
            return false;
        }

        public async Task<bool> ExchangeCardWithPlayer(Color cardColor, int cardNumber, Player player, SchafkopfHub hub, Game game)
        {
            foreach (Card card in HandCards)
            {
                if (card.Color == cardColor && card.Number == cardNumber)
                {
                    if (card.IsTrump(game.GameState.AnnouncedGame, Color.Herz))
                    {
                        foreach (String connectionId in GetConnectionIds())
                        {
                            await hub.Clients.Client(connectionId).SendAsync("ReceiveSystemMessage", "Du kannst deinem Mitspieler kein Trumpf geben!");
                        }
                        return false;
                    }
                    player.HandCards.Add(card);
                    HandCards.Remove(card);
                    Card trumpCard = player.HandCards.Single(c => c.IsTrump(game.GameState.AnnouncedGame, Color.Herz));
                    player.HandCards.Remove(trumpCard);
                    HandCards.Add(trumpCard);
                    foreach (String connectionId in game.GetPlayingPlayersConnectionIds())
                    {
                        await hub.Clients.Client(connectionId).SendAsync(
                            "ReceiveSystemMessage",
                            $"{player.Name} und {Name} haben eine Karte getauscht"
                        );
                    }
                    return true;
                }
            }
            throw new Exception("There is something wrong, the card is not on the hand.");
        }

        public async Task<bool> IsSauspielOnColorPossible(Color searchedColor, SchafkopfHub hub)
        {
            if (searchedColor == Color.Herz)
            {
                foreach (String connectionId in GetConnectionIds())
                {
                    await hub.Clients.Client(connectionId).SendAsync("ReceiveSystemMessage", "Du kannst die Herz-Sau nicht suchen!");
                }
                return false;
            }
            if (
                   HandContainsColor(searchedColor, GameType.Sauspiel, Color.Herz) &&
                   !HandContainsSearchedSau(searchedColor)
               )
            {
                return true;
            }
            foreach (String connectionId in GetConnectionIds())
            {
                await hub.Clients.Client(connectionId).SendAsync("ReceiveSystemMessage", $"Du kannst nicht auf die {searchedColor}-Sau spielen!");
            }
            return false;
        }

        public string GetSpectatorNames()
        {
            if (Spectators.Where(s => s.GetConnectionIds().Count > 0).ToList().Count > 0)
            {
                return $" ({String.Join(", ", Spectators.Where(s => s.GetConnectionIds().Count > 0).Select(s => s.Name))})";
            }
            return "";
        }

        public string GetCurrentInfo(Game game)
        {
            if (game.GameState.CurrentGameState == State.AnnounceHochzeit || game.GameState.CurrentGameState == State.HochzeitExchangeCards)
            {
                if (game.GameState.Leader == this)
                {
                    return "Wer will mich heiraten?";
                }
                else if (HasAnsweredMarriageOffer)
                {
                    if (game.GameState.HusbandWife == this)
                    {
                        return "Ich will!";
                    }
                    else
                    {
                        return "Ich nicht";
                    }
                }
            }
            else if (game.GameState.CurrentGameState == State.AnnounceGameColor || (game.GameState.CurrentGameState == State.AnnounceGameType && AnnouncedGameType != GameType.Ramsch))
            {
                switch (AnnouncedGameType)
                {
                    case GameType.Farbsolo:
                        return "Ich hab ein Solo";
                    case GameType.Wenz:
                        return "Ich hab ein Wenz";
                    case GameType.Sauspiel:
                        return "Ich hab ein Sauspiel";
                }
            }
            else if (game.GameState.CurrentGameState == State.AnnounceGameType || (game.GameState.CurrentGameState == State.Announce && WantToPlayAnswered))
            {
                if (WantToPlay)
                {
                    return "Ich würde";
                }
                else
                {
                    return "Weiter";
                }
            }
            return "";
        }
    }
}

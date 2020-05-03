using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Schafkopf.Hubs;
using Schafkopf.Models;

namespace Schafkopf.Logic
{
    public class GameState
    {
        private readonly List<PlayerState> _Players = new List<PlayerState>();
        private List<PlayerState> _PlayingPlayers = new List<PlayerState>();
        public readonly Carddeck Carddeck = new Carddeck();
        public State CurrentGameState = State.Idle;
        public int[] Groups = new int[] { 0, 0, 0, 0 };
        public int StartPlayer = -1;
        public int ActionPlayer = -1;
        public bool NewGame = false;
        public GameType AnnouncedGame = GameType.Ramsch;
        private PlayerState _Leader;
        public Player Leader
        {
            get => _Leader;
            set
            {
                lock (_Lock)
                {
                    if (value == null)
                    {
                        _Leader = null;
                    }
                    else
                    {
                        _Leader = _Players.Single(p => p.Id == value.Id);
                    }
                }
            }
        }
        private PlayerState _HusbandWife = null;
        public Player HusbandWife
        {
            get => _HusbandWife;
            set
            {
                lock (_Lock)
                {
                    if (value == null)
                    {
                        _HusbandWife = null;
                    }
                    else
                    {
                        _HusbandWife = _Players.Single(p => p.Id == value.Id);
                    }
                }
            }
        }
        private TrickState _Trick = null;
        private TrickState _LastTrick = null;
        public int TrickCount = 0;
        private readonly object _Lock = new object();

        public Color GetTrumpColor()
        {
            switch (AnnouncedGame)
            {
                case GameType.Ramsch:
                case GameType.Sauspiel:
                case GameType.Hochzeit:
                    return Color.Herz;
                case GameType.Farbsolo:
                case GameType.FarbsoloTout:
                    return Leader.AnnouncedColor;
                case GameType.Wenz:
                case GameType.WenzTout:
                default:
                    return Color.None;
            }
        }

        public void AddCardToTrick(Card card, Player player)
        {
            lock (_Lock)
            {
                PlayerState playerState = _Players.Single(p => p.Id == player.Id);
                _Trick.AddCard(card, playerState);
            }
        }

        internal void Announce(bool wantToPlay)
        {
            lock (_Lock)
            {
                _PlayingPlayers[ActionPlayer].Announce(wantToPlay);
            }
        }

        public void NewTrick()
        {
            lock (_Lock)
            {
                if (_Trick != null)
                {
                    _LastTrick = _Trick;
                }
                _Trick = new TrickState(AnnouncedGame, GetTrumpColor(), ActionPlayer);
            }
        }

        public Trick Trick => _Trick;
        public Trick LastTrick => _LastTrick;
        public List<Player> Players => _Players.Cast<Player>().ToList();
        public List<Player> PlayingPlayers => _PlayingPlayers.Cast<Player>().ToList();

        public void Reset()
        {
            lock (_Lock)
            {
                CurrentGameState = State.Idle;
                Groups = new int[] { 0, 0, 0, 0 };
                AnnouncedGame = GameType.Ramsch;
                Leader = null;
                HusbandWife = null;
                _Trick = null;
                _LastTrick = null;
                TrickCount = 0;
                ActionPlayer = -1;
                _PlayingPlayers = new List<PlayerState>();

                foreach (PlayerState player in _Players)
                {
                    player.Reset();
                }
            }
        }

        internal void StartGame()
        {
            lock (_Lock)
            {
                CurrentGameState = State.AnnounceHochzeit;

                //New first player
                StartPlayer = (StartPlayer + 1) % Players.Count;
                while (!PlayingPlayers.Contains(Players[StartPlayer]))
                {
                    StartPlayer = (StartPlayer + 1) % Players.Count;
                }
                //Shuffle cards
                Card[] shuffledCards = Carddeck.Shuffle();
                //Distribute cards to the players
                //Player 1 gets first 8 cards, Player 2 gets second 8 cards, an so on ...
                for (int i = 0; i < 4; i++)
                {
                    Card[] HandCards = new Card[8];
                    for (int j = i * 8; j < (i + 1) * 8; j++)
                    {
                        HandCards[j % 8] = shuffledCards[j];
                        _PlayingPlayers[i].HandCards = new List<Card>(HandCards);
                    }
                }
            }
        }

        internal (bool, string, List<string>) ExchangeCardWithPlayer(Player player, Color cardColor, int cardNumber, Player leader, SchafkopfHub hub, Game game)
        {
            lock (_Lock)
            {
                PlayerState playerState = _Players.Single(p => p.Id == player.Id);
                PlayerState leaderState = _Players.Single(p => p.Id == leader.Id);
                return playerState.ExchangeCardWithPlayer(cardColor, cardNumber, leaderState, hub, game);
            }
        }

        internal void SetPlayerPlaying(bool value, Player player)
        {
            lock (_Lock)
            {
                PlayerState playerState = _Players.Single(p => p.Id == player.Id);
                playerState._Playing = value;
                if (!_PlayingPlayers.Contains(playerState))
                {
                    for (int i = 0; i < _PlayingPlayers.Count; i++)
                    {
                        if (_Players.IndexOf(_PlayingPlayers[i]) > _Players.IndexOf(playerState))
                        {
                            _PlayingPlayers.Insert(i, playerState);
                            break;
                        }
                    }
                    if (!_PlayingPlayers.Contains(playerState))
                    {
                        _PlayingPlayers.Add(playerState);
                    }
                }
            }
        }
        internal void SetPlayerHasBeenAskedToOfferMarriage(bool value, Player player)
        {
            lock (_Lock)
            {
                PlayerState playerState = _Players.Single(p => p.Id == player.Id);
                playerState._HasBeenAskedToOfferMarriage = value;
            }
        }

        internal void SetPlayerHasAnsweredMarriageOffer(bool value, Player player)
        {
            lock (_Lock)
            {
                PlayerState playerState = _Players.Single(p => p.Id == player.Id);
                playerState._HasAnsweredMarriageOffer = value;
            }
        }
        internal (Card, string) PlayCard(Color cardColor, int cardNumber, SchafkopfHub hub, Game game, Player player)
        {
            lock (_Lock)
            {
                PlayerState playerState = _Players.Single(p => p.Id == player.Id);
                return playerState.PlayCard(cardColor, cardNumber, hub, game);
            }
        }

        internal void AnnounceGameType(GameType gameType)
        {
            lock (_Lock)
            {
                PlayerState player = _PlayingPlayers[ActionPlayer];
                player.AnnounceGameType(gameType);
            }
        }

        internal void SetAnnouncedColor(Color color)
        {
            lock (_Lock)
            {
                _Leader._AnnouncedColor = color;
            }
        }

        internal void AddPlayerConnectionId(string connectionId, Player player)
        {
            lock (_Lock)
            {
                PlayerState playerState = _Players.Single(p => p.Id == player.Id);
                playerState.AddConnectionId(connectionId);
            }
        }

        internal bool RemovePlayerConnectionId(string connectionId, Player player)
        {
            lock (_Lock)
            {
                PlayerState playerState = _Players.Single(p => p.Id == player.Id);
                return playerState.RemoveConnectionId(connectionId);
            }
        }

        internal void SetPlayerName(string userName, Player player)
        {
            lock (_Lock)
            {
                PlayerState playerState = _Players.Single(p => p.Id == player.Id);
                playerState._Name = userName;
            }
        }

        internal void SetPlayerId(string newUserId, Player player)
        {
            lock (_Lock)
            {
                PlayerState playerState = _Players.Single(p => p.Id == player.Id);
                playerState._Id = newUserId;
            }
        }

        internal Player AddPlayer(string userName, string connectionId)
        {
            lock (_Lock)
            {
                PlayerState player = new PlayerState(userName, connectionId);
                _Players.Add(player);
                return player;
            }
        }

        internal void EnqueueSpectatorForApproval(Player player, Player spectator)
        {
            lock (_Lock)
            {
                PlayerState playerState = _Players.Single(p => p.Id == player.Id);
                PlayerState spectatorState = _Players.Single(p => p.Id == player.Id);
                playerState._SpectatorsWaitingForApproval.Enqueue(spectatorState);
            }
        }
        internal Player DequeueSpectator(Player player, bool allow)
        {
            lock (_Lock)
            {
                PlayerState playerState = _Players.Single(p => p.Id == player.Id);
                PlayerState spectator = playerState._SpectatorsWaitingForApproval.Dequeue();
                if (allow)
                {
                    playerState.AddSpectator(spectator);
                }
                return spectator;
            }
        }


        internal void TakeTrick()
        {
            lock (_Lock)
            {
                PlayerState winner = _Players.Single(p => p.Id == Trick.Winner.Id);
                winner.AddPoints(Trick.Points);
                TrickCount++;
            }
        }
    }
}
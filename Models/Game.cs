using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Schafkopf.Hubs;

namespace Schafkopf.Models
{
    public class Game
    {
        public List<Player> Players = new List<Player>();
        public List<Player> PlayingPlayers = new List<Player>();
        public static Card[] Cards = new Card[32];
        public Card[] MixedCards = null;
        public State CurrentGameState = State.Idle;
        public int[] Groups = new int[] { 0, 0, 0, 0 };
        //Rotates around after each game
        public int StartPlayer = 3;
        public int ActionPlayer = 0;
        public Boolean NewGame = false;
        public GameType AnnouncedGame = GameType.Ramsch;
        public Player Leader = null;
        public Player HusbandWife = null;
        public Trick Trick = new Trick();
        public int TrickCount = 0;

        private Random random = new Random();

        public Game()
        {
            #region InitCards
            Cards[0] = new Card(Color.Schellen, 7);
            Cards[1] = new Card(Color.Schellen, 8);
            Cards[2] = new Card(Color.Schellen, 9);
            Cards[3] = new Card(Color.Schellen, 10);
            Cards[4] = new Card(Color.Schellen, 2);
            Cards[5] = new Card(Color.Schellen, 3);
            Cards[6] = new Card(Color.Schellen, 4);
            Cards[7] = new Card(Color.Schellen, 11);

            Cards[8] = new Card(Color.Herz, 7);
            Cards[9] = new Card(Color.Herz, 8);
            Cards[10] = new Card(Color.Herz, 9);
            Cards[11] = new Card(Color.Herz, 10);
            Cards[12] = new Card(Color.Herz, 2);
            Cards[13] = new Card(Color.Herz, 3);
            Cards[14] = new Card(Color.Herz, 4);
            Cards[15] = new Card(Color.Herz, 11);

            Cards[16] = new Card(Color.Gras, 7);
            Cards[17] = new Card(Color.Gras, 8);
            Cards[18] = new Card(Color.Gras, 9);
            Cards[19] = new Card(Color.Gras, 10);
            Cards[20] = new Card(Color.Gras, 2);
            Cards[21] = new Card(Color.Gras, 3);
            Cards[22] = new Card(Color.Gras, 4);
            Cards[23] = new Card(Color.Gras, 11);

            Cards[24] = new Card(Color.Eichel, 7);
            Cards[25] = new Card(Color.Eichel, 8);
            Cards[26] = new Card(Color.Eichel, 9);
            Cards[27] = new Card(Color.Eichel, 10);
            Cards[28] = new Card(Color.Eichel, 2);
            Cards[29] = new Card(Color.Eichel, 3);
            Cards[30] = new Card(Color.Eichel, 4);
            Cards[31] = new Card(Color.Eichel, 11);
            #endregion

        }

        public async Task Reset(SchafkopfHub hub) {
            CurrentGameState = State.Idle;
            Groups = new int[] { 0, 0, 0, 0 };
            AnnouncedGame = GameType.Ramsch;
            Leader = null;
            HusbandWife = null;
            Trick = new Trick();
            TrickCount = 0;

            // TODO: let players stay in the game if they want
            // foreach (Player player in Players) {
            //     if (PlayingPlayers.Contains(player))
            //     {
            //         if (player.GetConnectionIds().Count == 0)
            //         {
            //             PlayingPlayers.Remove(player);
            //         }
            //         else
            //         {
            //             player.AskIfWantToPlayWithTimeout();
            //         }
            //     } else {
            //         player.AskIfWantToPlay();
            //     }
            // }
            await Trick.SendTrick(hub, this);
            PlayingPlayers = new List<Player>();
            foreach (Player player in Players)
            {
                player.Reset();
                await player.SendHand(hub);
            }
            await SendPlayers(hub);
            foreach (String connectionId in GetPlayersConnectionIds()) {
                await hub.Clients.Client(connectionId).SendAsync("CloseAnnounceModal");
                await hub.Clients.Client(connectionId).SendAsync("CloseAnnounceGameTypeModal");
                await hub.Clients.Client(connectionId).SendAsync("CloseGameColorModal");
                await hub.Clients.Client(connectionId).SendAsync("CloseGameOverModal");
                await hub.Clients.Client(connectionId).SendAsync("AskWantToPlay");
            }
        }

        public async Task ResetIfAllConnectionsLost(SchafkopfHub hub) {
            foreach (Player player in PlayingPlayers) {
                if (player.GetConnectionIds().Count > 0) {
                    return;
                }
            }
            await Reset(hub);
        }

        public async Task DealCards(SchafkopfHub hub)
        {

            await SendPlayers(hub);
            await SendAskWantToSpectate(hub, GetNonPlayingPlayersConnectionIds());
            // TODO: if one of the players has exactly one trump, ask if he wants to play a Hochzeit
            foreach (String connectionId in GetPlayersConnectionIds())
            {
                await hub.Clients.Client(connectionId).SendAsync("CloseWantToPlayModal");
            }
            CurrentGameState = State.Announce;

            //New first player
            StartPlayer = (StartPlayer + 1) % 4;
            //Shuffle cards
            Shuffle();
            //Distribute cards to the players
            //Player 1 gets first 8 cards, Player 2 gets second 8 cards, an so on ...
            for (int i = 0; i < 4; i++)
            {
                Card[] HandCards = new Card[8];
                for (int j = i * 8; j < (i + 1) * 8; j++)
                {
                    HandCards[j % 8] = MixedCards[j];
                    PlayingPlayers[i].HandCards = new List<Card>(HandCards);
                }
                await PlayingPlayers[i].SendHand(hub);
            }

            ActionPlayer = StartPlayer;
            await SendAskAnnounce(hub);
        }

        public void DecideWhoIsPlaying()
        {
            ActionPlayer = StartPlayer;
            for (int i = 0; i < 4; i++)
            {
                if (PlayingPlayers[ActionPlayer].WantToPlay)
                {
                    if (AnnouncedGame < PlayingPlayers[ActionPlayer].AnnouncedGameType)
                    {
                        //Player announces a higher game to play
                        AnnouncedGame = PlayingPlayers[ActionPlayer].AnnouncedGameType;
                        Leader = PlayingPlayers[ActionPlayer];
                    }
                }
                ActionPlayer = (ActionPlayer + 1) % 4;
            }
        }

        public async Task StartGame(SchafkopfHub hub)
        {
            await SendPlayerIsPlayingGameTypeAndColor(hub, GetPlayingPlayersConnectionIds());
            FindTeams();
            Trick.DetermineTrumpf(this);
            CurrentGameState = State.Playing;
            ActionPlayer = StartPlayer;
            await SendPlayerIsStartingTheRound(hub, GetPlayingPlayersConnectionIds());
        }

        public async Task SendPlayerIsPlayingGameTypeAndColor(SchafkopfHub hub, List<String> connectionIds) {
            String message = "";
            if (AnnouncedGame == GameType.Farbsolo)
            {
                message = $"{Leader.Name} spielt ein {Leader.AnnouncedColor}-{AnnouncedGame}";
            }
            else if (AnnouncedGame == GameType.Sauspiel)
            {
                message = $"{Leader.Name} spielt auf die {Leader.AnnouncedColor}-Sau";
            }
            else if (AnnouncedGame == GameType.Ramsch)
            {
                message = $"Es wird geramscht!";
            }
            else {
                message = $"{Leader.Name} spielt {AnnouncedGame}";
            }
            foreach (String connectionId in connectionIds)
            {
                await hub.Clients.Client(connectionId).SendAsync("ReceiveSystemMessage", message);
            }
        }

        public async Task SendPlayerIsStartingTheRound(SchafkopfHub hub, List<String> connectionIds) {
            foreach (String connectionId in connectionIds) {
                await hub.Clients.Client(connectionId).SendAsync(
                    "ReceiveSystemMessage",
                    $"{PlayingPlayers[ActionPlayer].Name} kommt raus"
                );
            }
        }

        private void FindTeams()
        {
            //Set up the team combination
            for (int i = 0; i < 4; i++)
            {
                if (AnnouncedGame == GameType.Ramsch)
                {
                    Groups[i] = 0;
                }
                else if (AnnouncedGame == GameType.Sauspiel)
                {
                    if (PlayingPlayers[i] == Leader)
                    {
                        Groups[i] = 1;
                    }
                    else
                    {
                        foreach (Card c in PlayingPlayers[i].HandCards)
                        {
                            if (c.Number == 11 && c.Color == Leader.AnnouncedColor)
                            {
                                Groups[i] = 1;
                                break;
                            }
                            else
                            {
                                Groups[i] = 0;
                            }
                        }
                    }
                }
                else if (AnnouncedGame == GameType.Hochzeit)
                {
                    if (PlayingPlayers[i] == Leader || PlayingPlayers[i] == HusbandWife)
                    {
                        Groups[i] = 1;
                    }
                    else
                    {
                        Groups[i] = 0;
                    }
                }
                // Wenz, Farbsolo, WenzTout, FarbsoloTout
                else if ((int)AnnouncedGame >= 3)
                {
                    if (PlayingPlayers[i] == Leader)
                    {
                        Groups[i] = 1;
                    }
                    else
                    {
                        Groups[i] = 0;
                    }
                }
            }
        }

        public async Task PlayCard(Player player, Color cardColor, int cardNumber, SchafkopfHub hub)
        {
            if (player != PlayingPlayers[ActionPlayer] || CurrentGameState != State.Playing)
            {
                return;
            }
            Card playedCard = await player.PlayCard(cardColor, cardNumber, hub);
            await Trick.AddCard(playedCard, player, hub, this);

            if (Trick.Count < 4)
            {
                ActionPlayer = (ActionPlayer + 1) % 4;
            } else {
                Player winner = Trick.GetWinner();
                winner.TakeTrick(Trick);
                TrickCount++;
                if (TrickCount == 8) {
                    await SendEndGameModal(hub, GetPlayingPlayersConnectionIds());
                }

                ActionPlayer = PlayingPlayers.FindIndex(p => p == winner);
                Trick = new Trick();
                Trick.DetermineTrumpf(this);
                await SendPlayerIsStartingTheRound(hub, GetPlayingPlayersConnectionIds());
            }
        }

        //-------------------------------------------------
        // The players can choose together to play another game,
        // there will be two options for the main-player
        // new game or quit
        //-------------------------------------------------
        public async Task SendEndGameModal(SchafkopfHub hub, List<String> connectionIds)
        {
            //Show the amount of pointfor each team
            if (AnnouncedGame > 0)
            {
                int leaderPoints = 0;
                int followerPoints = 0;
                for (int i = 0; i < 4; i++)
                {
                    if (Groups[i] == 0)
                    {
                        followerPoints += PlayingPlayers[i].Balance;
                    }
                    else
                    {
                        leaderPoints += PlayingPlayers[i].Balance;
                    }
                }
                string gameOverTitle = "";
                if (leaderPoints <= 60)
                {
                    gameOverTitle = "Die Spieler haben verloren";
                }
                else
                {
                    gameOverTitle = "Die Spieler haben gewonnen";
                }
                foreach (String connectionId in connectionIds)
                {
                    await hub.Clients.Client(connectionId).SendAsync(
                        "GameOver",
                        gameOverTitle,
                        $"Spieler: {leaderPoints} Punkte, Andere: {followerPoints} Punkte"
                    );
                }
            }
            else
            {
                List<Player> player = new List<Player>();

                for (int i = 0; i < 4; i++)
                {
                    player.Add(PlayingPlayers[i]);
                }

                player.OrderBy(o => o.Balance).ToList();
                foreach (String connectionId in connectionIds)
                {
                    await hub.Clients.Client(connectionId).SendAsync(
                    "GameOver",
                    "Ramsch vorbei",
                    String.Join(", ", player.Select(p => $"{p.Name}: {p.Balance} Punkte")));
                }

            }
            //Ask player whether they want to play another game
            //TODO::Wait for Button press
            //Do you want to play another game
            if (NewGame == false)
            {
                //End Game and push every player in the initial screen
                return;
            }
        }

        #region Player actions
        //-------------------------------------------------
        // Add a player to the game
        // The amount of player is limitless inside a game
        // The amount of playing players has to be excactly 4
        //-------------------------------------------------
        public async Task AddPlayer(Player player, SchafkopfHub hub)
        {
            if (player == null && Players.Contains(player))
            {
                throw new Exception("There is something wrong with the new player.");
            }
            Players.Add(player);
            if (CurrentGameState == State.Idle) {
                await PlayerPlaysTheGame(player, hub);
            } else {
                await SendAskWantToSpectate(hub, player.GetConnectionIds());
            }
        }

        //-------------------------------------------------
        // Remove player
        // Each player has the ability to leave the game, except when they are playing
        //-------------------------------------------------
        // public void RemovePlayer(Player player)
        // {
        //     if (player.Playing)
        //     {
        //         //Sorry, you can not leave the game during the game. You are able to quit the game.
        //         //TODO::Notification that the player is not allowed to leave the game;
        //         return;
        //     }
        //     if (!Players.Contains(player))
        //     {
        //         throw new Exception("There is something wrong with the player who wants to leave.");
        //     }
        //     PlayerDoesNotPlaysTheGame(player);
        //     Players.Remove(player);
        // }

        //-------------------------------------------------
        // Player decides to play the game
        //-------------------------------------------------
        public async Task PlayerPlaysTheGame(Player player, SchafkopfHub hub)
        {
            if (PlayingPlayers.Count < 4 && CurrentGameState == State.Idle)
            {
                player.Playing = true;
                lock (PlayingPlayers)
                {
                    if (!PlayingPlayers.Contains(player))
                    {
                        for (int i = 0; i < PlayingPlayers.Count; i++)
                        {
                            if (Players.IndexOf(PlayingPlayers[i]) > Players.IndexOf(player))
                            {
                                PlayingPlayers.Insert(i, player);
                                break;
                            }
                        }
                        if (!PlayingPlayers.Contains(player)) {
                            PlayingPlayers.Add(player);
                        }
                    }
                }
                await hub.UpdatePlayingPlayers();
                if (PlayingPlayers.Count == 4) {
                    await DealCards(hub);
                }
            }
            else
            {
                //Sorry, there are too many players who want to play, what about some Netflix and Chill?
            }
        }

        //-------------------------------------------------
        // Player decides to not play the next game
        //-------------------------------------------------
        public async Task PlayerDoesNotPlayTheGame(Player player, SchafkopfHub hub)
        {
            if (CurrentGameState != State.Idle)
            {
                //Sorry, you can not pause the game during the game. You are able to pause afterwards.
                //TODO::Notification that the player is not allowed to leave the game;
                return;
            }
            player.Playing = false;
            if (PlayingPlayers.Contains(player))
            {
                lock (PlayingPlayers)
                {
                    PlayingPlayers.Remove(player);
                }
                await hub.UpdatePlayingPlayers();
            }
        }
        #endregion

        //-------------------------------------------------
        // Shuffle the cards using the FISHER-YATES-Method
        //-------------------------------------------------
        public void Shuffle()
        {
            MixedCards = Cards;
            int n = MixedCards.Length;
            for (int i = 0; i < (n - 1); i++)
            {
                int r = i + random.Next(n - i);
                Card t = MixedCards[r];
                MixedCards[r] = MixedCards[i];
                MixedCards[i] = t;
            }
        }

        //-------------------------------------------------
        // Determines the partner for a Marriage (Hochzeit)
        //-------------------------------------------------
        public void IWantToMarryU(Player p)
        {
            HusbandWife = p;
        }

        public async Task SendConnectionToPlayerLostModal(SchafkopfHub hub, List<string> connectionIds) {
            foreach (String connectionId in connectionIds)
            {
                await hub.Clients.Client(connectionId).SendAsync(
                    "GameOver",
                    "Verbindung zu Spieler verloren",
                    "Möchtest du neustarten oder auf den anderen Spieler warten?"
                );
            }
        }

        public List<String> GetPlayingPlayersConnectionIds() {
            return PlayingPlayers.Aggregate(new List<String>(), (acc, x) => acc.Concat(x.GetConnectionIdsWithSpectators()).ToList());
        }

        public List<String> GetNonPlayingPlayersConnectionIds()
        {
            return Players
                    .Where(p => !PlayingPlayers.Contains(p))
                    .Aggregate(new List<String>(), (acc, x) => acc.Concat(x.GetConnectionIdsWithSpectators()).ToList());
        }

        public List<String> GetPlayersConnectionIds()
        {
            return Players.Aggregate(new List<String>(), (acc, x) => acc.Concat(x.GetConnectionIds()).ToList());
        }

        public async Task SendPlayers(SchafkopfHub hub)
        {
            if (PlayingPlayers.Count != 4) {
                foreach (String connectionId in GetPlayersConnectionIds())
                {
                    await hub.Clients.Client(connectionId).SendAsync(
                        "ReceivePlayers",
                        new String[4] {"", "", "", ""}
                    );
                }
                return;
            }
            for (int i = 0; i < 4; i++)
            {
                String[] permutedPlayers = new String[4];
                for (int j = 0; j < 4; j++)
                {
                    permutedPlayers[j] = PlayingPlayers[(j + i) % 4].Name;
                }
                foreach (String connectionId in PlayingPlayers[i].GetConnectionIdsWithSpectators())
                {
                    await hub.Clients.Client(connectionId).SendAsync(
                        "ReceivePlayers",
                        permutedPlayers
                    );
                }
            }
        }

        public async Task SendAskAnnounce(SchafkopfHub hub) {
            foreach (String connectionId in PlayingPlayers[ActionPlayer].GetConnectionIdsWithSpectators())
            {
                await hub.Clients.Client(connectionId).SendAsync("AskAnnounce");
            }
        }

        public async Task SendAskForGameType(SchafkopfHub hub)
        {
            for (int i = 0; i < 4; i++)
            {
                if (PlayingPlayers[ActionPlayer].WantToPlay)
                {
                    // game type not anounced
                    if (PlayingPlayers[ActionPlayer].AnnouncedGameType == GameType.Ramsch)
                    {
                        foreach (String connectionId in PlayingPlayers[ActionPlayer].GetConnectionIdsWithSpectators())
                        {
                            await hub.Clients.Client(connectionId).SendAsync("AskGameType");
                        }
                    }
                    // game type already anounnced for everyone
                    else
                    {
                        CurrentGameState = State.AnnounceGameColor;
                        // decide who plays and ask for color
                        DecideWhoIsPlaying();
                        await SendAskForGameColor(hub);
                    }
                    return;
                }
                ActionPlayer = (ActionPlayer + 1) % 4;
            }
            // no one wants to play => it's a ramsch
            await StartGame(hub);
        }
        public async Task SendAskForGameColor(SchafkopfHub hub)
        {
            // Leader has to choose a color he wants to play with or a color to escort his solo
            if (AnnouncedGame == GameType.Sauspiel || AnnouncedGame == GameType.Farbsolo)
            {
                foreach (String connectionId in Leader.GetConnectionIdsWithSpectators())
                {
                    await hub.Clients.Client(connectionId).SendAsync("AskColor");
                }
            }
            else if (AnnouncedGame == GameType.Hochzeit) //Hochzeit, announce husband or wife
            {
                //TODO::Wait for somebody to press the Button
            }
            else
            {
                await StartGame(hub);
            }
        }

        public async Task SendAskWantToSpectate(SchafkopfHub hub, List<String> connectionIds)
        {
            foreach (String connectionId in connectionIds)
            {
                await hub.Clients.Client(connectionId).SendAsync("AskWantToSpectate", PlayingPlayers.Select(p => p.Name));
            }
        }

        public async Task SendUpdatedGameState(Player player, SchafkopfHub hub) {
            if (CurrentGameState == State.Playing)
            {
                await SendPlayerIsPlayingGameTypeAndColor(hub, new List<string>() { hub.Context.ConnectionId });
                if (Trick.Count == 0)
                {
                    await SendPlayerIsStartingTheRound(hub, new List<string>() { hub.Context.ConnectionId });
                }
            }
            await player.SendHand(hub);
            await Trick.SendTrick(hub, this);
            await SendPlayers(hub);
            // send modals
            if (CurrentGameState == State.Playing && TrickCount == 8)
            {
                await SendEndGameModal(hub, new List<String>() { hub.Context.ConnectionId });
            }
            foreach (Player p in PlayingPlayers)
            {
                if (p.GetConnectionIds().Count == 0)
                {
                    await SendConnectionToPlayerLostModal(hub, new List<String>() { hub.Context.ConnectionId });
                    break;
                }
            }
            if (PlayingPlayers[ActionPlayer] == player)
            {
                if (CurrentGameState == State.Announce)
                {
                    await SendAskAnnounce(hub);
                }
                else if (CurrentGameState == State.AnnounceGameType)
                {
                    await SendAskForGameType(hub);
                }
            }
            if (Leader == player && CurrentGameState == State.AnnounceGameColor)
            {
                await SendAskForGameColor(hub);
            }
        }
    }
}

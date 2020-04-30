using System.Collections.Generic;
using Schafkopf.Models;

namespace Schafkopf.Logic
{
    public class GameState
    {
        public readonly List<Player> Players = new List<Player>();
        public List<Player> PlayingPlayers = new List<Player>();
        public readonly Carddeck Carddeck = new Carddeck();
        public State CurrentGameState = State.Idle;
        public int[] Groups = new int[] { 0, 0, 0, 0 };
        public int StartPlayer = -1;
        public int ActionPlayer = -1;
        public bool NewGame = false;
        public GameType AnnouncedGame = GameType.Ramsch;
        public Player Leader = null;
        public Player HusbandWife = null;
        public Trick Trick = null;
        public Trick LastTrick = null;
        public int TrickCount = 0;

    }
}
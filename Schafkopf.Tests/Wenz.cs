using Xunit;
using Schafkopf.Models;

namespace Schafkopf.UnitTests
{
    public class SchafkopfModels
    {
        private Trick trick;
        private Game game;
        private Player[] players;
        [Fact]
        public void Wenz()
        {
            game = new Game();
            game.AnnouncedGame = GameType.Wenz;

            players = new Player[4];
            players[0] = new Player("P1", "");
            players[1] = new Player("P2", "");
            players[2] = new Player("P3", "");
            players[3] = new Player("P4", "");

            trick = new Trick(game, 0);
            trick.AddCard(new Card(Color.Herz, 8), players[0], game);
            trick.AddCard(new Card(Color.Herz, 3), players[1], game);
            trick.AddCard(new Card(Color.Gras, 3), players[2], game);
            trick.AddCard(new Card(Color.Herz, 11), players[3], game);

            Assert.Equal(trick.GetWinner(), players[3]);
        }
    }
}

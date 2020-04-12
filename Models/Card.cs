using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Schafkopf.Models
{
    public class Card
    {
        public Color Color;
        public int Number;
        public int Value = 0;

        public Card(Color color, int number)
        {
            Color = color;
            Number = number;
        }

    }
}

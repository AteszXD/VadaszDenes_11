using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Bemutato.models
{
    public class RoverEvent
    {
        public string Time { get; set; }   // pl. "12:30"
        public string Icon { get; set; }   // pl. "⛏", "⚡", "🚀"
        public Brush Color { get; set; }   // pl. Brushes.Yellow
        public string Message { get; set; }
    }
}

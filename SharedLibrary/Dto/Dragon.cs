using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibrary.Dto
{
    public class Dragon
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Element { get; set; } = string.Empty;
        public string Class { get; set; } = string.Empty;
        public int Defence { get; set; }
        public string Map { get; set; } = string.Empty;
    }
}
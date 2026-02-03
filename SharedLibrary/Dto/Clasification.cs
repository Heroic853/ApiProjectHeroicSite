using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibrary.Dto
{
    public class Clasification
    {
        public int Id { get; set; }
        public string Monster { get; set; } = string.Empty;
        public string Feedback { get; set; } = string.Empty;
    }
}
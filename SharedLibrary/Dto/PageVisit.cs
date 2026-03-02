using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibrary.Dto
{
    public class PageVisit
    {
        public int Id { get; set; }
        public DateTime VisitedAt { get; set; } = DateTime.Now;
    }
}

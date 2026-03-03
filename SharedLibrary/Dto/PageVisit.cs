using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibrary.Dto
{
    public class PageVisit
    {
        public Guid Id { get; set; } 
        public DateTime VisitedAt { get; set; }
        public string? PageName { get; set; }
        public string? UserEmail { get; set; }
    }
}

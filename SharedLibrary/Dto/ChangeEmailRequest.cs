using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibrary.Dto
{
    public class ChangeEmailRequest
    {
        public string Username { get; set; }
        public string NewEmail { get; set; }
    }
}

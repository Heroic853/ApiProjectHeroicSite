using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibrary.Dto
{
    public class UserProfileResponse
    {
        public string Username { get; set; }
        public string Account { get; set; }
        public DateTime RegistrationDate { get; set; }
    }
}

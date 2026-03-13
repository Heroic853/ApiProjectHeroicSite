using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibrary.Dto
{
    public class CheckoutRequest
    {
        public string PlanName { get; set; }
        public long AmountCents { get; set; }
    }
}

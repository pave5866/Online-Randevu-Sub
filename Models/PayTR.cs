using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Randevu.Models
{
    public class PayTR
    {
        public string Status { get; set; }
        public string Token { get; set; }
        public string Reason { get; set; }
        public string Url { get; set; }
        public bool Visible { get; set; }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Randevu.Models
{
    public class SmsSettings
    {
        public required string Ka { get; set; }
        public required string Pwd { get; set; }
        public string? Org { get; set; }
        public required string Url { get; set; }
    }
}
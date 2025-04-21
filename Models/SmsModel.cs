using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Randevu.Models
{
    public class SmsModel
    {
        public required string Content { get; set; }
        public required List<string> NumberList { get; set; }
    }
}
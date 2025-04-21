using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Randevu.Models
{
    public class PayTRResponse
    {
        public required string Merchant_oid { get; set; }
        public required string Status { get; set; }
        public required string Total_amount { get; set; }
        public required string Hash { get; set; }
        public string? Failed_reason_code { get; set; }
        public string? Failed_reason_msg { get; set; }
        public string? Test_mode { get; set; }
        public required string Payment_type { get; set; }
        public required string Currency { get; set; }
        public required string Payment_amount { get; set; }
    }
}
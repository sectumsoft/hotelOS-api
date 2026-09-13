using HotelManagement.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HotelManagement.Domain.Entities
{
    public class HotelSettings : BaseEntity
    {
        public string HotelName { get; set; }
        public string Subdomain { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }

        /// <summary>
        /// GST/tax rate for this hotel, configured once instead of typed per bill.
        /// Room rates and extra-service prices are tax-INCLUSIVE — this is used to
        /// back the tax portion out of the total for display, not add it on top.
        /// </summary>
        public decimal TaxPercent { get; set; } = 0;
    }
}

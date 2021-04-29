using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ModernTacoShop.WebApp.Models
{
    public class MenuPageItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string DisplayPrice => $"{Price:C}";
        public string OrderPrice => $"{(Quantity * (Price + (CartExtraOptions?.Sum(s => (double)(s.UnitAmount / 100)) ?? 0))):C}";
        public int Likes { get; set; }
        public double Price { get; set; }
        public int Quantity { get; set; } = 1;

        public List<ExtraOption> CartExtraOptions { get; set; }
        public List<ExtraOption> SourceExtraOptions { get; set; }
    }
}

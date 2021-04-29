using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ModernTacoShop.WebApp.Models
{
    public class MenuPageItemList
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public MenuPageItem[] Items { get; set; }
    }
}

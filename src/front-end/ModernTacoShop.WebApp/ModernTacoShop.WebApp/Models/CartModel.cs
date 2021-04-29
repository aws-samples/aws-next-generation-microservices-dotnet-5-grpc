using System;
using System.Collections.Generic;

namespace ModernTacoShop.WebApp.Models
{
    public class CartModel
    {
        public long OrderId { get; set; }
        public DateTimeOffset OrderPlaced { get; set; }
        public List<MenuPageItem> MealOrdered { get; set; } = new List<MenuPageItem>();
        public DeliveryInfoModel DeliveryInfo { get; set; }

        public SubmitOrder.Protos.Order CreateOrderToSubmit()
        {
            var order = new SubmitOrder.Protos.Order()
            {
                OrderId = this.OrderId
                // TODO
            };

            return order;
        }
    }
}
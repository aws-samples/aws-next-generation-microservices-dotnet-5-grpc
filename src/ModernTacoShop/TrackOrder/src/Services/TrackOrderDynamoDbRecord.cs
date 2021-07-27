using System;
using Amazon.DynamoDBv2.DataModel;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Builder;
using ModernTacoShop.TrackOrder.Protos;

namespace ModernTacoShop.TrackOrder.Server
{
    [DynamoDBTable("ModernTacoShop-TrackOrder-Order")]
    public class TrackOrderDynamoDbRecord
    {
        public TrackOrderDynamoDbRecord() { }

        public TrackOrderDynamoDbRecord(Order order)
        {
            Id = order.OrderId;
            LastPositionLatitude = order.LastPosition?.Latitude;
            LastPositionLongitude = order.LastPosition?.Longitude;
            LastUpdated = order.LastUpdated.ToDateTime().ToUniversalTime();
            PlacedOn = order.PlacedOn.ToDateTime().ToUniversalTime();
            Status = System.Enum.GetName(order.Status);
        }

        public Order ToOrder()
        {
            var result = new Order()
            {
                LastPosition = null,
                LastUpdated = Timestamp.FromDateTime(LastUpdated.ToUniversalTime()),
                OrderId = Id,
                PlacedOn = Timestamp.FromDateTime(PlacedOn.ToUniversalTime()),
                Status = System.Enum.Parse<OrderStatus>(Status),
            };

            // Create a Point for the last position if we have lat & long values.
            if (!string.IsNullOrEmpty(LastPositionLatitude) && !string.IsNullOrEmpty(LastPositionLongitude))
            {
                result.LastPosition = new Point()
                {
                    Latitude = LastPositionLatitude,
                    Longitude = LastPositionLongitude
                };
            }

            return result;
        }

        [DynamoDBHashKey]
        public long Id { get; set; }

        [DynamoDBProperty]
        public string LastPositionLatitude { get; set; }

        [DynamoDBProperty]
        public string LastPositionLongitude { get; set; }

        [DynamoDBProperty]
        public DateTime LastUpdated { get; set; }

        [DynamoDBProperty]
        public DateTime PlacedOn { get; set; }

        [DynamoDBProperty]
        public string Status { get; set; }
    }
}

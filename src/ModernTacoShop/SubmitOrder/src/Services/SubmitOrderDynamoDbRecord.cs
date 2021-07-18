using System;
using Amazon.DynamoDBv2.DataModel;
using ModernTacoShop.SubmitOrder.Protos;

namespace ModernTacoShop.SubmitOrder.Server
{
    [DynamoDBTable("ModernTacoShop-SubmitOrder-Order")]
    public class SubmitOrderDynamoDbRecord
    {
        public SubmitOrderDynamoDbRecord() { }

        public SubmitOrderDynamoDbRecord(Order order)
        {
            Id = order.OrderId;
            Comments = order.Comments;
            PlacedOn = order.PlacedOn.ToDateTime().ToUniversalTime();
            TacoCountBeef = order.TacoCountBeef;
            TacoCountCarnitas = order.TacoCountCarnitas;
            TacoCountChicken = order.TacoCountChicken;
            TacoCountShrimp = order.TacoCountShrimp;
            TacoCountTofu = order.TacoCountTofu;
        }

        [DynamoDBHashKey]
        public long Id { get; set; }

        [DynamoDBProperty]
        public string Comments { get; set; }

        [DynamoDBProperty]
        public DateTime PlacedOn { get; set; }

        [DynamoDBProperty]
        public uint TacoCountBeef { get; set; }

        [DynamoDBProperty]
        public uint TacoCountCarnitas { get; set; }

        [DynamoDBProperty]
        public uint TacoCountChicken { get; set; }

        [DynamoDBProperty]
        public uint TacoCountShrimp { get; set; }

        [DynamoDBProperty]
        public uint TacoCountTofu { get; set; }
    }
}

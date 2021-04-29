using System;
using Google.Protobuf.Collections;
using Grpc.Net.Client;
using Xunit;

namespace ModernTacoShop.SubmitOrder.Server.Test
{
    public class SubmitOrderServerUnitTest
    {
        [Fact]
        public async void TestSubmitOrder_1()
        {
            var submitOrderServiceDomainName = "submit-order.modern-taco-shop.clinmatt.people.aws.dev";

            using var channel = GrpcChannel.ForAddress($"https://{submitOrderServiceDomainName}");
            var client = new ModernTacoShop.SubmitOrder.Protos.SubmitOrder.SubmitOrderClient(channel);
            var reply = await client.SubmitOrderAsync(new SubmitOrder.Protos.Order
            {
                OrderId = DateTime.UtcNow.Ticks,
                OrderJson = "{ 'foo': 'bar' }"
            });
        }
    }
}

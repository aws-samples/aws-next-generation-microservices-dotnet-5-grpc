/*
 * Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
 * SPDX-License-Identifier: MIT-0
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy of this
 * software and associated documentation files (the "Software"), to deal in the Software
 * without restriction, including without limitation the rights to use, copy, modify,
 * merge, publish, distribute, sublicense, and/or sell copies of the Software, and to
 * permit persons to whom the Software is furnished to do so.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
 * INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A
 * PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
 * HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
 * OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
 * SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */
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

using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using ModernTacoShop.WebApp.Models;

namespace ModernTacoShop.WebApp.Pages
{
    public partial class Checkout
    {
        [Inject]
        HttpClient Http { get; set; }

        [Inject]
        NavigationManager NavigationManager { get; set; }

        [Inject]
        IConfiguration Configuration { get; set; }

        [Inject]
        Blazored.LocalStorage.ILocalStorageService LocalStorage { get; set; }

        [Parameter]
        public string OrderId { get; set; }

        private CartModel cartModel;
        public StateModel[] States { get; set; }

        protected async override Task OnInitializedAsync()
        {
            if (await LocalStorage.ContainKeyAsync(OrderId))
            {
                cartModel = await LocalStorage.GetItemAsync<CartModel>(OrderId);
            }
        }

        protected async override Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender && (await LocalStorage.ContainKeyAsync(OrderId)))
            {
                States = await Http.GetFromJsonAsync<StateModel[]>("sample-data/states_titlecase.json");
            }
        }

        private async Task SubmitOrderAsync()
        {
            if (cartModel?.MealOrdered?.Any() ?? false)
            {
                var order = cartModel.CreateOrderToSubmit();

                var submitOrderUri = Configuration.GetValue<string>("ServiceUris:SubmitOrder");

                var httpHandler = new GrpcWebHandler(GrpcWebMode.GrpcWebText, new HttpClientHandler());
                var submitOrderChannel = GrpcChannel.ForAddress(submitOrderUri, new GrpcChannelOptions { HttpHandler = httpHandler });
                var submitOrderClient = new ModernTacoShop.SubmitOrder.Protos.SubmitOrder.SubmitOrderClient(submitOrderChannel);
                await submitOrderClient.SubmitOrderAsync(order);

                NavigationManager.NavigateTo($"/Tracking/{OrderId}");
            }
        }
    }
}
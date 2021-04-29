using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using ModernTacoShop.WebApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace ModernTacoShop.WebApp.Pages
{
    //https://track-order.modern-taco-shop.clinmatt.people.aws.dev
    //https://submit-order.modern-taco-shop.clinmatt.people.aws.dev
    public partial class Index
    {
        private const string ORDER_IN_PROGRESS = "ORDER_IN_PROGRESS";
        private MenuPageItemList[] itemLists;
        private MenuPageItem selectedItem;
        private ExtraOption[] extraOption;
        // private IList<MenuPageItem> CartOrders;
        private CartModel Cart ;
        [Inject]
        HttpClient Http { get; set; }
        [Inject]
        IJSRuntime JS { get; set; }
        [Inject]
        Blazored.LocalStorage.ILocalStorageService LocalStorage { get; set; }

        [Inject]
        NavigationManager NavigationManager { get; set; }
        protected override async Task OnInitializedAsync()
        {
            itemLists = await Http.GetFromJsonAsync<MenuPageItemList[]>("sample-data/menu_data.json");
            extraOption = await Http.GetFromJsonAsync<ExtraOption[]>("sample-data/extra_options.json");
            Cart = new CartModel();

            if (await LocalStorage.ContainKeyAsync(ORDER_IN_PROGRESS))
            {
                var orderId = await LocalStorage.GetItemAsStringAsync(ORDER_IN_PROGRESS);
                Cart = await LocalStorage.GetItemAsync<CartModel>(orderId);
            }
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                await JS.InvokeVoidAsync("initBootstrapModel", "#mealModal");
            }
        }

        private static void MinusQuantity(MenuPageItem item)
        {
            if (item.Quantity > 1)
            {
                item.Quantity--;
            }
        }

        private static void PlusQuantity(MenuPageItem item)
        {
            item.Quantity++;
        }

        private async Task OpenModelSelectdItem(MenuPageItem item)
        {
            if (item.SourceExtraOptions == null) item.SourceExtraOptions = new List<ExtraOption>();

            if (!item.SourceExtraOptions.Any())
            {
                item.SourceExtraOptions.AddRange((ExtraOption[])extraOption.Clone());
            }


            await JS.InvokeVoidAsync("openBootstrapModel", "#mealModal");
            selectedItem = item;

        }

        private async Task OnExtraSelected(ChangeEventArgs e, ExtraOption extraOption)
        {
            Console.WriteLine(e);
            if (e.Value is bool isChecked)
            {
                if (selectedItem.CartExtraOptions == null) selectedItem.CartExtraOptions = new List<ExtraOption>();
                if (isChecked)
                {
                    selectedItem.CartExtraOptions.Add(extraOption);
                }
                else
                {
                    selectedItem.CartExtraOptions.Remove(extraOption);
                }
                extraOption.IsChecked = isChecked;

            }
            await Task.CompletedTask;
        }

        private async Task OnAddToCartClicked()
        {
            await Task.CompletedTask;
            if (Cart.MealOrdered == null) Cart.MealOrdered = new List<MenuPageItem>();

            Cart.MealOrdered.Add(selectedItem);
            await JS.InvokeVoidAsync("closeBootstrapModel", "#mealModal");
        }

        private async Task GoToCheckout()
        {
            if (Cart.MealOrdered != null && Cart.MealOrdered.Any())
            {
                Cart.OrderId = (new Random()).Next(1, int.MaxValue);
                await LocalStorage.SetItemAsync(ORDER_IN_PROGRESS, Cart.OrderId);
                await LocalStorage.SetItemAsync(Cart.OrderId.ToString(), Cart);
                NavigationManager.NavigateTo($"/Checkout/{Cart.OrderId}");
            }
        }

    }
}

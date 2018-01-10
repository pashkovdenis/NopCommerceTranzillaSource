using Nop.Core.Plugins;
using Nop.Services.Payments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nop.Core.Domain.Orders;
using System.Web.Routing;
using Nop.Core.Domain.Payments;
using Nop.Services.Configuration; 
using Nop.Core;
using Nop.Core.Infrastructure;
using Nop.Services.Logging;
using System.Net;
using System.IO;
using System.Web;
using Nop.Plugin.Payment.Tranzilla.Models;
using Nop.Plugin.Payment.Tranzilla.Controllers;
using Nop.Services.Common;

namespace Nop.Plugin.Payments.Tranzilla
{

    public class TranzillaProcsessor : BasePlugin, IPaymentMethod
    {

        private readonly ISettingService _settingService;  
        private readonly IWebHelper _webHelper;  
        public TranzillaProcsessor(
             ISettingService settingService,
             IWebHelper webHelper
            )
        {
            _webHelper = webHelper;

            this._settingService = settingService; 
        } 

        public bool SupportCapture => true;

        public bool SupportPartiallyRefund => false;

        public bool SupportRefund => false;

        public bool SupportVoid => false;

        public RecurringPaymentType RecurringPaymentType => RecurringPaymentType.NotSupported;

        public PaymentMethodType PaymentMethodType => PaymentMethodType.Redirection;

        public bool SkipPaymentInfo => false;

        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            return new CancelRecurringPaymentResult();
        }
        public bool CanRePostProcessPayment(Order order) => false;
           
 
        /// <summary>
        /// 
        /// </summary>
        /// <param name="capturePaymentRequest"></param>
        /// <returns></returns>
        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            var result = new CapturePaymentResult();
            var order = capturePaymentRequest.Order; 
              ServicePointManager.SecurityProtocol =  SecurityProtocolType.Ssl3 
                | SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12; 

            if (!string.IsNullOrEmpty(order.GetAttribute<string>("TranzilaTK")))
            {  
                var settings = _settingService.LoadSetting<TranzillaConfiguration>(capturePaymentRequest.Order.StoreId);
                var operationLog = new StringBuilder();   
                int fpay = int.Parse((order.GetAttribute<string>("fpay")??"0"));
                int npay = int.Parse(order.GetAttribute<string>("npay")??"0");
                int spay = int.Parse(order.GetAttribute<string>("spay")??"0"); 

                string cvv = "";
                if (order.GetAttribute<string>("cvv") != null)
                    cvv = order.GetAttribute<string>("cvv");  

                operationLog.AppendLine($"default from order  :  npay = {npay} fpay = {fpay} spay={spay}");  

                if (npay > 0)
                {  
                    int first = (int)Math.Abs( order.OrderTotal / (npay + 1));   
                    if (  (order.OrderTotal / (npay + 1)) % 1 ==0    )
                        fpay = first ;
                    else 
                        fpay = first + 1;   
                    spay = first;    
                }  

                var url = $"https://secure5.tranzila.com/cgi-bin/tranzila71u.cgi?supplier={settings.TokenAuth}&TranzilaPW={order.GetAttribute<string>("TranzilaPW")}&TranzilaTK={order.GetAttribute<string>("TranzilaTK")}&expdate={order.GetAttribute<string>("expdate")}&sum={order.OrderTotal.ToString("#.##")}&cy=1&cred_type=1&tranmode=A&npay={npay}&fpay={fpay}&spay={spay}";
       
                try
                {
                    operationLog.AppendLine(url);

                    var response = new WebClient().DownloadString(url); 

                    operationLog.AppendLine($"Sending Request {url}");  
                    operationLog.AppendLine("Response : "+ response);

                    if (response.Contains("Response=000"))
                    {
                        result.NewPaymentStatus = PaymentStatus.Paid;
                        operationLog.AppendLine($" MARK AS PAID "); 
                    }
                    else
                    {
                        result.NewPaymentStatus = PaymentStatus.Authorized;
                        operationLog.AppendLine($" MARK AS NOT PAID  "); 
                    } 

                }
                catch(Exception e)
                {
                    operationLog.AppendLine("Error " + e.Message) ; 
                }
                finally
                { 
                    EngineContext.Current.Resolve<ILogger>().InsertLog(Core.Domain.Logging.LogLevel.Information, 
                        "CapturePAyment", operationLog.ToString()); 
                    System.IO.File.WriteAllText(EngineContext.Current.Resolve<HttpContextBase>().Server.MapPath("~/Content/Capture.txt"), url +" "+ operationLog.ToString()); 
                }
            }
            else
            {
                result.NewPaymentStatus = PaymentStatus.Pending; 
            }
			
            return result;
        }

       
        public decimal GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            var id= EngineContext.Current.Resolve<IStoreContext>().CurrentStore.Id;
            var settings = _settingService.LoadSetting<TranzillaConfiguration>(id);
            decimal total = cart.Sum(c => c.Product.Price); 
            return (total*settings.AdditionalPercentFee); 
        }   

        public void GetConfigurationRoute(out string actionName, out string controllerName, out System.Web.Routing.RouteValueDictionary routeValues)
        {
            actionName = "Configure";
            controllerName = "Tranzilla";
            routeValues = new RouteValueDictionary()
            {
                { "Namespaces", "Nop.Plugin.Payments.Tranzilla.Controllers" },
                { "area", null }
            };
        }

        public Type GetControllerType() => typeof(TranzillaController);
         
        public void GetPaymentInfoRoute(out string actionName, out string controllerName, out System.Web.Routing.RouteValueDictionary routeValues)
        {
            actionName = "PaymentInfo";
            controllerName = "Tranzilla";

            routeValues = new RouteValueDictionary()
            {
                { "Namespaces", "Nop.Plugin.Payments.Tranzilla.Controllers" },
                { "area", null }
            };
        }

        public bool HidePaymentMethod(IList<ShoppingCartItem> cart) => false;
         
       
        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            var settings = _settingService.LoadSetting<TranzillaConfiguration>(postProcessPaymentRequest.Order.StoreId); 
            var url = $"https://direct.tranzila.com/{settings.Terminal}/iframe.php?sum={postProcessPaymentRequest.Order.OrderTotal.ToString("#.##")}&maxpay={settings.GetNumberOfPayments(postProcessPaymentRequest.Order.OrderTotal)}&currency={settings.Currency}&nologo=1&cred_type={settings.CreditType}{(string.IsNullOrEmpty(settings.TranMode) ? "" : $"&tranmode={settings.TranMode}")}&lang={settings.Lang}&orderId={postProcessPaymentRequest.Order.OrderGuid}&trTextColor=503F2E&trButtonColor=503F2E";
            EngineContext.Current.Resolve<HttpContextBase>().Session["tranzilla"] = url;
             
        } 

        public ProcessPaymentResult ProcessPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            result.NewPaymentStatus = PaymentStatus.Pending;  
            var settings = _settingService.LoadSetting<TranzillaConfiguration>(processPaymentRequest.StoreId);
            var url = $"https://direct.tranzila.com/{settings.Terminal}/iframe.php?sum={processPaymentRequest.OrderTotal.ToString("#.##")}&maxpay={settings.GetNumberOfPayments(processPaymentRequest.OrderTotal)}&nologo=1&currency={settings.Currency}&cred_type={settings.CreditType}{(string.IsNullOrEmpty(settings.TranMode) ? "" : $"&tranmode={settings.TranMode}")}&lang={settings.Lang}&orderId={processPaymentRequest.OrderGuid.ToString()}&trTextColor=503F2E&trButtonColor=503F2E";
            EngineContext.Current.Resolve<HttpContextBase>().Session["tranzilla"] = url; 
            return result;
        }
                 

        // 
        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest) => new ProcessPaymentResult();
        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest) => new RefundPaymentResult();
        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest) => new VoidPaymentResult(); 



    }
}

using Nop.Web.Framework.Mvc.Routes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web.Routing;

namespace Nop.Plugin.Payments.Tranzilla
{
    public class RouteProvider : IRouteProvider
    {
        public int Priority
        {
            get
            {
                return 0;
            }
        }

        public void RegisterRoutes(RouteCollection routes)
        {

            routes.MapRoute("tranzilla_success",
                 "Plugins/Tranzilla/success",
                 new { controller = "Tranzilla", action = "success" },
                 new[] { "Nop.Plugin.Payments.Tranzilla.Controllers" }
            );


            routes.MapRoute("tranzilla_dropdown",
                       "Plugins/Tranzilla/dropdown",
                       new { controller = "Tranzilla", action = "getDropdown" },
                       new[] { "Nop.Plugin.Payments.Tranzilla.Controllers" }
            );

            routes.MapRoute("tranzilla_fail",
                            "Plugins/Tranzilla/fail",
                            new { controller = "Tranzilla", action = "fail" },
                            new[] { "Nop.Plugin.Payments.Tranzilla.Controllers" });

            routes.MapRoute("tranzilla_payment",
                            "Plugins/Tranzilla/payment",
                            new { controller = "Tranzilla", action = "payment" },
                            new[] { "Nop.Plugin.Payments.Tranzilla.Controllers" });


            routes.MapRoute("tranzilla_PaymentInfo",
                         "Plugins/Tranzilla/PaymentInfo",
                         new { controller = "Tranzilla", action = "PaymentInfo" },
                         new[] { "Nop.Plugin.Payments.Tranzilla.Controllers" });

            routes.MapRoute("RemovePaymentOption",
                         "Plugins/Tranzilla/RemovePaymentOption",
                         new { controller = "Tranzilla", action = "RemovePaymentOption" },
                         new[] { "Nop.Plugin.Payments.Tranzilla.Controllers" });

            routes.MapRoute("tranzilla_ValidatePaymentForm",
                         "Plugins/Tranzilla/ValidatePaymentForm",
                         new { controller = "Tranzilla", action = "ValidatePaymentForm" },
                         new[] { "Nop.Plugin.Payments.Tranzilla.Controllers" });

            routes.MapRoute("updatePayments",
                      "Plugins/Tranzilla/updatePayments",
                      new { controller = "Tranzilla", action = "updatePayments" },
                      new[] { "Nop.Plugin.Payments.Tranzilla.Controllers" });


            routes.MapRoute("tranzilla_notify",
          "Plugins/Tranzilla/notify",
          new { controller = "Tranzilla", action = "notify" },
          new[] { "Nop.Plugin.Payments.Tranzilla.Controllers" }


     );


            routes.MapRoute("TRanzilla_PaymentPage",
           "Plugins/Tranzilla/PaymentPage",
          new { controller = "Tranzilla", action = "PaymentPage" },
          new[] { "Nop.Plugin.Payments.Tranzilla.Controllers" }


     );


        }
    }
}
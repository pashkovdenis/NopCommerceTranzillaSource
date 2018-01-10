 
using Nop.Core.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Nop.Plugin.Payment.Tranzilla.Models
{

    public class TranzillaConfiguration : ISettings
    {

        public string Terminal  { get; set; } = "";

        public string TokenAuth { set; get; } = "";

        public string TranMode { set; get; } = "";

        public string Currency { set; get; } = "1";

        public string Lang { set; get; } = "heb"; 

        public string PaymentInfo { set; get; } = "Tranzilla";  

        public decimal AdditionalPercentFee { set; get; } = decimal.Zero; 

        public int CreditType { set; get; } = 8; 

        public int MaxPay { set; get; } = 12;

        public string PaymentSettings { set; get; } = string.Empty;

        public string BaseUrl { set; get; } = "https://direct.tranzila.com/cexpress/iframe.php";
         

        public IList<PaymentModel> GetPaymentsSettings()
        {
            if (!string.IsNullOrEmpty(PaymentSettings))
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
 
                return serializer.Deserialize<IList<PaymentModel>>(PaymentSettings); 
            }
            return new List<PaymentModel>();
        }


        public void SavePayments(IList<PaymentModel> payments)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            PaymentSettings = serializer.Serialize(payments); 

        }


        public int GetNumberOfPayments(decimal total)
        {
            var payments = GetPaymentsSettings(); 
            if (GetPaymentsSettings().Any(p=>total>=p.StartsFrom  && total <= p.UpTo))
               return  GetPaymentsSettings().First(p => total >= p.StartsFrom && total <= p.UpTo).Payments;  
            return 0;             
        }

         
    }
}

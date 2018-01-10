using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nop.Plugin.Payment.Tranzilla.Models
{
    public class PaymentModel
    { 
        public int Id { set; get; } = 1;  
        [Required]
        public int StartsFrom { set; get; } 
        [Required]
        public int UpTo { set; get; } 
        [Required]
        public int Payments { set; get; } 

    }
}

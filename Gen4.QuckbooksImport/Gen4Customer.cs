using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gen4.QuckbooksImport
{
    public class Gen4Customer
    {
        public int CustomerID { get; set; }
        public string CompanyName { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Status { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string PostalCode { get; set; }
        public string LoginName { get; set; }
        public CustomerQuickbookMatch QuickbooksMatch { get; set; }
        public QuickbooksAction Action { get; set; }
        public string QuickbooksGen4Action { get; set; }
        public string QuickbookPaymentPlan { get; set; }
        public string QuickbookCustomerID { get; set; }
        public bool IsErrorPresent { get; set; }
        public string ErrorMessage { get; set; }

        public Gen4Customer()
        {
            QuickbooksMatch = CustomerQuickbookMatch.NotFound;
        }
    }


    public enum CustomerQuickbookMatch
    {
        NotFound,
        Matched,
        Mismatched,
    }
}

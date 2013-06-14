using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gen4.QuckbooksImport
{
    public class Gen4Lead
    {
        public int TransactionID { get; set; }
        public int? ParentTransactionID { get; set; }
        public int CustomerID { get; set; }
        public string Company { get; set; }
        public string CustomerName { get; set; }
        public string CustomerEmail { get; set; }
        public string CustomerPhone { get; set; }
        public int LeadID { get; set; }
        public string LeadType { get; set; }
        public string PatientInfo { get; set; }
        public int MarketID { get; set; }
        public string Source { get; set; }
        public string Description { get; set; }
        public string TransactionType { get; set; }
        public bool IsReported { get; set; }
        public DateTime Created { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gen4.QuckbooksImport
{
    public class IdaLeadsManager
    {
        private List<Gen4PplLedger> Gen4PplLedgerTransactions;
        private List<Gen4Customer> Gen4Customers;
        Dictionary<int, string> ExcludedCustomers;
        private List<Gen4Transaction> Gen4Transactions;
        private List<Customer> QuickbooksCustomers;
        private List<Item> QuickbookItems;

        public List<Gen4Customer> GetIdaCustomers(out ActionResult actionResult)
        {
            this.Gen4Customers = GenerateIdaCustomers(out actionResult);
            return this.Gen4Customers;
        }

        private List<Gen4Customer> GenerateIdaCustomers(out ActionResult actionResult)
        {
            actionResult = new ActionResult();
            Gen4LiveEntities gen4Ctx = new Gen4LiveEntities();
            Gen4Customers = (from c in gen4Ctx.IdaCustomers
                             join b in gen4Ctx.ContactPersons on c.CustomerID equals b.EntityID
                             join f in gen4Ctx.Principals on b.PrincipalID equals f.PrincipalID
                             join d in gen4Ctx.CustomerStatuses on c.Status equals d.CustomerStatusID
                             where c.CustomerID == b.EntityID && b.EntityScope == 5
                             && f.PrincipalType == 2 && b.ContactPersonType == 0
                             select new Gen4Customer
                             {
                                 CustomerID = c.CustomerID,
                                 CompanyName = c.CompanyName,
                                 Status = d.DisplayName,
                                 FirstName = b.FirstName,
                                 LastName = b.LastName,
                                 Email = b.Email,
                                 Phone = b.Phone,
                                 City = b.City,
                                 State = b.State,
                                 PostalCode = b.PostalCode,
                                 LoginName = f.LoginName,
                                 //QuickbooksMatch = CustomerQuickbookMatch.NotFound,
                                 //Action = QuickbooksAction.None,
                                 QuickbookPaymentPlan = null,
                                 IsErrorPresent = false,
                                 ErrorMessage = null,
                             }).ToList();

            // Get the list of all Test and Play Customer Account IDs
            var excludedTestCustomersIDs = (from c in gen4Ctx.CustomerDataFlakes
                                            where c.JsonData.Contains("\"IsPlayAccount\":\"True\"")
                                            || c.JsonData.Contains("\"IsTestAccount\":\"True\"")
                                            select c.CustomerID).ToList();
            // Getting the list of Excluded Gen4 Customers - this customers will be ignore from
            // any processing.
            System.Xml.Linq.XElement a = System.Xml.Linq.XElement.Load("ExcludedGen4Customers.xml");
            var gen4ExcludedCustomers = (from x in a.Descendants("Gen4Customer")
                                         select x).ToList();
            ExcludedCustomers = new Dictionary<int, string>();
            foreach (var gen4ExcludedCustomer in gen4ExcludedCustomers)
            {
                if (gen4ExcludedCustomer.Attribute("id") != null) 
                {
                    int excludedCustomerID;
                    if (System.Int32.TryParse(gen4ExcludedCustomer.Attribute("id").Value, out excludedCustomerID))
                    {
                        string excludedCustomerName = "";
                        if (gen4ExcludedCustomer.Attribute("name") != null)
                        {
                            excludedCustomerName = gen4ExcludedCustomer.Attribute("name").Value;
                        }
                        ExcludedCustomers.Add(excludedCustomerID, excludedCustomerName);
                    }
                }
            }
            // Update Gen4 Customer's list by removing all excluded customers from it.
            for (int i = Gen4Customers.Count - 1; i >= 0; i--)
            {
                if ((from x in ExcludedCustomers
                     where x.Key == Gen4Customers[i].CustomerID
                     select x).Count() > 0 
                    || excludedTestCustomersIDs.Contains(Gen4Customers[i].CustomerID))
                {
                    var aa = Gen4Customers[i].CustomerID;
                    Gen4Customers.Remove(Gen4Customers[i]);
                }
            }

            IdaQuickbookEntities ctxQuickbook = new IdaQuickbookEntities();
            QuickbooksCustomers = (from c in ctxQuickbook.Customers
                                  select c).ToList();
            foreach (var customer in QuickbooksCustomers)
            {
                int Gen4CustomerID = 0;
                string PplPlanName = null;
                string Gen4Action = null;
 
                var xElement = System.Xml.Linq.XElement.Parse("<CustomFields>" + customer.CustomFields + "</CustomFields>");
                foreach (var b in xElement.Descendants("CustomField"))
                {
                    if (b.Element("Name").Value == "Gen4CustomerID")
                    {
                        Gen4CustomerID = Convert.ToInt32(b.Element("Value").Value);
                    }

                    if (b.Element("Name").Value == "Gen4 Action")
                    {
                        Gen4Action = b.Element("Value").Value;
                    }

                    if (b.Element("Name").Value == "PPL Plan")
                    {
                        PplPlanName = b.Element("Value").Value;
                    }
                }

                if (Gen4CustomerID != 0)
                {
                    var gen4Customer = (from c in Gen4Customers
                                        where c.CustomerID == Gen4CustomerID
                                        select c).SingleOrDefault();
                    if (gen4Customer != null)
                    {
                        gen4Customer.QuickbookCustomerID = customer.ID;
                        if (gen4Customer.CompanyName.ToLower() == customer.Company.ToLower() ||
                            gen4Customer.LastName.ToLower() == customer.LastName.ToLower()
                            || gen4Customer.FirstName.ToLower() == customer.FirstName.ToLower()
                            || gen4Customer.Phone.Replace(" ", "") == customer.Phone.Replace(" ", "")
                            || gen4Customer.Email.ToLower() == customer.Email.ToLower())
                        {
                            gen4Customer.QuickbooksMatch = CustomerQuickbookMatch.Matched;
                        }
                        else
                        {
                            gen4Customer.QuickbooksMatch = CustomerQuickbookMatch.Mismatched;
                        }

                        if (!String.IsNullOrEmpty(Gen4Action))
                        {
                            gen4Customer.QuickbooksGen4Action = Gen4Action;
                        }
                        else
                        {
                            gen4Customer.Action = QuickbooksAction.None;
                        }

                        if (!String.IsNullOrEmpty(PplPlanName))
                        {
                            gen4Customer.QuickbookPaymentPlan = PplPlanName;
                        }

                    }
                }
            }

            IdaQuickbookEntities ctxQuickbooks = new IdaQuickbookEntities();
            QuickbookItems = (from q in ctxQuickbooks.Items
                              select q).ToList();

            StringBuilder sbErrorMessages = new StringBuilder();
            sbErrorMessages.AppendLine("Following ACTIVE Gen4 Customers have errors:");

            foreach (var customer in Gen4Customers)
            {
                if (customer.Status == "Active")
                {
                    if (customer.QuickbooksMatch == CustomerQuickbookMatch.NotFound)
                    {
                        actionResult.IsErrorPresent = true;
                        sbErrorMessages.AppendLine(customer.CustomerID + ", " + customer.CompanyName + ", " + customer.FirstName + " " + customer.LastName + " - Not Found in Quickbooks.");
                        customer.Action = QuickbooksAction.Error;
                        customer.IsErrorPresent = true;
                        customer.ErrorMessage = "Not Found in Quickbooks";
                    }
                    else
                    {
                        if (String.IsNullOrEmpty(customer.QuickbooksGen4Action))
                        {
                            actionResult.IsErrorPresent = true;
                            sbErrorMessages.AppendLine(customer.CustomerID + ", " + customer.CompanyName + ", " + customer.FirstName + " " + customer.LastName + " - Property \"Gen4 Action\" not set in Quickbooks.");
                            customer.Action = QuickbooksAction.Error;
                            customer.IsErrorPresent = true;
                            customer.ErrorMessage = "Property \"Gen4 Action\" not set in Quickbooks.";
                        }
                        else
                        {
                            switch (customer.QuickbooksGen4Action)
                            {
                                case "Ignore":
                                    customer.Action = QuickbooksAction.Ignore;
                                    break;
                                case "Import to QB":
                                    customer.Action = QuickbooksAction.Import;
                                    break;
                                case "Export to CSV":
                                    customer.Action = QuickbooksAction.Export;
                                    break;
                                default:
                                    actionResult.IsErrorPresent = true;
                                    sbErrorMessages.AppendLine(customer.CustomerID + ", " + customer.CompanyName + ", " + customer.FirstName + " " + customer.LastName + " - Property \"Gen4 Action\" not set in Quickbooks.");
                                    customer.Action = QuickbooksAction.Error;
                                    customer.IsErrorPresent = true;
                                    customer.ErrorMessage = "Invalid Property \"Gen4 Action\" in Quickbooks: [" + customer.QuickbooksGen4Action + "].";
                                    break;
                            }
                        }
                        //else if (customer.Action == QuickbooksAction.Error)
                        //{
                        //    actionResult.IsErrorPresent = true;
                        //    sbErrorMessages.AppendLine(customer.CustomerID + ", " + customer.CompanyName + ", " + customer.FirstName + " " + customer.LastName + " - Property \"Gen4 Action\" not set in Quickbooks.");
                        //    customer.IsErrorPresent = true;
                        //    customer.ErrorMessage = "Invalid Property \"Gen4 Action\" in Quickbooks.";
                        //}

                        if (customer.Action == QuickbooksAction.Import)
                        {
                            // if customer action is Import then QuickbookPaymentPlan has to be a valid QuickBooks List Item.
                            if (String.IsNullOrEmpty(customer.QuickbookPaymentPlan))
                            {
                                actionResult.IsErrorPresent = true;
                                sbErrorMessages.AppendLine(customer.CustomerID + ", " + customer.CompanyName + ", " + customer.FirstName + " " + customer.LastName + " - Property \"PPL Plan\" not set in Quickbooks.");
                                customer.Action = QuickbooksAction.Error;
                                customer.IsErrorPresent = true;
                                customer.ErrorMessage = "Property \"PPL Plan\" not set in Quickbooks.";
                            }
                            else if (customer.QuickbookPaymentPlan == "None")
                            {
                                actionResult.IsErrorPresent = true;
                                sbErrorMessages.AppendLine(customer.CustomerID + ", " + customer.CompanyName + ", " + customer.FirstName + " " + customer.LastName + " - If \"Gen4 Action\" is \"Import\" then Property \"PPL Plan\" can not be \"None\" in Quickbooks.");
                                customer.Action = QuickbooksAction.Error;
                                customer.IsErrorPresent = true;
                                customer.ErrorMessage = "If \"Gen4 Action\" is \"Import\" then Property \"PPL Plan\" can not be \"None\" in Quickbooks.";
                            }
                            else
                            {
                                var item = (from q in QuickbookItems
                                            where q.Name == customer.QuickbookPaymentPlan
                                            select q).SingleOrDefault();
                                if (item == null)
                                {
                                    actionResult.IsErrorPresent = true;
                                    sbErrorMessages.AppendLine(customer.CustomerID + ", " + customer.CompanyName + ", " + customer.FirstName + " " + customer.LastName + " - Property \"PPL Plan\" is set to an Invalid List Item in Quickbooks.");
                                    customer.Action = QuickbooksAction.Error;
                                    customer.IsErrorPresent = true;
                                    customer.ErrorMessage = "Property \"PPL Plan\" is not valid List Item in Quickbooks.";
                                }
                            }
                        }
                        else
                        {
                            //if customer action is NOT Import and QuickbookPaymentPlan is neither empty or "None"
                            // then QuickbookPaymentPlan has to be a valid QuickBooks List Item.
                            if (!String.IsNullOrEmpty(customer.QuickbookPaymentPlan) && customer.QuickbookPaymentPlan != "None")
                            {
                                var item = (from q in QuickbookItems
                                            where q.Name == customer.QuickbookPaymentPlan
                                            select q).SingleOrDefault();
                                if (item == null)
                                {
                                    actionResult.IsErrorPresent = true;
                                    sbErrorMessages.AppendLine(customer.CustomerID + ", " + customer.CompanyName + ", " + customer.FirstName + " " + customer.LastName + " - Property \"PPL Plan\" is set to an Invalid List Item in Quickbooks.");
                                    customer.Action = QuickbooksAction.Error;
                                    customer.IsErrorPresent = true;
                                    customer.ErrorMessage = "Property \"PPL Plan\" is not valid List Item in Quickbooks.";
                                }
                            }
                        }
                    }
                }
                else
                {
                    customer.Action = QuickbooksAction.None;
                }
            }

            //ErrorMessage = null;
            if (actionResult.IsErrorPresent)
            {
                actionResult.ErrorMessage = sbErrorMessages.ToString();
            }

            return Gen4Customers;
        }

        public List<Gen4Transaction> GetIdaLeads(DateTime StartDate, DateTime EndDate, bool? IsReported, out ActionResult actionResult)
        {
            actionResult = new ActionResult();

            Gen4Transactions = new List<Gen4Transaction>();

            Gen4LiveEntities gen4Ctx = new Gen4LiveEntities();
            if (IsReported.HasValue)
            {
                Gen4PplLedgerTransactions = (from l in gen4Ctx.Gen4PplLedger
                                             where l.Created >= StartDate && l.Created <= EndDate
                                             && l.IsReported == IsReported
                                             select l).ToList();
            }
            else
            {
                Gen4PplLedgerTransactions = (from l in gen4Ctx.Gen4PplLedger
                                             where l.Created >= StartDate && l.Created <= EndDate
                                             select l).ToList();
            }
            var CustomerLocationWebsiteIDs = (from w in gen4Ctx.Websites
                                              join l in gen4Ctx.CustomerLocations on w.LocationID equals l.LocationID
                                              join c in gen4Ctx.IdaCustomers on l.CustomerID equals c.CustomerID
                                              select new
                                              {
                                                  CustomerID = c.CustomerID,
                                                  LocationID = l.LocationID,
                                                  WebsiteID = w.WebsiteID
                                              }).Distinct().ToList();

            StringBuilder sbErrorMessages = new StringBuilder();
            foreach (var ledgerTransaction in Gen4PplLedgerTransactions)
            {
                var customer = (from c in Gen4Customers
                                where c.CustomerID == ledgerTransaction.CustomerID
                                select c).SingleOrDefault();

                if (customer == null)
                {
                    if (ExcludedCustomers.ContainsKey(ledgerTransaction.CustomerID))
                    {
                        actionResult.IsErrorPresent = true;
                        sbErrorMessages.AppendLine("TrxID: " + ledgerTransaction.TransactionID + " belongs to an excluded Customer. Customer: " + ledgerTransaction.CustomerID + " - " + ExcludedCustomers[ledgerTransaction.CustomerID]);
                        sbErrorMessages.AppendLine("  If you would like to process this Transaction remove CustomerID from Excluded Customers XML file.\r\n");
                        continue;
                    }
                }

                Gen4Transaction newLead = new Gen4Transaction();
                newLead.TransactionID = ledgerTransaction.TransactionID;
                newLead.ParentTransactionID = ledgerTransaction.ParentTransactionID;
                newLead.CustomerID = ledgerTransaction.CustomerID;
                newLead.Company = customer.CompanyName;
                newLead.CustomerName = customer.FirstName + " " + customer.LastName;
                newLead.CustomerEmail = customer.Email;
                newLead.CustomerPhone = customer.Phone;
                newLead.LeadID = ledgerTransaction.LeadID;
                newLead.LeadType = ledgerTransaction.LeadType;
                newLead.MarketID = ledgerTransaction.MarketID;
                newLead.Source = ledgerTransaction.Source;
                newLead.Description = ledgerTransaction.Description;
                newLead.TransactionType = ledgerTransaction.TransactionType;
                newLead.IsReported = ledgerTransaction.IsReported;
                newLead.Created = ledgerTransaction.Created;
                newLead.ReportStatus = ledgerTransaction.ReportStatus;

                if (ledgerTransaction.IsReported)
                {
                    newLead.Action = QuickbooksAction.None;
                    //override any errors for already reported transactions
                    newLead.IsErrorPresent = false;
                    newLead.ErrorMessage = null;
                }
                else
                {
                    newLead.Action = customer.Action;
                    newLead.IsErrorPresent = customer.IsErrorPresent;
                    newLead.ErrorMessage = customer.ErrorMessage;
                }

                Gen4Transactions.Add(newLead);
            }

            if (actionResult.IsErrorPresent)
            {
                actionResult.ErrorMessage = sbErrorMessages.ToString();
            }

            return Gen4Transactions;
        }

        public void ImportToQuickbooks(Gen4Transaction Gen4ImportTransaction)
        {
            var gen4Customer = (from c in Gen4Customers
                                where c.CustomerID == Gen4ImportTransaction.CustomerID
                                select c).Single();
            var quickBookItem = (from i in QuickbookItems
                                 where i.Name == gen4Customer.QuickbookPaymentPlan
                                 select i).Single();

            IdaQuickbookEntities ctxQuickbooks = new IdaQuickbookEntities();
            if (Gen4ImportTransaction.TransactionType == "Invoice")
            {
                InvoiceLineItem myItem = new InvoiceLineItem();
                myItem.CustomerId = gen4Customer.QuickbookCustomerID;
                myItem.ItemId = quickBookItem.ID;
                myItem.ItemQuantity = 1;
                myItem.Memo = "TrxID: {" + Gen4ImportTransaction.TransactionID + "}, Type: {" + Gen4ImportTransaction.LeadType + "}, Source: {" + Gen4ImportTransaction.Source + "}";
                ctxQuickbooks.InvoiceLineItems.AddObject(myItem);
                ctxQuickbooks.SaveChanges();
            }
            else if (Gen4ImportTransaction.TransactionType == "Credit")
            {
                var quickbooksCustomer = (from q in QuickbooksCustomers
                                          where q.ID == gen4Customer.QuickbookCustomerID
                                          select q).Single();

                CreditMemo creditItem = new CreditMemo();
                creditItem.CustomerName = quickbooksCustomer.Name;
                creditItem.CustomerId = gen4Customer.QuickbookCustomerID;
                creditItem.ItemAggregate = "<CreditMemoLineItems><Row><ItemQuantity>1</ItemQuantity><ItemName>" + quickBookItem.FullName + "</ItemName></Row></CreditMemoLineItems>";
                creditItem.Memo = "TrxID: {" + Gen4ImportTransaction.TransactionID + "}, Type: {" + Gen4ImportTransaction.LeadType + "}, Source: {" + Gen4ImportTransaction.Source + "}, ParentTrxID: {" + Gen4ImportTransaction.ParentTransactionID + "}";
                ctxQuickbooks.CreditMemos.AddObject(creditItem);
                ctxQuickbooks.SaveChanges();
            }

            Gen4LiveEntities gen4Ctx = new Gen4LiveEntities();
            var gen4Transaction = (from t in gen4Ctx.Gen4PplLedger
                                   where t.TransactionID == Gen4ImportTransaction.TransactionID
                                   select t).Single();
            gen4Transaction.IsReported = true;
            gen4Transaction.ReportStatus = "Imported to QB on " + DateTime.Now.ToString();
            gen4Transaction.LastModified = DateTime.Now;
            gen4Ctx.SaveChanges();

        }

        public void ExportToCsv(List<Gen4Transaction> Gen4ExportTransactions)
        {
            DateTime exportDateTime = DateTime.Now;

            StringBuilder sbExportFile = new StringBuilder();
            sbExportFile.AppendLine("TransactionID,ParentTransactionID,CustomerID,Company,CustomerName,CustomerEmail,CustomerPhone,LeadID,LeadType,Source,Description,TransactionType,IsReported,Action,Created");

            foreach (var gen4ExportTransaction in Gen4ExportTransactions)
            {
                sbExportFile.AppendLine(this.FormatForExportToCSV(gen4ExportTransaction.TransactionID.ToString()) + "," +
                                        this.FormatForExportToCSV(gen4ExportTransaction.ParentTransactionID.ToString()) + "," +
                                        this.FormatForExportToCSV(gen4ExportTransaction.CustomerID.ToString()) + "," +
                                        this.FormatForExportToCSV(gen4ExportTransaction.Company) + "," +
                                        this.FormatForExportToCSV(gen4ExportTransaction.CustomerName) + "," +
                                        this.FormatForExportToCSV(gen4ExportTransaction.CustomerEmail) + "," +
                                        this.FormatForExportToCSV(gen4ExportTransaction.CustomerPhone) + "," +
                                        this.FormatForExportToCSV(gen4ExportTransaction.LeadID.ToString()) + "," +
                                        this.FormatForExportToCSV(gen4ExportTransaction.LeadType) + "," +
                                        this.FormatForExportToCSV(gen4ExportTransaction.Source) + "," +
                                        this.FormatForExportToCSV(gen4ExportTransaction.Description) + "," +
                                        this.FormatForExportToCSV(gen4ExportTransaction.TransactionType) + "," +
                                        this.FormatForExportToCSV(gen4ExportTransaction.IsReported.ToString()) + "," +
                                        this.FormatForExportToCSV(gen4ExportTransaction.Action.ToString()) + "," +
                                        this.FormatForExportToCSV(gen4ExportTransaction.Created.ToString()));
            }

            string exportedFilename = this.GetExportFilename(exportDateTime);

            using (System.Transactions.TransactionScope trans = new System.Transactions.TransactionScope())
            {
                using (System.IO.StreamWriter sw = new System.IO.StreamWriter(exportedFilename))
                {
                    sw.WriteLine(sbExportFile);
                }
                
                Gen4LiveEntities gen4Ctx = new Gen4LiveEntities();
                foreach (var gen4ExportTransaction in Gen4ExportTransactions)
                {
                    var gen4Transaction = (from t in gen4Ctx.Gen4PplLedger
                                           where t.TransactionID == gen4ExportTransaction.TransactionID
                                           select t).Single();
                    gen4Transaction.IsReported = true;
                    gen4Transaction.ReportStatus = "Exported to CSV on " + exportDateTime.ToString();
                    gen4Transaction.LastModified = DateTime.Now;
                    gen4Ctx.SaveChanges();
                }

                trans.Complete();
            }            
        }

        private string FormatForExportToCSV(string Value)
        {
            if (Value.Contains("\""))
            {
                Value = Value.Replace("\"", "\"\"");
            }
            return "\"" + Value + "\"";
        }

        private string GetExportFilename(DateTime exportDateTime)
        {
            string exportedFilename = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\Gen4 PPL Export " + exportDateTime.ToString("yyyy-MM-dd HH-mm-ss") + ".csv";

            if (!System.IO.File.Exists(exportedFilename))
            {
                return exportedFilename;
            }
            else
            {
                // Append ASCII Characters from A to Z to the end of the filename until a non-existing 
                // filename is found and returned.
                for (int i = 65; i <= 90; i++)
                {
                    var suffix = Convert.ToChar(i).ToString();
                    var exportedFilenameUpdated = exportedFilename.Replace(".csv", " " + suffix + ".csv");
                    if (!System.IO.File.Exists(exportedFilenameUpdated))
                    {
                        return exportedFilenameUpdated;
                    }
                }

                // if non-existing filename is still not found then 
                // append a new GUID to the end of the filename and return it.
                var exportedFilenameUpdated3 = exportedFilename.Replace(".csv", " " + System.Guid.NewGuid().ToString() + ".csv");
                return exportedFilenameUpdated3;
            }
        }
    }
}

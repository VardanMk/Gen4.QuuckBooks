using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace Gen4.QuckbooksImport
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        List<Gen4Customer> Gen4Customers = null;
        List<Gen4Transaction> Gen4Transactions = null;
        string EventLogFile = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\EventLog.txt";
        string ErrorLogFile = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\ErrorLog.txt";

        IdaLeadsManager LeadManager = new IdaLeadsManager();

        public MainWindow()
        {
            InitializeComponent();

            try
            {
                // Upon starting the application the Start Date will be set to the day of the last
                // reported (improted into QuickBoos or Exported to CSV) Ledger record.
                // If no IsReported Records are found then the start day will the current day minus 8;
                Gen4LiveEntities gen4Ctx = new Gen4LiveEntities();
                var lead = (from l in gen4Ctx.Gen4PplLedger
                            where l.IsReported == true
                            orderby l.LastModified
                            select l).Take(1).SingleOrDefault();
                if (lead != null)
                {
                    datePickerFrom.SelectedDate = lead.LastModified;
                }
                else
                {
                    datePickerFrom.SelectedDate = DateTime.Today.AddDays(-8);
                }
                if (datePickerFrom.SelectedDate > DateTime.Today.AddDays(-1))
                {
                    datePickerTo.SelectedDate = datePickerFrom.SelectedDate;
                }
                else
                {
                    datePickerTo.SelectedDate = DateTime.Today.AddDays(-1);
                }
            }
            catch (System.Exception ex)
            {
                this.WriteToLogFile(this.ErrorLogFile, DateTime.Now.ToString() + " - Error Starting Application. Exception Details: " + ex.ToString());
            }
        }

        #region " Customers Code "
        private void btn_CheckCustomers_Click(object sender, RoutedEventArgs e)
        {
            Gen4Customers = null;
            Gen4Transactions = null;
            this.dataGrid_Customers.ItemsSource = null;
            this.comboBox_MatchFilter.SelectedItem = null;
            this.comboBox_MatchFilter.IsEnabled = false;
            this.comboBox_ActionFilter.SelectedItem = null;
            this.comboBox_ActionFilter.IsEnabled = false;
            this.btn_RemoveCustomerFilter.IsEnabled = false;

            //Reset properties for Transaction Tab
            this.tabItem_Leads.IsEnabled = false;
            this.btn_ProcessPayPerLeadTransactions.IsEnabled = false;
            Gen4Transactions = null;
            this.dataGrid_PPALeads.ItemsSource = null;

            this.textBox_CheckCustomersProgress.Visibility = System.Windows.Visibility.Visible;
            BackgroundWorker _backgroundWorker = new BackgroundWorker();
            _backgroundWorker.DoWork += _backgroundWorker_DoWork_GetCustomers;
            _backgroundWorker.RunWorkerCompleted += _backgroundWorker_RunWorkerCompleted_GetCustomers;
            _backgroundWorker.RunWorkerAsync();
        }

        void _backgroundWorker_DoWork_GetCustomers(object sender, DoWorkEventArgs e)
        {
            ActionResult actionResult = new ActionResult();
            try
            {
                Gen4Customers = LeadManager.GetIdaCustomers(out actionResult);
                actionResult.Result = Gen4Customers;
            }
            catch (SystemException ex)
            {
                actionResult.IsErrorPresent = true;
                actionResult.ErrorMessage = ex.ToString();
            }
            e.Result = actionResult;
        }

        void _backgroundWorker_RunWorkerCompleted_GetCustomers(object sender, RunWorkerCompletedEventArgs e)
        {
            this.textBox_CheckCustomersProgress.Visibility = System.Windows.Visibility.Hidden;
            this.comboBox_MatchFilter.IsEnabled = true;
            this.comboBox_ActionFilter.IsEnabled = true;
            ActionResult result = (ActionResult)e.Result;

            if (Gen4Customers != null)
            {
                if ((bool)this.chkBox_DisplayOnlyActiveCustomers.IsChecked)
                {
                    var activeCustomer = (from c in Gen4Customers
                                          where c.Status == "Active"
                                          select c).ToList();
                    this.dataGrid_Customers.ItemsSource = activeCustomer;
                }
                else
                {
                    this.dataGrid_Customers.ItemsSource = Gen4Customers;
                }

                this.tabItem_Leads.IsEnabled = true;
            }

            if (result.IsErrorPresent)
            {
                Error errDialog = new Error();
                errDialog.Owner = this;
                errDialog.textBox_Error.Text = result.ErrorMessage;
                errDialog.ShowDialog();
            }            
        }

        private void chkBox_DisplayOnlyActiveCustomers_Click(object sender, RoutedEventArgs e)
        {
            if (Gen4Customers != null)
            {
                if ((bool)this.chkBox_DisplayOnlyActiveCustomers.IsChecked)
                {
                    var activeCustomer = (from c in Gen4Customers
                                          where c.Status == "Active"
                                          select c).ToList();
                    this.dataGrid_Customers.ItemsSource = activeCustomer;
                }
                else
                {
                    this.dataGrid_Customers.ItemsSource = Gen4Customers;
                }

                this.comboBox_MatchFilter.SelectedItem = null;
                this.btn_RemoveCustomerFilter.IsEnabled = false;
            }
        }

        private void comboBox_MatchFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.ApplyCustomerFilters();
        }

        private void comboBox_ActionFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.ApplyCustomerFilters();
        }

        private void btn_RemoveCustomerFilter_Click(object sender, RoutedEventArgs e)
        {
            this.comboBox_MatchFilter.SelectedItem = null;
            this.comboBox_ActionFilter.SelectedItem = null;
            this.btn_RemoveCustomerFilter.IsEnabled = false;

            if ((bool)this.chkBox_DisplayOnlyActiveCustomers.IsChecked)
            {
                var activeCustomer = (from c in Gen4Customers
                                      where c.Status == "Active"
                                      select c).ToList();
                this.dataGrid_Customers.ItemsSource = activeCustomer;
            }
            else
            {
                this.dataGrid_Customers.ItemsSource = Gen4Customers;
            }
        }

        private void ApplyCustomerFilters()
        {
            if (Gen4Customers != null)
            {
                List<Gen4Customer> FilteredGen4Customers;

                if (this.comboBox_MatchFilter.SelectedItem != null)
                {
                    ComboBoxItem selectedMatchFilterItem = (ComboBoxItem)this.comboBox_MatchFilter.SelectedItem;
                    FilteredGen4Customers = (from c in Gen4Customers
                                             where c.QuickbooksMatch.ToString() == selectedMatchFilterItem.Content.ToString()
                                             select c).ToList();
                }
                else
                {
                    FilteredGen4Customers = Gen4Customers;
                }

                if (this.comboBox_ActionFilter.SelectedItem != null)
                {
                    ComboBoxItem selectedActionFilterItem = (ComboBoxItem)this.comboBox_ActionFilter.SelectedItem;
                    QuickbooksAction action = (QuickbooksAction)Enum.Parse(typeof(QuickbooksAction), selectedActionFilterItem.Content.ToString());
                    FilteredGen4Customers = (from c in FilteredGen4Customers
                                             where c.Action == action
                                             select c).ToList();
                }


                if ((bool)this.chkBox_DisplayOnlyActiveCustomers.IsChecked)
                {
                    FilteredGen4Customers = (from c in FilteredGen4Customers
                                             where c.Status == "Active"
                                             select c).ToList();
                }

                this.dataGrid_Customers.ItemsSource = FilteredGen4Customers;
                this.btn_RemoveCustomerFilter.IsEnabled = true;
            }
        }
        #endregion

        #region " Leads Code "
        private void btn_DisplayPayPerLeadTransactions_Click(object sender, RoutedEventArgs e)
        {
            this.btn_ProcessPayPerLeadTransactions.IsEnabled = false;
            this.textBox_ImportProgress.Text = "Retrieving Gen4 Transactions … ";
            this.textBox_ImportProgress.Visibility = System.Windows.Visibility.Visible;

            BackgroundWorker _backgroundWorker = new BackgroundWorker();
            _backgroundWorker.DoWork += _backgroundWorker_DoWork_DisplayPayPerLeadTransactions;
            _backgroundWorker.RunWorkerCompleted += _backgroundWorker_RunWorkerCompleted_DisplayPayPerLeadTransactions;
            _backgroundWorker.RunWorkerAsync();
        }

        private void comboBox_TrxReportedStatus_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.btn_ProcessPayPerLeadTransactions.IsEnabled = false;
            this.textBox_ImportProgress.Text = "Retrieving Gen4 Transactions … ";
            this.textBox_ImportProgress.Visibility = System.Windows.Visibility.Visible;

            BackgroundWorker _backgroundWorker = new BackgroundWorker();
            _backgroundWorker.DoWork += _backgroundWorker_DoWork_DisplayPayPerLeadTransactions;
            _backgroundWorker.RunWorkerCompleted += _backgroundWorker_RunWorkerCompleted_DisplayPayPerLeadTransactions;
            _backgroundWorker.RunWorkerAsync();
        }

        private void _backgroundWorker_DoWork_DisplayPayPerLeadTransactions(object sender, DoWorkEventArgs e)
        {
            try
            {
                DateTime startDate = new DateTime();
                DateTime endDate = new DateTime();
                string selectedTrxReportedStatus = null;
                this.Dispatcher.Invoke(
                    System.Windows.Threading.DispatcherPriority.Normal,
                        new Action(
                            delegate()
                            {
                                startDate = (DateTime)this.datePickerFrom.SelectedDate;
                                endDate = ((DateTime)this.datePickerTo.SelectedDate).AddDays(1);
                                selectedTrxReportedStatus = ((ComboBoxItem)this.comboBox_TrxReportedStatus.SelectedItem).Content.ToString();
                            }
                            ));
                if (startDate == DateTime.MinValue)
                {
                    startDate = DateTime.Now.AddMonths(-1);
                }
                if (endDate == DateTime.MinValue)
                {
                    endDate = DateTime.Now.AddDays(1);
                }
                ActionResult actionResult;

                bool? IsReported;

                
                switch (selectedTrxReportedStatus)
                {
                    case "Show Non-Reported Transactions Only":
                        IsReported = false;
                        break;
                    case "Snow Reported Transactions Only":
                        IsReported = true;
                        break;
                    case "Show All Transactions":
                        IsReported = null;
                        break;
                    default:
                        IsReported = null;
                        break;
                }

                Gen4Transactions = LeadManager.GetIdaLeads(startDate, endDate, IsReported, out actionResult);

                actionResult.Result = Gen4Transactions;
                e.Result = actionResult;
            }
            catch (System.Exception ex)
            {
                ActionResult result = new ActionResult();
                result.IsErrorPresent = true;
                result.ErrorMessage = ex.ToString();
                e.Result = result;
            }
        }

        private void _backgroundWorker_RunWorkerCompleted_DisplayPayPerLeadTransactions(object sender, RunWorkerCompletedEventArgs e)
        {
            this.textBox_ImportProgress.Visibility = System.Windows.Visibility.Hidden;
            this.btn_ProcessPayPerLeadTransactions.IsEnabled = true;
            ActionResult result = (ActionResult)e.Result;

            if (Gen4Transactions != null)
            {
                this.dataGrid_PPALeads.ItemsSource = Gen4Transactions;
            }

            if (result.IsErrorPresent)
            {
                Error errDialog = new Error();
                errDialog.Owner = this;
                errDialog.textBox_Error.Text = result.ErrorMessage;
                errDialog.ShowDialog();
            }
        }

        private void btn_ProcessPayPerLeadTransactions_Click(object sender, RoutedEventArgs e)
        {
            if (Gen4Transactions == null)
            {
                MessageBox.Show("Please \"Display PPA Leads\" before tryign to import them.");
                return;
            }

            this.textBox_ImportProgress.Visibility = System.Windows.Visibility.Visible;
            BackgroundWorker _backgroundWorker = new BackgroundWorker();
            _backgroundWorker.DoWork += _backgroundWorker_DoWork_ProcessPayPerLeadTransactions;
            _backgroundWorker.RunWorkerCompleted += _backgroundWorker_RunWorkerCompleted_ProcessPayPerLeadTransactions;
            _backgroundWorker.RunWorkerAsync();
        }

        void _backgroundWorker_DoWork_ProcessPayPerLeadTransactions(object sender, DoWorkEventArgs e)
        {
            ActionResult actionResult = new ActionResult();

            var gen4ImportTransactions = (from t in Gen4Transactions
                                          where t.IsReported == false && t.Action == QuickbooksAction.Import
                                          select t).ToList();

            int Counter = 1;
            int counterSuccessfullyImported = 0;
            foreach (var gen4Transaction in gen4ImportTransactions)
            {
                try
                {
                    this.Dispatcher.Invoke(
                    System.Windows.Threading.DispatcherPriority.Normal,
                        new Action(
                            delegate()
                            {
                                this.textBox_ImportProgress.Text = "Importing " + Counter + " of " + gen4ImportTransactions.Count;
                            }
                            ));
                    LeadManager.ImportToQuickbooks(gen4Transaction);
                    this.WriteToLogFile(EventLogFile, DateTime.Now.ToString() + " - Imported TransactionID: " + gen4Transaction.TransactionID + " for Customer: " + gen4Transaction.CustomerID + " - " + gen4Transaction.CustomerName);
                    counterSuccessfullyImported++;
                    Counter++;
                }
                catch (System.Exception ex1)
                {
                    actionResult.IsErrorPresent = true;
                    string importErrorMessage = "Error Importing Gen4 TransactionID: " + gen4Transaction.TransactionID + ".\r\n" +
                                                "Exception Details: " + ex1.ToString() + "\r\n\r\n";
                    actionResult.ErrorMessage += importErrorMessage;
                    this.WriteToLogFile(this.ErrorLogFile, DateTime.Now.ToString() + " - " + importErrorMessage);
                }
            }

            actionResult.Message = "Imported " + counterSuccessfullyImported + " of " + gen4ImportTransactions.Count + " transactions into Quickbooks\r\n";

            var gen4ExportTransactions = (from t in Gen4Transactions
                                          where t.IsReported == false && t.Action == QuickbooksAction.Export
                                          select t).ToList();
            if (gen4ExportTransactions.Count > 0)
            {
                this.Dispatcher.Invoke(
                    System.Windows.Threading.DispatcherPriority.Normal,
                        new Action(
                            delegate()
                            {
                                this.textBox_ImportProgress.Text = "Exporting Records to CSV File";
                            }
                            ));
                try
                {
                    LeadManager.ExportToCsv(gen4ExportTransactions);
                    this.WriteToLogFile(EventLogFile, DateTime.Now.ToString() + " - Exorted " + gen4ExportTransactions.Count + " transactions to CSV File.");
                    actionResult.Message += "Exorted " + gen4ExportTransactions.Count + " transactions to CSV File.";
                }
                catch (System.Exception ex2)
                {
                    actionResult.IsErrorPresent = true;
                    string exportErrorMessage = "Error Exporting Gen4 Transactions to CSV file.\r\n" +
                                                "Exception Details: " + ex2.ToString() + "\r\n\r\n";
                    actionResult.ErrorMessage += exportErrorMessage;
                    this.WriteToLogFile(this.ErrorLogFile, DateTime.Now.ToString() + " - " + exportErrorMessage);
                }
                
            }

            e.Result = actionResult;
        }

        void _backgroundWorker_RunWorkerCompleted_ProcessPayPerLeadTransactions(object sender, RunWorkerCompletedEventArgs e)
        {
            this.textBox_ImportProgress.Visibility = System.Windows.Visibility.Hidden;

            ActionResult actionResult = (ActionResult)e.Result;
            if (actionResult.IsErrorPresent)
            {
                Error errDialog = new Error();
                errDialog.Owner = this;
                errDialog.textBox_Error.Text = actionResult.Message + "\r\n\r\n" + actionResult.ErrorMessage;
                errDialog.ShowDialog();
            }
            else
            {
                MessageBox.Show(actionResult.Message);
            }

            this.btn_DisplayPayPerLeadTransactions_Click(null, null);
        }

        #endregion

        private void btn_Test_Click(object sender, RoutedEventArgs e)
        {
            IdaQuickbookEntities ctxQuickbooks = new IdaQuickbookEntities();

            var invoiceLineItems = (from i in ctxQuickbooks.InvoiceLineItems
                                    select i).ToList();

            foreach (var invoiceLineItem in invoiceLineItems)
            {
                if (invoiceLineItem.Memo.StartsWith("TrxID: 4,"))
                {
                    MessageBox.Show("Invoice Present: " + invoiceLineItem.ID);
                }
            }


            //CreditMemo creditItem = new CreditMemo();
            //creditItem.CustomerName = "Brother, James DDS";
            //creditItem.CustomerId = "80000004-1336073105";
            //creditItem.ItemAggregate = "<CreditMemoLineItems><Row><ItemQuantity>3</ItemQuantity><ItemName>PPL-S1</ItemName></Row></CreditMemoLineItems>";
            //creditItem.Memo = "TrxID: " + "123" + ", Type: " + "test";

            ////CreditMemoLineItem creditItem = new CreditMemoLineItem();
            ////creditItem.CustomerId = "80000004-1336073105";
            ////creditItem.CustomerName = "Brother, James DDS";
            ////creditItem.ItemId = "800000C9-1343541568";
            ////creditItem.ItemQuantity = 1;
            ////creditItem.Memo = "TrxID: " + "123" + ", Type: " + "test";
            ////ctxQuickbooks.CreditMemoLineItems.AddObject(creditItem);
            //ctxQuickbooks.CreditMemos.AddObject(creditItem);
            //ctxQuickbooks.SaveChanges();
            //MessageBox.Show("Test");
            ////var a = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            ////using (System.IO.StreamWriter sw = new System.IO.StreamWriter(a + "\\IdaImportLog" + DateTime.Now.ToString("_yyyy-MM-dd") + ".txt", true))
            ////{
            ////    sw.WriteLine("testing");
            ////}

            ////var c = System.Reflection.Assembly.GetExecutingAssembly();
            ////var b = Process.GetCurrentProcess().MainModule.FileName;

            ////Uri uri = new Uri(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase);

            ////string path = uri.LocalPath;
            ////string appDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        }

        private void WriteToLogFile(string LogFile, string Message)
        {
            StreamWriter swOriginal = new StreamWriter(LogFile, true);
            TextWriter sw = TextWriter.Synchronized(swOriginal);
            sw.Write("\r\n\r\n" + Message);
            sw.Close();
            sw = null;
        }

        static void GlobalExceptionHandler(object sender, UnhandledExceptionEventArgs args)
        {
            Exception e = (Exception)args.ExceptionObject;
            Console.WriteLine("MyHandler caught : " + e.Message);
        }

    }
}

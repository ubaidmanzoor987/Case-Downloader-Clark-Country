using System.Windows.Forms;
using System;
using System.Threading;
using Excel = Microsoft.Office.Interop.Excel;

namespace ClarkCountryCaseDownloader
{
    public partial class Form2 : Form
    {
        Thread backgroudThread;
        delegate void showMessage(string msg);
        Excel.Application xlApp = new Excel.Application();
        Excel.Workbook xlWorkBook;
        Excel.Worksheet xlWorkSheet;
        Excel.Range xlRange;
        public Form2()
        {
            InitializeComponent();
            download_directory_input.Text = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            locate.DownloadPath = download_directory_input.Text;
        }

        private void updateRichTextbox(string message)
        {
            activityBar.Text += Environment.NewLine + message;
            activityBar.Visible = true;
            Show();
        }

        void showStatusMessage(string msg)
        {
            if (activityBar.InvokeRequired)
            {
                activityBar.Invoke(new Action<string>(showStatusMessage), msg);
                return;
            }
            activityBar.AppendText(msg + "\r\n");
        }

        void enableButton(bool enable)
        {
            if (start_download_button.InvokeRequired)
            {
                start_download_button.Invoke(new Action<bool>(enableButton), enable);
                return;
            }
            start_download_button.Enabled = true;
        }

        void toggleCrossRef(bool enable)
        {
            if (cross_Ref.InvokeRequired)
            {
                cross_Ref.Invoke(new Action<bool>(toggleCrossRef), enable);
                return;
            }
            cross_Ref.Enabled = enable;
        }

        void toggleExcelfileButton(bool enable)
        {
            if (excel_file_btn.InvokeRequired)
            {
                excel_file_btn.Invoke(new Action<bool>(toggleExcelfileButton), enable);
                return;
            }
            excel_file_btn.Enabled = enable;
        }

        private void start_download_button_Click(object sender, System.EventArgs e)
        {
            showStatusMessage("Initalizing Driver");
            if (manual_cross_ref.Checked)
            {
                if (cross_Ref.Text.Length == 0)
                {
                    MessageBox.Show("Enter Cross Ref Number Then Press Download");
                    return;
                }
                   
            } else
            {
                if (excel_file_input.Text.Length == 0)
                {
                    MessageBox.Show("Browse Excel File Then Press Download");
                    return;
                }
            }
            bool isConnected;
            backgroudThread = new Thread(() =>
            {
                for (int i=1; i<=5; i++)
                {
                    isConnected = ProcessDownload(); ;
                    if (isConnected == true)
                    {
                        break;
                    }else {
                        showStatusMessage("Failed To Initalize Driver. Retrying...");
                        if (i == 5){
                            enableButton(true);
                            showStatusMessage("Unable To Connect. Please Check Your Internet Connection.");
                            backgroudThread.Abort();
                        }
                    }

                }
            });
            backgroudThread.Start();
        }

        private bool ProcessDownload()
        {
            bool res = false;
            mUiContext = new WindowsFormsSynchronizationContext();
            res = locate.InitializeDriver();
            if (res == true)
            {
                showStatusMessage("Initalized");
                if (manual_cross_ref.Checked)
                {
                    toggleCrossRef(true);
                    toggleExcelfileButton(false);
                    string cross_ref_number = cross_Ref.Text;
                    toggleCrossRef(false);
                    showStatusMessage("Starting Scrapping...");
                    showStatusMessage("Opening Las Vegas Page...");
                    locate.openingMainPage();
                    showStatusMessage("Opening View And Pay Criminal Page...");
                    locate.ViewAndPayCriminalPage();
                    showStatusMessage("Solving Captcha and Inserting Cross Ref Number...");
                    showStatusMessage("Find Data");
                    bool isOnDataPage = locate.LookingAndSolvingCaptcha(cross_ref_number);
                    if (isOnDataPage)
                    {
                        showStatusMessage("Extracting And Saving Data");
                        locate.extractData(cross_ref_number);
                    }
                    showStatusMessage("Extraction Complete Cross Ref Number " + cross_ref_number + " Downloaded");
                    showStatusMessage("Enter Cross Ref Number");
                    toggleCrossRef(true);

                }
                else
                {
                    toggleCrossRef(false);
                    start_download_button.Enabled = false;
                    readExcelAndStartoperations(excel_file_input.Text);
                }
            }
            return res;
        }

        private void readExcelAndStartoperations(string sFile)
        {
            try
            {
                xlApp = new Excel.Application();
                xlWorkBook = xlApp.Workbooks.Open(sFile);           // WORKBOOK TO OPEN THE EXCEL FILE.
                xlWorkSheet = xlWorkBook.Sheets[1];      // NAME OF THE SHEET.
                xlRange = xlWorkSheet.UsedRange;

                int rowCount = xlRange.Rows.Count;
                int colCount = xlRange.Columns.Count;
                int event_number_index = -1;

                for (int j = 1; j <= colCount; j++)
                {
                    string col_name = xlRange.Cells[1, j].Value2.ToString();
                    if (col_name.ToLower() == "event number")
                    {
                        event_number_index = j;
                    }
                }

                for (int i = 2; i <= rowCount; i++)
                {
                    string cross_ref_number = xlRange.Cells[i, event_number_index].Value2.ToString();
                    Console.WriteLine(cross_ref_number);
                    showStatusMessage("Opening Las Vegas Page...");
                    locate.openingMainPage();
                    showStatusMessage("Opening View And Pay Criminal Page...");
                    locate.ViewAndPayCriminalPage();
                    showStatusMessage("Solving Captcha and Inserting Cross Ref Number...");
                    bool isOnDataPage = locate.LookingAndSolvingCaptcha(cross_ref_number);
                    if (isOnDataPage)
                    {
                        showStatusMessage("Extracting And Saving Data...");
                        locate.extractData(cross_ref_number);
                        showStatusMessage("Extraction Complete Cross Ref Number " + cross_ref_number + " Downloaded");
                    }
                }
                xlWorkBook.Close();
                xlApp.Quit();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception in Reading Excel File Path, ", ex.Message);
            }

        }

        private void excel_file_btn_Click(object sender, System.EventArgs e)
        {
            openFileDialog1 = new OpenFileDialog();
            openFileDialog1.Title = "Browse Excel File";
            openFileDialog1.CheckFileExists = true;
            openFileDialog1.CheckPathExists = true;
            openFileDialog1.RestoreDirectory = true;
            openFileDialog1.Filter = "Excel File|*.xlsx;*.xls";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    string file_name = openFileDialog1.FileName;
                    excel_file_input.Text = file_name;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Security error.\n\nError message: {ex.Message}\n\n" +
                    $"Details:\n\n{ex.StackTrace}");
                }
            }
        }

        private void download_path_button_Click(object sender, System.EventArgs e)
        {
            folderBrowserDialog = new FolderBrowserDialog();
            folderBrowserDialog.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            DialogResult result = folderBrowserDialog.ShowDialog();
            if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(folderBrowserDialog.SelectedPath))
            {
                string folder_path = folderBrowserDialog.SelectedPath;
                download_directory_input.Text = folder_path;
                locate.DownloadPath = folder_path;

            }
        }

        private void manual_cross_ref_CheckedChanged(object sender, System.EventArgs e)
        {
            if (manual_cross_ref.Checked)
            {
                excel_file_btn.Enabled = false;
                cross_Ref.Enabled = true;
            }
            else
            {
                excel_file_btn.Enabled = true;
                cross_Ref.Enabled = false;
            }
        }

    }
}

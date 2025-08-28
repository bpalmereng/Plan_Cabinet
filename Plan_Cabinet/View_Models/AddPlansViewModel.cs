using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Data.SqlClient;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using Plan_Cabinet.Connections;
using Plan_Cabinet.Sharepoint;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Diagnostics;

namespace Plan_Cabinet.View_Models
{
    public partial class AddPlansViewModel : ObservableObject, IDisposable
    {
        private readonly GraphService _graphService;
        private readonly INavigation _navigation;
        private CancellationTokenSource _cts;
        private bool _isDisposed = false;

        // Fields for Observable Properties
        [ObservableProperty] private int planID;
        [ObservableProperty] private string planName;
        [ObservableProperty] private string subdivision;
        [ObservableProperty] private string reference;
        [ObservableProperty] private DateTime planDate;
        [ObservableProperty] private string rdName;
        [ObservableProperty] private string pickedFileLabel;
        [ObservableProperty] private byte[] pickedFileBytes;

        [RelayCommand]
        private async Task PickFileAsync()
        {
            try
            {
                var pdfFileType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".pdf" } },
                    { DevicePlatform.Android, new[] { "application/pdf" } },
                    { DevicePlatform.iOS, new[] { "com.adobe.pdf" } }
                });

                var result = await FilePicker.PickAsync(new PickOptions
                {
                    FileTypes = pdfFileType,
                    PickerTitle = "Please select a PDF plan file"
                });

                if (result != null)
                {
                    using var stream = await result.OpenReadAsync();
                    using var ms = new MemoryStream();
                    await stream.CopyToAsync(ms);
                    PickedFileBytes = ms.ToArray();
                    PickedFileLabel = $"Selected file: {result.FileName}";
                }
            }
            catch (Exception ex)
            {
                if (ShowAlertRequested != null)
                {
                    await ShowAlertRequested?.Invoke("Error", $"File picking failed: {ex.Message}", "OK");
                }
            }
        }

        [RelayCommand]
        private async Task SubmitAsync()
        {
            if (string.IsNullOrWhiteSpace(PlanName))
            {
                if (ShowAlertRequested != null)
                {
                    await ShowAlertRequested?.Invoke("Alert", "Please enter a plan name", "Okay");
                }
                return;
            }

            if (PickedFileBytes == null || PickedFileBytes.Length == 0)
            {
                if (ShowAlertRequested != null)
                {
                    await ShowAlertRequested?.Invoke("Alert", "Please select a PDF file before submitting.", "Okay");
                }
                return;
            }

            string fileName = $"{PlanName}.pdf";

            try
            {
                bool fileExists = await _graphService.FileExistsAsync(fileName);

                if (fileExists)
                {
                    bool overwrite = false;
                    if (ShowAlertWithChoiceRequested != null)
                    {
                        overwrite = await ShowAlertWithChoiceRequested.Invoke(
                            "File Exists",
                            $"A file named '{fileName}' already exists in the SharePoint folder. Do you want to overwrite it?",
                            "Yes", "No");
                    }

                    if (!overwrite)
                    {
                        if (ShowAlertRequested != null)
                        {
                            await ShowAlertRequested?.Invoke("Alert", "Please choose a different plan name or file before submitting.", "Okay");
                        }
                        return;
                    }

                    await _graphService.DeleteFileIfExistsAsync(fileName);
                }

                await InsertDatabaseAsync();

                if (PickedFileBytes.Length > 0)
                {
                    await _graphService.UploadFileAsync(PlanName, PickedFileBytes);
                }

                bool addMore = false;
                if (ShowAlertWithChoiceRequested != null)
                {
                    addMore = await ShowAlertWithChoiceRequested.Invoke(
                        "Success",
                        $"{PlanName} was added successfully. Do you want to add more plans?",
                        "Yes", "No");
                }

                if (addMore)
                {
                    ResetForm();
                }
                else
                {
                    ClosePageRequested?.Invoke();
                }
            }
            catch (Exception ex)
            {
                if (ShowAlertRequested != null)
                {
                    await ShowAlertRequested?.Invoke("Error", $"An error occurred: {ex.Message}", "OK");
                }
            }
        }

        [RelayCommand]
        private async Task CancelAsync()
        {
            ClosePageRequested?.Invoke();
        }

        [RelayCommand]
        private async Task ValidatePlanNameAsync()
        {
            if (!string.IsNullOrWhiteSpace(PlanName))
            {
                await CheckPlanNameExistsAsync(PlanName);
            }
        }

        public event Func<string, string, string, Task> ShowAlertRequested;
        public event Func<string, string, string, string, Task<bool>> ShowAlertWithChoiceRequested;
        public event Action ClosePageRequested;


        public AddPlansViewModel(INavigation navigation)
        {
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            _graphService = new GraphService();
            _cts = new CancellationTokenSource();

            // Set default values for properties in the constructor
            PlanID = 0;
            PlanName = string.Empty;
            Subdivision = string.Empty;
            Reference = string.Empty;
            PlanDate = DateTime.Today;
            RdName = string.Empty;
            PickedFileLabel = string.Empty;
            PickedFileBytes = null;

            Task.Run(async () =>
            {
                try { await _graphService.InitializeAsync(_cts.Token); }
                catch { }
            });
        }

        partial void OnPlanNameChanged(string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                _ = ValidatePlanNameAsync();
        }

        private async Task CheckPlanNameExistsAsync(string name)
        {
            try
            {
                string query = $"SELECT PlanID FROM Digital_Plans WHERE Plan_Name ='{name}'";
                using var connector = new ConnectionQuery(query);
                DataTable dtbl = await connector.RunQuery();

                if (dtbl.Rows.Count > 0)
                {
                    if (ShowAlertRequested != null)
                    {
                        await ShowAlertRequested.Invoke("Alert", $"{name} already exists in the database, please choose a different name", "Okay");
                    }
                    PlanName = string.Empty;
                }
            }
            catch (Exception ex)
            {
                if (ShowAlertRequested != null)
                {
                    await ShowAlertRequested?.Invoke("Error", $"Failed to validate plan name: {ex.Message}", "OK");
                }
            }
        }

        private void ResetForm()
        {
            PlanName = string.Empty;
            RdName = string.Empty;
            Subdivision = string.Empty;
            Reference = string.Empty;
            PlanDate = DateTime.Today;
            PickedFileLabel = string.Empty;
            PickedFileBytes = null;
        }

        private async Task InsertDatabaseAsync()
        {
            try
            {
                string query = "INSERT INTO Digital_Plans (Plan_Name, Rd_Name, Subdivision, Reference, Plan_Date) VALUES(@Plan_Name, @Rd_Name, @Subdivision, @Reference, @Plan_Date)";
                using (var connector = new ConnectionQuery(query))
                {
                    SqlCommand command = connector.Command();
                    command.Parameters.Add("@Plan_Name", SqlDbType.VarChar).Value = PlanName ?? string.Empty;
                    command.Parameters.Add("@Rd_Name", SqlDbType.VarChar).Value = RdName ?? string.Empty;
                    command.Parameters.Add("@Subdivision", SqlDbType.VarChar).Value = Subdivision ?? string.Empty;
                    command.Parameters.Add("@Reference", SqlDbType.VarChar).Value = Reference ?? string.Empty;
                    command.Parameters.Add("@Plan_Date", SqlDbType.DateTime).Value = PlanDate;

                    await connector.OpenConnectionAsync();
                    int rowsAffected = await command.ExecuteNonQueryAsync();
                    await connector.CloseConnectionAsync();

                    if (rowsAffected <= 0)
                    {
                        if (ShowAlertRequested != null)
                        {
                            await ShowAlertRequested?.Invoke("Alert", "Sorry there was an error. Please try again or contact support.", "Ok");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (ShowAlertRequested != null)
                {
                    await ShowAlertRequested?.Invoke("Error", $"Database insert failed: {ex.Message}", "OK");
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed)
                return;

            if (disposing)
            {
                _cts?.Cancel();
                _cts?.Dispose();
                _cts = null;
                Debug.WriteLine("AddPlansViewModel disposed.");
            }

            _isDisposed = true;
        }

        ~AddPlansViewModel()
        {
            Dispose(false);
        }
    }
}
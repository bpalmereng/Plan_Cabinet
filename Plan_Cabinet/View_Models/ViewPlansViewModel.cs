using CommunityToolkit.Mvvm.ComponentModel;
using Plan_Cabinet.Connections;
using Plan_Cabinet.Models;
using Plan_Cabinet.Sharepoint;
using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Plan_Cabinet.View_Models
{
    public class ViewPlansViewModel : ObservableObject, IDisposable
    {

        private readonly GraphService _graphService;
        private CancellationTokenSource? _cts;
        private bool _isDisposed = false;

        // CancellationTokenSource for PDF loading cancellation
        private CancellationTokenSource? _pdfLoadCts;

        // Properties

        private ObservableCollection<Digital_Plans> _planList = new();
        public ObservableCollection<Digital_Plans> PlanList
        {
            get => _planList;
            set => SetProperty(ref _planList, value);
        }

        private string _currentFilterColumn = "Plan_Name";
        public string CurrentFilterColumn
        {
            get => _currentFilterColumn;
            set => SetProperty(ref _currentFilterColumn, value);
        }

        private string _searchQuery = string.Empty;
        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value))
                {
                    _ = FilterPlansAsync(CurrentFilterColumn, value);
                }
            }
        }

        private Digital_Plans? _selectedPlan;
        public Digital_Plans? SelectedPlan
        {
            get => _selectedPlan;
            set
            {
                if (SetProperty(ref _selectedPlan, value) && value != null)
                {
                    _ = OnPlanSelected(value); // fire and forget
                }
            }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private string _busyText = "Loading Please Wait......";
        public string BusyText
        {
            get => _busyText;
            set => SetProperty(ref _busyText, value);
        }

        private bool _isFilterPopupVisible;
        public bool IsFilterPopupVisible
        {
            get => _isFilterPopupVisible;
            set => SetProperty(ref _isFilterPopupVisible, value);
        }

        private bool _isEditPopupVisible;
        public bool IsEditPopupVisible
        {
            get => _isEditPopupVisible;
            set => SetProperty(ref _isEditPopupVisible, value);
        }

        // Editable fields for the edit popup
        private string _planNameEdit = string.Empty;
        public string PlanNameEdit
        {
            get => _planNameEdit;
            set => SetProperty(ref _planNameEdit, value);
        }

        private string _rdNameEdit = string.Empty;
        public string RdNameEdit
        {
            get => _rdNameEdit;
            set => SetProperty(ref _rdNameEdit, value);
        }

        private string _subdivisionEdit = string.Empty;
        public string SubdivisionEdit
        {
            get => _subdivisionEdit;
            set => SetProperty(ref _subdivisionEdit, value);
        }

        private string _referenceEdit = string.Empty;
        public string ReferenceEdit
        {
            get => _referenceEdit;
            set => SetProperty(ref _referenceEdit, value);
        }

        private DateTime _planDateEdit = DateTime.Today;
        public DateTime PlanDateEdit
        {
            get => _planDateEdit;
            set => SetProperty(ref _planDateEdit, value);
        }

        // Commands
        public ICommand LoadPlansCommand { get; }
        public ICommand FilterPlansCommand { get; }
        public ICommand ShowFilterPopupCommand { get; }
        public ICommand CloseFilterPopupCommand { get; }
        public ICommand SelectPlanCommand { get; }
        public ICommand SubmitEditCommand { get; }
        public ICommand DeleteEditCommand { get; }
        public ICommand CancelEditCommand { get; }
        public ICommand ReturnHomeCommand { get; }

        // Events for View interactions
        public event Func<string, string, string, string, Task<bool>> ShowAlertWithChoiceRequested = delegate { return Task.FromResult(false); };
        public event Func<string, string, string, Task> ShowAlertRequested = delegate { return Task.CompletedTask; };
        public event Func<string, Task> NavigateToPdfRequested = delegate { return Task.CompletedTask; };
        public event Func<Task> ClosePageRequested = delegate { return Task.CompletedTask; };

        public ViewPlansViewModel(string? clientId, string? tenantId, string? clientSecret, string? driveId)
        {
            _graphService = new GraphService(clientId, tenantId, clientSecret, driveId);

            LoadPlansCommand = new Command(async () => await LoadPlansAsync());
            FilterPlansCommand = new Command<string>(async (col) => await FilterPlansAsync(col));
            ShowFilterPopupCommand = new Command(ShowFilterPopup);
            CloseFilterPopupCommand = new Command(CloseFilterPopup);
            SelectPlanCommand = new Command<Digital_Plans>(async (plan) => await OnPlanSelected(plan));
            SubmitEditCommand = new Command(async () => await SubmitEditAsync());
            DeleteEditCommand = new Command(async () => await DeleteEditAsync());
            CancelEditCommand = new Command(async () => await CancelEditAsync());
            ReturnHomeCommand = new Command(async () => await ClosePageRequested.Invoke());
        }

        public async Task InitializeAsync()
        {
            _cts = new CancellationTokenSource();
            await _graphService.InitializeAsync(_cts.Token);
            await LoadPlansAsync();
            await FilterPlansAsync(CurrentFilterColumn);
        }

        private void ShowFilterPopup() => IsFilterPopupVisible = true;

        private void CloseFilterPopup() => IsFilterPopupVisible = false;

        public async Task LoadPlansAsync()
        {
            try
            {
                IsBusy = true;

                const string query = "SELECT * FROM Digital_Plans ORDER BY Plan_Name ASC";
                using var connection = new ConnectionQuery(query);
                PlanList = await connection.Get_Plans();

                if (PlanList == null || PlanList.Count == 0)
                {
                    await ShowAlertRequested.Invoke("Notice", "No plans found.", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadPlansAsync Error: {ex}");
                await ShowAlertRequested.Invoke("Load Error", ex.Message, "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task FilterPlansAsync(string columnName, string? searchQuery = null)
        {
            CurrentFilterColumn = columnName;

            try
            {
                IsBusy = true;
                string safeColumnName = GetSafeColumnName(columnName);
                string sql;

                if (!string.IsNullOrWhiteSpace(searchQuery))
                {
                    sql = $@"
                        SELECT * FROM Digital_Plans
                        WHERE {safeColumnName} LIKE @Query
                        ORDER BY PATINDEX(@Query, {safeColumnName}) ASC, LEN({safeColumnName}) ASC";
                }
                else
                {
                    sql = $"SELECT * FROM Digital_Plans ORDER BY {safeColumnName}";
                }

                using var connection = new ConnectionQuery(sql);

                if (!string.IsNullOrWhiteSpace(searchQuery))
                {
                    connection.AddParameter("@Query", SqlDbType.VarChar, $"%{searchQuery}%");
                }

                // Assuming Get_Plans is an async method that returns the ObservableCollection
                PlanList = await connection.Get_Plans();

                // CloseFilterPopup() needs to be handled somewhere if it's not a part of this ViewModel
                // CloseFilterPopup();
            }
            catch (Exception ex)
            {
                if (ShowAlertRequested != null)
                {
                    await ShowAlertRequested.Invoke("Filter Error", ex.Message, "OK");
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private string GetSafeColumnName(string columnName)
        {
            switch (columnName)
            {
                case "Plan_Name":
                case "Subdivision":
                case "Reference":
                case "Rd_Name":
                    return columnName;
                default:
                    // Return a default safe column name to prevent errors
                    return "Plan_Name";
            }
        }

        private async Task OnPlanSelected(Digital_Plans selectedPlan)
        {
            if (selectedPlan == null)
                return;

            SelectedPlan = selectedPlan;

            // Cancel previous PDF load, dispose CTS, create new
            _pdfLoadCts?.Cancel();
            _pdfLoadCts?.Dispose();
            _pdfLoadCts = new CancellationTokenSource();

            try
            {
                bool isEdit = await ShowAlertWithChoiceRequested.Invoke(
                    "Choose Action",
                    "What do you want to do?",
                    "Edit",
                    "View");

                if (_pdfLoadCts.IsCancellationRequested)
                    return; // exit if cancelled

                if (isEdit)
                {
                    await ShowEditPopupAsync();
                }
                else
                {
                    if (string.IsNullOrEmpty(SelectedPlan.Plan_Name))
                    {
                        await ShowAlertRequested.Invoke("Error", "Plan name is missing.", "OK");
                        return;
                    }

                    // Pass cancellation token if your NavigateToPdfRequested supports it,
                    // or just check _pdfLoadCts.IsCancellationRequested before/after
                    await NavigateToPdfRequested.Invoke(SelectedPlan.Plan_Name + ".pdf");

                    if (_pdfLoadCts.IsCancellationRequested)
                        return; // stop further action if cancelled
                }
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("PDF loading cancelled safely.");
                // No crash, just return
                return;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OnPlanSelected: {ex}");
                await ShowAlertRequested.Invoke("Error", "An unexpected error occurred.", "OK");
            }
        }

        private Task ShowEditPopupAsync()
        {
            PlanNameEdit = SelectedPlan?.Plan_Name ?? string.Empty;
            RdNameEdit = SelectedPlan?.Rd_Name ?? string.Empty;
            SubdivisionEdit = SelectedPlan?.Subdivision ?? string.Empty;
            ReferenceEdit = SelectedPlan?.Reference ?? string.Empty;

            if (DateTime.TryParse(SelectedPlan?.Plan_Date, out var parsedDate))
                PlanDateEdit = parsedDate;
            else
                PlanDateEdit = DateTime.Today;

            IsEditPopupVisible = true;
            return Task.CompletedTask;
        }

        private async Task SubmitEditAsync()
        {
            if (string.IsNullOrWhiteSpace(PlanNameEdit))
            {
                await ShowAlertRequested.Invoke("Alert", "Please fill in the plan name", "Okay");
                return;
            }

            IsBusy = true;

            string newPlanName = PlanNameEdit.Trim();
            bool isRenaming = SelectedPlan != null && !string.Equals(newPlanName, SelectedPlan.Plan_Name, StringComparison.OrdinalIgnoreCase);

            if (isRenaming)
            {
                bool canProceed = await HandlePlanRenameAsync(newPlanName);
                if (!canProceed)
                {
                    IsBusy = false;
                    return;
                }
            }

            bool updateSuccess = await UpdatePlanInDatabaseAsync(newPlanName);

            IsBusy = false;

            if (!updateSuccess)
            {
                await ShowAlertRequested.Invoke("Update Failed", "Could not save changes to the database.", "OK");
                return;
            }

            await CancelEditAsync();

            await ShowAlertRequested.Invoke("Success", $"{newPlanName} has been successfully changed.", "Okay");

            await LoadPlansAsync();
        }

        private async Task<bool> HandlePlanRenameAsync(string newPlanName)
        {
            if (SelectedPlan == null)
                return false;

            string newFileName = $"{newPlanName}.pdf";

            bool planExists = await PlanNameExistsAsync(newPlanName, SelectedPlan.PlanID);
            bool fileExists = await _graphService.FileExistsAsync(newFileName);

            if (planExists || fileExists)
            {
                bool overwrite = await ShowAlertWithChoiceRequested.Invoke(
                    "Exists",
                    $"A plan or file named '{newPlanName}' already exists. Do you want to overwrite it?",
                    "Yes",
                    "No");

                if (!overwrite)
                {
                    await ShowAlertRequested.Invoke("Alert", "Please choose a different plan name before submitting.", "Okay");
                    return false;
                }

                if (planExists)
                    await DeletePlanByNameAsync(newPlanName);

                if (fileExists)
                    await _graphService.DeleteFileIfExistsAsync(newFileName);
            }

            string oldFileName = $"{SelectedPlan.Plan_Name}.pdf";
            bool renamed = await _graphService.RenameFileAsync(oldFileName, newFileName);

            if (!renamed)
            {
                await ShowAlertRequested.Invoke("Rename Failed", $"Could not rename file from '{oldFileName}' to '{newFileName}'.", "OK");
                return false;
            }

            return true;
        }

        private async Task<bool> UpdatePlanInDatabaseAsync(string newPlanName)
        {
            if (SelectedPlan == null)
                return false;

            try
            {
                string query = @"
                    UPDATE Digital_Plans
                    SET Plan_Name = @Plan_Name,
                        Rd_Name = @Rd_Name,
                        Subdivision = @Subdivision,
                        Reference = @Reference,
                        Plan_Date = @Plan_Date
                    WHERE PlanID = @PlanID";

                using var connection = new ConnectionQuery(query);
                var command = connection.Command();

                command.Parameters.AddWithValue("@PlanID", SelectedPlan.PlanID);
                command.Parameters.AddWithValue("@Plan_Name", newPlanName);
                command.Parameters.AddWithValue("@Rd_Name", RdNameEdit ?? string.Empty);
                command.Parameters.AddWithValue("@Subdivision", SubdivisionEdit ?? string.Empty);
                command.Parameters.AddWithValue("@Reference", ReferenceEdit ?? string.Empty);
                command.Parameters.AddWithValue("@Plan_Date", PlanDateEdit);

                await connection.OpenConnectionAsync();
                int rows = await command.ExecuteNonQueryAsync();
                await connection.CloseConnectionAsync();

                return rows > 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Database update failed: {ex}");
                return false;
            }
        }

        private async Task DeleteEditAsync()
        {
            if (SelectedPlan == null) return;

            bool confirm = await ShowAlertWithChoiceRequested.Invoke(
                "Confirm Delete",
                $"Are you sure you want to delete '{SelectedPlan.Plan_Name}'?",
                "Yes",
                "No");
            if (!confirm) return;

            IsBusy = true;

            try
            {
                string sharePointFileName = SelectedPlan.Plan_Name + ".pdf";

                bool sharePointDeleted = await _graphService.DeleteFileIfExistsAsync(sharePointFileName);
                Debug.WriteLine(sharePointDeleted
                    ? $"SharePoint file '{sharePointFileName}' deleted."
                    : $"SharePoint file '{sharePointFileName}' not found.");

                await DeletePlanAsync(SelectedPlan.PlanID);
                await LoadPlansAsync();

                await CancelEditAsync();
            }
            catch (Exception ex)
            {
                await ShowAlertRequested.Invoke("Delete Error", ex.Message, "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task CancelEditAsync()
        {
            try
            {
                _pdfLoadCts?.Cancel();
                _pdfLoadCts?.Dispose();
                _pdfLoadCts = null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error cancelling PDF load: {ex}");
            }

            IsEditPopupVisible = false;
            ClearEditFields();
            await Task.CompletedTask;
        }

        private void ClearEditFields()
        {
            PlanNameEdit = string.Empty;
            RdNameEdit = string.Empty;
            SubdivisionEdit = string.Empty;
            ReferenceEdit = string.Empty;
            PlanDateEdit = DateTime.Today;
        }

        private async Task DeletePlanAsync(int planID)
        {
            try
            {
                string query = "DELETE FROM Digital_Plans WHERE PlanID = @PlanID";
                using var connection = new ConnectionQuery(query);
                var command = connection.Command();
                command.Parameters.AddWithValue("@PlanID", planID);

                await connection.OpenConnectionAsync();
                int rowsAffected = await command.ExecuteNonQueryAsync();
                await connection.CloseConnectionAsync();

                if (rowsAffected == 0)
                {
                    await ShowAlertRequested.Invoke("Error", "No record was deleted.", "OK");
                }
                else
                {
                    await ShowAlertRequested.Invoke("Deleted", "Plan successfully deleted.", "OK");
                }
            }
            catch (Exception ex)
            {
                await ShowAlertRequested.Invoke("SQL Error", ex.Message, "OK");
            }
        }

        private async Task<bool> PlanNameExistsAsync(string planName, int excludePlanID)
        {
            try
            {
                string query = "SELECT COUNT(1) FROM Digital_Plans WHERE Plan_Name = @Plan_Name AND PlanID != @PlanID";
                using var connection = new ConnectionQuery(query);
                var command = connection.Command();

                command.Parameters.AddWithValue("@Plan_Name", planName);
                command.Parameters.AddWithValue("@PlanID", excludePlanID);

                await connection.OpenConnectionAsync();

                object? result = await command.ExecuteScalarAsync();

                await connection.CloseConnectionAsync();

                if (result != null && int.TryParse(result.ToString(), out int count))
                {
                    return count > 0;
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking plan name existence: {ex}");
                return false;
            }
        }

        private async Task DeletePlanByNameAsync(string planName)
        {
            try
            {
                string query = "DELETE FROM Digital_Plans WHERE Plan_Name = @Plan_Name";
                using var connection = new ConnectionQuery(query);
                var command = connection.Command();

                command.Parameters.AddWithValue("@Plan_Name", planName);

                await connection.OpenConnectionAsync();
                await command.ExecuteNonQueryAsync();
                await connection.CloseConnectionAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error deleting plan by name: {ex}");
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
                // Add this line to dispose your GraphService instance
                (_graphService as IDisposable)?.Dispose();

                _cts?.Cancel();
                _cts?.Dispose();
                _cts = null;

                _pdfLoadCts?.Cancel();
                _pdfLoadCts?.Dispose();
                _pdfLoadCts = null;
            }

            _isDisposed = true;
        }

        ~ViewPlansViewModel()
        {
            Dispose(false);
        }
    }
}

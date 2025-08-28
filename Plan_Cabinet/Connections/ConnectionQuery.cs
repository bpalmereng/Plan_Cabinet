using Microsoft.Data.SqlClient;
using Plan_Cabinet.Models;
using System.Collections.ObjectModel;
using System.Data;

namespace Plan_Cabinet.Connections
{
    public class ConnectionQuery : IDisposable
    {
        public ObservableCollection<Digital_Plans> Digital_Plans = new ObservableCollection<Digital_Plans>();

        private static readonly string ConnectionString = @"data source=192.168.1.4;initial Catalog=Plan_Cabinet;User ID=Plan_Inventory_Admin;Password=Pworks78;Encrypt=false";
        private readonly SqlConnection sqlcon = new SqlConnection(ConnectionString);
        private readonly Dictionary<string, SqlParameter> parameters = new();

        public string Query_;

        public ConnectionQuery(string Query_)
        {
            this.Query_ = Query_;
        }

        public void AddParameter(string name, SqlDbType type, object value)
        {
            parameters[name] = new SqlParameter(name, type) { Value = value };
        }

        public bool IsServerConnected()
        {
            using (var connection = new SqlConnection(sqlcon.ConnectionString))
            {
                try
                {
                    connection.Open();
                    return true;
                }
                catch (SqlException)
                {
                    return false;
                }
            }
        }

        // Synchronous open/close (keep if needed)
        public void OpenConnection()
        {
            if (sqlcon.State != ConnectionState.Open)
                sqlcon.Open();
        }

        public void CloseConnection()
        {
            if (sqlcon.State == ConnectionState.Open)
                sqlcon.Close();
        }

        // Async open/close methods
        public async Task OpenConnectionAsync()
        {
            if (sqlcon.State != ConnectionState.Open)
                await sqlcon.OpenAsync();
        }

        public Task CloseConnectionAsync()
        {
            if (sqlcon.State == ConnectionState.Open)
            {
                sqlcon.Close(); // Close is synchronous; no async version available
            }
            return Task.CompletedTask;
        }

        public SqlCommand Command()
        {
            SqlCommand command = new SqlCommand(Query_, sqlcon);
            foreach (var param in parameters.Values)
            {
                command.Parameters.Add(param);
            }
            return command;
        }

        public async Task<DataTable> RunQuery()
        {
            await OpenConnectionAsync();
            DataTable dtbl = new DataTable();
            SqlDataReader reader = await DataReader();
            dtbl.Load(reader);
            await CloseConnectionAsync();
            return dtbl;
        }

        public async Task<SqlDataReader> DataReader()
        {
            SqlCommand command = Command();
            SqlDataReader reader = await command.ExecuteReaderAsync();
            return reader;
        }

        public async Task<ObservableCollection<Digital_Plans>> Get_Plans()
        {
            DataTable dtbl = await RunQuery();

            Digital_Plans.Clear();
            foreach (DataRow row in dtbl.Rows)
            {
                Digital_Plans.Add(new Digital_Plans()
                {
                    PlanID = row.GetValue<int>("PlanID"),
                    Plan_Name = row.GetValue<string>("Plan_Name"),
                    Rd_Name = row.GetValue<string>("Rd_Name"),
                    Subdivision = row.GetValue<string>("Subdivision"),
                    Reference = row.GetValue<string>("Reference"),
                    Plan_Date = row.GetValue<DateTime>("Plan_Date").ToString("MM-dd-yyyy")
                });
            }

            return Digital_Plans;
        }

        // IDisposable implementation
        private bool _disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                sqlcon?.Dispose();
            }

            _disposed = true;
        }
    }

    public static class ExtensionMethods
    {
        public static T GetValue<T>(this DataRow row, string columnName, T defaultValue = default!)
        {
            object obj = row[columnName];
            return obj is T t ? t : defaultValue;
        }
    }
}


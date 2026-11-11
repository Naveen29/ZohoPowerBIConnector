namespace ZohoPowerBIConnector.Services.ZohoSyncService
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using Microsoft.Data.SqlClient;
    using System.Linq;
    using Newtonsoft.Json.Linq;
    using ZohoPowerBIConnector.Services.ZohoSyncService.Models;
    using Microsoft.AspNetCore.DataProtection;

    public class DatabaseService
    {
        private readonly string _conn;
        private readonly IDataProtector _protector;

        public DatabaseService(IDataProtectionProvider protectionProvider, string connectionString = null)
        {
            _conn = connectionString ?? Config.ConnectionString;
            _protector = protectionProvider.CreateProtector("ZohoSecretsProtector");
        }

        public List<Client> GetSubscribedClients()
        {
            var clients = new List<Client>();

            using var con = new SqlConnection(_conn);
            con.Open();

            var cmd = new SqlCommand(@"
                Select u.Id, U.FullName as Name, u.Email as SubEmail, p.Name + ' - ' + p.Description as PlanName,
                    z.ClientId AS ZohoClientId, z.ClientSecret as ZohoClientSecret, z.RefreshToken as ZohoRefreshToken, z.OrganizationId, z.BaseUrl as ApiBaseUrl
                    ,s.IsActive , s.ExpiryDate
                from Users u 
                    Inner Join ZohoConfigurations z On z.UserId = u.Id
                    Inner join Subscriptions s on s.UserId = u.Id
                    Inner Join Plans p on p.Id = s.PlanId
                where u.IsActive = 1 and s.IsActive = 1
                    and s.ExpiryDate >= GETUTCDATE()
            ", con);

            var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                clients.Add(new Client
                {
                    Id = (int)reader["Id"],
                    Name = reader["Name"].ToString(),
                    ZohoClientId = reader["ZohoClientId"].ToString(),
                    ZohoClientSecret = _protector.Unprotect(reader["ZohoClientSecret"].ToString()),
                    ZohoRefreshToken = _protector.Unprotect(reader["ZohoRefreshToken"].ToString()),
                    OrganizationId = reader["OrganizationId"].ToString(),
                    ApiBaseUrl = reader["ApiBaseUrl"].ToString(),
                    IsActive = Convert.ToBoolean(reader["IsActive"]),

                    SubscriptionEndDate = Convert.ToDateTime(reader["ExpiryDate"]),
                    Email = reader["SubEmail"]?.ToString(),
                    PlanName = reader["PlanName"]?.ToString(),
                });
            }

            return clients;
        }

        public DateTime GetLastSyncTime(string module, int clientId)
        {
            using var connection = new SqlConnection(_conn);
            connection.Open();

            var cmd = new SqlCommand(
                "SELECT LastSyncTime FROM ZohoSyncHistory WHERE ModuleName=@Module AND ClientId=@ClientId",
                connection);

            cmd.Parameters.AddWithValue("@Module", module);
            cmd.Parameters.AddWithValue("@ClientId", clientId);

            var result = cmd.ExecuteScalar();

            if (result == null)
                return DateTime.Parse("2000-01-01");

            return (DateTime)result;
        }

        public void UpdateLastSyncTime(string module, int clientId, DateTime time)
        {
            using var connection = new SqlConnection(_conn);
            connection.Open();

            var cmd = new SqlCommand(@"
        MERGE ZohoSyncHistory t
        USING (SELECT @Module ModuleName, @ClientId ClientId) s
        ON t.ModuleName = s.ModuleName AND t.ClientId = s.ClientId
        WHEN MATCHED THEN UPDATE SET LastSyncTime=@Time
        WHEN NOT MATCHED THEN
        INSERT(ModuleName, ClientId, LastSyncTime)
        VALUES(@Module, @ClientId, @Time);", connection);

            cmd.Parameters.AddWithValue("@Module", module);
            cmd.Parameters.AddWithValue("@ClientId", clientId);
            cmd.Parameters.AddWithValue("@Time", time);

            cmd.ExecuteNonQuery();
        }

        public void DetectNewFields(string module, JToken records)
        {
            var columns = GetExistingFields(module);

            foreach (var r in records)
            {
                foreach (var prop in ((JObject)r).Properties())
                {
                    if (!columns.Contains(prop.Name))
                    {
                        AddNewField(module, prop.Name);
                        columns.Add(prop.Name);
                    }
                }
            }
        }

        private HashSet<string> GetExistingFields(string module)
        {
            var fields = new HashSet<string>();

            using var con = new SqlConnection(_conn);
            con.Open();

            var cmd = new SqlCommand(
                "SELECT FieldName FROM Zoho_FieldMetadata WHERE ModuleName=@m", con);

            cmd.Parameters.AddWithValue("@m", module);

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
                fields.Add(reader.GetString(0));

            return fields;
        }

        private void AddNewField(string module, string field)
        {
            using var con = new SqlConnection(_conn);
            con.Open();

            var sql = @"INSERT INTO Zoho_FieldMetadata(ModuleName,FieldName)
                        VALUES(@m,@f)";

            var cmd = new SqlCommand(sql, con);

            cmd.Parameters.AddWithValue("@m", module);
            cmd.Parameters.AddWithValue("@f", field);

            cmd.ExecuteNonQuery();

            SyncLogger.Log($"New Zoho field detected → {module}.{field}");
        }

        public DataTable BuildStageTable(string module, string recordId, JToken records, int clientId)
        {
            var table = new DataTable();

            table.Columns.Add("ClientId", typeof(int));
            table.Columns.Add("ModuleName");
            table.Columns.Add("RecordId");
            table.Columns.Add("LastModifiedTime", typeof(DateTime));
            table.Columns.Add("RawJson");

            foreach (var r in records)
            {
                var row = table.NewRow();

                row["ClientId"] = clientId;
                row["ModuleName"] = module;
                row["RecordId"] = r[recordId]?.ToString();
                row["LastModifiedTime"] =
                    r["last_modified_time"]?.ToObject<DateTime?>()
                    ?? DateTime.UtcNow;

                row["RawJson"] = r.ToString();

                table.Rows.Add(row);
            }

            return table;
        }

        public void BulkInsertStage(DataTable table, int clientId)
        {
            using var con = new SqlConnection(_conn);
            con.Open();

            using var bulk = new SqlBulkCopy(con);

            bulk.DestinationTableName = "Zoho_Stage";

            bulk.ColumnMappings.Add("ClientId", "ClientId");
            bulk.ColumnMappings.Add("ModuleName", "ModuleName");
            bulk.ColumnMappings.Add("RecordId", "RecordId");
            bulk.ColumnMappings.Add("LastModifiedTime", "LastModifiedTime");
            bulk.ColumnMappings.Add("RawJson", "RawJson");

            bulk.WriteToServer(table);

            SyncLogger.Log($"Bulk inserted {table.Rows.Count} records.");
        }

        public void RunMergeProcedure(string procedure, int clientId)
        {
            using var connection = new SqlConnection(_conn);
            connection.Open();

            var cmd = new SqlCommand(procedure, connection);
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.AddWithValue("@ClientId", clientId);

            cmd.ExecuteNonQuery();
        }

        public List<(long InvoiceId, string InvoiceNumber)> GetInvoiceList(int clientId)
        {
            var pendingInvoices = new List<(long, string)>();

            using var con = new SqlConnection(_conn);
            con.Open();

            using var cmd = new SqlCommand(
                "SELECT InvoiceId, InvoiceNumber FROM ZohoInvoices WHERE IsLineItemAdded IS NULL AND ClientId=@ClientId",
                con);

            cmd.Parameters.AddWithValue("@ClientId", clientId);

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                pendingInvoices.Add((
                    Convert.ToInt64(reader["InvoiceId"]),
                    reader["InvoiceNumber"].ToString()
                ));
            }

            return pendingInvoices;
        }

        public void ProcessInvoiceItems(JToken invoice, int clientId)
        {
            long InvoiceId = ((long)invoice["invoice_id"]);
            string InvoiceNumber = ((string)invoice["invoice_number"]);
            string InvoiceDate = ((string)invoice["date"]);

            DataTable dt = new DataTable();
            dt.Columns.Add("ClientId", typeof(int));
            dt.Columns.Add("InvoiceId", typeof(long));
            dt.Columns.Add("InvoiceNumber", typeof(string));
            dt.Columns.Add("InvoiceDate", typeof(string));
            dt.Columns.Add("LineItemId", typeof(long));
            dt.Columns.Add("ItemId", typeof(long));
            dt.Columns.Add("SKU", typeof(string));
            dt.Columns.Add("ItemName", typeof(string));
            dt.Columns.Add("Quantity", typeof(decimal));
            dt.Columns.Add("Rate", typeof(decimal));
            dt.Columns.Add("ItemTotal", typeof(decimal));
            dt.Columns.Add("DiscountAmount", typeof(decimal));
            dt.Columns.Add("DiscountPercent", typeof(decimal));
            dt.Columns.Add("TaxId", typeof(long));
            dt.Columns.Add("TaxName", typeof(string));
            dt.Columns.Add("TaxType", typeof(string));
            dt.Columns.Add("TaxPercentage", typeof(decimal));
            dt.Columns.Add("TaxAmount", typeof(decimal));
            dt.Columns.Add("Unit", typeof(string));
            dt.Columns.Add("ItemType", typeof(string));
            dt.Columns.Add("SalesOrderItemId", typeof(long));

            var lineItems = invoice["line_items"];

            foreach (var item in lineItems)
            {
                decimal taxAmount = 0;
                decimal taxPercent = 0;

                if (item["line_item_taxes"] != null && item["line_item_taxes"].Any())
                {
                    var tax = item["line_item_taxes"][0];
                    taxAmount = string.IsNullOrWhiteSpace(tax["tax_amount"]?.ToString())
                                    ? 0
                                    : Convert.ToDecimal(tax["tax_amount"]);
                    taxPercent = string.IsNullOrWhiteSpace(tax["tax_percentage"]?.ToString())
                                    ? 0
                                    : Convert.ToDecimal(tax["tax_percentage"]);
                }

                dt.Rows.Add(
                    clientId,
                    SafeConvert.ToLong(InvoiceId),
                    SafeConvert.ToStringSafe(InvoiceNumber),
                    InvoiceDate,
                    SafeConvert.ToLong(item["line_item_id"]),
                    SafeConvert.ToLong(item["item_id"]),
                    SafeConvert.ToStringSafe(item["sku"]),
                    SafeConvert.ToStringSafe(item["name"]),
                    SafeConvert.ToDecimal(item["quantity"]),
                    SafeConvert.ToDecimal(item["rate"]),
                    SafeConvert.ToDecimal(item["item_total"]),
                    SafeConvert.ToDecimal(item["discount_amount"]),
                    SafeConvert.ToDecimal(item["discount_percentage"]),
                    SafeConvert.ToLong(item["tax_id"]),
                    SafeConvert.ToStringSafe(item["tax_name"]),
                    SafeConvert.ToStringSafe(item["tax_type"]),
                    taxPercent,
                    taxAmount,
                    SafeConvert.ToStringSafe(item["unit"]),
                    SafeConvert.ToStringSafe(item["item_type"]),
                    SafeConvert.ToLong(item["sales_order_item_id"])
                );
            }

            using var con = new SqlConnection(_conn);
            con.Open();

            using var bulk = new SqlBulkCopy(con);
            bulk.DestinationTableName = "ZohoInvoiceLineItems";
            bulk.WriteToServer(dt);

            using var updateCmd = new SqlCommand(
                "UPDATE ZohoInvoices SET IsLineItemAdded = 1 WHERE InvoiceId = @InvoiceId AND ClientId = @ClientId",
                con);
            updateCmd.Parameters.AddWithValue("@InvoiceId", InvoiceId);
            updateCmd.Parameters.AddWithValue("@ClientId", clientId);
            updateCmd.ExecuteNonQuery();
        }

        public bool IsExpiredMailSentToday(int clientId)
        {
            using var con = new SqlConnection(_conn);
            con.Open();

            var cmd = new SqlCommand(
                "SELECT COUNT(*) FROM SyncLog WHERE ClientId=@ClientId AND EventType='ExpiredMail' AND CAST(CreatedAt AS DATE) = CAST(GETUTCDATE() AS DATE)",
                con);

            cmd.Parameters.AddWithValue("@ClientId", clientId);

            int count = (int)cmd.ExecuteScalar();
            return count > 0;
        }

        public void UpdateExpiredMailSent(int clientId)
        {
            using var con = new SqlConnection(_conn);
            con.Open();

            var cmd = new SqlCommand(
                "INSERT INTO SyncLog(ClientId, EventType, CreatedAt) VALUES(@ClientId, 'ExpiredMail', GETUTCDATE())",
                con);

            cmd.Parameters.AddWithValue("@ClientId", clientId);
            cmd.ExecuteNonQuery();
        }

        public bool IsReminderSentToday(int clientId)
        {
            using var con = new SqlConnection(_conn);
            con.Open();

            var cmd = new SqlCommand(
                "SELECT COUNT(*) FROM SyncLog WHERE ClientId=@ClientId AND EventType='ReminderMail' AND CAST(CreatedAt AS DATE) = CAST(GETUTCDATE() AS DATE)",
                con);

            cmd.Parameters.AddWithValue("@ClientId", clientId);

            int count = (int)cmd.ExecuteScalar();
            return count > 0;
        }

        public void UpdateReminderSent(int clientId)
        {
            using var con = new SqlConnection(_conn);
            con.Open();

            var cmd = new SqlCommand(
                "INSERT INTO SyncLog(ClientId, EventType, CreatedAt) VALUES(@ClientId, 'ReminderMail', GETUTCDATE())",
                con);

            cmd.Parameters.AddWithValue("@ClientId", clientId);
            cmd.ExecuteNonQuery();
        }

        public void DisableSubscription(int clientId)
        {
            using var con = new SqlConnection(_conn);
            con.Open();

            var cmd = new SqlCommand(
                "UPDATE Subscriptions SET IsActive = 0 WHERE ClientId = @ClientId",
                con);

            cmd.Parameters.AddWithValue("@ClientId", clientId);
            cmd.ExecuteNonQuery();
        }

        public EmailConfig GetEmailConfig()
        {
            using var con = new SqlConnection(_conn);
            con.Open();

            var cmd = new SqlCommand("SELECT TOP 1 * FROM EmailConfig WHERE IsActive = 1", con);
            using var reader = cmd.ExecuteReader();

            if (reader.Read())
            {
                return new EmailConfig
                {
                    SmtpHost = reader["SmtpHost"].ToString(),
                    SmtpPort = (int)reader["SmtpPort"],
                    Username = reader["Username"].ToString(),
                    Password = reader["Password"].ToString(),
                    EnableSSL = (bool)reader["EnableSSL"],
                    FromEmail = reader["FromEmail"].ToString(),
                    FromName = reader["FromName"].ToString()
                };
            }

            return null;
        }

        public EmailTemplate GetEmailTemplate(string templateType)
        {
            using var con = new SqlConnection(_conn);
            con.Open();

            var cmd = new SqlCommand("SELECT * FROM EmailTemplates WHERE TemplateType = @Type", con);
            cmd.Parameters.AddWithValue("@Type", templateType);

            using var reader = cmd.ExecuteReader();

            if (reader.Read())
            {
                return new EmailTemplate
                {
                    Subject = reader["Subject"].ToString(),
                    Body = reader["Body"].ToString()
                };
            }

            return null;
        }
    }

    public static class SafeConvert
    {
        public static long ToLong(object value)
        {
            if (value == null || value is DBNull)
                return 0;
            return Convert.ToInt64(value);
        }

        public static string ToStringSafe(object value)
        {
            return value?.ToString() ?? string.Empty;
        }

        public static decimal ToDecimal(object value)
        {
            if (value == null || value is DBNull || string.IsNullOrWhiteSpace(value.ToString()))
                return 0;
            return Convert.ToDecimal(value);
        }
    }

    public class EmailTemplate
    {
        public string? Subject { get; set; }
        public string? Body { get; set; }
    }
}

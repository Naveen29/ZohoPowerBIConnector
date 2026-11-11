namespace ZohoPowerBIConnector.Services.ZohoSyncService
{
    using Newtonsoft.Json.Linq;
    using ZohoPowerBIConnector.Services.ZohoSyncService.Models;

    public class ZohoSyncEngine
    {
        private readonly DatabaseService _db;

        public ZohoSyncEngine(DatabaseService db)
        {
            _db = db;
        }

        public async Task SyncModule(ZohoModule module, Client client, ZohoApiClient api)
        {
            int errorCount = 0;

            try
            {
                if (module.Name == "InvoicesItems")
                {
                    // FILTER BY CLIENT
                    var pendingInvoices = _db.GetInvoiceList(client.Id);

                    foreach (var inv in pendingInvoices)
                    {
                        try
                        {
                            var response = await api.GetModuleData(
                                module.Endpoint,
                                DateTime.UtcNow,
                                0,
                                inv.InvoiceId
                            );

                            // PASS CLIENT ID
                            _db.ProcessInvoiceItems(response[module.DataRoot], client.Id);
                        }
                        catch (Exception ex)
                        {
                            SyncLogger.Log($"[{client.Name}] Error syncing {module.Name}: {ex.Message}");
                            errorCount++;
                        }

                        await Task.Delay(200);
                    }

                    SyncLogger.Log($"[{client.Name}] Error Summary: {module.Name} Count: {errorCount}");
                }
                else
                {
                    // CLIENT-BASED SYNC TIME
                    DateTime lastSync = _db.GetLastSyncTime(module.Name, client.Id);

                    DateTime apiTime = lastSync.AddMinutes(-Config.BufferMinutes);

                    int page = 1;
                    bool hasMore = true;

                    while (hasMore)
                    {
                        var response = await api.GetModuleData(module.Endpoint, apiTime, page);

                        var data = response[module.Endpoint];

                        if (data == null || data.Count() == 0)
                            break;

                        // PASS CLIENT ID
                        _db.DetectNewFields(module.Name, response[module.DataRoot]);

                        var table = _db.BuildStageTable(
                            module.Name,
                            module.RecordId,
                            response[module.DataRoot],
                            client.Id // IMPORTANT
                        );

                        // INSERT WITH CLIENT
                        _db.BulkInsertStage(table, client.Id);

                        _db.RunMergeProcedure(module.StoredProcedure, client.Id);

                        hasMore = response["page_context"]?["has_more_page"]?.Value<bool>() ?? false;

                        page++;
                    }

                    // UPDATE PER CLIENT
                    _db.UpdateLastSyncTime(module.Name, client.Id, DateTime.UtcNow);
                }

                await Task.Delay(200);
            }
            catch (Exception ex)
            {
                SyncLogger.Log($"[{client.Name}] Error syncing {module.Name}: {ex.Message}");
            }
        }
    }
}

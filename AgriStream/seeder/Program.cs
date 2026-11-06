// Usage: dotnet run -- "<storage connection string>" [rowsPerField=1000]
using Azure.Data.Tables;

if (args.Length < 1) { Console.WriteLine("Pass the storage connection string as the first argument."); return; }
int perField = args.Length > 1 ? int.Parse(args[1]) : 1000;
var table = new TableClient(args[0], "Telemetry");
await table.CreateIfNotExistsAsync();

var fields = new Dictionary<string, string[]>
{
    ["field-A"] = new[] { "sensor-001", "sensor-002", "sensor-003" },
    ["field-B"] = new[] { "sensor-004", "sensor-005", "sensor-006" },
    ["field-C"] = new[] { "sensor-007", "sensor-008", "sensor-009" },
};
var rnd = new Random();
var now = DateTime.UtcNow;
int total = 0;
foreach (var (field, devices) in fields)
{
    var batch = new List<TableTransactionAction>();
    for (int i = 0; i < perField; i++)
    {
        var ts = now.AddSeconds(-i * 10);
        var dev = devices[i % devices.Length];
        // Inverted ticks (zero-padded) => newest row sorts first inside the partition
        var rowKey = (DateTime.MaxValue.Ticks - ts.Ticks).ToString("D19") + "-" + dev;
        batch.Add(new TableTransactionAction(TableTransactionActionType.UpsertReplace, new TableEntity(field, rowKey)
        {
            ["DeviceId"] = dev,
            ["SoilMoisture"] = Math.Round(20 + rnd.NextDouble() * 40, 1),
            ["Temperature"] = Math.Round(15 + rnd.NextDouble() * 20, 1),
            ["ReadingUtc"] = ts
        }));
        if (batch.Count == 100) { await table.SubmitTransactionAsync(batch); total += 100; batch.Clear(); }
    }
    if (batch.Count > 0) { await table.SubmitTransactionAsync(batch); total += batch.Count; }
    Console.WriteLine($"{field}: done");
}
Console.WriteLine($"Inserted {total} rows into Telemetry.");

# AgriStream IoT: Step-by-Step Guide

Folder map
- `scripts/1-provision.sh`   creates every Azure resource (tasks a, b, c, and the Functions/App Insights shells)
- `sql/schema.sql`           Users + Devices tables (task b)
- `web/`                     MVC application (App Service)
- `functions/`               the 4 Azure Functions (task d)
- `seeder/`                  fills the Telemetry table with sample data
- `loadtest/agri-load.jmx`   JMeter test plan (task e)
- `scripts/2-autoscale.sh`   scale-out rules (task g)

Tools to install on your PC first: .NET 8 SDK, Azure Functions Core Tools v4, Azure CLI, Apache JMeter 5.6+ (needs Java 17).
An Azure account with a subscription is required. Sign in once with `az login`.

---------------------------------------------------------------------
STEP 1. Create all Azure resources  (Tasks a, b, c)
---------------------------------------------------------------------
1. Go to portal.azure.com and open Cloud Shell (top bar, `>_` icon), choose Bash.
2. Click the "Upload/Download files" icon and upload `scripts/1-provision.sh`.
3. Edit the top of the file: `nano 1-provision.sh`. Set a strong SQL password and your public IP
   (find it at whatismyip.com). Save with Ctrl+O, Enter, Ctrl+X.
4. Run: `bash 1-provision.sh`   (takes 5 to 10 minutes)
5. At the end it prints your **web URL**, **suffix number**, and **storage connection string**.
   Copy all three into a notepad. The suffix is also saved in `~/agri-suffix.txt`.
   If a region error appears, change `LOC=` (for example `northeurope` or `westeurope`) and delete the
   resource group (`az group delete -n rg-agristream`) before re-running.

SCREENSHOTS: resource group overview; App Service overview; SQL database overview; storage account
showing container `drone-images`, tables `Telemetry` and `ImageAudit`, queue `irrigation-commands`.

---------------------------------------------------------------------
STEP 2. Create the SQL tables  (Task b)
---------------------------------------------------------------------
1. Portal > SQL databases > AgriDb > **Query editor (preview)**. Sign in with `sqladmin` and your password.
   (If blocked, click the firewall link and add your IP.)
2. Paste the contents of `sql/schema.sql` and click Run.
SCREENSHOT: Tables list showing `Users` and `Devices`. Later: `SELECT * FROM Users` after registering.

---------------------------------------------------------------------
STEP 3. Deploy the 4 Azure Functions  (Task d)
---------------------------------------------------------------------
In a terminal on your PC (replace SUF with your suffix):
```
az login
cd functions
func azure functionapp publish agristream-func-SUF
```
Wait for "Deployment successful" and a list of 4 functions. Then get the function key:
```
az functionapp keys list -g rg-agristream -n agristream-func-SUF --query functionKeys.default -o tsv
```
Copy the key. Set it on the web app:
```
az webapp config appsettings set -g rg-agristream -n agristream-web-SUF --settings "FunctionApp__Key=PASTE_KEY"
```
SCREENSHOT: Portal > Function App > Functions list showing all four.

---------------------------------------------------------------------
STEP 4. Add sample telemetry
---------------------------------------------------------------------
```
cd seeder
dotnet run -- "PASTE_STORAGE_CONNECTION_STRING" 1000
```
This inserts 3,000 rows. SCREENSHOT: Storage account > Storage browser > Tables > Telemetry.

---------------------------------------------------------------------
STEP 5. Deploy the MVC app  (Task a, evidence for b)
---------------------------------------------------------------------
```
cd web
dotnet publish -c Release -o publish
cd publish && zip -r ../site.zip . && cd ..      (Windows: right-click publish folder > Compress)
az webapp deploy -g rg-agristream -n agristream-web-SUF --src-path site.zip --type zip
```
Open `https://agristream-web-SUF.azurewebsites.net`.
On first start the app creates user **admin / Admin123!** (hash stored in Azure SQL).
Use "Create account" to add a Farmer and an Agronomist as well.
SCREENSHOT: live site; successful login; Query editor `SELECT UserId, Username, Role FROM Users`.

---------------------------------------------------------------------
STEP 6. Prove every function works  (Task d evidence)
---------------------------------------------------------------------
1. Sign in > Field dashboard. The telemetry table is filled by Function 2 (GET).
2. Choose an image, click **Upload image** (Function 1 fires). Allow 1 to 5 minutes on the Consumption plan.
   Check Storage browser > Tables > ImageAudit for a new row with Status = Received.
   Check Function App > ImageUploadAudit > Invocations / Monitor for logs.
3. Click **Start irrigation** for sensor-001 (Function 3 puts a message on the queue, Function 4 processes it).
   In SQL Query editor run `SELECT * FROM Devices` and see sensor-001 Status = Irrigating.
SCREENSHOTS: blob in container `drone-images`; ImageAudit row; function invocation logs;
Devices row before and after.

---------------------------------------------------------------------
STEP 7. Configure autoscale  (Task g). Do this BEFORE load testing.
---------------------------------------------------------------------
In Cloud Shell edit `2-autoscale.sh` (set SUF), upload it, run `bash 2-autoscale.sh`.
SCREENSHOT: Portal > App Service plan `plan-agri` > Scale out (App Service plan) showing the custom
autoscale rules (CPU > 60% out, Memory > 75% out, CPU < 30% in, min 1, max 5).

---------------------------------------------------------------------
STEP 8. Run the load test  (Task e)
---------------------------------------------------------------------
Option A, JMeter on your PC:
```
jmeter -n -t loadtest/agri-load.jmx -Jhost=agristream-web-SUF.azurewebsites.net -Jusers=400 -Jramp=120 -Jduration=900 -l results.jtl -e -o report
```
Open `report/index.html` for graphs. If one PC cannot generate enough load (CPU stays under 60%),
raise `-Jusers` or run the command on two machines at once.

Option B, Azure Load Testing: create an "Azure Load Testing" resource, choose Create > Upload a JMeter
script, upload `agri-load.jmx`, add parameters `host`, `users`, `ramp`, `duration`, use 3 engine instances.

The test calls `/Home/Ping` (burns about 200 ms CPU per request) so App Service CPU rises and autoscale triggers.
SCREENSHOTS: test configuration; JMeter report (Statistics table, Response Times Over Time, Throughput).

---------------------------------------------------------------------
STEP 9. Monitor during the test  (Task f)
---------------------------------------------------------------------
While the test runs capture:
1. Application Insights `agri-ai` > Live Metrics (requests/sec, response time, failures, servers).
2. App Service plan `plan-agri` > Metrics: CPU Percentage, Memory Percentage (Avg), and Instance count
   (use metric "CpuPercentage" with a second chart for scale activity).
3. Application Insights > Performance and Failures blades.
4. Storage account > Metrics > Queue message count; SQL database > Metrics > DTU percentage.
5. Logs (Log Analytics) query, run after the test:
```
requests
| where timestamp > ago(1h)
| summarize reqs=count(), avgMs=avg(duration), p95=percentile(duration,95), failed=countif(success==false) by bin(timestamp,1m)
| render timechart
```
Then answer in your report: did p95 stay under about 1 s, was error rate under 1%, which resource limited
you (CPU, SQL DTU, queue), and how response time changed before, during, and after scale-out.

---------------------------------------------------------------------
STEP 10. Autoscale evidence  (Task g)
---------------------------------------------------------------------
After the test (scale-out can take 5 to 10 minutes to trigger):
1. App Service plan > Scale out > **Run history** tab (shows instance count rising to 2, 3 or more).
2. Monitor > Activity log > filter "Autoscale scale up".
3. After the load stops and 10+ minutes pass, the **scale in** event appears.
If it never scales, increase users, or temporarily change the rule to CPU > 40%.

---------------------------------------------------------------------
Clean up (avoid charges)
---------------------------------------------------------------------
`az group delete -n rg-agristream --yes --no-wait`

Report points: queue decouples the web tier from SQL; failed queue messages retry 5 times then go to
`irrigation-commands-poison` (no data loss); Table Storage partitioned by field with inverted-tick RowKeys
for fast "latest N" reads; Functions scale automatically on the Consumption plan. Improvements: Key Vault +
Managed Identity for secrets, Azure SQL scaling, CDN for images, IoT Hub/Event Hubs for real sensor ingestion.

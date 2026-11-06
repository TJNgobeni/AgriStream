#!/usr/bin/env bash
# Run in Azure Cloud Shell (bash). Edit the 3 values below first.
set -e
SQL_ADMIN_PASSWORD='ChangeMe-Str0ng!Pass'
MY_IP='<your public IP from whatismyip.com>'
LOC=southafricanorth          # change if your subscription has no quota here
RG=rg-agristream; SUF=$RANDOM
echo "SUFFIX=$SUF" > ~/agri-suffix.txt   # you need this later

az group create -n $RG -l $LOC
az appservice plan create -g $RG -n plan-agri --sku S1
az webapp create -g $RG -p plan-agri -n agristream-web-$SUF --runtime "dotnet:8"

az sql server create -g $RG -n agri-sql-$SUF -l $LOC -u sqladmin -p "$SQL_ADMIN_PASSWORD"
az sql db create -g $RG -s agri-sql-$SUF -n AgriDb --service-objective S0
az sql server firewall-rule create -g $RG -s agri-sql-$SUF -n AllowAzure --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0
az sql server firewall-rule create -g $RG -s agri-sql-$SUF -n MyIP --start-ip-address $MY_IP --end-ip-address $MY_IP

az storage account create -g $RG -n agristore$SUF -l $LOC --sku Standard_LRS
CS=$(az storage account show-connection-string -g $RG -n agristore$SUF --query connectionString -o tsv)
az storage container create --connection-string "$CS" -n drone-images
az storage table create --connection-string "$CS" -n Telemetry
az storage table create --connection-string "$CS" -n ImageAudit
az storage queue create --connection-string "$CS" -n irrigation-commands

az monitor log-analytics workspace create -g $RG -n agri-logs -l $LOC
WS=$(az monitor log-analytics workspace show -g $RG -n agri-logs --query id -o tsv)
az monitor app-insights component create -g $RG -a agri-ai -l $LOC --workspace $WS
AI=$(az monitor app-insights component show -g $RG -a agri-ai --query connectionString -o tsv)

az functionapp create -g $RG -n agristream-func-$SUF --storage-account agristore$SUF \
  --consumption-plan-location $LOC --runtime dotnet-isolated --runtime-version 8 --functions-version 4
SQLCS="Server=tcp:agri-sql-$SUF.database.windows.net,1433;Database=AgriDb;User ID=sqladmin;Password=$SQL_ADMIN_PASSWORD;Encrypt=True;TrustServerCertificate=False;"
az functionapp config appsettings set -g $RG -n agristream-func-$SUF --settings \
  "StorageConn=$CS" "SqlConn=$SQLCS" "APPLICATIONINSIGHTS_CONNECTION_STRING=$AI"
az webapp config appsettings set -g $RG -n agristream-web-$SUF --settings \
  "ConnectionStrings__AgriDb=$SQLCS" "Storage__Conn=$CS" \
  "FunctionApp__BaseUrl=https://agristream-func-$SUF.azurewebsites.net/" \
  "APPLICATIONINSIGHTS_CONNECTION_STRING=$AI"

echo "DONE. Suffix=$SUF"
echo "Web: https://agristream-web-$SUF.azurewebsites.net"
echo "Storage connection string (for seeder): $CS"

#!/usr/bin/env bash
# Task g. Run AFTER the app is deployed. Replace SUF with your number.
SUF=<your-suffix>; RG=rg-agristream
PLAN_ID=$(az appservice plan show -g $RG -n plan-agri --query id -o tsv)
az monitor autoscale create -g $RG --resource $PLAN_ID --resource-type Microsoft.Web/serverfarms \
  -n agri-autoscale --min-count 1 --max-count 5 --count 1
az monitor autoscale rule create -g $RG --autoscale-name agri-autoscale \
  --condition "CpuPercentage > 60 avg 5m" --scale out 1 --cooldown 5
az monitor autoscale rule create -g $RG --autoscale-name agri-autoscale \
  --condition "MemoryPercentage > 75 avg 5m" --scale out 1 --cooldown 5
az monitor autoscale rule create -g $RG --autoscale-name agri-autoscale \
  --condition "CpuPercentage < 30 avg 10m" --scale in 1 --cooldown 10

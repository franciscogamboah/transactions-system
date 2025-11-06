
param(
  [string]$BaseUrl = "http://127.0.0.1:5313",
  [string]$KafkaBootstrap = "localhost:9092",
  [int]$WaitSec = 2
)

function Write-Section($t) { Write-Host "`n=== $t ===" -ForegroundColor Cyan }

function Invoke-Json {
  param([string]$Method, [string]$Url, [object]$Body = $null, [hashtable]$Headers = @{})
  try {
    if ($Body) {
      return Invoke-RestMethod -Method $Method -Uri $Url -Headers $Headers -ContentType "application/json" -Body ($Body | ConvertTo-Json -Compress)
    }
    else {
      return Invoke-RestMethod -Method $Method -Uri $Url -Headers $Headers
    }
  }
  catch {
    # Defensive: Exception.Response can be $null (connection refused, host down, DNS failure).
    if ($PSItem.Exception.Response -ne $null) {
      try { Write-Host "HTTP ERROR $($PSItem.Exception.Response.StatusCode.value__)" -ForegroundColor Red } catch { Write-Host "HTTP ERROR (no status)" -ForegroundColor Red }
      $stream = $PSItem.Exception.Response.GetResponseStream()
      if ($stream) { $r = New-Object IO.StreamReader($stream); Write-Host ($r.ReadToEnd()) }
    }
    else {
      # No response object: most likely the API is down or connection refused.
      Write-Host "HTTP ERROR: $($PSItem.Exception.Message)" -ForegroundColor Red
    }
    throw
  }
}

function Ensure-Topics {
  Write-Section "Verificando/creando tópicos en Kafka"
  $cmd = @"
L=`$(kafka-topics --bootstrap-server $KafkaBootstrap --list)
echo "`$L"
echo "--- ensure topics ---"
echo "`$L" | grep -qx "transactions.created.v1"   || kafka-topics --bootstrap-server $KafkaBootstrap --create --topic transactions.created.v1   --partitions 1 --replication-factor 1
echo "`$L" | grep -qx "transactions.validated.v1" || kafka-topics --bootstrap-server $KafkaBootstrap --create --topic transactions.validated.v1 --partitions 1 --replication-factor 1
"@
  docker exec -i kafka bash -lc "$cmd" | Out-Host
}


function Health-Check {
  Write-Section "Health API"
  try {
    $h = Invoke-Json -Method GET -Url "$BaseUrl/health"
    Write-Host ("Health: " + ($h | ConvertTo-Json -Compress)) -ForegroundColor Green
  }
  catch {
    Write-Host "La API no respondió /health. Asegúrate de tenerla levantada." -ForegroundColor Red
    throw
  }
}

function New-Transaction {
  param([int]$Value)
  $idem = "demo-" + (Get-Random)
  $body = @{
    sourceAccountId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"
    targetAccountId = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"
    transferTypeId  = 1
    value           = $Value
  }
  Write-Host "POST /api/transactions value=$Value  Idempotency-Key=$idem"
  $res = Invoke-Json -Method POST -Url "$BaseUrl/api/transactions" -Body $body -Headers @{ "Idempotency-Key" = $idem }
  $res
}

function Publish-Validated {
  param([string]$TransactionId, [ValidateSet("approved", "rejected")][string]$Status, [string]$Reason = "manual")
  $payload = @{
    transactionExternalId = $TransactionId
    status                = $Status
    reason                = $Reason
    evaluatedAt           = (Get-Date).ToUniversalTime().ToString("o")
  } | ConvertTo-Json -Compress

  Write-Host "Produce → transactions.validated.v1  ($Status $Reason) id=$TransactionId"
  $payload | docker exec -i kafka bash -lc "kafka-console-producer --bootstrap-server $KafkaBootstrap --topic transactions.validated.v1" | Out-Null
}

function Get-Transaction {
  param([string]$TransactionId)
  Invoke-Json -Method GET -Url "$BaseUrl/api/transactions/$TransactionId"
}

function Wait-Until-NotPending {
  param([string]$TransactionId, [int]$TimeoutSec = 15)
  $deadline = (Get-Date).AddSeconds($TimeoutSec)
  do {
    Start-Sleep -Seconds $WaitSec
    try {
      $r = Get-Transaction -TransactionId $TransactionId
      if ($r.status -ne "pending") { return $r }
      Write-Host "Aún pending… reintentando" -ForegroundColor DarkYellow
    }
    catch { }
  } while ((Get-Date) -lt $deadline)
  return $null
}

# --- RUN ---
Write-Section "Inicio demo E2E"
Health-Check
Ensure-Topics

# A) Flujo APPROVED
Write-Section "Flujo APPROVED"
$txA = New-Transaction -Value 100
$txA | Format-Table | Out-Host
Publish-Validated -TransactionId $txA.transactionExternalId -Status approved -Reason ok
$finalA = Wait-Until-NotPending -TransactionId $txA.transactionExternalId -TimeoutSec 20
if ($finalA) {
  Write-Host "Resultado APPROVED:" -ForegroundColor Green
  $finalA | Format-Table | Out-Host
}
else {
  Write-Host "Timeout esperando cambio de estado (approved)" -ForegroundColor Red
}

# B) Flujo REJECTED
Write-Section "Flujo REJECTED"
$txR = New-Transaction -Value 3000
$txR | Format-Table | Out-Host
Publish-Validated -TransactionId $txR.transactionExternalId -Status rejected -Reason amount_limit
$finalR = Wait-Until-NotPending -TransactionId $txR.transactionExternalId -TimeoutSec 20
if ($finalR) {
  Write-Host "Resultado REJECTED:" -ForegroundColor Yellow
  $finalR | Format-Table | Out-Host
}
else {
  Write-Host "Timeout esperando cambio de estado (rejected)" -ForegroundColor Red
}

Write-Section "Fin demo"

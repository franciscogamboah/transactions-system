param(
    [string]$ComposeFile = ".\infra\docker-compose.yml",
    [string]$SchemaFile = ".\infra\schema\outbox.sql",
    [string]$KafkaContainer = "kafka",
    [string]$PostgresContainer = "postgres",

    [string]$BaseUrl = "http://127.0.0.1:5313",
    [string]$KafkaBootstrap = "localhost:9092",
    [string]$PgDatabase = "transactionsdb",
    [string]$PgUser = "postgres",
    [string]$PgPassword = "postgres",

    [string]$ApiCreatePath = "/api/transactions",
    [string]$ApiGetPathTemplate = "/api/transactions/{id}",

    [switch]$PublishValidated,
    [int]$WaitSec = 2,
    [int]$TimeoutSec = 120
)

$ErrorActionPreference = "Stop"

function Write-Section($t) { Write-Host "`n=== $t ===" -ForegroundColor Cyan }
function Write-OK($t) { Write-Host "[OK] $t" -ForegroundColor Green }
function Write-Warn($t) { Write-Host "[..] $t" -ForegroundColor DarkYellow }
function Write-Err($t) { Write-Host "[ERR] $t" -ForegroundColor Red }

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
        if ($PSItem.Exception.Response -ne $null) {
            try { $code = [int]$PSItem.Exception.Response.StatusCode } catch { $code = 0 }
            Write-Err "HTTP $code en $Method $Url"
            $stream = $PSItem.Exception.Response.GetResponseStream()
            if ($stream) { $r = New-Object IO.StreamReader($stream); Write-Host ($r.ReadToEnd()) }
        }
        else {
            Write-Err "HTTP ERROR: $($PSItem.Exception.Message)"
        }
        throw
    }
}

function Wait-Container-Healthy {
    param([string]$Name, [int]$Timeout = 90)
    $deadline = (Get-Date).AddSeconds($Timeout)
    do {
        $state = (docker inspect --format "{{json .State.Health.Status }}" $Name) 2>$null
        if ($LASTEXITCODE -ne 0) { Start-Sleep 2; continue }
        if ($state -match "healthy") { Write-OK "$Name healthy"; return }
        Start-Sleep 2
    } while ((Get-Date) -lt $deadline)
    throw "$Name no llego a healthy en $Timeout s"
}

function Ensure-Compose-Up {
    Write-Section "Docker Compose"
    if (-not (Test-Path $ComposeFile)) { throw "No existe $ComposeFile" }
    docker compose -f $ComposeFile up -d
    Write-OK "Compose up"
}

function Ensure-Db-Schema {
    Write-Section "Aplicando esquema BD (outbox)"
    if (Test-Path $SchemaFile) {
        Write-Host "Usando archivo: $SchemaFile"
        $sql = Get-Content -Raw -Path $SchemaFile
    }
    else {
        $sql = @'
CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE TABLE IF NOT EXISTS public.outbox (
  id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  aggregate_type  TEXT NOT NULL DEFAULT 'transaction',
  aggregate_id    UUID NOT NULL,
  event_type      TEXT NOT NULL,
  payload         JSONB NOT NULL,
  status          TEXT  NOT NULL DEFAULT 'pending',
  retry_count     INT   NOT NULL DEFAULT 0,
  next_attempt_at TIMESTAMPTZ,
  created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  sent_at         TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS idx_outbox_pending
  ON public.outbox (status, COALESCE(next_attempt_at, NOW()), created_at);
'@
        Write-Warn "Schema file no encontrado, usando DDL embebido"
    }

    $env:PGPASSWORD = $PgPassword
    $sql | docker exec -i $PostgresContainer psql -U $PgUser -d $PgDatabase -v "ON_ERROR_STOP=1" -q
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
    Write-OK "Esquema aplicado/actualizado"
}

function Ensure-Topics {
    Write-Section "Asegurando topicos Kafka"
    docker exec -i $KafkaContainer kafka-topics --bootstrap-server $KafkaBootstrap --create --if-not-exists --topic transactions.created.v1   --partitions 1 --replication-factor 1 | Out-Host
    docker exec -i $KafkaContainer kafka-topics --bootstrap-server $KafkaBootstrap --create --if-not-exists --topic transactions.validated.v1 --partitions 1 --replication-factor 1 | Out-Host
    Write-OK "Topicos OK"
}

function Health-Check {
    Write-Section "Health API"
    $h = Invoke-Json -Method GET -Url "$BaseUrl/health"
    Write-Host ("Health: " + ($h | ConvertTo-Json -Compress)) -ForegroundColor Green
}

function Create-Transaction {
    param([int]$Value)
    $idem = "demo-" + (Get-Random)
    $body = @{
        sourceAccountId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"
        targetAccountId = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"
        transferTypeId  = 1
        value           = $Value
    }
    $url = $BaseUrl.TrimEnd('/') + $ApiCreatePath
    Write-Host "POST $ApiCreatePath value=$Value  Idempotency-Key=$idem"
    $res = Invoke-Json -Method POST -Url $url -Body $body -Headers @{ "Idempotency-Key" = $idem }

    foreach ($k in @("transactionExternalId", "externalId", "id")) {
        if ($res.PSObject.Properties.Name -contains $k) { return [pscustomobject]@{ Id = $res.$k; Raw = $res } }
    }
    Write-Warn "No encontre propiedad de id, respuesta:"
    $res | ConvertTo-Json -Depth 5 | Write-Host
    throw "No se pudo obtener el id de la transaccion"
}

function Publish-Validated {
    param([string]$TransactionId, [ValidateSet("approved", "rejected")][string]$Status, [string]$Reason = "manual")
    $payload = @{
        transactionExternalId = $TransactionId
        status                = $Status
        reason                = $Reason
        evaluatedAt           = (Get-Date).ToUniversalTime().ToString("o")
    } | ConvertTo-Json -Compress
    Write-Host "Produce -> transactions.validated.v1  ($Status $Reason) id=$TransactionId"
    $payload | docker exec -i $KafkaContainer bash -lc "kafka-console-producer --bootstrap-server $KafkaBootstrap --topic transactions.validated.v1" | Out-Null
}

function Get-Transaction {
    param([string]$TransactionId)
    $path = $ApiGetPathTemplate.TrimStart('/').Replace('{id}', $TransactionId)
    $url = '{0}/{1}' -f $BaseUrl.TrimEnd('/'), $path
    Invoke-Json -Method GET -Url $url
}

function Wait-Until-NotPending {
    param([string]$TransactionId, [int]$Timeout = 20)
    $deadline = (Get-Date).AddSeconds($Timeout)
    do {
        Start-Sleep -Seconds $WaitSec
        try {
            $r = Get-Transaction -TransactionId $TransactionId
            if ($r.status -ne "pending") { return $r }
            Write-Host "Aun pending... reintentando" -ForegroundColor DarkYellow
        }
        catch {
            Write-Warn "GET fallo (transitorio), reintento..."
        }
    } while ((Get-Date) -lt $deadline)
    return $null
}

# --- RUN ---
Write-Section "Inicio DEMO E2E"

# 1) Infra
docker compose -f $ComposeFile down -v --remove-orphans | Out-Null
docker compose -f $ComposeFile up -d
Write-OK "Compose up"

Wait-Container-Healthy -Name $PostgresContainer  -Timeout $TimeoutSec
Wait-Container-Healthy -Name "zookeeper"         -Timeout $TimeoutSec
Wait-Container-Healthy -Name $KafkaContainer     -Timeout $TimeoutSec

# 2) DB + Topics
Ensure-Db-Schema
Ensure-Topics

# 3) API health (asume API ya corriendo en otra consola)
Health-Check

# 4) Flujo APPROVED
Write-Section "Flujo APPROVED"
$txA = Create-Transaction -Value 100
$txA.Raw | Format-List | Out-Host
if ($PublishValidated) {
    Publish-Validated -TransactionId $txA.Id -Status approved -Reason ok
    $finalA = Wait-Until-NotPending -TransactionId $txA.Id -Timeout 20
    if ($finalA) { Write-OK "Resultado APPROVED"; $finalA | Format-List | Out-Host } else { Write-Err "Timeout esperando approved" }
}
else {
    Write-Warn "PublishValidated no activado. Saltando cambio de estado."
}

# 5) Flujo REJECTED
Write-Section "Flujo REJECTED"
$txR = Create-Transaction -Value 3000
$txR.Raw | Format-List | Out-Host
if ($PublishValidated) {
    Publish-Validated -TransactionId $txR.Id -Status rejected -Reason amount_limit
    $finalR = Wait-Until-NotPending -TransactionId $txR.Id -Timeout 20
    if ($finalR) { Write-Warn "Resultado REJECTED"; $finalR | Format-List | Out-Host } else { Write-Err "Timeout esperando rejected" }
}

Write-Section "Fin DEMO"

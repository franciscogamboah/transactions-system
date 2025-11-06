# Transactions System

Sistema de ejemplo para gestionar transacciones con **Transactional Outbox** + **Kafka**, asegurando entrega eventual y resiliencia entre servicios.

---

## Contenidos

- `src/Transactions.Api` — API REST (crea/consulta transacciones) + workers:
  - `OutboxPublisherWorker`: publica `transactions.created.v1` desde la tabla outbox.
  - `StatusConsumerWorker`: consume `transactions.validated.v1` y actualiza estado.
- `src/Transactions.Application` / `src/Transactions.Domain` — casos de uso y dominio.
- `src/Transactions.Infrastructure` — Postgres (Dapper), Outbox Store, Kafka producer/consumer.
- `src/Antifraud.Mock` — servicio mock: consume `created` y publica `validated`.
- `infra/` — Docker Compose (Postgres, Zookeeper, Kafka) + scripts:
  - `infra/001_schema.sql` y `infra/002_outbox.sql` — scripts BD.
  - `infra/demo-transactions.ps1` — demo E2E (opcional).

---

## Arquitectura (resumen)

1. **Cliente → `Transactions.Api`** (`POST /api/transactions`): crea una transacción `pending` en Postgres y agrega fila en **outbox**.
2. **`OutboxPublisherWorker`** publica eventos `transactions.created.v1` a Kafka desde la outbox.
3. **`Antifraud.Mock`** consume `created`, evalúa (approved/rejected) y publica `transactions.validated.v1`.
4. **`StatusConsumerWorker`** consume `validated` y actualiza el estado en Postgres.

> Diagrama (conceptual)
>
> ```mermaid
> flowchart LR
>   A[Client / Postman] -->|POST /api/transactions| B[Transactions.Api]
>   B -->|INSERT pending + outbox| C[(Postgres)]
>   D[OutboxPublisherWorker] -->|publish created| K1[(Kafka: transactions.created.v1)]
>   E[Antifraud.Mock] -->|consume created| K1
>   E -->|publish validated| K2[(Kafka: transactions.validated.v1)]
>   F[StatusConsumerWorker] -->|consume validated| K2
>   F -->|update status| C
> ```

---

## Requisitos

- **.NET SDK 9**
- **Docker** y **Docker Compose** (entorno de desarrollo)
- **PowerShell (Windows)** para los scripts en `infra/`

> La API expone `GET /health` y endpoints bajo `/api/transactions`.

---

## Variables / `appsettings` (ejemplo)

Usa `appsettings.Development.json` o variables de entorno equivalentes:

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5432;Database=transactionsdb;Username=postgres;Password=postgres"
  },
  "Kafka": {
    "BootstrapServers": "localhost:9092",
    "TopicCreated": "transactions.created.v1",
    "TopicValidated": "transactions.validated.v1",
    "GroupId": "status-worker"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}


---

## Variables / `appsettings` (ejemplo)

Pasos manuales (arranque completo)

Ejecuta estos pasos desde la raíz del repositorio.

1) Levantar infraestructura
docker compose -f infra/docker-compose.yml up -d


Validar contenedores/health:

docker ps
docker inspect --format "{{.Name}} => {{.State.Health.Status}}" zookeeper
docker inspect --format "{{.Name}} => {{.State.Health.Status}}" kafka
docker inspect --format "{{.Name}} => {{.State.Health.Status}}" postgres

2) Crear/asegurar tópicos de Kafka
docker exec -it kafka bash -lc "kafka-topics --bootstrap-server localhost:9092 --create --if-not-exists --topic transactions.created.v1    --partitions 1 --replication-factor 1 --config retention.ms=86400000"
docker exec -it kafka bash -lc "kafka-topics --bootstrap-server localhost:9092 --create --if-not-exists --topic transactions.validated.v1  --partitions 1 --replication-factor 1 --config retention.ms=86400000"
docker exec -it kafka bash -lc "kafka-topics --bootstrap-server localhost:9092 --create --if-not-exists --topic transactions.created.dlq.v1 --partitions 1 --replication-factor 1 --config retention.ms=86400000"

# Listar para validar
docker exec -it kafka bash -lc "kafka-topics --bootstrap-server localhost:9092 --list"

3) Aplicar scripts SQL

Rutas: infra/001_schema.sql y infra/002_outbox.sql.

$env:PGPASSWORD="postgres"
type .\infra\001_schema.sql | docker exec -i postgres psql -U postgres -d transactionsdb -v "ON_ERROR_STOP=1" -q
type .\infra\002_outbox.sql | docker exec -i postgres psql -U postgres -d transactionsdb -v "ON_ERROR_STOP=1" -q
Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue


Validación rápida de la tabla outbox:

$check = @"
SELECT column_name, data_type
FROM information_schema.columns
WHERE table_schema='public' AND table_name='outbox'
ORDER BY ordinal_position;
"@
$check | docker exec -i postgres psql -U postgres -d transactionsdb -q

4) Levantar la API (Terminal 1)
dotnet run --project src/Transactions.Api --no-launch-profile --urls http://0.0.0.0:5313


Health (otra consola):

Invoke-RestMethod http://127.0.0.1:5313/health

5) Levantar el Antifraud Mock (Terminal 2)
dotnet run --project src/Antifraud.Mock

6) Ejecutar la demo E2E (opcional)

El script infra/demo-transactions.ps1 crea transacciones y puede publicar a validated (según flags). Requiere la API arriba.

.\infra\demo-transactions.ps1
# (opcional) forzar publicación a validated:
# .\infra\demo-transactions.ps1 -PublishValidated

Endpoints principales
Health
GET /health
200 OK
{ "ok": true }

Crear transacción
POST /api/transactions
Headers:
  Content-Type: application/json
  Idempotency-Key: <string>
Body:
{
  "sourceAccountId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "targetAccountId": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
  "transferTypeId": 1,
  "value": 100
}
201 Created
{
  "transactionExternalId": "<guid>",
  "status": "pending",
  "createdAt": "2025-01-01T00:00:00Z"
}

Obtener por ID
GET /api/transactions/{id}
200 OK
{
  "externalId": "<guid>",
  "status": "approved|rejected|pending",
  "createdAt": "2025-01-01T00:00:00Z"
}
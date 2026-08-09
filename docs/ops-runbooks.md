# Shora Ops Runbooks

Runbook content is maintained in [`src/backend/Shora.Application/Ops/runbooks.json`](../src/backend/Shora.Application/Ops/runbooks.json) (embedded in the API at build time).

Other operator docs: [docs/README.md](README.md) (Azure deploy, production config).

| Where to read | How |
| ------------- | --- |
| **Admin dashboard** | `/admin/ops` — active alerts with expandable runbook steps |
| **API** | `GET /api/v1/admin/ops/runbooks` (admin auth) |
| **Source file** | [`runbooks.json`](../src/backend/Shora.Application/Ops/runbooks.json) |

Alert `runbookId` values match [`OpsRunbookIds.cs`](../src/backend/Shora.Application/Ops/OpsRunbookIds.cs). Edit `runbooks.json` and redeploy the API to update runbook text.

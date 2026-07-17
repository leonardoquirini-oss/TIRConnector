# TIRConnector — Sicurezza DB (read-only)

Script pronti da eseguire **tu (DBA)**. Il connector non crea credenziali né si connette per provisioning.

## Cosa fanno

| File | Motore | Effetto |
|---|---|---|
| `01_create_readonly_login.sql` | SQL Server (`TirSQL`) | Crea login `tir_connector_ro`: solo `db_datareader` + `DENY` scrittura/EXECUTE. Garanzia 100% niente scritture sul target. |

> Nota: il DB target è SQL Server → **non** esiste un read-only per-sessione lato app
> (a differenza di PostgreSQL). L'unica garanzia reale è questo login con permessi ridotti.

Lo storage dei template di TIR è su PostgreSQL (`PostgresConnection` → DB `berlink`).
Per ridurre anche lì il superuser, riusa `berlink_app` (vedi
`../../../BERLinkConnector/database/security/02_create_template_role.sql`).

## Passi

1. Aprire `01_create_readonly_login.sql`, sostituire `<STRONG_PASSWORD>`, verificare il nome DB (`TirSQL`).
2. Eseguirlo come `sysadmin`.
3. Verificare (blocco `VERIFICA` in fondo allo script): la `SELECT` passa, l'`INSERT` fallisce con "permission denied".
4. Aggiornare la connection string del connector — **non** committare segreti reali:

   `TIRConnector.API/appsettings.json` → `ConnectionStrings:DefaultConnection`
   ```
   Server=...;Database=TirSQL;User Id=tir_connector_ro;Password=<STRONG_PASSWORD>;TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=true
   ```
   In produzione passare via variabile d'ambiente / secret store (vedi Livello 4 di `HARDENING.md`),
   non nel file committato.

## Prova finale (il vero 100%)

Con il connector che punta a `tir_connector_ro`, via API (`X-API-Key`) inviare una
scrittura (`INSERT ...`) a `/api/query/execute`: deve fallire perché **il database** la
rifiuta, indipendentemente dal validatore.

# Hardening SQL Injection / Passthrough SQL grezzo — TIRConnector + BERLinkConnector

## Contesto

Obiettivo: essere **sicuri al 100%** che nessuno dei due connector possa essere usato per
SQL injection o per scrivere sul database via passthrough SQL grezzo. Vale sia per
`TIRConnector` (SQL Server, utente `sa`) sia per il gemello `BERLinkConnector`
(PostgreSQL, utente `postgres`).

Esito dell'audit (mappa completa di ogni punto in cui una stringa arriva al motore SQL):

- **La injection classica sui valori è già impossibile.** Ogni parametro è associato con
  `DbConnection.CreateCommand()` + `CreateParameter` (mai concatenato). Gli identificatori
  utente in `MetadataService` sono passati come parametri (`@Schema`/`@ObjectName`). La
  paginazione `OFFSET…FETCH` usa `int` tipizzati. Non esistono `FromSqlRaw`/`ExecuteSqlRaw`/
  `string.Format` né SQL con identificatori dinamici.
- **La vera esposizione è per come è progettato**: `/api/query/execute` e l'esecuzione dei
  template sono un **passthrough SQL in sola lettura**. L'intera SELECT è scritta dal chiamante.
  La sicurezza dipende solo da (a) l'API-key, (b) il `SqlQueryValidator` a regex, (c) i
  permessi dell'utente DB.
- **Un validatore a blocklist non può dare il 100%.** Falle concrete trovate:
  - **BERLink — bypass di scrittura DML reale**: `SELECT query_to_xml('DELETE FROM t',true,false,'')`
    — il validatore rimuove i literal `'…'` *prima* di cercare le keyword, quindi il `DELETE`
    diventa invisibile. `dblink()`/`dblink_exec()` eseguono SQL arbitrario. `pg_read_file`/
    `lo_export` (lettura/scrittura file), `pg_sleep` (DoS): nessuno bloccato.
  - La blocklist di TIR è più forte ma manca comunque funzioni di lettura + DoS con query pesanti.
  - Entrambi si connettono come **superuser** del DB → ogni falla è pienamente sfruttabile.
  - `EnableQueryValidation=false` rimuove anche il blocco dei `;`/stacked query (path ad-hoc).
  - TIR **non** valida l'SQL dei template al salvataggio (BERLink sì).

**Decisione: lockdown completo + includere anche i problemi di sicurezza adiacenti.**
L'unica garanzia reale del 100% è il **read-only imposto dal database**; il validatore resta
come difesa in profondità.

---

## Livello 1 — Read-only imposto dal database (LA garanzia del 100%) — priorità massima

Un utente non-superuser che *fisicamente non può* scrivere rende innocua ogni falla del validatore.

> **Responsabilità: l'utente read-only lo crei TU (DBA), NON io.**
> Io fornisco solo gli **script SQL pronti** (`CREATE LOGIN/ROLE`, `GRANT`, `DENY`,
> `ALTER ROLE … read_only`) come file nel repo (es. `database/security/`).
> **Non** mi connetto ad alcun database, **non** eseguo gli script, **non** creo credenziali.
> Le uniche modifiche lato app che faccio: sostituire le connection string col nuovo utente e
> aggiungere il wrapper di transazione read-only.

### SQL Server (TIRConnector) — *io consegno lo script, tu lo esegui*
- Lo script crea un login dedicato, es. `tir_connector_ro`, mappato nel DB target solo su
  **`db_datareader`** — niente `db_datawriter`, `db_ddladmin`, `db_owner`.
- `DENY EXECUTE` sullo schema (niente stored procedure / `xp_`/`sp_`).
- Poi io tolgo `sa` dalla connection string. File: `TIRConnector.API/appsettings.json`
  (`ConnectionStrings:DefaultConnection`) + override di produzione in `docker-compose.yml`/
  variabili d'ambiente. I segreti fuori da git (vedi Livello 4).
- Opzionale: `ApplicationIntent=ReadOnly` se esiste una replica di Availability Group.

### PostgreSQL (BERLinkConnector) — *io consegno lo script, tu lo esegui*
- Lo script crea un ruolo dedicato, es. `berlink_ro`: `GRANT CONNECT` + `GRANT SELECT` solo
  sullo schema/tabelle necessari. **Nessun** grant INSERT/UPDATE/DELETE/TRUNCATE.
- `ALTER ROLE berlink_ro SET default_transaction_read_only = on;` — blocco netto di ogni scrittura.
- **Non** concedere `pg_read_server_files` / `pg_write_server_files` / `pg_execute_server_program`.
  Verificare che le estensioni `dblink`/`file_fdw` siano assenti o non usabili da questo ruolo.
- Poi io tolgo `postgres` da entrambe le connection string in
  `BERLinkConnector.API/appsettings.json` (`DefaultConnection`, e `PostgresConnection` — il DB
  dei template può restare in scrittura ma non deve essere superuser).
- Cintura + bretelle lato app: incapsulare ogni comando ad-hoc/template in una transazione
  read-only (`SET TRANSACTION READ ONLY` / aprire la transazione Npgsql senza mai committare
  scritture) in `QueryService`/`QueryTemplateService`.

*Questo livello da solo soddisfa il "sicuro al 100% niente scritture/injection". I Livelli 2–3
riducono gli abusi in lettura e il DoS.*

---

## Livello 2 — Hardening del validatore (difesa in profondità)

### BERLink — `BERLinkConnector.API/Configuration/SqlQueryValidator.cs`
1. **Correggere il bypass `query_to_xml` da strip dei literal**: applicare una denylist di
   *funzioni* pericolose sulla query grezza (prima/indipendentemente dallo strip dei literal),
   OPPURE rifiutare le query i cui literal contengono verbi SQL. Aggiungere denylist di funzioni
   (word-boundary, case-insensitive): `pg_read_file`, `pg_read_binary_file`, `pg_ls_dir`,
   `pg_stat_file`, `lo_import`, `lo_export`, `dblink`, `dblink_exec`, `set_config`, `pg_sleep`,
   `pg_terminate_backend`, `query_to_xml`, `query_to_xmlschema`, `pg_ls_*`.
2. **Portare lo strip più robusto di TIR** (`NoiseTokenRegex`): gestire gli apici escapati `''`,
   gli identificatori tra doppi apici, e aggiungere la consapevolezza del **dollar-quoting**
   (`$$…$$`, `$tag$…$tag$`) così le stringhe Postgres non possono nascondere contenuto allo scan.
3. Mantenere la blocklist di statement esistente (COPY/DO/CALL/… già presenti).

### TIR — `TIRConnector.API/Validation/SqlQueryValidator.cs`
1. Aggiungere denylist di funzioni di lettura/DoS rilevanti per SQL Server; assicurarsi che le
   famiglie `xp_`/`sp_` siano coperte — oggi ci sono solo `XP_CMDSHELL`/`SP_EXECUTESQL`;
   valutare regole per prefisso `\bXP_\w+`, `\bSP_\w+`.
2. Validare l'SQL dei template **al salvataggio** (`QueryTemplateService.CreateTemplateAsync` /
   `UpdateTemplateAsync`) — oggi validato solo in esecuzione. Allinearsi a BERLink che valida
   già in salvataggio.

### Entrambi
- **Bloccare sempre i `;`/stacked query, a prescindere da `EnableQueryValidation`.** Spostare il
  controllo `;` + start SELECT/WITH fuori dal flag kill-switch in `QueryService.ValidateQuery`,
  così disabilitare la validazione non può mai riabilitare le stacked query.
- Preferire una postura allowlist dove possibile (token iniziale deve essere SELECT/WITH — già forzato).
- Forzare un cap `TOP`/`LIMIT` lato SQL e limitare l'**export CSV illimitato**
  (`QueryTemplateService.ExecuteTemplateCsvAsync` — in TIR il path CSV non ha limite di righe).

---

## Livello 3 — Test (prova, non affermazione)

`BERLinkConnector.Tests` esiste ma è vuoto; TIR non ha progetto di test. Aggiungere unit test
sui due validatori con un **corpus di attacco** che deve essere sempre rifiutato:

- Stacked: `SELECT 1; DROP TABLE t`
- Offuscamento commenti/maiuscole: `SEL/**/ECT`, `DeLeTe`, injection con `--`
- Apici escapati/dollar-quote: `'it''s'`, `$$ … $$`
- Bypass Postgres: `SELECT query_to_xml('DELETE …',…)`, `dblink(…)`, `pg_read_file('/etc/passwd')`,
  `lo_export(…)`, `pg_sleep(10)`, `set_config(…)`
- CTE che modifica dati: `WITH x AS (DELETE FROM t RETURNING *) SELECT * FROM x`
- Letture legittime da far passare: colonne `delete_date`/`updated_at`, cast `col::int`, `IN (:list)`

**Prova d'integrazione del Livello 1**: eseguire una scrittura (`INSERT`/`query_to_xml('DELETE…')`)
contro il nuovo utente read-only e verificare che sia il **database** a rifiutarla — questa è la
prova reale del 100%.

---

## Livello 4 — Problemi di sicurezza adiacenti — entrambi i connector

Problemi speculari trovati in entrambi i `Program.cs` / middleware / appsettings:

- **Superuser + credenziali di default in `appsettings.json`** (`sa`, `postgres`, API key di
  default `default-key-change-me2`). Spostare su env/secret store; ruotare; mai committare
  segreti reali.
- **Swagger UI servita anche in produzione** (branch non-Development in `Program.cs`, ~riga 125).
  Limitare a Development o proteggere con API key.
- **API key loggate in chiaro all'avvio** (`Program.cs:158` TIR, `:149-150` BERLink). Loggare
  solo il conteggio, o un fingerprint mascherato/hash.
- **Swagger + `/admin` senza autenticazione** (skip list in `ApiKeyAuthenticationMiddleware`).
  Confermare se voluto; valutare auth su `/admin`.
- **Fallback CORS `*`** quando `AllowedOrigins` non è impostato — richiedere origin espliciti in
  produzione.
- **Messaggi d'errore DB grezzi restituiti al client** (`ErrorResponse.Message` nei controller) —
  aiutano il probing blind/error-based. Restituire messaggio generico; log del dettaglio lato
  server via `GlobalExceptionFilter`.

---

## File critici

| Ambito | TIRConnector | BERLinkConnector |
|---|---|---|
| Validatore | `TIRConnector.API/Validation/SqlQueryValidator.cs` | `BERLinkConnector.API/Configuration/SqlQueryValidator.cs` |
| Esec. ad-hoc | `Services/QueryService.cs` | `Services/QueryService.cs` |
| Esec. template | `Services/QueryTemplateService.cs` (aggiungere validazione al salvataggio, cap CSV) | `Services/QueryTemplateService.cs` |
| Conn string / segreti | `appsettings.json`, `appsettings.Production.json`, `docker-compose.yml` | `appsettings.json` |
| Avvio/CORS/Swagger/log-key | `Program.cs`, `Middleware/ApiKeyAuthenticationMiddleware.cs` | stessi path |
| Script SQL read-only (io li scrivo, tu li esegui) | `database/security/` (nuovo) | `database/security/` (nuovo) |
| Test | (nuovo progetto di test) | `BERLinkConnector.Tests/Services/` (oggi vuoto) |

## Verifica

1. `dotnet test` — suite corpus d'attacco verdi in entrambi i repo.
2. Puntare ogni app al nuovo utente DB **read-only** (creato da te); via API in esecuzione
   (`task tir` / porta 9090, header `X-API-Key`) fare POST di un payload di scrittura
   (`query_to_xml('DELETE…')`, `INSERT…`) e verificare che sia il DB a rifiutarlo (500/permesso),
   provando il Livello 1 indipendentemente dal validatore.
3. Verificare che SELECT legittime, template, `IN (:list)` e query paginate funzionino ancora.
4. Verificare che Swagger non sia raggiungibile in produzione e che i log d'avvio non stampino
   più le chiavi in chiaro.

## Fuori scope
- Ridisegnare il passthrough in un'API a sole query nominate/fisse (cambio di prodotto più
  ampio; l'utente DB read-only neutralizza già il rischio di scrittura).
- Creazione/provisioning dell'utente DB read-only: la fai tu, io consegno solo gli script.

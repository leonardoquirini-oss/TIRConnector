/* =====================================================================
   TIRConnector — Login SQL Server READ-ONLY per il connector
   =====================================================================
   Scopo: sostituire l'uso di 'sa' con un login che FISICAMENTE non puo'
   scrivere sul database target. Questa e' LA garanzia del 100% contro
   scritture/DML: nessun bug del validatore applicativo puo' aggirare i
   permessi del motore SQL Server.

   ESEGUIRE COME: amministratore (sysadmin) dell'istanza SQL Server.
   Lo esegue il DBA (tu). Il connector NON crea credenziali.

   PRIMA DI ESEGUIRE:
     - Sostituire <STRONG_PASSWORD> con una password robusta.
     - Verificare il nome del database target (qui: TirSQL).
   DOPO L'ESECUZIONE:
     - Aggiornare ConnectionStrings:DefaultConnection del connector con
       User Id=tir_connector_ro (vedi 00_README.md).
   ===================================================================== */

USE [master];
GO

/* 1) Login a livello di istanza -------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'tir_connector_ro')
BEGIN
    CREATE LOGIN [tir_connector_ro]
        WITH PASSWORD = N'<STRONG_PASSWORD>',
             CHECK_POLICY = ON,
             DEFAULT_DATABASE = [TirSQL];
END
GO

USE [TirSQL];
GO

/* 2) Utente di database mappato al login ----------------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'tir_connector_ro')
BEGIN
    CREATE USER [tir_connector_ro] FOR LOGIN [tir_connector_ro];
END
GO

/* 3) Sola lettura: db_datareader. NIENTE db_datawriter/ddladmin/owner - */
ALTER ROLE [db_datareader] ADD MEMBER [tir_connector_ro];
GO

/* 4) Nega esplicitamente scrittura ed esecuzione (difesa in profondita)
      Anche se un ruolo venisse aggiunto per errore, il DENY vince.      */
DENY INSERT, UPDATE, DELETE, EXECUTE, ALTER, CONTROL
    ON DATABASE::[TirSQL] TO [tir_connector_ro];
GO

/* 5) Nega EXECUTE su schema (blocca stored procedure / xp_ / sp_)      */
DENY EXECUTE ON SCHEMA::[dbo] TO [tir_connector_ro];
GO

/* 6) Blocca lettura di viste di sistema sensibili (opzionale) --------- */
-- DENY VIEW ANY DATABASE, VIEW SERVER STATE TO [tir_connector_ro];

/* VERIFICA:
   EXECUTE AS LOGIN = N'tir_connector_ro';
     SELECT TOP 1 * FROM <una_tabella>;         -- deve funzionare
     INSERT INTO <una_tabella> DEFAULT VALUES;  -- deve fallire: permesso negato
   REVERT;
*/

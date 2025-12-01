-- ============================================================================
-- SCHEMA COMPLETO PER REPOSITORY QUERY SQL VERSIONATO
-- ============================================================================


CREATE SEQUENCE IF NOT EXISTS s_query_templates START 1000;
CREATE SEQUENCE IF NOT EXISTS s_query_versions START 1000;

-- ============================================================================
-- TABELLA PRINCIPALE: query_templates
-- Contiene le query attive e i loro metadati
-- ============================================================================
CREATE TABLE query_templates (
    id_query_template integer PRIMARY KEY,
    name VARCHAR(200) NOT NULL,
    description TEXT,
    category VARCHAR(50),  -- es: 'reporting', 'analytics', 'operativo'
    
    -- Query SQL corrente
    query_sql TEXT NOT NULL,
    
    -- Metadati parametri (JSON per flessibilità)
    params JSONB DEFAULT '[]'::jsonb,
    -- Esempio: [{"name": "data_da", "type": "date", "required": true, "default": "NOW()"}]
    
    -- Configurazione output
    output_format VARCHAR(20) DEFAULT 'json',  -- json, csv, excel
    max_results INT DEFAULT 10000,
    timeout_seconds INT DEFAULT 30,
    
    -- Versioning
    version INT NOT NULL DEFAULT 1,
    
    -- Metadata
    creation_date TIMESTAMP DEFAULT NOW(),
    update_date TIMESTAMP ,
    
    -- Stato
    active BOOLEAN DEFAULT true,
    deprecated BOOLEAN DEFAULT false,
    deprecation_date TIMESTAMP
);



-- Tabella: query_templates
COMMENT ON TABLE query_templates IS 'Repository principale delle query SQL con metadati e configurazione';
COMMENT ON COLUMN query_templates.id_query_template IS 'Identificatore univoco della query';
COMMENT ON COLUMN query_templates.name IS 'Nome human-readable della query visualizzato nell''interfaccia utente';
COMMENT ON COLUMN query_templates.description IS 'Descrizione dettagliata dello scopo e funzionamento della query';
COMMENT ON COLUMN query_templates.category IS 'Categoria per raggruppare query simili (es: reporting, analytics, operativo, admin)';
COMMENT ON COLUMN query_templates.query_sql IS 'Query SQL effettiva con parametri named (:nome_parametro)';
COMMENT ON COLUMN query_templates.params IS 'Array JSON dei parametri: [{name, type, required, default, description}]';
COMMENT ON COLUMN query_templates.output_format IS 'Formato output desiderato: json, csv, excel';
COMMENT ON COLUMN query_templates.max_results IS 'Numero massimo di righe che la query può restituire (protezione)';
COMMENT ON COLUMN query_templates.timeout_seconds IS 'Timeout massimo di esecuzione in secondi';
COMMENT ON COLUMN query_templates.version IS 'Numero della versione attualmente attiva (auto-incrementato)';
COMMENT ON COLUMN query_templates.creation_date IS 'Timestamp di creazione iniziale della query';
COMMENT ON COLUMN query_templates.update_date IS 'Timestamp dell''ultima modifica';
COMMENT ON COLUMN query_templates.active IS 'Flag per disabilitare temporaneamente una query senza eliminarla';
COMMENT ON COLUMN query_templates.deprecated IS 'Flag per marcare query obsolete che verranno rimosse';
COMMENT ON COLUMN query_templates.deprecation_date IS 'Data pianificata per la rimozione della query deprecata';

-- ============================================================================
-- TABELLA STORICO: query_versions
-- Mantiene tutte le versioni precedenti di ogni query
-- ============================================================================
CREATE TABLE query_versions (
    id_query_version integer PRIMARY KEY ,
    id_query_template integer NOT NULL REFERENCES query_templates(id_query_template) ,
    version INT NOT NULL,
    
    -- Snapshot completo della query a questa versione
    query_sql TEXT NOT NULL,
    params JSONB,
    name VARCHAR(200),
    description TEXT,
    
    creation_date TIMESTAMP DEFAULT NOW(),
    
    -- Motivo del cambiamento
    change_reason TEXT,
    change_type VARCHAR(20),  -- 'minor', 'major', 'bugfix', 'rollback'
    
    -- Diffs (opzionale, per UI)
    sql_diff TEXT  -- diff testuale della query
);


-- ============================================================================
-- TRIGGER PER VERSIONAMENTO AUTOMATICO
-- ============================================================================

-- Funzione per creare automaticamente una versione quando la query cambia
CREATE OR REPLACE FUNCTION create_query_version()
RETURNS TRIGGER AS $$
BEGIN
    -- Solo se la query SQL è cambiata
    IF OLD.query_sql IS DISTINCT FROM NEW.query_sql THEN
        -- Incrementa versione
        NEW.version := OLD.version + 1;
        NEW.update_date := NOW();
        
        -- Salva versione precedente
        INSERT INTO query_versions (id_query_version,id_query_template,version, query_sql,params, name,description,creation_date) (
        VALUES (
            nextval('s_query_versions'), OLD.id_query_template, OLD.version, OLD.query_sql, 
            OLD.params, OLD.name, OLD.description,
            NEW.update_date,
        );
    END IF;
    
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trigger_query_version
    BEFORE UPDATE ON query_templates
    FOR EACH ROW
    WHEN (OLD.query_sql IS DISTINCT FROM NEW.query_sql)
    EXECUTE FUNCTION create_query_version();

-- ============================================================================
-- FUNZIONI UTILITY
-- ============================================================================

-- Funzione per rollback a una versione precedente
CREATE OR REPLACE FUNCTION rollback_query_version(
    p_query_id VARCHAR,
    p_versione INT,
    p_user VARCHAR
)
RETURNS BOOLEAN AS $$
DECLARE
    v_old_version RECORD;
BEGIN
    -- Recupera la vecchia versione
    SELECT * INTO v_old_version
    FROM query_versions
    WHERE query_id = p_query_id AND versione = p_versione;
    
    IF NOT FOUND THEN
        RAISE EXCEPTION 'Versione % non trovata per query %', p_versione, p_query_id;
    END IF;
    
    -- Aggiorna la query corrente
    UPDATE query_templates
    SET 
        query_sql = v_old_version.query_sql,
        parametri = v_old_version.parametri,
        updated_by = p_user,
        updated_at = NOW()
    WHERE id = p_query_id;
    
    -- Registra il rollback
    INSERT INTO query_versions (
        query_id, versione, query_sql, parametri,
        created_by, change_type, change_reason
    ) SELECT 
        query_id, (SELECT versione_corrente FROM query_templates WHERE id = p_query_id),
        query_sql, parametri,
        p_user, 'rollback', 'Rollback a versione ' || p_versione
    FROM query_templates WHERE id = p_query_id;
    
    RETURN TRUE;
END;
$$ LANGUAGE plpgsql;



-- ============================================================================
-- INDICI PER PERFORMANCE
-- ============================================================================

-- query_templates
CREATE INDEX idx_query_templates_category ON query_templates(category);
CREATE INDEX idx_query_templates_tags ON query_templates USING GIN(tags);
CREATE INDEX idx_query_templates_active ON query_templates(active) WHERE active = true;

-- query_versions
CREATE INDEX idx_query_versions_query_id ON query_versions(query_id);
CREATE INDEX idx_query_versions_created_at ON query_versions(created_at DESC);

-- query_executions
CREATE INDEX idx_query_executions_query_id ON query_executions(query_id);
CREATE INDEX idx_query_executions_executed_at ON query_executions(executed_at DESC);
CREATE INDEX idx_query_executions_executed_by ON query_executions(executed_by);
CREATE INDEX idx_query_executions_stato ON query_executions(stato);

-- query_approvals
CREATE INDEX idx_query_approvals_stato ON query_approvals(stato) WHERE stato = 'pending';

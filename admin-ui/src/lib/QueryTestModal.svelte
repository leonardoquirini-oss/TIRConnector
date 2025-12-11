<script lang="ts">
  import { createEventDispatcher } from 'svelte';
  import { executeQuery, type QueryResponse } from './api';

  export let query: string = '';

  const dispatch = createEventDispatcher<{ close: void }>();

  let parameters: string[] = [];
  let paramValues: Record<string, string> = {};
  let loading = false;
  let error = '';
  let result: QueryResponse | null = null;

  // Extract parameters from query on mount/change
  $: {
    const matches = query.match(/:(\w+)/g) || [];
    parameters = [...new Set(matches.map(m => m.substring(1)))];
    // Initialize param values
    parameters.forEach(p => {
      if (!(p in paramValues)) {
        paramValues[p] = '';
      }
    });
  }

  async function handleExecute() {
    loading = true;
    error = '';
    result = null;

    try {
      const params: Record<string, unknown> = {};
      parameters.forEach(p => {
        params[p] = paramValues[p] || null;
      });

      result = await executeQuery(query, parameters.length > 0 ? params : undefined);
    } catch (e) {
      error = e instanceof Error ? e.message : 'Errore nell\'esecuzione della query';
    } finally {
      loading = false;
    }
  }

  function handleClose() {
    dispatch('close');
  }

  function formatValue(value: unknown): string {
    if (value === null || value === undefined) return '-';
    if (typeof value === 'object') return JSON.stringify(value);
    return String(value);
  }
</script>

<div class="modal-overlay">
  <div class="modal test-modal">
    <div class="modal-header">
      <h2>Test Query</h2>
      <button class="close-btn" on:click={handleClose}>&times;</button>
    </div>

    <div class="modal-body">
      {#if parameters.length > 0}
        <div class="params-section">
          <h3>Parametri</h3>
          <div class="params-grid">
            {#each parameters as param}
              <div class="param-row">
                <label for="param-{param}">:{param}</label>
                <input
                  type="text"
                  id="param-{param}"
                  bind:value={paramValues[param]}
                  placeholder="Valore per :{param}"
                />
              </div>
            {/each}
          </div>
        </div>
      {/if}

      <button
        class="primary execute-btn"
        on:click={handleExecute}
        disabled={loading}
      >
        {loading ? 'Esecuzione...' : 'Esegui Query'}
      </button>

      {#if error}
        <div class="error">{error}</div>
      {/if}

      {#if result}
        <div class="results-section">
          <div class="results-stats">
            <span><strong>Righe:</strong> {result.rowCount}</span>
            <span><strong>Tempo:</strong> {result.executionTimeMs}ms</span>
          </div>

          {#if result.data.length > 0}
            <div class="results-table-container">
              <table class="results-table">
                <thead>
                  <tr>
                    {#each result.columns as col}
                      <th>{col.name}</th>
                    {/each}
                  </tr>
                </thead>
                <tbody>
                  {#each result.data.slice(0, 100) as row}
                    <tr>
                      {#each result.columns as col}
                        <td>{formatValue(row[col.name])}</td>
                      {/each}
                    </tr>
                  {/each}
                </tbody>
              </table>
            </div>
            {#if result.data.length > 100}
              <p class="truncated-note">Mostrate prime 100 righe di {result.rowCount}</p>
            {/if}
          {:else}
            <p class="no-results">Nessun risultato</p>
          {/if}
        </div>
      {/if}
    </div>

    <div class="modal-footer">
      <button class="secondary" on:click={handleClose}>Chiudi</button>
    </div>
  </div>
</div>

<style>
  .test-modal {
    width: 90%;
    max-width: 1200px;
    max-height: 90vh;
    display: flex;
    flex-direction: column;
  }

  .modal-body {
    flex: 1;
    overflow-y: auto;
    padding: 20px;
    display: flex;
    flex-direction: column;
    gap: 16px;
  }

  .close-btn {
    background: none;
    border: none;
    font-size: 1.5rem;
    color: #6b7280;
    cursor: pointer;
    padding: 4px 8px;
    line-height: 1;
    border-radius: 4px;
  }

  .close-btn:hover {
    background: #f3f4f6;
    color: #111827;
  }

  .params-section h3 {
    margin: 0 0 12px 0;
    font-size: 0.875rem;
    font-weight: 600;
    color: #374151;
    text-transform: uppercase;
  }

  .params-grid {
    display: flex;
    flex-direction: column;
    gap: 8px;
  }

  .param-row {
    display: flex;
    align-items: center;
    gap: 12px;
  }

  .param-row label {
    min-width: 120px;
    font-family: 'Consolas', 'Monaco', monospace;
    font-size: 14px;
    color: #6b7280;
  }

  .param-row input {
    flex: 1;
    padding: 8px 12px;
    border: 1px solid #d1d5db;
    border-radius: 6px;
    font-size: 14px;
  }

  .param-row input:focus {
    outline: none;
    border-color: #2563eb;
    box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.1);
  }

  .execute-btn {
    align-self: flex-start;
  }

  .results-section {
    display: flex;
    flex-direction: column;
    gap: 12px;
  }

  .results-stats {
    display: flex;
    gap: 24px;
    font-size: 14px;
    color: #374151;
  }

  .results-table-container {
    overflow-x: auto;
    border: 1px solid #e5e7eb;
    border-radius: 8px;
    max-height: 400px;
    overflow-y: auto;
  }

  .results-table {
    width: 100%;
    border-collapse: collapse;
    font-size: 13px;
  }

  .results-table th,
  .results-table td {
    padding: 8px 12px;
    text-align: left;
    border-bottom: 1px solid #e5e7eb;
    white-space: nowrap;
  }

  .results-table th {
    background: #f9fafb;
    font-weight: 600;
    color: #374151;
    position: sticky;
    top: 0;
  }

  .results-table tbody tr:hover {
    background: #f3f4f6;
  }

  .results-table tbody tr:last-child td {
    border-bottom: none;
  }

  .truncated-note {
    font-size: 12px;
    color: #6b7280;
    font-style: italic;
    margin: 0;
  }

  .no-results {
    color: #6b7280;
    font-style: italic;
    margin: 0;
  }
</style>

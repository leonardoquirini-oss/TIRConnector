<script lang="ts">
  import { createEventDispatcher } from 'svelte';
  import type { Template, TemplateDto } from './api';
  import { getTemplate, createTemplate, updateTemplate, deleteTemplate } from './api';
  import QueryTestModal from './QueryTestModal.svelte';
  import SqlEditor from './SqlEditor.svelte';

  export let template: Template | null = null;
  export let isNew = false;

  const dispatch = createEventDispatcher<{
    close: void;
    saved: void;
  }>();

  let loading = false;
  let saving = false;
  let error = '';
  let showTestModal = false;

  let form: TemplateDto = {
    name: '',
    description: null,
    category: null,
    querySql: '',
    outputFormat: 'json',
    maxResults: 10000,
    timeoutSeconds: 30,
    active: true,
  };

  // Load template data when editing
  $: if (template && !isNew) {
    loadTemplate(template.idQueryTemplate);
  } else if (isNew) {
    resetForm();
  }

  function resetForm() {
    form = {
      name: '',
      description: null,
      category: null,
      querySql: '',
      outputFormat: 'json',
      maxResults: 10000,
      timeoutSeconds: 30,
      active: true,
    };
  }

  async function loadTemplate(id: number) {
    loading = true;
    error = '';
    try {
      const data = await getTemplate(id);
      form = {
        name: data.name,
        description: data.description,
        category: data.category,
        querySql: data.querySql || '',
        outputFormat: data.outputFormat,
        maxResults: data.maxResults,
        timeoutSeconds: data.timeoutSeconds,
        active: data.active,
      };
    } catch (e) {
      error = e instanceof Error ? e.message : 'Errore nel caricamento';
    } finally {
      loading = false;
    }
  }

  async function handleSave() {
    if (!form.name.trim()) {
      error = 'Il nome è obbligatorio';
      return;
    }
    if (!form.querySql.trim()) {
      error = 'La query SQL è obbligatoria';
      return;
    }
    if (form.maxResults < 1) {
      error = 'Max Results deve essere maggiore di 0';
      return;
    }
    if (form.timeoutSeconds < 1) {
      error = 'Timeout deve essere maggiore di 0';
      return;
    }

    saving = true;
    error = '';

    try {
      if (isNew) {
        await createTemplate(form);
      } else if (template) {
        await updateTemplate(template.idQueryTemplate, form);
      }
      dispatch('saved');
    } catch (e) {
      error = e instanceof Error ? e.message : 'Errore nel salvataggio';
    } finally {
      saving = false;
    }
  }

  async function handleDelete() {
    if (!template || isNew) return;

    if (!confirm(`Sei sicuro di voler eliminare il template "${template.name}"?`)) {
      return;
    }

    saving = true;
    error = '';

    try {
      await deleteTemplate(template.idQueryTemplate);
      dispatch('saved');
    } catch (e) {
      error = e instanceof Error ? e.message : 'Errore nell\'eliminazione';
    } finally {
      saving = false;
    }
  }

  function handleClose() {
    dispatch('close');
  }

  function handleTestQuery() {
    if (!form.querySql.trim()) {
      error = 'Inserisci una query SQL prima di testarla';
      return;
    }
    showTestModal = true;
  }
</script>

<div class="details-panel">
  <div class="details-header">
    <h2>{isNew ? 'Nuovo Template' : 'Modifica Template'}</h2>
    <button class="close-btn" on:click={handleClose}>&times;</button>
  </div>

  <div class="details-body">
    {#if loading}
      <div class="loading">Caricamento...</div>
    {:else}
      {#if error}
        <div class="error">{error}</div>
      {/if}

      <div class="form-row">
        <div class="form-group">
          <label for="name">Nome *</label>
          <input
            type="text"
            id="name"
            bind:value={form.name}
            placeholder="Nome del template"
            required
          />
        </div>

        <div class="form-group">
          <label for="description">Descrizione</label>
          <input
            type="text"
            id="description"
            bind:value={form.description}
            placeholder="Descrizione del template"
          />
        </div>
      </div>

      <div class="form-row-4">
        <div class="form-group">
          <label for="category">Categoria</label>
          <input
            type="text"
            id="category"
            bind:value={form.category}
            placeholder="es: reporting"
          />
        </div>

        <div class="form-group">
          <label for="outputFormat">Formato</label>
          <select id="outputFormat" bind:value={form.outputFormat}>
            <option value="json">JSON</option>
            <option value="csv">CSV</option>
          </select>
        </div>

        <div class="form-group">
          <label for="maxResults">Max Results</label>
          <input
            type="number"
            id="maxResults"
            bind:value={form.maxResults}
            min="1"
          />
        </div>

        <div class="form-group">
          <label for="timeoutSeconds">Timeout (s)</label>
          <input
            type="number"
            id="timeoutSeconds"
            bind:value={form.timeoutSeconds}
            min="1"
          />
        </div>
      </div>

      <div class="form-group">
        <div class="checkbox-group">
          <input
            type="checkbox"
            id="active"
            bind:checked={form.active}
          />
          <label for="active">Attiva</label>
        </div>
      </div>

      <div class="form-group form-group-query">
        <label>Query SQL *</label>
        <SqlEditor bind:value={form.querySql} placeholder="SELECT * FROM ..." />
      </div>
    {/if}
  </div>

  <div class="details-footer">
    {#if !isNew && template}
      <button class="danger" on:click={handleDelete} disabled={saving}>
        Elimina
      </button>
    {/if}
    <div style="flex: 1;"></div>
    <button class="test-btn" on:click={handleTestQuery} disabled={saving || loading}>
      Test Query
    </button>
    <button class="secondary" on:click={handleClose} disabled={saving}>
      Chiudi
    </button>
    <button class="primary" on:click={handleSave} disabled={saving || loading}>
      {saving ? 'Salvataggio...' : 'Salva'}
    </button>
  </div>
</div>

{#if showTestModal}
  <QueryTestModal
    query={form.querySql}
    on:close={() => showTestModal = false}
  />
{/if}

<style>
  .details-panel {
    display: flex;
    flex-direction: column;
    height: 100%;
    background: white;
    border-radius: 8px;
    box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
  }

  .details-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 16px 20px;
    border-bottom: 1px solid #e5e7eb;
  }

  .details-header h2 {
    margin: 0;
    font-size: 1.25rem;
    font-weight: 600;
    color: #111827;
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

  .details-body {
    flex: 1;
    overflow-y: auto;
    padding: 20px;
    display: flex;
    flex-direction: column;
  }

  .form-group-query {
    flex: 1;
    display: flex;
    flex-direction: column;
    min-height: 200px;
  }

  .details-footer {
    display: flex;
    gap: 12px;
    padding: 16px 20px;
    border-top: 1px solid #e5e7eb;
    background: #f9fafb;
    border-radius: 0 0 8px 8px;
  }

  .loading {
    text-align: center;
    padding: 40px;
    color: #6b7280;
  }

  .test-btn {
    background-color: #059669;
    color: white;
  }

  .test-btn:hover {
    background-color: #047857;
  }

  .test-btn:disabled {
    background-color: #9ca3af;
    cursor: not-allowed;
  }
</style>

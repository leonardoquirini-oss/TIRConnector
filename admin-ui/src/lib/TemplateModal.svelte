<script lang="ts">
  import { createEventDispatcher } from 'svelte';
  import type { Template, TemplateDto } from './api';
  import { getTemplate, createTemplate, updateTemplate, deleteTemplate } from './api';

  export let template: Template | null = null;
  export let isNew = false;

  const dispatch = createEventDispatcher<{
    close: void;
    saved: void;
  }>();

  let loading = false;
  let saving = false;
  let error = '';

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

  function handleCancel() {
    dispatch('close');
  }

  function handleKeydown(e: KeyboardEvent) {
    if (e.key === 'Escape') {
      handleCancel();
    }
  }
</script>

<svelte:window on:keydown={handleKeydown} />

<div class="modal-overlay" on:click|self={handleCancel}>
  <div class="modal">
    <div class="modal-header">
      <h2>{isNew ? 'Nuovo Template' : 'Modifica Template'}</h2>
      <button class="secondary" on:click={handleCancel}>&times;</button>
    </div>

    <div class="modal-body">
      {#if loading}
        <div class="loading">Caricamento...</div>
      {:else}
        {#if error}
          <div class="error">{error}</div>
        {/if}

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
          <textarea
            id="description"
            bind:value={form.description}
            placeholder="Descrizione del template"
            rows="2"
          ></textarea>
        </div>

        <div class="form-row">
          <div class="form-group">
            <label for="category">Categoria</label>
            <input
              type="text"
              id="category"
              bind:value={form.category}
              placeholder="es: reporting, analytics"
            />
          </div>

          <div class="form-group">
            <label for="outputFormat">Formato Output</label>
            <select id="outputFormat" bind:value={form.outputFormat}>
              <option value="json">JSON</option>
              <option value="csv">CSV</option>
            </select>
          </div>
        </div>

        <div class="form-row">
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
            <label for="timeoutSeconds">Timeout (secondi)</label>
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

        <div class="form-group">
          <label for="querySql">Query SQL *</label>
          <textarea
            id="querySql"
            class="query"
            bind:value={form.querySql}
            placeholder="SELECT * FROM ..."
            required
          ></textarea>
        </div>
      {/if}
    </div>

    <div class="modal-footer">
      {#if !isNew && template}
        <button class="danger" on:click={handleDelete} disabled={saving}>
          Elimina
        </button>
      {/if}
      <div style="flex: 1;"></div>
      <button class="secondary" on:click={handleCancel} disabled={saving}>
        CANCEL
      </button>
      <button class="primary" on:click={handleSave} disabled={saving || loading}>
        {saving ? 'Salvataggio...' : 'SAVE'}
      </button>
    </div>
  </div>
</div>

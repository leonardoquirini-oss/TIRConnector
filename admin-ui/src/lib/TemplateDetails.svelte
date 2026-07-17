<script lang="ts">
  import { createEventDispatcher } from 'svelte';
  import type { Template, TemplateDto, QueryTagDetails, QueryTag } from './api';
  import { getTemplate, createTemplate, updateTemplate, deleteTemplate, deleteTag, getTemplateTags } from './api';
  import QueryTestModal from './QueryTestModal.svelte';
  import TagModal from './TagModal.svelte';
  import SqlEditor from './SqlEditor.svelte';

  export let template: Template | null = null;
  export let isNew = false;
  export let viewingTag: QueryTagDetails | null = null;

  $: isReadonly = viewingTag !== null;

  const dispatch = createEventDispatcher<{
    close: void;
    saved: Template | null;
    viewTag: QueryTag;
    diffTag: { tag: QueryTag; template: Template };
  }>();

  let loading = false;
  let saving = false;
  let error = '';
  let showTestModal = false;
  let showTagModal = false;

  let tags: QueryTag[] = [];
  let loadingTags = false;
  let showTags = false;

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

  // Load template or tag data
  $: if (viewingTag) {
    loadTagData(viewingTag);
  } else if (template && !isNew) {
    loadTemplate(template.idQueryTemplate);
  } else if (isNew) {
    resetForm();
  }

  function loadTagData(tag: QueryTagDetails) {
    form = {
      name: tag.name || '',
      description: tag.description,
      category: null,
      querySql: tag.querySql || '',
      outputFormat: 'json',
      maxResults: 10000,
      timeoutSeconds: 30,
      active: true,
    };
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
      let savedTemplate: Template | null = null;
      if (isNew) {
        savedTemplate = await createTemplate(form);
      } else if (template) {
        savedTemplate = await updateTemplate(template.idQueryTemplate, form);
      }
      dispatch('saved', savedTemplate);
    } catch (e) {
      error = e instanceof Error ? e.message : 'Errore nel salvataggio';
    } finally {
      saving = false;
    }
  }

  async function handleDelete() {
    if (viewingTag) {
      // Elimina tag
      if (!confirm(`Sei sicuro di voler eliminare questo tag?`)) {
        return;
      }

      saving = true;
      error = '';

      try {
        await deleteTag(viewingTag.idQueryQueryTag);
        dispatch('saved', null);
      } catch (e) {
        error = e instanceof Error ? e.message : 'Errore nell\'eliminazione del tag';
      } finally {
        saving = false;
      }
    } else {
      // Elimina template
      if (!template || isNew) return;

      if (!confirm(`Sei sicuro di voler eliminare il template "${template.name}"?`)) {
        return;
      }

      saving = true;
      error = '';

      try {
        await deleteTemplate(template.idQueryTemplate);
        dispatch('saved', null);
      } catch (e) {
        error = e instanceof Error ? e.message : 'Errore nell\'eliminazione';
      } finally {
        saving = false;
      }
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

  function handleTag() {
    if (!template || isNew) {
      error = 'Salva prima il template per poter creare un tag';
      return;
    }
    showTagModal = true;
  }

  $: if (template && !isNew && !viewingTag) {
    loadTags(template.idQueryTemplate);
  }

  async function loadTags(templateId: number) {
    loadingTags = true;
    try {
      tags = await getTemplateTags(templateId);
    } catch (e) {
      console.error('Error loading tags:', e);
      tags = [];
    } finally {
      loadingTags = false;
    }
  }

  function handleViewTag(tag: QueryTag) {
    dispatch('viewTag', tag);
  }

  function handleDiffTag(tag: QueryTag) {
    if (!template) return;
    dispatch('diffTag', { tag, template });
  }

  async function handleDeleteSingleTag(tag: QueryTag) {
    if (!confirm(`Sei sicuro di voler eliminare il tag v${tag.version}?`)) return;
    try {
      await deleteTag(tag.idQueryQueryTag);
      if (template) loadTags(template.idQueryTemplate);
    } catch (e) {
      error = e instanceof Error ? e.message : 'Errore nell\'eliminazione del tag';
    }
  }

  function formatDate(dateStr: string): string {
    const d = new Date(dateStr);
    const pad = (n: number) => n.toString().padStart(2, '0');
    return `${pad(d.getDate())}/${pad(d.getMonth() + 1)}/${d.getFullYear()} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
  }

  function handleTagSaved() {
    showTagModal = false;
    if (template) loadTags(template.idQueryTemplate);
  }
</script>

<div class="details-panel">
  <div class="details-header">
    <h2>{#if viewingTag}Tag: {viewingTag.changeReason || 'Senza messaggio'} - Versione: {viewingTag.version}{:else if isNew}Nuovo Template{:else}Template: {template?.name} - Versione: {template?.version}{/if}</h2>
    <button class="close-btn" on:click={handleClose}>&times;</button>
  </div>

  <div class="details-body">
    {#if loading}
      <div class="loading">Caricamento...</div>
    {:else}
      {#if error}
        <div class="error">{error}</div>
      {/if}

      <div class="form-row-header" class:readonly={isReadonly}>
        <div class="form-group">
          <label for="name">Nome *</label>
          <input
            type="text"
            id="name"
            bind:value={form.name}
            placeholder="Nome del template"
            required
            disabled={isReadonly}
          />
        </div>

        <div class="form-group">
          <label for="description">Descrizione</label>
          <input
            type="text"
            id="description"
            bind:value={form.description}
            placeholder="Descrizione del template"
            disabled={isReadonly}
          />
        </div>

        {#if !isReadonly}
          <div class="form-group">
            <label for="category">Categoria</label>
            <input
              type="text"
              id="category"
              bind:value={form.category}
              placeholder="es: reporting"
            />
          </div>

          <div class="checkbox-group-inline">
            <input
              type="checkbox"
              id="active"
              bind:checked={form.active}
            />
            <label for="active">Attiva</label>
          </div>
        {/if}
      </div>

      <div class="form-group form-group-query">
        <label>Query SQL *</label>
        <SqlEditor bind:value={form.querySql} placeholder="SELECT * FROM ..." readonly={isReadonly} />
      </div>

      {#if !isNew && !isReadonly && template}
        <div class="tag-history">
          <button class="tag-history-header" on:click={() => showTags = !showTags}>
            <span class="chevron" class:open={showTags}>&#9654;</span>
            <span>Cronologia Tag</span>
            <span class="tag-count-badge">{tags.length}</span>
          </button>

          {#if showTags}
            {#if loadingTags}
              <div class="tag-empty">Caricamento tag...</div>
            {:else if tags.length === 0}
              <div class="tag-empty">Nessun tag creato</div>
            {:else}
              <div class="tag-list">
                {#each tags as tag (tag.idQueryQueryTag)}
                  <div class="tag-item">
                    <span class="tag-version-badge">v{tag.version}</span>
                    {#if tag.changeType}
                      <span class="tag-type-badge {tag.changeType}">{tag.changeType}</span>
                    {/if}
                    <span class="tag-reason" title={tag.changeReason || ''}>{tag.changeReason || '-'}</span>
                    <span class="tag-date">{formatDate(tag.creationDate)}</span>
                    <div class="tag-actions">
                      <button class="tag-action-btn" title="Visualizza SQL" on:click={() => handleViewTag(tag)}>SQL</button>
                      <button class="tag-action-btn" title="Confronta con versione attuale" on:click={() => handleDiffTag(tag)}>Diff</button>
                      <button class="tag-action-btn danger" title="Elimina tag" on:click={() => handleDeleteSingleTag(tag)}>&#128465;</button>
                    </div>
                  </div>
                {/each}
              </div>
            {/if}
          {/if}
        </div>
      {/if}
    {/if}
  </div>

  <div class="details-footer">
    {#if viewingTag}
      <button class="danger" on:click={handleDelete} disabled={saving}>
        Elimina Tag
      </button>
    {:else if !isNew && template}
      <button class="danger" on:click={handleDelete} disabled={saving}>
        Elimina
      </button>
    {/if}
    <div style="flex: 1;"></div>
    {#if !isReadonly && !isNew && template}
      <button class="tag-btn" on:click={handleTag} disabled={saving || loading}>
        Tag
      </button>
    {/if}
    <button class="test-btn" on:click={handleTestQuery} disabled={saving || loading}>
      Test Query
    </button>
    <button class="secondary" on:click={handleClose} disabled={saving}>
      Chiudi
    </button>
    {#if !isReadonly}
      <button class="primary" on:click={handleSave} disabled={saving || loading}>
        {saving ? 'Salvataggio...' : 'Salva'}
      </button>
    {/if}
  </div>
</div>

{#if showTestModal}
  <QueryTestModal
    query={form.querySql}
    on:close={() => showTestModal = false}
  />
{/if}

{#if showTagModal && template}
  <TagModal
    templateId={template.idQueryTemplate}
    on:close={() => showTagModal = false}
    on:saved={handleTagSaved}
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
    padding: 16px 20px;
    display: flex;
    flex-direction: column;
  }

  .details-body :global(.form-group) {
    margin-bottom: 8px;
  }

  .form-row-header {
    display: grid;
    grid-template-columns: 1fr 1fr 1fr auto;
    gap: 12px;
    margin-bottom: 4px;
  }

  .form-row-header.readonly {
    grid-template-columns: 1fr 1fr;
  }

  .checkbox-group-inline {
    display: flex;
    align-items: center;
    gap: 8px;
    padding-top: 22px;
  }

  .checkbox-group-inline input[type="checkbox"] {
    width: auto;
  }

  .form-group-query {
    flex: 1;
    display: flex;
    flex-direction: column;
    min-height: 250px;
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

  .tag-btn {
    background-color: #7c3aed;
    color: white;
  }

  .tag-btn:hover {
    background-color: #6d28d9;
  }

  .tag-btn:disabled {
    background-color: #9ca3af;
    cursor: not-allowed;
  }

  .tag-history {
    border-top: 1px solid #e5e7eb;
    margin-top: 16px;
    padding-top: 12px;
  }

  .tag-history-header {
    display: flex;
    align-items: center;
    gap: 8px;
    background: none;
    border: none;
    cursor: pointer;
    font-size: 0.95rem;
    font-weight: 600;
    color: #374151;
    padding: 4px 0;
    width: 100%;
    text-align: left;
  }

  .tag-history-header:hover {
    color: #111827;
  }

  .chevron {
    font-size: 0.7rem;
    transition: transform 0.2s;
    display: inline-block;
  }

  .chevron.open {
    transform: rotate(90deg);
  }

  .tag-count-badge {
    background: #e5e7eb;
    color: #374151;
    font-size: 0.75rem;
    font-weight: 600;
    padding: 1px 7px;
    border-radius: 10px;
  }

  .tag-list {
    max-height: 200px;
    overflow-y: auto;
    margin-top: 8px;
  }

  .tag-item {
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 6px 4px;
    border-radius: 4px;
    font-size: 0.85rem;
  }

  .tag-item:hover {
    background: #f9fafb;
  }

  .tag-version-badge {
    background: #374151;
    color: white;
    font-size: 0.75rem;
    font-weight: 600;
    padding: 1px 6px;
    border-radius: 4px;
    white-space: nowrap;
  }

  .tag-type-badge {
    font-size: 0.7rem;
    font-weight: 600;
    padding: 1px 6px;
    border-radius: 4px;
    white-space: nowrap;
    text-transform: uppercase;
  }

  .tag-type-badge.minor {
    background: #dbeafe;
    color: #1d4ed8;
  }

  .tag-type-badge.major {
    background: #ffedd5;
    color: #c2410c;
  }

  .tag-type-badge.bugfix {
    background: #dcfce7;
    color: #15803d;
  }

  .tag-type-badge.rollback {
    background: #fee2e2;
    color: #b91c1c;
  }

  .tag-reason {
    flex: 1;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    color: #4b5563;
    min-width: 0;
  }

  .tag-date {
    color: #9ca3af;
    font-size: 0.78rem;
    white-space: nowrap;
  }

  .tag-actions {
    display: flex;
    gap: 4px;
    flex-shrink: 0;
  }

  .tag-action-btn {
    background: #f3f4f6;
    border: 1px solid #e5e7eb;
    color: #374151;
    font-size: 0.75rem;
    padding: 2px 8px;
    border-radius: 4px;
    cursor: pointer;
    line-height: 1.4;
  }

  .tag-action-btn:hover {
    background: #e5e7eb;
  }

  .tag-action-btn.danger {
    color: #b91c1c;
  }

  .tag-action-btn.danger:hover {
    background: #fee2e2;
  }

  .tag-empty {
    text-align: center;
    color: #9ca3af;
    padding: 16px 0;
    font-size: 0.85rem;
  }
</style>

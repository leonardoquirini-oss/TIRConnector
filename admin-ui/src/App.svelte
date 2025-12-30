<script lang="ts">
  import { onMount } from 'svelte';
  import TemplateTable from './lib/TemplateTable.svelte';
  import TemplateDetails from './lib/TemplateDetails.svelte';
  import DiffModal from './lib/DiffModal.svelte';
  import type { Template, QueryTag, QueryTagDetails } from './lib/api';
  import { getTemplates, getTemplate, getTag } from './lib/api';

  const appName = window.APP_CONFIG?.appName || 'TIR';

  let templates: Template[] = [];
  let loading = true;
  let error = '';

  let selectedTemplate: Template | null = null;
  let isNewTemplate = false;
  let viewingTag: QueryTagDetails | null = null;

  // Diff modal state
  let showDiffModal = false;
  let diffData: { currentSql: string; tagSql: string; templateName: string; tagVersion: number } | null = null;

  onMount(() => {
    loadTemplates();
  });

  async function loadTemplates() {
    loading = true;
    error = '';
    try {
      templates = await getTemplates();
    } catch (e) {
      error = e instanceof Error ? e.message : 'Errore nel caricamento';
    } finally {
      loading = false;
    }
  }

  function handleNewTemplate() {
    selectedTemplate = null;
    isNewTemplate = true;
    viewingTag = null;
  }

  function handleSelectTemplate(event: CustomEvent<Template>) {
    selectedTemplate = event.detail;
    isNewTemplate = false;
    viewingTag = null;
  }

  async function handleViewTag(event: CustomEvent<QueryTag>) {
    try {
      viewingTag = await getTag(event.detail.idQueryQueryTag);
      selectedTemplate = null;
      isNewTemplate = false;
    } catch (e) {
      console.error('Error loading tag:', e);
    }
  }

  async function handleDiffTag(event: CustomEvent<{ tag: QueryTag; template: Template }>) {
    const { tag, template } = event.detail;
    try {
      const [templateDetails, tagDetails] = await Promise.all([
        getTemplate(template.idQueryTemplate),
        getTag(tag.idQueryQueryTag)
      ]);
      diffData = {
        currentSql: templateDetails.querySql || '',
        tagSql: tagDetails.querySql,
        templateName: template.name,
        tagVersion: tag.version
      };
      showDiffModal = true;
    } catch (e) {
      console.error('Error loading diff data:', e);
    }
  }

  function handleClose() {
    selectedTemplate = null;
    isNewTemplate = false;
    viewingTag = null;
  }

  function handleSaved(event: CustomEvent<Template | null>) {
    if (event.detail) {
      selectedTemplate = event.detail;
      isNewTemplate = false;
    } else {
      // Template o tag eliminato: chiudi il pannello
      selectedTemplate = null;
      isNewTemplate = false;
      viewingTag = null;
    }
    loadTemplates();
  }

  $: selectedId = selectedTemplate?.idQueryTemplate ?? null;
  $: showDetails = selectedTemplate !== null || isNewTemplate || viewingTag !== null;
</script>

<header>
  <h1>{appName} Query Templates Admin</h1>
  <button class="primary" on:click={handleNewTemplate}>
    + Nuovo Template
  </button>
</header>

<div class="main-container">
  <div class="left-panel">
    {#if error}
      <div class="error">{error}</div>
    {/if}

    <TemplateTable
      {templates}
      {loading}
      {selectedId}
      on:select={handleSelectTemplate}
      on:viewTag={handleViewTag}
      on:diffTag={handleDiffTag}
    />
  </div>

  <div class="right-panel">
    {#if showDetails}
      <TemplateDetails
        template={selectedTemplate}
        isNew={isNewTemplate}
        {viewingTag}
        on:close={handleClose}
        on:saved={handleSaved}
      />
    {:else}
      <div class="right-panel-empty">
        <svg width="64" height="64" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
          <path d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
        </svg>
        <p>Seleziona un template dalla lista</p>
        <small>oppure crea un nuovo template</small>
      </div>
    {/if}
  </div>
</div>

{#if showDiffModal && diffData}
  <DiffModal
    currentSql={diffData.currentSql}
    tagSql={diffData.tagSql}
    templateName={diffData.templateName}
    tagVersion={diffData.tagVersion}
    on:close={() => showDiffModal = false}
  />
{/if}

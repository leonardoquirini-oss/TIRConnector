<script lang="ts">
  import { onMount } from 'svelte';
  import TemplateTable from './lib/TemplateTable.svelte';
  import TemplateModal from './lib/TemplateModal.svelte';
  import { getTemplates, type Template } from './lib/api';

  let templates: Template[] = [];
  let loading = true;
  let error = '';

  let showModal = false;
  let selectedTemplate: Template | null = null;
  let isNewTemplate = false;

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
    showModal = true;
  }

  function handleSelectTemplate(event: CustomEvent<Template>) {
    selectedTemplate = event.detail;
    isNewTemplate = false;
    showModal = true;
  }

  function handleModalClose() {
    showModal = false;
    selectedTemplate = null;
  }

  function handleModalSaved() {
    showModal = false;
    selectedTemplate = null;
    loadTemplates();
  }
</script>

<header>
  <h1>TIR Query Templates Admin</h1>
  <button class="primary" on:click={handleNewTemplate}>
    + Nuovo Template
  </button>
</header>

<div class="container">
  {#if error}
    <div class="error">{error}</div>
  {/if}

  <TemplateTable
    {templates}
    {loading}
    on:select={handleSelectTemplate}
  />
</div>

{#if showModal}
  <TemplateModal
    template={selectedTemplate}
    isNew={isNewTemplate}
    on:close={handleModalClose}
    on:saved={handleModalSaved}
  />
{/if}

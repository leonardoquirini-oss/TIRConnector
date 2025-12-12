<script lang="ts">
  import type { Template, QueryTag } from './api';
  import { getTemplateTags } from './api';
  import { createEventDispatcher } from 'svelte';

  export let templates: Template[] = [];
  export let loading = false;
  export let selectedId: number | null = null;

  const dispatch = createEventDispatcher<{
    select: Template;
    viewTag: QueryTag;
    diffTag: { tag: QueryTag; template: Template };
  }>();

  let expandedTemplateId: number | null = null;
  let loadingTags = false;
  let tags: QueryTag[] = [];

  function handleRowClick(template: Template) {
    dispatch('select', template);
  }

  function truncate(text: string | null, maxLength: number): string {
    if (!text) return '-';
    return text.length > maxLength ? text.substring(0, maxLength) + '...' : text;
  }

  async function handleTagClick(e: MouseEvent, template: Template) {
    e.stopPropagation();

    if (expandedTemplateId === template.idQueryTemplate) {
      // Chiudi accordion
      expandedTemplateId = null;
      tags = [];
    } else {
      // Apri accordion e carica tags
      expandedTemplateId = template.idQueryTemplate;
      loadingTags = true;
      try {
        tags = await getTemplateTags(template.idQueryTemplate);
      } catch (err) {
        console.error('Error loading tags:', err);
        tags = [];
      } finally {
        loadingTags = false;
      }
    }
  }

  function handleDiffClick(e: MouseEvent, tag: QueryTag, template: Template) {
    e.stopPropagation();
    dispatch('diffTag', { tag, template });
  }

  function handleViewTag(e: MouseEvent, tag: QueryTag) {
    e.stopPropagation();
    dispatch('viewTag', tag);
  }

  function formatDate(dateString: string): string {
    return new Date(dateString).toLocaleDateString('it-IT', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  }

  function getChangeTypeBadgeClass(changeType: string | null): string {
    switch (changeType) {
      case 'major': return 'badge-major';
      case 'minor': return 'badge-minor';
      case 'bugfix': return 'badge-bugfix';
      case 'rollback': return 'badge-rollback';
      default: return '';
    }
  }
</script>

{#if loading}
  <div class="loading">Caricamento...</div>
{:else if templates.length === 0}
  <div class="empty-state">
    <p>Nessun template trovato.</p>
    <p>Clicca "Nuovo Template" per crearne uno.</p>
  </div>
{:else}
  <table>
    <thead>
      <tr>
        <th>Nome</th>
        <th>Descrizione</th>
        <th>Categoria</th>
        <th>Versione</th>
        <th>Attiva</th>
        <th>Tag</th>
      </tr>
    </thead>
    <tbody>
      {#each templates as template (template.idQueryTemplate)}
        <tr
          class:selected={template.idQueryTemplate === selectedId}
          on:click={() => handleRowClick(template)}
        >
          <td><strong>{template.name}</strong></td>
          <td>{truncate(template.description, 50)}</td>
          <td>{template.category || '-'}</td>
          <td>v{template.version}</td>
          <td>
            <span class="badge" class:active={template.active} class:inactive={!template.active}>
              {template.active ? 'Attiva' : 'Inattiva'}
            </span>
          </td>
          <td>
            {#if template.tagCount > 0}
              <button
                class="tag-btn"
                class:expanded={expandedTemplateId === template.idQueryTemplate}
                on:click={(e) => handleTagClick(e, template)}
              >
                <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                  <path d="M20.59 13.41l-7.17 7.17a2 2 0 0 1-2.83 0L2 12V2h10l8.59 8.59a2 2 0 0 1 0 2.82z"></path>
                  <line x1="7" y1="7" x2="7.01" y2="7"></line>
                </svg>
                {template.tagCount}
              </button>
            {:else}
              <span class="no-tags">-</span>
            {/if}
          </td>
        </tr>
        {#if expandedTemplateId === template.idQueryTemplate}
          <tr class="accordion-row">
            <td colspan="6">
              <div class="accordion-content">
                {#if loadingTags}
                  <div class="loading-tags">Caricamento tag...</div>
                {:else if tags.length === 0}
                  <div class="no-tags-message">Nessun tag trovato</div>
                {:else}
                  <table class="tags-table">
                    <thead>
                      <tr>
                        <th>Versione</th>
                        <th>Tag Message</th>
                        <th>Tipo</th>
                        <th>Data</th>
                        <th>Azioni</th>
                      </tr>
                    </thead>
                    <tbody>
                      {#each tags as tag (tag.idQueryQueryTag)}
                        <tr>
                          <td>v{tag.version}</td>
                          <td>{tag.changeReason || '-'}</td>
                          <td>
                            {#if tag.changeType}
                              <span class="badge-type {getChangeTypeBadgeClass(tag.changeType)}">
                                {tag.changeType}
                              </span>
                            {:else}
                              -
                            {/if}
                          </td>
                          <td>{formatDate(tag.creationDate)}</td>
                          <td class="actions-cell">
                            <button
                              class="view-btn"
                              on:click={(e) => handleViewTag(e, tag)}
                              title="Visualizza tag"
                            >
                              <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                <circle cx="11" cy="11" r="8"></circle>
                                <path d="m21 21-4.35-4.35"></path>
                              </svg>
                            </button>
                            <button
                              class="diff-btn"
                              on:click={(e) => handleDiffClick(e, tag, template)}
                              title="Confronta versioni"
                            >
                              <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                <path d="M12 3v18"></path>
                                <rect x="3" y="6" width="6" height="12" rx="1"></rect>
                                <rect x="15" y="6" width="6" height="12" rx="1"></rect>
                              </svg>
                            </button>
                          </td>
                        </tr>
                      {/each}
                    </tbody>
                  </table>
                {/if}
              </div>
            </td>
          </tr>
        {/if}
      {/each}
    </tbody>
  </table>
{/if}

<style>
  .tag-btn {
    display: inline-flex;
    align-items: center;
    gap: 4px;
    padding: 4px 8px;
    background-color: #7c3aed;
    color: white;
    border: none;
    border-radius: 4px;
    font-size: 12px;
    font-weight: 500;
    cursor: pointer;
    transition: background-color 0.2s;
  }

  .tag-btn:hover {
    background-color: #6d28d9;
  }

  .tag-btn.expanded {
    background-color: #5b21b6;
  }

  .tag-btn svg {
    flex-shrink: 0;
  }

  .no-tags {
    color: var(--gray-500);
  }

  .accordion-row {
    background-color: #f8fafc !important;
  }

  .accordion-row:hover {
    background-color: #f8fafc !important;
    cursor: default;
  }

  .accordion-row td {
    padding: 0 !important;
  }

  .accordion-content {
    padding: 16px;
    border-top: 2px solid #7c3aed;
  }

  .loading-tags,
  .no-tags-message {
    text-align: center;
    padding: 20px;
    color: var(--gray-500);
    font-style: italic;
  }

  .tags-table {
    width: 100%;
    background: white;
    border-collapse: collapse;
    border-radius: 6px;
    overflow: hidden;
    box-shadow: 0 1px 2px rgba(0, 0, 0, 0.05);
    font-size: 13px;
  }

  .tags-table th,
  .tags-table td {
    padding: 10px 12px;
    text-align: left;
    border-bottom: 1px solid var(--gray-200);
  }

  .tags-table th {
    background-color: #f1f5f9;
    font-weight: 600;
    color: var(--gray-700);
    font-size: 11px;
    text-transform: uppercase;
  }

  .tags-table tbody tr:hover {
    background-color: #f8fafc;
    cursor: default;
  }

  .tags-table tbody tr:last-child td {
    border-bottom: none;
  }

  .badge-type {
    display: inline-block;
    padding: 2px 6px;
    border-radius: 4px;
    font-size: 11px;
    font-weight: 500;
    text-transform: uppercase;
  }

  .badge-major {
    background-color: #fee2e2;
    color: #dc2626;
  }

  .badge-minor {
    background-color: #dbeafe;
    color: #2563eb;
  }

  .badge-bugfix {
    background-color: #dcfce7;
    color: #16a34a;
  }

  .badge-rollback {
    background-color: #fef3c7;
    color: #d97706;
  }

  .actions-cell {
    display: flex;
    gap: 6px;
  }

  .view-btn,
  .diff-btn {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    padding: 6px;
    background-color: var(--gray-200);
    color: var(--gray-700);
    border: none;
    border-radius: 4px;
    cursor: pointer;
    transition: background-color 0.2s;
  }

  .view-btn:hover {
    background-color: #dbeafe;
    color: #2563eb;
  }

  .diff-btn:hover {
    background-color: var(--gray-300);
  }

  .view-btn svg,
  .diff-btn svg {
    flex-shrink: 0;
  }
</style>

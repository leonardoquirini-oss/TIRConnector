<script lang="ts">
  import type { Template } from './api';
  import { createEventDispatcher } from 'svelte';

  export let templates: Template[] = [];
  export let loading = false;
  export let selectedId: number | null = null;

  const dispatch = createEventDispatcher<{
    select: Template;
  }>();

  let searchQuery = '';
  let expandedCategories: Set<string> = new Set();
  let initialized = false;

  function groupByCategory(items: Template[]): Map<string, Template[]> {
    const groups = new Map<string, Template[]>();
    const uncategorized: Template[] = [];

    for (const t of items) {
      const cat = t.category?.trim();
      if (!cat) {
        uncategorized.push(t);
      } else {
        if (!groups.has(cat)) groups.set(cat, []);
        groups.get(cat)!.push(t);
      }
    }

    const sorted = new Map(
      [...groups.entries()].sort(([a], [b]) => a.localeCompare(b, 'it'))
    );

    if (uncategorized.length > 0) {
      sorted.set('__uncategorized__', uncategorized);
    }

    return sorted;
  }

  $: filteredTemplates = searchQuery.trim()
    ? templates.filter(t => {
        const q = searchQuery.toLowerCase();
        return t.name.toLowerCase().includes(q) ||
               (t.description && t.description.toLowerCase().includes(q));
      })
    : templates;

  $: grouped = groupByCategory(filteredTemplates);

  // Initialize expanded categories when templates first load
  $: if (templates.length > 0 && !initialized) {
    expandedCategories = new Set(groupByCategory(templates).keys());
    initialized = true;
  }

  // When searching, expand all categories
  $: if (searchQuery.trim()) {
    expandedCategories = new Set(grouped.keys());
  }

  function toggleCategory(cat: string) {
    if (expandedCategories.has(cat)) {
      expandedCategories.delete(cat);
    } else {
      expandedCategories.add(cat);
    }
    expandedCategories = expandedCategories; // trigger reactivity
  }

  function handleSelect(template: Template) {
    dispatch('select', template);
  }

  function categoryLabel(key: string): string {
    return key === '__uncategorized__' ? 'Senza categoria' : key;
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
  <div class="template-tree">
    <div class="search-bar">
      <svg class="search-icon" xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
        <circle cx="11" cy="11" r="8"></circle>
        <path d="m21 21-4.35-4.35"></path>
      </svg>
      <input
        type="text"
        placeholder="Cerca template..."
        bind:value={searchQuery}
      />
      {#if searchQuery}
        <button class="search-clear" on:click={() => searchQuery = ''}>
          <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <line x1="18" y1="6" x2="6" y2="18"></line>
            <line x1="6" y1="6" x2="18" y2="18"></line>
          </svg>
        </button>
      {/if}
    </div>

    {#if filteredTemplates.length === 0}
      <div class="no-results">Nessun risultato per "{searchQuery}"</div>
    {:else}
      {#each [...grouped.entries()] as [category, items] (category)}
        <div class="category-group">
          <button
            class="category-header"
            class:uncategorized={category === '__uncategorized__'}
            on:click={() => toggleCategory(category)}
          >
            <span class="chevron" class:expanded={expandedCategories.has(category)}>
              <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
                <polyline points="9 18 15 12 9 6"></polyline>
              </svg>
            </span>
            <span class="category-name">{categoryLabel(category)}</span>
            <span class="category-count">({items.length})</span>
          </button>

          {#if expandedCategories.has(category)}
            <div class="category-items">
              {#each items as template (template.idQueryTemplate)}
                <button
                  class="template-item"
                  class:selected={template.idQueryTemplate === selectedId}
                  on:click={() => handleSelect(template)}
                >
                  <span class="template-name" title={template.name}>{template.name}</span>
                  <span class="version-badge">v{template.version}</span>
                  <span
                    class="status-dot"
                    class:active={template.active}
                    title={template.active ? 'Attiva' : 'Inattiva'}
                  ></span>
                </button>
              {/each}
            </div>
          {/if}
        </div>
      {/each}
    {/if}
  </div>
{/if}

<style>
  .template-tree {
    display: flex;
    flex-direction: column;
    gap: 2px;
  }

  .search-bar {
    position: relative;
    margin-bottom: 12px;
  }

  .search-bar input {
    width: 100%;
    padding: 8px 12px 8px 36px;
    border: 1px solid var(--gray-300);
    border-radius: 6px;
    font-size: 13px;
    background: white;
    transition: border-color 0.2s;
  }

  .search-bar input:focus {
    outline: none;
    border-color: var(--primary);
    box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.1);
  }

  .search-bar input::placeholder {
    color: var(--gray-500);
  }

  .search-icon {
    position: absolute;
    left: 10px;
    top: 50%;
    transform: translateY(-50%);
    color: var(--gray-500);
    pointer-events: none;
  }

  .search-clear {
    position: absolute;
    right: 6px;
    top: 50%;
    transform: translateY(-50%);
    display: flex;
    align-items: center;
    justify-content: center;
    width: 22px;
    height: 22px;
    padding: 0;
    border: none;
    border-radius: 50%;
    background: var(--gray-200);
    color: var(--gray-700);
    cursor: pointer;
  }

  .search-clear:hover {
    background: var(--gray-300);
  }

  .no-results {
    text-align: center;
    padding: 24px 12px;
    color: var(--gray-500);
    font-size: 13px;
    font-style: italic;
  }

  .category-header {
    display: flex;
    align-items: center;
    gap: 6px;
    width: 100%;
    padding: 8px 10px;
    border: none;
    border-radius: 6px;
    background: transparent;
    font-size: 13px;
    font-weight: 600;
    color: var(--gray-700);
    cursor: pointer;
    transition: background-color 0.15s;
    text-align: left;
  }

  .category-header:hover {
    background-color: var(--gray-200);
  }

  .category-header.uncategorized {
    color: var(--gray-500);
    font-style: italic;
    margin-top: 4px;
    border-top: 1px solid var(--gray-200);
    border-radius: 0 0 6px 6px;
    padding-top: 10px;
  }

  .chevron {
    display: flex;
    align-items: center;
    transition: transform 0.15s;
    flex-shrink: 0;
  }

  .chevron.expanded {
    transform: rotate(90deg);
  }

  .category-name {
    flex: 1;
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .category-count {
    color: var(--gray-500);
    font-weight: 400;
    font-size: 12px;
    flex-shrink: 0;
  }

  .category-items {
    display: flex;
    flex-direction: column;
    gap: 1px;
    padding-left: 10px;
    margin-bottom: 4px;
  }

  .template-item {
    display: flex;
    align-items: center;
    gap: 8px;
    width: 100%;
    padding: 6px 10px;
    border: none;
    border-left: 2px solid transparent;
    border-radius: 0 4px 4px 0;
    background: transparent;
    font-size: 13px;
    color: var(--gray-900);
    cursor: pointer;
    transition: background-color 0.15s, border-color 0.15s;
    text-align: left;
  }

  .template-item:hover {
    background-color: var(--gray-200);
  }

  .template-item.selected {
    background-color: #dbeafe;
    border-left-color: #2563eb;
  }

  .template-item.selected:hover {
    background-color: #bfdbfe;
  }

  .template-name {
    flex: 1;
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .version-badge {
    flex-shrink: 0;
    padding: 1px 6px;
    border-radius: 4px;
    background-color: var(--gray-200);
    color: var(--gray-500);
    font-size: 11px;
    font-weight: 500;
    font-family: 'Consolas', 'Monaco', monospace;
  }

  .status-dot {
    flex-shrink: 0;
    width: 8px;
    height: 8px;
    border-radius: 50%;
    background-color: var(--gray-300);
  }

  .status-dot.active {
    background-color: var(--success);
  }
</style>

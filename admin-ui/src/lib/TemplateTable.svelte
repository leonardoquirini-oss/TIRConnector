<script lang="ts">
  import type { Template } from './api';
  import { createEventDispatcher } from 'svelte';
  import { slide } from 'svelte/transition';

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

  $: allExpanded = grouped.size > 0 && expandedCategories.size === grouped.size;

  function toggleAll() {
    if (allExpanded) {
      expandedCategories = new Set();
    } else {
      expandedCategories = new Set(grouped.keys());
    }
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

    <button class="toggle-all" on:click={toggleAll}>
      {allExpanded ? 'COMPRIMI TUTTO' : 'ESPANDI TUTTO'}
    </button>

    {#if filteredTemplates.length === 0}
      <div class="no-results">Nessun risultato per "{searchQuery}"</div>
    {:else}
      {#each [...grouped.entries()] as [category, items] (category)}
        <div class="category-group">
          <button
            class="category-header"
            class:uncategorized={category === '__uncategorized__'}
            class:expanded={expandedCategories.has(category)}
            on:click={() => toggleCategory(category)}
          >
            <span class="chevron" class:expanded={expandedCategories.has(category)}>
              <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
                <polyline points="9 18 15 12 9 6"></polyline>
              </svg>
            </span>
            {#if category === '__uncategorized__'}
              <svg class="category-icon" xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <polyline points="22 12 16 12 14 15 10 15 8 12 2 12"></polyline>
                <path d="M5.45 5.11 2 12v6a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-6l-3.45-6.89A2 2 0 0 0 16.76 4H7.24a2 2 0 0 0-1.79 1.11z"></path>
              </svg>
            {:else if expandedCategories.has(category)}
              <svg class="category-icon" xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="m6 14 1.5-2.9A2 2 0 0 1 9.24 10H20a2 2 0 0 1 1.94 2.5l-1.54 6a2 2 0 0 1-1.95 1.5H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h3.9a2 2 0 0 1 1.69.9l.81 1.2a2 2 0 0 0 1.67.9H18a2 2 0 0 1 2 2v2"></path>
              </svg>
            {:else}
              <svg class="category-icon" xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="M20 20a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.9a2 2 0 0 1-1.69-.9L9.6 3.9A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13a2 2 0 0 0 2 2Z"></path>
              </svg>
            {/if}
            <span class="category-name">{categoryLabel(category)}</span>
            <span class="category-count">{items.length}</span>
          </button>

          {#if expandedCategories.has(category)}
            <div class="category-items" transition:slide={{duration: 150}}>
              {#each items as template (template.idQueryTemplate)}
                <button
                  class="template-item"
                  class:selected={template.idQueryTemplate === selectedId}
                  class:active-template={template.active}
                  on:click={() => handleSelect(template)}
                >
                  <span class="template-name" title={template.name}>{template.name}</span>
                  <span class="version-badge">v{template.version}</span>
                  {#if template.tagCount > 0}
                    <span class="tag-chip" title="{template.tagCount} tag">{template.tagCount} tag</span>
                  {/if}
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

  /* --- Search bar --- */
  .search-bar {
    position: relative;
    margin-bottom: 8px;
  }

  .search-bar input {
    width: 100%;
    padding: 10px 14px 10px 38px;
    border: 1px solid var(--gray-200);
    border-radius: 8px;
    font-size: 13px;
    background: white;
    box-shadow: 0 1px 3px rgba(0, 0, 0, 0.06);
    transition: border-color 0.2s, box-shadow 0.2s;
  }

  .search-bar input:focus {
    outline: none;
    border-color: var(--primary);
    box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.15), 0 1px 3px rgba(0, 0, 0, 0.06);
  }

  .search-bar input::placeholder {
    color: var(--gray-500);
  }

  .search-icon {
    position: absolute;
    left: 11px;
    top: 50%;
    transform: translateY(-50%);
    color: var(--gray-500);
    pointer-events: none;
    transition: color 0.2s;
  }

  .search-bar:focus-within .search-icon {
    color: var(--primary);
  }

  .search-clear {
    position: absolute;
    right: 8px;
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

  /* --- Toggle all link --- */
  .toggle-all {
    display: block;
    width: auto;
    margin: 0 0 8px auto;
    padding: 0;
    border: none;
    background: none;
    color: var(--primary);
    font-size: 11px;
    font-weight: 600;
    letter-spacing: 0.04em;
    cursor: pointer;
    text-transform: uppercase;
  }

  .toggle-all:hover {
    text-decoration: underline;
  }

  .no-results {
    text-align: center;
    padding: 24px 12px;
    color: var(--gray-500);
    font-size: 13px;
    font-style: italic;
  }

  /* --- Category header --- */
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
    transition: background-color 0.2s;
    text-align: left;
  }

  .category-header:hover {
    background-color: var(--gray-200);
  }

  .category-header.expanded {
    background-color: #eff6ff;
  }

  .category-header.expanded:hover {
    background-color: #dbeafe;
  }

  .category-header.uncategorized {
    color: var(--gray-500);
    font-style: italic;
    margin-top: 4px;
    border-top: 1px dashed var(--gray-300);
    border-radius: 0 0 6px 6px;
    padding-top: 10px;
  }

  .category-icon {
    flex-shrink: 0;
    color: var(--gray-500);
  }

  .category-header.expanded .category-icon {
    color: var(--primary);
  }

  .chevron {
    display: flex;
    align-items: center;
    transition: transform 0.2s ease;
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
    flex-shrink: 0;
    padding: 1px 8px;
    border-radius: 10px;
    background-color: #dbeafe;
    color: #2563eb;
    font-weight: 600;
    font-size: 11px;
  }

  .category-header.uncategorized .category-count {
    background-color: var(--gray-200);
    color: var(--gray-500);
  }

  /* --- Category items (animated) --- */
  .category-items {
    display: flex;
    flex-direction: column;
    gap: 1px;
    padding-left: 10px;
    margin-bottom: 4px;
  }

  /* --- Template items --- */
  .template-item {
    display: flex;
    align-items: center;
    gap: 8px;
    width: 100%;
    padding: 6px 10px;
    border: none;
    border-left: 3px solid transparent;
    border-radius: 0 4px 4px 0;
    background: transparent;
    font-size: 13px;
    color: var(--gray-900);
    cursor: pointer;
    transition: background-color 0.15s, border-color 0.15s, transform 0.15s;
    text-align: left;
  }

  .template-item:hover {
    background-color: var(--gray-200);
    transform: translateX(2px);
  }

  .template-item.selected {
    background-color: #dbeafe;
    border-left-color: #2563eb;
    box-shadow: inset 3px 0 0 0 #2563eb;
  }

  .template-item.selected:hover {
    background-color: #bfdbfe;
    transform: translateX(2px);
  }

  .template-name {
    flex: 1;
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    font-weight: 500;
    font-family: 'Consolas', 'Monaco', monospace;
  }

  /* --- Version badge --- */
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

  .active-template .version-badge {
    background-color: #dbeafe;
    color: #2563eb;
  }

  /* --- Tag chip --- */
  .tag-chip {
    flex-shrink: 0;
    padding: 1px 6px;
    border-radius: 4px;
    background-color: #ede9fe;
    color: #7c3aed;
    font-size: 10px;
    font-weight: 600;
    font-family: 'Consolas', 'Monaco', monospace;
  }

  /* --- Status dot --- */
  .status-dot {
    flex-shrink: 0;
    width: 9px;
    height: 9px;
    border-radius: 50%;
    background-color: var(--gray-300);
    transition: box-shadow 0.2s;
  }

  .status-dot.active {
    background-color: var(--success);
    box-shadow: 0 0 6px rgba(22, 163, 74, 0.4);
  }
</style>

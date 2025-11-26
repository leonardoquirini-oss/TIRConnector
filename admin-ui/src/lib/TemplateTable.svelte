<script lang="ts">
  import type { Template } from './api';
  import { createEventDispatcher } from 'svelte';

  export let templates: Template[] = [];
  export let loading = false;

  const dispatch = createEventDispatcher<{
    select: Template;
  }>();

  function handleRowClick(template: Template) {
    dispatch('select', template);
  }

  function truncate(text: string | null, maxLength: number): string {
    if (!text) return '-';
    return text.length > maxLength ? text.substring(0, maxLength) + '...' : text;
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
        <th>Attiva</th>
      </tr>
    </thead>
    <tbody>
      {#each templates as template (template.idQueryTemplate)}
        <tr on:click={() => handleRowClick(template)}>
          <td><strong>{template.name}</strong></td>
          <td>{truncate(template.description, 50)}</td>
          <td>{template.category || '-'}</td>
          <td>
            <span class="badge" class:active={template.active} class:inactive={!template.active}>
              {template.active ? 'Attiva' : 'Inattiva'}
            </span>
          </td>
        </tr>
      {/each}
    </tbody>
  </table>
{/if}

<script lang="ts">
  import { createEventDispatcher } from 'svelte';
  import { createTag, type QueryTagCreateDto } from './api';

  export let templateId: number;

  const dispatch = createEventDispatcher<{ close: void; saved: void }>();

  let changeReason = '';
  let changeType: QueryTagCreateDto['changeType'] = 'minor';
  let saving = false;
  let error = '';

  async function handleSave() {
    if (!changeReason.trim()) {
      error = 'Il Tag Message è obbligatorio';
      return;
    }

    if (!confirm(`Sei sicuro di voler creare un tag "${changeType}" per questa query?`)) {
      return;
    }

    saving = true;
    error = '';

    try {
      await createTag(templateId, {
        changeReason: changeReason.trim(),
        changeType,
      });
      dispatch('saved');
    } catch (e) {
      error = e instanceof Error ? e.message : 'Errore nella creazione del tag';
    } finally {
      saving = false;
    }
  }

  function handleClose() {
    dispatch('close');
  }

  function handleKeydown(e: KeyboardEvent) {
    if (e.key === 'Escape') {
      handleClose();
    }
  }
</script>

<svelte:window on:keydown={handleKeydown} />

<div class="modal-overlay" on:click|self={handleClose}>
  <div class="modal tag-modal">
    <div class="modal-header">
      <h2>Crea Tag</h2>
      <button class="close-btn" on:click={handleClose}>&times;</button>
    </div>

    <div class="modal-body">
      {#if error}
        <div class="error">{error}</div>
      {/if}

      <div class="form-group">
        <label for="changeReason">Tag Message *</label>
        <textarea
          id="changeReason"
          bind:value={changeReason}
          placeholder="Descrivi il motivo del tag (es: versione stabile, fix bug xyz...)"
          rows="3"
        ></textarea>
      </div>

      <div class="form-group">
        <label for="changeType">Change Type</label>
        <select id="changeType" bind:value={changeType}>
          <option value="minor">Minor</option>
          <option value="major">Major</option>
          <option value="bugfix">Bugfix</option>
          <option value="rollback">Rollback</option>
        </select>
      </div>
    </div>

    <div class="modal-footer">
      <button class="secondary" on:click={handleClose} disabled={saving}>
        Annulla
      </button>
      <button class="primary" on:click={handleSave} disabled={saving}>
        {saving ? 'Salvataggio...' : 'Salva Tag'}
      </button>
    </div>
  </div>
</div>

<style>
  .tag-modal {
    width: 90%;
    max-width: 500px;
  }

  .modal-body {
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

  .form-group {
    display: flex;
    flex-direction: column;
    gap: 6px;
  }

  .form-group label {
    font-weight: 500;
    color: #374151;
    font-size: 14px;
  }

  .form-group textarea,
  .form-group select {
    padding: 10px 12px;
    border: 1px solid #d1d5db;
    border-radius: 6px;
    font-size: 14px;
    font-family: inherit;
  }

  .form-group textarea:focus,
  .form-group select:focus {
    outline: none;
    border-color: #2563eb;
    box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.1);
  }

  .form-group textarea {
    resize: vertical;
    min-height: 80px;
  }
</style>

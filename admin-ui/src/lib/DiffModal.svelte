<script lang="ts">
  import { createEventDispatcher } from 'svelte';
  import * as Diff from 'diff';

  export let currentSql: string = '';
  export let tagSql: string = '';
  export let templateName: string = '';
  export let tagVersion: number = 0;

  const dispatch = createEventDispatcher<{ close: void }>();

  interface DiffLine {
    type: 'unchanged' | 'added' | 'removed';
    content: string;
    lineNumber: number | null;
  }

  function computeDiff(): { left: DiffLine[]; right: DiffLine[] } {
    const changes = Diff.diffLines(currentSql, tagSql);

    const left: DiffLine[] = [];
    const right: DiffLine[] = [];

    let leftLineNum = 1;
    let rightLineNum = 1;

    changes.forEach(change => {
      const lines = change.value.split('\n');
      // Remove last empty line if it's just from the split
      if (lines[lines.length - 1] === '') {
        lines.pop();
      }

      if (change.added) {
        // Added in tag (right side)
        lines.forEach(line => {
          left.push({ type: 'added', content: '', lineNumber: null });
          right.push({ type: 'added', content: line, lineNumber: rightLineNum++ });
        });
      } else if (change.removed) {
        // Removed from current (left side)
        lines.forEach(line => {
          left.push({ type: 'removed', content: line, lineNumber: leftLineNum++ });
          right.push({ type: 'removed', content: '', lineNumber: null });
        });
      } else {
        // Unchanged
        lines.forEach(line => {
          left.push({ type: 'unchanged', content: line, lineNumber: leftLineNum++ });
          right.push({ type: 'unchanged', content: line, lineNumber: rightLineNum++ });
        });
      }
    });

    return { left, right };
  }

  $: diffResult = computeDiff();

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

<div class="modal-overlay" on:click|self={handleClose} role="dialog" aria-modal="true">
  <div class="modal diff-modal">
    <div class="modal-header">
      <div class="header-content">
        <h2>Confronto Query: {templateName}</h2>
        <span class="subtitle">Versione Attuale vs Tag v{tagVersion}</span>
      </div>
      <button class="close-btn" on:click={handleClose}>&times;</button>
    </div>

    <div class="modal-body">
      <div class="diff-container">
        <div class="diff-panel">
          <div class="panel-header current-header">Versione Attuale</div>
          <div class="diff-content">
            {#each diffResult.left as line, i}
              <div class="diff-line {line.type}">
                <span class="line-number">{line.lineNumber ?? ''}</span>
                <span class="line-prefix">{line.type === 'removed' ? '-' : line.type === 'added' ? '' : ' '}</span>
                <pre class="line-content">{line.content}</pre>
              </div>
            {/each}
          </div>
        </div>

        <div class="diff-panel">
          <div class="panel-header tag-header">Tag v{tagVersion}</div>
          <div class="diff-content">
            {#each diffResult.right as line, i}
              <div class="diff-line {line.type}">
                <span class="line-number">{line.lineNumber ?? ''}</span>
                <span class="line-prefix">{line.type === 'added' ? '+' : line.type === 'removed' ? '' : ' '}</span>
                <pre class="line-content">{line.content}</pre>
              </div>
            {/each}
          </div>
        </div>
      </div>
    </div>

    <div class="modal-footer">
      <button class="secondary" on:click={handleClose}>Chiudi</button>
    </div>
  </div>
</div>

<style>
  .diff-modal {
    width: 95%;
    max-width: 1400px;
    max-height: 90vh;
    display: flex;
    flex-direction: column;
  }

  .modal-header {
    display: flex;
    justify-content: space-between;
    align-items: flex-start;
  }

  .header-content {
    display: flex;
    flex-direction: column;
    gap: 4px;
  }

  .header-content h2 {
    margin: 0;
    font-size: 1.25rem;
    font-weight: 600;
    color: #111827;
  }

  .subtitle {
    font-size: 0.875rem;
    color: #6b7280;
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

  .modal-body {
    flex: 1;
    overflow: hidden;
    padding: 0;
  }

  .diff-container {
    display: flex;
    height: 100%;
    overflow: hidden;
  }

  .diff-panel {
    flex: 1;
    display: flex;
    flex-direction: column;
    min-width: 0;
    overflow: hidden;
  }

  .diff-panel:first-child {
    border-right: 1px solid #e5e7eb;
  }

  .panel-header {
    padding: 10px 16px;
    font-weight: 600;
    font-size: 0.875rem;
    text-transform: uppercase;
    letter-spacing: 0.025em;
    border-bottom: 1px solid #e5e7eb;
  }

  .tag-header {
    background-color: #fef3c7;
    color: #92400e;
  }

  .current-header {
    background-color: #dbeafe;
    color: #1e40af;
  }

  .diff-content {
    flex: 1;
    overflow: auto;
    font-family: 'Consolas', 'Monaco', monospace;
    font-size: 13px;
    line-height: 1.5;
  }

  .diff-line {
    display: flex;
    align-items: stretch;
    min-height: 22px;
  }

  .diff-line.unchanged {
    background-color: white;
  }

  .diff-line.added {
    background-color: #dcfce7;
  }

  .diff-line.removed {
    background-color: #fee2e2;
  }

  .line-number {
    min-width: 40px;
    padding: 0 8px;
    text-align: right;
    color: #9ca3af;
    background-color: #f9fafb;
    border-right: 1px solid #e5e7eb;
    user-select: none;
  }

  .diff-line.added .line-number {
    background-color: #bbf7d0;
  }

  .diff-line.removed .line-number {
    background-color: #fecaca;
  }

  .line-prefix {
    width: 20px;
    text-align: center;
    color: #6b7280;
    user-select: none;
  }

  .diff-line.added .line-prefix {
    color: #16a34a;
    font-weight: bold;
  }

  .diff-line.removed .line-prefix {
    color: #dc2626;
    font-weight: bold;
  }

  .line-content {
    flex: 1;
    margin: 0;
    padding: 0 8px;
    white-space: pre;
    overflow-x: auto;
  }

  .modal-footer {
    border-top: 1px solid #e5e7eb;
    padding: 16px 20px;
    display: flex;
    justify-content: flex-end;
    background: #f9fafb;
    border-radius: 0 0 12px 12px;
  }
</style>

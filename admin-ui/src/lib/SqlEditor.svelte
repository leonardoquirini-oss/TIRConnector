<script lang="ts">
  import { onMount, onDestroy } from 'svelte';
  import { EditorView, keymap, lineNumbers, highlightActiveLineGutter, highlightSpecialChars, drawSelection, highlightActiveLine } from '@codemirror/view';
  import { EditorState } from '@codemirror/state';
  import { sql } from '@codemirror/lang-sql';
  import { defaultKeymap, history, historyKeymap } from '@codemirror/commands';
  import { syntaxHighlighting, defaultHighlightStyle, bracketMatching } from '@codemirror/language';

  export let value: string = '';
  export let placeholder: string = 'SELECT * FROM ...';
  export let readonly: boolean = false;

  let container: HTMLDivElement;
  let view: EditorView;
  let isUpdatingFromProp = false;

  onMount(() => {
    const updateListener = EditorView.updateListener.of((update) => {
      if (update.docChanged && !isUpdatingFromProp) {
        value = update.state.doc.toString();
      }
    });

    const state = EditorState.create({
      doc: value,
      extensions: [
        lineNumbers(),
        highlightActiveLineGutter(),
        highlightSpecialChars(),
        history(),
        drawSelection(),
        EditorState.allowMultipleSelections.of(true),
        syntaxHighlighting(defaultHighlightStyle, { fallback: true }),
        bracketMatching(),
        highlightActiveLine(),
        keymap.of([
          ...defaultKeymap,
          ...historyKeymap,
        ]),
        sql(),
        updateListener,
        EditorView.theme({
          '&': {
            height: '100%',
            fontSize: '14px',
          },
          '.cm-scroller': {
            overflow: 'auto',
            fontFamily: "'Consolas', 'Monaco', monospace",
          },
          '.cm-content': {
            minHeight: '150px',
          },
          '&.cm-focused': {
            outline: 'none',
          },
        }),
        EditorView.baseTheme({
          '&.cm-editor': {
            border: '1px solid #d1d5db',
            borderRadius: '6px',
          },
          '&.cm-editor.cm-focused': {
            borderColor: '#2563eb',
            boxShadow: '0 0 0 3px rgba(37, 99, 235, 0.1)',
          },
        }),
        placeholder ? EditorView.contentAttributes.of({ 'data-placeholder': placeholder }) : [],
        readonly ? EditorState.readOnly.of(true) : [],
        readonly ? EditorView.editable.of(false) : [],
      ],
    });

    view = new EditorView({
      state,
      parent: container,
    });
  });

  onDestroy(() => {
    if (view) {
      view.destroy();
    }
  });

  // Sync when value prop changes externally
  $: if (view && value !== view.state.doc.toString()) {
    isUpdatingFromProp = true;
    view.dispatch({
      changes: {
        from: 0,
        to: view.state.doc.length,
        insert: value,
      },
    });
    isUpdatingFromProp = false;
  }
</script>

<div bind:this={container} class="sql-editor-container"></div>

<style>
  .sql-editor-container {
    flex: 1;
    display: flex;
    flex-direction: column;
    min-height: 150px;
  }

  .sql-editor-container :global(.cm-editor) {
    flex: 1;
    background: white;
  }
</style>

<script lang="ts">
  import { onMount, onDestroy } from 'svelte'
  import { EditorState, type Extension } from '@codemirror/state'
  import {
    EditorView,
    keymap,
    lineNumbers,
    highlightActiveLine,
    highlightActiveLineGutter,
    drawSelection,
    rectangularSelection
  } from '@codemirror/view'
  import { history, historyKeymap, defaultKeymap, indentWithTab } from '@codemirror/commands'
  import { bracketMatching, indentUnit } from '@codemirror/language'
  import { closeBrackets, closeBracketsKeymap } from '@codemirror/autocomplete'
  import { searchKeymap, highlightSelectionMatches } from '@codemirror/search'
  import { latexLanguage } from './latexLanguage'

  interface Props {
    value: string
    onChange: (value: string) => void
  }

  let { value = '', onChange }: Props = $props()

  let mount: HTMLDivElement | null = $state(null)
  let view: EditorView | null = null

  const chromeTheme = EditorView.theme(
    {
      '&': {
        color: 'var(--color-text)',
        backgroundColor: 'var(--color-bg)',
        fontSize: '13px',
        fontFamily: 'var(--font-mono)',
        height: '100%'
      },
      '.cm-scroller': {
        fontFamily: 'var(--font-mono)',
        fontSize: '13px',
        lineHeight: '1.5',
        overflow: 'auto'
      },
      '.cm-content': {
        padding: '16px 0',
        caretColor: 'var(--color-text)',
        fontFamily: 'var(--font-mono)',
        backgroundImage: `linear-gradient(to right, color-mix(in srgb, var(--color-border) 20%, transparent) 1px, transparent 1px)`,
        backgroundSize: '2ch 100%'
      },
      '.cm-cursor': {
        borderLeftColor: 'var(--color-text)'
      },
      '.cm-gutters': {
        backgroundColor: 'var(--color-bg-subtle)',
        color: 'var(--color-text-muted)',
        borderRight: '1px solid var(--color-border)',
        fontFamily: 'var(--font-mono)',
        fontSize: '13px'
      },
      '.cm-activeLine': {
        backgroundColor: 'var(--color-bg-subtle)'
      },
      '.cm-activeLineGutter': {
        backgroundColor: 'var(--color-bg-subtle)'
      },
      '.cm-selectionBackground': {
        backgroundColor: 'color-mix(in srgb, var(--color-accent) 30%, transparent)'
      },
      '.cm-focused .cm-selectionBackground': {
        backgroundColor: 'color-mix(in srgb, var(--color-accent) 40%, transparent)'
      },
      '.cm-panel': {
        backgroundColor: 'var(--color-bg-subtle)',
        borderBottom: '1px solid var(--color-border)',
        color: 'var(--color-text)'
      },
      '.cm-panel button': {
        backgroundColor: 'var(--color-bg)',
        border: '1px solid var(--color-border)',
        color: 'var(--color-text)'
      },
      '.cm-textfield': {
        backgroundColor: 'var(--color-bg)',
        border: '1px solid var(--color-border)',
        color: 'var(--color-text)'
      },
      '.cm-button': {
        backgroundColor: 'var(--color-bg)',
        border: '1px solid var(--color-border)',
        color: 'var(--color-text)'
      }
    },
    { dark: true }
  )

  const syntaxTheme = EditorView.theme(
    {
      '.cm-keyword': { color: '#c678dd' },
      '.cm-string': { color: '#98c379' },
      '.cm-comment': { color: '#5c6370', fontStyle: 'italic' },
      '.cm-paren': { color: '#56b6c2' },
      '.cm-name': { color: '#e5c07b', fontWeight: 'bold' },
      '.cm-content': { color: 'var(--color-text)' }
    },
    { dark: true }
  )

  function createExtensions(): Extension[] {
    return [
      latexLanguage,
      lineNumbers(),
      highlightActiveLine(),
      highlightActiveLineGutter(),
      history(),
      drawSelection(),
      rectangularSelection(),
      bracketMatching(),
      closeBrackets(),
      EditorState.allowMultipleSelections.of(true),
      indentUnit.of('  '),
      keymap.of([
        ...defaultKeymap,
        ...historyKeymap,
        ...closeBracketsKeymap,
        ...searchKeymap,
        indentWithTab
      ]),
      highlightSelectionMatches(),
      chromeTheme,
      syntaxTheme,
      EditorView.updateListener.of((update) => {
        if (update.docChanged) {
          onChange(update.state.doc.toString())
        }
      })
    ]
  }

  function createState(text: string) {
    return EditorState.create({
      doc: text,
      extensions: createExtensions()
    })
  }

  onMount(() => {
    if (!mount) return
    view = new EditorView({
      state: createState(value),
      parent: mount
    })
  })

  onDestroy(() => {
    view?.destroy()
    view = null
  })

  $effect(() => {
    const next = value
    if (view && view.state.doc.toString() !== next) {
      view.setState(createState(next))
    }
  })
</script>

<div bind:this={mount} class="h-full"></div>

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
  import {
    history,
    historyKeymap,
    defaultKeymap,
    indentWithTab,
    undo as undoCommand,
    redo as redoCommand
  } from '@codemirror/commands'
  import { bracketMatching, indentUnit } from '@codemirror/language'
  import { closeBrackets, closeBracketsKeymap } from '@codemirror/autocomplete'
  import { searchKeymap, highlightSelectionMatches, openSearchPanel } from '@codemirror/search'
  import { linter, lintGutter, setDiagnostics, type Diagnostic } from '@codemirror/lint'
  import { latexLanguage } from './latexLanguage'
  import { latexAutocompleteExtension } from './latexCompletion'

  interface LogError {
    file: string
    line: number
    message: string
  }

  interface Props {
    value: string
    onChange: (value: string) => void
    onSave?: () => void
    errors?: LogError[]
    onCursorChange?: (line: number, col: number) => void
  }

  let { value = '', onChange, onSave, errors = [], onCursorChange }: Props = $props()

  let mount: HTMLDivElement | null = $state(null)
  let view: EditorView | null = null

  export function undo() {
    if (view) undoCommand(view)
  }

  export function redo() {
    if (view) redoCommand(view)
  }

  export function findInFile() {
    if (view) openSearchPanel(view)
  }

  export function replaceInFile() {
    // @codemirror/search does not export openReplacePanel; the search panel covers replace.
    if (view) openSearchPanel(view)
  }

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
        fontFamily: 'var(--font-mono)'
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
      },
      '.cm-lintRange-error': {
        backgroundColor: 'color-mix(in srgb, var(--color-error) 20%, transparent)'
      },
      '.cm-lintMarker-error': {
        color: 'var(--color-error)'
      },
      '.cm-tooltip-lint': {
        backgroundColor: 'var(--color-bg-subtle)',
        border: '1px solid var(--color-border)',
        color: 'var(--color-text)'
      },
      '.cm-diagnostic': {
        color: 'var(--color-text)'
      },
      '.cm-diagnostic-error': {
        color: 'var(--color-error)'
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

  function toDiagnostics(state: EditorState, errs: LogError[]): Diagnostic[] {
    return errs.map((err) => {
      const lineNo = Math.max(1, err.line)
      const line = state.doc.lines >= lineNo ? state.doc.line(lineNo) : state.doc.line(state.doc.lines)
      return {
        from: line.from,
        to: line.to,
        severity: 'error',
        message: err.message
      }
    })
  }

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
      latexAutocompleteExtension,
      linter(() => []),
      lintGutter(),
      EditorState.allowMultipleSelections.of(true),
      indentUnit.of('  '),
      keymap.of([
        ...defaultKeymap,
        ...historyKeymap,
        ...closeBracketsKeymap,
        ...searchKeymap,
        indentWithTab,
        {
          key: 'Mod-s',
          run: () => {
            onSave?.()
            return true
          },
          preventDefault: true
        }
      ]),
      highlightSelectionMatches(),
      chromeTheme,
      syntaxTheme,
      EditorView.updateListener.of((update) => {
        if (update.docChanged) {
          onChange(update.state.doc.toString())
        }
        if (update.selectionSet) {
          const head = update.state.selection.main.head
          const line = update.state.doc.lineAt(head)
          onCursorChange?.(line.number, head - line.from + 1)
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
    const errs = errors
    if (!view) return
    if (view.state.doc.toString() !== next) {
      view.setState(createState(next))
    }
    view.dispatch(setDiagnostics(view.state, toDiagnostics(view.state, errs)))
  })
</script>

<div bind:this={mount} class="h-full"></div>

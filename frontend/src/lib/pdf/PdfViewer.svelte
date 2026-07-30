<script lang="ts">
  import * as pdfjsLib from 'pdfjs-dist'
  import workerUrl from 'pdfjs-dist/build/pdf.worker.min.mjs?url'
  import type { PDFDocumentProxy, PageViewport } from 'pdfjs-dist'

  pdfjsLib.GlobalWorkerOptions.workerSrc = workerUrl

  interface Props {
    url: string | null
  }

  let { url }: Props = $props()

  let canvasEl: HTMLCanvasElement | null = $state(null)
  let containerEl: HTMLDivElement | null = $state(null)
  let containerWidth = $state(0)

  let loading = $state(false)
  let error = $state<string | null>(null)
  let pdf: PDFDocumentProxy | null = $state(null)
  let pageNumber = $state(1)
  let pageCount = $state(0)
  let scale = $state(1)
  let baseViewport: PageViewport | null = $state(null)
  let fitted = $state(false)

  let renderTask: ReturnType<pdfjsLib.PDFPageProxy['render']> | null = null
  let zoomPercent = $derived(Math.round(scale * 100))

  $effect(() => {
    const currentUrl = url
    fitted = false
    closeDocument()
    error = null

    if (!currentUrl) {
      loading = false
      return
    }

    loading = true
    openDocument(currentUrl)

    return () => {
      closeDocument()
    }
  })

  $effect(() => {
    if (!pdf || !baseViewport || containerWidth <= 0 || fitted) return
    scale = Math.max(0.1, containerWidth / baseViewport.width)
    fitted = true
  })

  $effect(() => {
    if (!pdf || !baseViewport || !canvasEl) return
    renderPage()
  })

  async function openDocument(currentUrl: string) {
    try {
      const doc = await pdfjsLib.getDocument(currentUrl).promise
      if (currentUrl !== url) {
        doc.destroy()
        return
      }
      const page = await doc.getPage(1)
      pdf = doc
      pageCount = doc.numPages
      pageNumber = 1
      baseViewport = page.getViewport({ scale: 1 })
    } catch (err) {
      if (currentUrl !== url) return
      loading = false
      error = err instanceof Error ? err.message : 'Failed to load PDF'
      pdf = null
      pageCount = 0
      baseViewport = null
    }
  }

  function closeDocument() {
    if (renderTask) {
      renderTask.cancel()
      renderTask = null
    }
    if (pdf) {
      pdf.destroy()
      pdf = null
    }
    pageCount = 0
    pageNumber = 1
    baseViewport = null
    fitted = false
  }

  async function renderPage() {
    if (!pdf || !canvasEl || !baseViewport) return

    if (renderTask) {
      renderTask.cancel()
      renderTask = null
    }

    try {
      const page = await pdf.getPage(pageNumber)
      const dpr = window.devicePixelRatio || 1
      const viewport = page.getViewport({ scale: Math.max(0.1, scale) * dpr })

      canvasEl.width = viewport.width
      canvasEl.height = viewport.height
      canvasEl.style.width = `${Math.floor(viewport.width / dpr)}px`
      canvasEl.style.height = `${Math.floor(viewport.height / dpr)}px`

      const ctx = canvasEl.getContext('2d')
      if (!ctx) return

      renderTask = page.render({ canvasContext: ctx, viewport })
      await renderTask.promise
      renderTask = null
      loading = false
      error = null
    } catch (err) {
      renderTask = null
      if (String(err).toLowerCase().includes('cancelled')) return
      loading = false
      error = err instanceof Error ? err.message : 'Failed to render page'
    }
  }

  function goPrevious() {
    if (pageNumber > 1) pageNumber--
  }

  function goNext() {
    if (pageNumber < pageCount) pageNumber++
  }

  function zoomIn() {
    scale = Math.min(5, scale * 1.2)
  }

  function zoomOut() {
    scale = Math.max(0.1, scale / 1.2)
  }

  function fitWidth() {
    if (!baseViewport || containerWidth <= 0) return
    scale = Math.max(0.1, containerWidth / baseViewport.width)
  }
</script>

<div bind:this={containerEl} bind:clientWidth={containerWidth} class="flex flex-col h-full min-h-0 bg-bg">
  <div
    class="h-8 shrink-0 flex items-center justify-between px-2 border-b border-border bg-bg-subtle"
  >
    <div class="flex items-center gap-1">
      <button
        type="button"
        onclick={goPrevious}
        disabled={!pdf || pageNumber <= 1}
        class="px-2 text-xs text-text hover:text-accent disabled:opacity-40"
        aria-label="Previous page"
      >
        ‹
      </button>
      <span class="text-xs text-text min-w-[3rem] text-center">
        {pdf ? `${pageNumber} / ${pageCount}` : '—'}
      </span>
      <button
        type="button"
        onclick={goNext}
        disabled={!pdf || pageNumber >= pageCount}
        class="px-2 text-xs text-text hover:text-accent disabled:opacity-40"
        aria-label="Next page"
      >
        ›
      </button>
    </div>

    <div class="flex items-center gap-1">
      <button
        type="button"
        onclick={zoomOut}
        disabled={!pdf}
        class="px-2 text-xs text-text hover:text-accent disabled:opacity-40"
        aria-label="Zoom out"
      >
        −
      </button>
      <span class="text-xs text-text min-w-[3rem] text-center">{zoomPercent}%</span>
      <button
        type="button"
        onclick={zoomIn}
        disabled={!pdf}
        class="px-2 text-xs text-text hover:text-accent disabled:opacity-40"
        aria-label="Zoom in"
      >
        +
      </button>
      <button
        type="button"
        onclick={fitWidth}
        disabled={!pdf}
        class="px-2 text-xs text-text hover:text-accent disabled:opacity-40"
        aria-label="Fit width"
      >
        fit
      </button>
    </div>

    <a
      href={url ?? undefined}
      download
      class="text-xs text-accent hover:opacity-90 disabled:opacity-40"
      class:pointer-events-none={!url}
    >
      download
    </a>
  </div>

  <div class="flex-1 min-h-0 overflow-auto flex items-start justify-center p-4">
    {#if !url}
      <div class="self-center text-text-muted">Compile to see the PDF</div>
    {:else if loading}
      <div class="self-center text-text-muted">Loading PDF…</div>
    {:else if error}
      <div class="self-center text-center">
        <div class="text-error mb-1">{error}</div>
        <div class="text-xs text-text-muted">The link may have expired. Recompile to refresh.</div>
      </div>
    {:else}
      <canvas bind:this={canvasEl} class="border border-border"></canvas>
    {/if}
  </div>
</div>

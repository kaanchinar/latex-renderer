<script lang="ts">
  interface Props {
    direction: 'vertical' | 'horizontal'
    value: number
    min: number
    max: number
    onChange: (value: number) => void
    onReset?: () => void
    testid?: string
  }

  let { direction, value, min, max, onChange, onReset, testid }: Props = $props()

  let dragging = $state(false)
  let startPos = 0
  let startValue = 0

  function clamp(v: number) {
    return Math.max(min, Math.min(max, v))
  }

  function onPointerDown(event: PointerEvent) {
    const target = event.currentTarget as HTMLDivElement
    dragging = true
    startPos = direction === 'vertical' ? event.clientX : event.clientY
    startValue = value
    target.setPointerCapture(event.pointerId)
  }

  function onPointerMove(event: PointerEvent) {
    if (!dragging) return
    const pos = direction === 'vertical' ? event.clientX : event.clientY
    const delta = pos - startPos
    onChange(clamp(startValue + delta))
  }

  function onPointerUp(event: PointerEvent) {
    const target = event.currentTarget as HTMLDivElement
    dragging = false
    target.releasePointerCapture(event.pointerId)
  }
</script>

{#if direction === 'vertical'}
  <div
    role="separator"
    tabindex="-1"
    aria-orientation="vertical"
    data-testid={testid}
    class="w-[6px] shrink-0 h-full cursor-col-resize group flex justify-center"
    class:bg-bg-subtle={dragging}
    onpointerdown={onPointerDown}
    onpointermove={onPointerMove}
    onpointerup={onPointerUp}
    ondblclick={onReset}
  >
    <div class="w-px h-full bg-border group-hover:bg-accent"></div>
  </div>
{:else}
  <div
    role="separator"
    tabindex="-1"
    aria-orientation="horizontal"
    data-testid={testid}
    class="h-[6px] shrink-0 w-full cursor-row-resize group flex items-center"
    class:bg-bg-subtle={dragging}
    onpointerdown={onPointerDown}
    onpointermove={onPointerMove}
    onpointerup={onPointerUp}
    ondblclick={onReset}
  >
    <div class="h-px w-full bg-border group-hover:bg-accent"></div>
  </div>
{/if}

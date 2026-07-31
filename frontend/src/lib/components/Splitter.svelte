<script lang="ts">
  interface Props {
    direction: 'vertical' | 'horizontal'
    value: number
    min: number
    max: number
    onChange: (value: number) => void
    onEnd?: () => void
    onReset?: () => void
    testid?: string
  }

  let { direction, value, min, max, onChange, onEnd, onReset, testid }: Props = $props()

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
    event.preventDefault()
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
    if (target.hasPointerCapture(event.pointerId)) {
      target.releasePointerCapture(event.pointerId)
    }
    onEnd?.()
  }
</script>

{#if direction === 'vertical'}
  <div
    role="separator"
    tabindex="-1"
    aria-orientation="vertical"
    data-testid={testid}
    class="w-[8px] shrink-0 h-full cursor-col-resize group flex justify-center items-stretch"
    class:bg-bg-subtle={dragging}
    onpointerdown={onPointerDown}
    onpointermove={onPointerMove}
    onpointerup={onPointerUp}
    ondblclick={onReset}
  >
    <div
      class="w-px h-full bg-border group-hover:bg-accent group-hover:w-[2px] transition-[background-color,width]"
      class:bg-accent={dragging}
      class:w-[2px]={dragging}
    ></div>
  </div>
{:else}
  <div
    role="separator"
    tabindex="-1"
    aria-orientation="horizontal"
    data-testid={testid}
    class="h-[8px] shrink-0 w-full cursor-row-resize group flex items-center"
    class:bg-bg-subtle={dragging}
    onpointerdown={onPointerDown}
    onpointermove={onPointerMove}
    onpointerup={onPointerUp}
    ondblclick={onReset}
  >
    <div
      class="h-px w-full bg-border group-hover:bg-accent group-hover:h-[2px] transition-[background-color,height]"
      class:bg-accent={dragging}
      class:h-[2px]={dragging}
    ></div>
  </div>
{/if}

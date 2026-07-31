<script lang="ts">
  import { ChevronDown } from '@lucide/svelte'

  interface Item {
    label: string
    shortcut?: string
    disabled?: boolean
    action: () => void
  }

  interface Props {
    label: string
    name: string
    openName: string | null
    onOpen: (name: string | null) => void
    items: Item[]
  }

  let { label, name, openName, onOpen, items }: Props = $props()

  let open = $derived(openName === name)
  let buttonEl: HTMLButtonElement | null = $state(null)
  let menuEl: HTMLDivElement | null = $state(null)

  $effect(() => {
    if (!open) return

    function onDocClick(event: MouseEvent) {
      const target = event.target as Node | null
      if (!target) return
      if (menuEl?.contains(target) || buttonEl?.contains(target)) return
      onOpen(null)
    }

    function onKey(event: KeyboardEvent) {
      if (event.key === 'Escape') onOpen(null)
    }

    document.addEventListener('pointerdown', onDocClick)
    document.addEventListener('keydown', onKey)
    return () => {
      document.removeEventListener('pointerdown', onDocClick)
      document.removeEventListener('keydown', onKey)
    }
  })
</script>

<div class="relative">
  <button
    type="button"
    bind:this={buttonEl}
    data-testid="menu-{name}-button"
    onclick={() => onOpen(open ? null : name)}
    aria-haspopup="true"
    aria-expanded={open}
    class="h-8 px-3 text-xs text-text hover:bg-bg-subtle flex items-center gap-1"
  >
    {label}
    <ChevronDown size={14} strokeWidth={1.75} />
  </button>

  {#if open}
    <div
      bind:this={menuEl}
      class="absolute top-full left-0 mt-px min-w-[180px] border border-border bg-bg flex flex-col z-50"
    >
      {#each items as item (item.label)}
        <button
          type="button"
          disabled={item.disabled}
          onclick={() => {
            item.action()
            onOpen(null)
          }}
          class="flex items-center justify-between px-3 py-2 text-xs text-left text-text hover:bg-bg-subtle disabled:opacity-50 disabled:hover:bg-transparent disabled:cursor-default"
        >
          <span>{item.label}</span>
          {#if item.shortcut}
            <span class="text-text-muted text-xs ml-4">{item.shortcut}</span>
          {/if}
        </button>
      {/each}
    </div>
  {/if}
</div>

<script lang="ts">
  import { link } from '../router'

  type Props = {
    id: string
  }

  let { id }: Props = $props()
</script>

<div class="w-full max-w-[880px] border border-border bg-bg p-6 text-center">
  <h1 class="text-lg font-semibold text-text mb-2">Project workspace</h1>
  <p class="text-text-muted mb-4">Project {id} will be editable here in F4.</p>
  <a use:link href="/projects" class="text-accent hover:underline">Back to projects</a>
</div>

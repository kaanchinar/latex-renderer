export interface LogError {
  file: string
  line: number
  message: string
}

const fileLineRe = /^([^:\n]+?):(\d+):\s*(.*)$/
const errorAtLineRe = /error:\s*(.*?)\s+at\s+line\s+(\d+)(?:\s+in\s+file\s+([^\n]+?)(?=:|$))?(?::)?\s*(.*)$/i

export function parseLogErrors(logs: string): LogError[] {
  const seen = new Set<string>()
  const errors: LogError[] = []

  for (const raw of logs.split('\n')) {
    const line = raw.trim()
    if (!line) continue

    let match = fileLineRe.exec(line)
    if (match) {
      const [, file, lineNo, message] = match
      if (message.toLowerCase().includes('error') || file.toLowerCase().endsWith('.tex')) {
        add({ file: file.trim(), line: parseInt(lineNo, 10), message: message.trim() })
      }
      continue
    }

    match = errorAtLineRe.exec(line)
    if (match) {
      const [, msg1, lineNo, file, msg2] = match
      const message = (msg1 + (msg2 ? ' ' + msg2 : '')).trim()
      add({ file: file ? file.trim() : '', line: parseInt(lineNo, 10), message })
    }
  }

  return errors

  function add(error: LogError) {
    if (!error.line || error.line < 1) return
    const key = `${error.file}:${error.line}:${error.message}`
    if (seen.has(key)) return
    seen.add(key)
    errors.push(error)
  }
}


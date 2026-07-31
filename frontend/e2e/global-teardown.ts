import fs from 'node:fs'
import path from 'node:path'

const skipFile = path.join(import.meta.dirname, '.skip-e2e')

export default async function globalTeardown() {
  if (fs.existsSync(skipFile)) {
    fs.unlinkSync(skipFile)
  }
}

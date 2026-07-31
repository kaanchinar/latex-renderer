import fs from 'node:fs'
import path from 'node:path'

const skipFile = path.join(import.meta.dirname, '.skip-e2e')

export default async function globalSetup() {
  if (fs.existsSync(skipFile)) {
    fs.unlinkSync(skipFile)
  }

  try {
    const response = await fetch('http://localhost:5000/health', {
      signal: AbortSignal.timeout(5000)
    })
    if (!response.ok) {
      throw new Error(`backend health returned ${response.status}`)
    }
    console.log('Backend available at http://localhost:5000; running e2e tests.')
  } catch {
    console.log(
      'Backend not available at http://localhost:5000; e2e tests will be skipped.'
    )
    fs.writeFileSync(skipFile, '')
  }
}

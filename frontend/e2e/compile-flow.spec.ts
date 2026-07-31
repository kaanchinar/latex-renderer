import { test, expect } from '@playwright/test'
import fs from 'node:fs'
import path from 'node:path'

const skipMarker = path.join(import.meta.dirname, '.skip-e2e')

test.skip(fs.existsSync(skipMarker), 'Backend not available; skipping e2e.')

test('register, create project, write LaTeX, compile, and render PDF', async ({
  page
}) => {
  test.setTimeout(240_000)

  const email = `e2e-${Date.now()}@example.com`
  const password = 'Password123!'
  const latex = [
    '\\documentclass{article}',
    '\\begin{document}',
    '\\section{Hello}',
    'World',
    '\\end{document}'
  ].join('\n')

  await page.goto('/#/login', { waitUntil: 'domcontentloaded' })
  await page.getByRole('link', { name: 'Create one' }).click()
  await page.locator('#register-email').fill(email)
  await page.locator('#register-password').fill(password)
  await page.getByRole('button', { name: 'Create account' }).click()

  await page.getByTestId('new-project-button').waitFor()
  await expect(page).toHaveURL(/.*\/projects/)

  await page.getByTestId('new-project-button').click()
  await page.getByTestId('project-name-input').fill('E2E')
  await page.getByTestId('create-project-button').click()

  await page.getByTestId('new-file-button').waitFor()
  await expect(page).toHaveURL(/.*\/projects\/.+/)

  await page.getByTestId('new-file-button').click()
  await page.getByTestId('new-file-input').fill('main.tex')
  await page.getByTestId('create-file-button').click()

  await page.getByTestId('file-item').filter({ hasText: 'main.tex' }).click()
  await page.locator('.cm-content').fill(latex)

  const compileButton = page.getByTestId('compile-button')
  const successStatus = page
    .getByTestId('compile-status')
    .filter({ hasText: /compiled/i })
  const failedStatus = page
    .getByTestId('compile-status')
    .filter({ hasText: /failed/i })

  await compileButton.click()
  try {
    await successStatus.first().waitFor({ timeout: 90_000 })
  } catch (firstError) {
    await failedStatus.first().waitFor({ timeout: 5_000 })
    await compileButton.click()
    await successStatus.first().waitFor({ timeout: 90_000 })
  }

  const downloadLink = page.getByTestId('pdf-download')
  await downloadLink.waitFor({ timeout: 5_000 })
  const pdfUrl = await downloadLink.getAttribute('href')

  const canvas = page.getByTestId('pdf-canvas')
  const loading = page.getByTestId('pdf-loading')
  const error = page.getByTestId('pdf-error')

  try {
    await canvas.waitFor({ timeout: 60_000 })
  } catch (canvasError) {
    const loadingVisible = await loading.isVisible().catch(() => false)
    const errorText = await error.innerText().catch(() => 'no error text')
    throw new Error(
      `PDF canvas did not render. loading=${loadingVisible}, error="${errorText}", url="${pdfUrl}"`,
      { cause: canvasError }
    )
  }

  const box = await canvas.boundingBox()
  expect(box).not.toBeNull()
  expect(box!.width).toBeGreaterThan(0)
  expect(box!.height).toBeGreaterThan(0)
})

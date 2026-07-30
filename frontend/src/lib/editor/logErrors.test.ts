import { describe, it, expect } from 'vitest'
import { parseLogErrors } from './logErrors'

describe('parseLogErrors', () => {
  it('parses main.tex:NN format', () => {
    const errors = parseLogErrors('main.tex:42: Undefined control sequence.')
    expect(errors).toEqual([
      { file: 'main.tex', line: 42, message: 'Undefined control sequence.' }
    ])
  })

  it('parses at line NN format without file', () => {
    const errors = parseLogErrors('error: something broke at line 12')
    expect(errors).toEqual([{ file: '', line: 12, message: 'something broke' }])
  })

  it('parses at line NN format with file', () => {
    const errors = parseLogErrors(
      'error: badness at line 5 in file main.tex: extra context'
    )
    expect(errors).toEqual([
      { file: 'main.tex', line: 5, message: 'badness extra context' }
    ])
  })

  it('matches basename for active file filtering', () => {
    const errors = parseLogErrors('./sections/intro.tex:7: Missing $ inserted.')
    expect(errors).toEqual([
      { file: './sections/intro.tex', line: 7, message: 'Missing $ inserted.' }
    ])
  })

  it('returns empty when no match', () => {
    expect(parseLogErrors('lorem ipsum dolor sit amet')).toEqual([])
  })

  it('deduplicates identical errors', () => {
    const errors = parseLogErrors('main.tex:1: Error.\nmain.tex:1: Error.')
    expect(errors).toHaveLength(1)
  })
})

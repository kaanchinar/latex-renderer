import { StreamLanguage } from '@codemirror/language'
import { tags } from '@lezer/highlight'

export const latexLanguage = StreamLanguage.define({
  name: 'latex',
  startState: () => null,
  token: (stream) => {
    if (stream.eatSpace()) return null
    const ch = stream.peek()
    if (!ch) return null

    if (ch === '%') {
      stream.skipToEnd()
      return 'comment'
    }

    if (ch === '$') {
      stream.next()
      const double = stream.peek() === '$'
      if (double) stream.next()
      while (!stream.eol()) {
        if (stream.peek() === '$') {
          stream.next()
          if (double && stream.peek() === '$') stream.next()
          break
        }
        stream.next()
      }
      return 'string'
    }

    if (ch === '{' || ch === '}' || ch === '[' || ch === ']') {
      stream.next()
      return 'bracket'
    }

    if (ch === '\\') {
      stream.next()
      const next = stream.peek()
      if (next && /[{}[\]\\%$]/.test(next)) {
        stream.next()
      } else {
        stream.eatWhile(/[A-Za-z]/)
        const cmd = stream.string.slice(stream.start + 1, stream.pos)
        if ((cmd === 'begin' || cmd === 'end') && stream.eat('{')) {
          stream.eatWhile(/[A-Za-z]/)
          if (stream.eat('}')) return 'name'
        }
      }
      return 'keyword'
    }

    stream.next()
    while (!stream.eol() && !stream.match(/[\\{}[\]%$]/, false)) {
      stream.next()
    }
    return 'text'
  },
  tokenTable: {
    text: tags.content,
    comment: tags.comment,
    keyword: tags.keyword,
    name: tags.name,
    string: tags.string,
    bracket: tags.paren
  }
})

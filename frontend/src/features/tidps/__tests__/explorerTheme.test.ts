import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'
import { explorerTokens } from '../explorerTheme'

// Both themes are designed, not one inverted from the other. A token defined only in
// the light block renders as nothing in dark mode, which is the classic way a page ends
// up with one theme's text on the other theme's ground.
describe('explorer theme tokens', () => {
  const css = readFileSync(resolve(process.cwd(), 'src/index.css'), 'utf8')
  const light = css.slice(css.indexOf(':root {'), css.indexOf('.dark {'))
  const dark = css.slice(css.indexOf('.dark {'))

  it.each(explorerTokens)('%s has a value in the light theme', (token) => {
    expect(light).toContain(`${token}:`)
  })

  it.each(explorerTokens)('%s has a value in the dark theme', (token) => {
    expect(dark).toContain(`${token}:`)
  })

  it('gives the folder a different value in each theme', () => {
    const value = (block: string, token: string) =>
      block.split(`${token}:`)[1]?.split(';')[0]?.trim()

    expect(value(light, '--folder')).not.toBe(value(dark, '--folder'))
  })
})

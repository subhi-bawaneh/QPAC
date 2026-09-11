// The explorer's own colours, as CSS variables defined in index.css for both themes.
//
// They are listed here so a test can assert that every one of them has a value in the
// light block and in the dark block. The dark folder is a warmer, less saturated yellow
// rather than the light one dimmed: the same hue at the same saturation on a dark ground
// reads as a warning badge, not as a folder.
export const explorerTokens = [
  '--folder',
  '--folder-shade',
  '--file',
  '--file-accent',
  '--selection',
  '--icon-label',
] as const

export type ExplorerToken = (typeof explorerTokens)[number]

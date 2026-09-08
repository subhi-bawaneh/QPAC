import { describe, expect, it } from 'vitest'
import { subtreeFolderIds } from '../tree'
import type { FolderTreeNode } from '@/shared/api/types'

const node = (id: string, children: FolderTreeNode[] = []): FolderTreeNode => ({
  id, parentId: null, name: id, path: id, target: 'Draft', isCompany: false, authorName: null,
  fileCount: 0, hasNewerDraft: false, children,
})

describe('subtreeFolderIds', () => {
  const tree = [node('root', [node('tidps', [node('afco', [node('afco-st')]), node('doka')])])]

  it('returns the folder and everything below it, in tree order', () => {
    expect(subtreeFolderIds(tree, 'tidps')).toEqual(['tidps', 'afco', 'afco-st', 'doka'])
  })

  it('returns nothing for no selection and just the id for an unknown folder', () => {
    expect(subtreeFolderIds(tree, null)).toEqual([])
    expect(subtreeFolderIds(tree, 'missing')).toEqual(['missing'])
  })
})

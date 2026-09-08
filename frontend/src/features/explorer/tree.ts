import type { FolderTreeNode } from '@/shared/api/types'

// The folder and every folder below it, in tree order.
export function subtreeFolderIds(nodes: FolderTreeNode[], folderId: string | null): string[] {
  if (folderId === null) return []
  const find = (list: FolderTreeNode[]): FolderTreeNode | null => {
    for (const node of list) {
      if (node.id === folderId) return node
      const below = find(node.children)
      if (below) return below
    }
    return null
  }
  const root = find(nodes)
  if (!root) return [folderId]
  const out: string[] = []
  const walk = (node: FolderTreeNode) => {
    out.push(node.id)
    node.children.forEach(walk)
  }
  walk(root)
  return out
}

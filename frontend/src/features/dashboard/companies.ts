import type { FolderTreeNode } from '@/shared/api/types'

/** Every company folder in the tree, whatever its depth. */
export function collectCompanies(nodes: FolderTreeNode[]): FolderTreeNode[] {
  const out: FolderTreeNode[] = []
  const walk = (list: FolderTreeNode[]) => {
    for (const node of list) {
      if (node.isCompany) out.push(node)
      walk(node.children)
    }
  }
  walk(nodes)
  return out
}

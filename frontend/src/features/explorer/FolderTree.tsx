import { useState } from 'react'
import { ChevronDown, ChevronRight, Folder, FolderOpen } from 'lucide-react'
import { cn } from '@/shared/lib/utils'
import type { FolderTreeNode } from '@/shared/api/types'
import { TargetBadge } from './FolderBadges'

export function FolderTree({ nodes, selectedId, onSelect }: {
  nodes: FolderTreeNode[]
  selectedId: string | null
  onSelect: (node: FolderTreeNode) => void
}) {
  if (nodes.length === 0) {
    return (
      <p className="px-3 py-4 text-xs text-muted-foreground">
        No folders yet. Create one, or sync from Drive.
      </p>
    )
  }

  return (
    <ul role="tree" aria-label="Folders" className="space-y-0.5">
      {nodes.map((node) => (
        <TreeNode key={node.id} node={node} depth={0} selectedId={selectedId} onSelect={onSelect} />
      ))}
    </ul>
  )
}

function TreeNode({ node, depth, selectedId, onSelect }: {
  node: FolderTreeNode
  depth: number
  selectedId: string | null
  onSelect: (node: FolderTreeNode) => void
}) {
  // Top-level folders start open; deeper ones stay collapsed so a large tree
  // does not arrive fully expanded.
  const [expanded, setExpanded] = useState(depth === 0)
  const hasChildren = node.children.length > 0
  const isSelected = node.id === selectedId

  return (
    <li role="none">
      <div
        role="treeitem"
        aria-selected={isSelected}
        aria-expanded={hasChildren ? expanded : undefined}
        className={cn(
          'flex items-center gap-1 rounded-md pr-2 text-sm',
          isSelected ? 'bg-primary text-primary-foreground' : 'hover:bg-muted',
        )}
        style={{ paddingLeft: `${depth * 12 + 4}px` }}
      >
        <button
          type="button"
          className={cn('flex h-6 w-6 items-center justify-center rounded', !hasChildren && 'invisible')}
          onClick={() => setExpanded((value) => !value)}
          aria-label={expanded ? `Collapse ${node.name}` : `Expand ${node.name}`}
          tabIndex={hasChildren ? 0 : -1}
        >
          {expanded ? <ChevronDown className="h-3 w-3" aria-hidden /> : <ChevronRight className="h-3 w-3" aria-hidden />}
        </button>

        <button
          type="button"
          className="flex flex-1 items-center gap-2 py-1.5 text-left"
          onClick={() => onSelect(node)}
        >
          {expanded && hasChildren
            ? <FolderOpen className="h-4 w-4 shrink-0" aria-hidden />
            : <Folder className="h-4 w-4 shrink-0" aria-hidden />}
          <span className="truncate">{node.name}</span>
          {node.authorName ? (
            <span className="truncate text-xs text-muted-foreground">{node.authorName}</span>
          ) : null}
        </button>

        {/* One dot for the whole branch: the badge survives a collapsed tree. */}
        {node.hasNewerDraft ? (
          <span
            aria-label={`${node.name} has newer Drive data`}
            title="Drive has newer data"
            className="h-2 w-2 shrink-0 rounded-full bg-amber-500"
          />
        ) : null}

        {node.target === 'Draft' ? <TargetBadge target={node.target} /> : null}
      </div>

      {hasChildren && expanded ? (
        <ul role="group" className="space-y-0.5">
          {node.children.map((child) => (
            <TreeNode
              key={child.id}
              node={child}
              depth={depth + 1}
              selectedId={selectedId}
              onSelect={onSelect}
            />
          ))}
        </ul>
      ) : null}
    </li>
  )
}

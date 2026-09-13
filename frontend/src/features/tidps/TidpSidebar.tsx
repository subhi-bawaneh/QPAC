import { useState } from 'react'
import { ChevronDown, ChevronRight, Folder } from 'lucide-react'
import {
  type TidpOwnerDto,
} from './api'

interface SidebarNodeProps {
  name: string
  isOwner?: boolean
  isDiscipline?: boolean
  isFile?: boolean
  isExpanded?: boolean
  onToggle?: () => void
  hasChildren?: boolean
  onClick?: () => void
  isActive?: boolean
  indent: number
}

function SidebarNode({
  name, isOwner, isDiscipline, isFile, isExpanded, onToggle, hasChildren, onClick, isActive, indent,
}: SidebarNodeProps) {
  const paddingLeft = `${indent * 16}px`

  return (
    <div>
      <button
        onClick={() => {
          onClick?.()
          if (hasChildren) onToggle?.()
        }}
        className={`w-full text-left px-3 py-2 text-sm hover:bg-accent rounded transition-colors ${
          isActive ? 'bg-accent font-medium' : ''
        }`}
        style={{ paddingLeft }}
      >
        <div className="flex items-center gap-1">
          {hasChildren && (
            <button
              onClick={(e) => {
                e.stopPropagation()
                onToggle?.()
              }}
              className="p-0"
            >
              {isExpanded ? (
                <ChevronDown className="h-4 w-4" />
              ) : (
                <ChevronRight className="h-4 w-4" />
              )}
            </button>
          )}
          {!hasChildren && <div className="w-4" />}
          {isFile && <span className="text-xs text-muted-foreground">📄</span>}
          {isDiscipline && <span className="text-xs text-muted-foreground">📁</span>}
          {isOwner && <Folder className="h-4 w-4" />}
          <span className="truncate">{name}</span>
        </div>
      </button>
    </div>
  )
}

interface TidpSidebarProps {
  data?: {
    owners: TidpOwnerDto[]
  }
  selectedPath: { id: string; name: string; type: 'owner' | 'discipline' }[]
  onNavigate: (path: { id: string; name: string; type: 'owner' | 'discipline' }[]) => void
  isCollapsed: boolean
  onToggleCollapse: () => void
}

export function TidpSidebar({
  data, selectedPath, onNavigate, isCollapsed, onToggleCollapse,
}: TidpSidebarProps) {
  const [expandedOwners, setExpandedOwners] = useState<Set<string>>(new Set())

  const toggleOwner = (ownerId: string) => {
    setExpandedOwners((prev) => {
      const next = new Set(prev)
      if (next.has(ownerId)) {
        next.delete(ownerId)
      } else {
        next.add(ownerId)
      }
      return next
    })
  }

  const isOwnerSelected = selectedPath.length > 0 && selectedPath[0]?.type === 'owner'
  const selectedOwnerId = isOwnerSelected ? selectedPath[0].id : null

  const isDisciplineSelected = selectedPath.length > 1 && selectedPath[1]?.type === 'discipline'
  const selectedDisciplineId = isDisciplineSelected ? selectedPath[1].id : null

  if (isCollapsed) {
    return (
      <button
        onClick={onToggleCollapse}
        className="p-2 hover:bg-accent rounded transition-colors"
        title="Show sidebar"
      >
        <ChevronRight className="h-4 w-4" />
      </button>
    )
  }

  return (
    <div className="w-64 border-r border-border bg-card flex flex-col h-full">
      <div className="flex items-center justify-between p-4 border-b border-border">
        <h2 className="font-semibold">TIDP Structure</h2>
        <button
          onClick={onToggleCollapse}
          className="p-1 hover:bg-accent rounded transition-colors"
          title="Hide sidebar"
        >
          <ChevronDown className="h-4 w-4 rotate-90" />
        </button>
      </div>

      <div className="overflow-y-auto flex-1">
        {data?.owners && data.owners.length > 0 ? (
          <div>
            {data.owners.map((owner) => {
              const isOwnerExpanded = expandedOwners.has(owner.id)
              const isOwnerActive = selectedOwnerId === owner.id && !isDisciplineSelected

              return (
                <div key={owner.id}>
                  <SidebarNode
                    name={owner.folderName}
                    isOwner
                    hasChildren={owner.disciplines.length > 0 || owner.files.length > 0}
                    isExpanded={isOwnerExpanded}
                    onToggle={() => toggleOwner(owner.id)}
                    onClick={() => onNavigate([{ id: owner.id, name: owner.folderName, type: 'owner' }])}
                    isActive={isOwnerActive}
                    indent={0}
                  />

                  {isOwnerExpanded && owner.disciplines.length > 0 && (
                    <div>
                      {owner.disciplines.map((discipline) => {
                        const isDisciplineActive = selectedDisciplineId === discipline.id && isOwnerActive

                        return (
                          <div key={discipline.id}>
                            <SidebarNode
                              name={discipline.folderName}
                              isDiscipline
                              hasChildren={discipline.files.length > 0}
                              isExpanded={false}
                              onClick={() =>
                                onNavigate([
                                  { id: owner.id, name: owner.folderName, type: 'owner' },
                                  { id: discipline.id, name: discipline.folderName, type: 'discipline' },
                                ])
                              }
                              isActive={isDisciplineActive}
                              indent={1}
                            />

                            {isDisciplineActive && discipline.files.length > 0 && (
                              <div>
                                {discipline.files.map((file) => (
                                  <SidebarNode
                                    key={file.id}
                                    name={file.fileName}
                                    isFile
                                    onClick={() => {
                                      // File selection handled by double-click in list view
                                    }}
                                    indent={2}
                                  />
                                ))}
                              </div>
                            )}
                          </div>
                        )
                      })}
                    </div>
                  )}

                  {isOwnerExpanded && owner.files.length > 0 && (
                    <div>
                      {owner.files.map((file) => (
                        <SidebarNode
                          key={file.id}
                          name={file.fileName}
                          isFile
                          indent={1}
                        />
                      ))}
                    </div>
                  )}
                </div>
              )
            })}
          </div>
        ) : (
          <div className="p-4 text-sm text-muted-foreground">No TIDP folder uploaded</div>
        )}
      </div>
    </div>
  )
}

import { ChevronDown } from 'lucide-react'
import { Button } from '@/shared/ui/button'
import { useState } from 'react'
import type { TidpFolderSyncResult } from './api'

interface TidpFolderSyncResultProps {
  result: TidpFolderSyncResult
  onDone: () => void
}

const ACTION_COLORS: Record<string, string> = {
  Added: 'bg-green-50 text-green-900',
  Updated: 'bg-blue-50 text-blue-900',
  Skipped: 'bg-gray-50 text-gray-600',
  Missing: 'bg-yellow-50 text-yellow-900',
  Failed: 'bg-red-50 text-red-900',
}

export function TidpFolderSyncResultView({ result, onDone }: TidpFolderSyncResultProps) {
  const [expandedErrors, setExpandedErrors] = useState<Set<string>>(new Set())

  const toggleError = (path: string) => {
    const newSet = new Set(expandedErrors)
    if (newSet.has(path)) {
      newSet.delete(path)
    } else {
      newSet.add(path)
    }
    setExpandedErrors(newSet)
  }

  const hasErrors = result.failed > 0

  return (
    <div className="space-y-4">
      <div className="rounded-lg border border-border bg-card p-4">
        <h3 className="font-semibold mb-3">Sync Summary</h3>
        <div className="grid grid-cols-2 gap-4 sm:grid-cols-3">
          <div className="text-center">
            <div className="text-2xl font-bold text-green-600">{result.added}</div>
            <div className="text-sm text-muted-foreground">Added</div>
          </div>
          <div className="text-center">
            <div className="text-2xl font-bold text-blue-600">{result.updated}</div>
            <div className="text-sm text-muted-foreground">Updated</div>
          </div>
          <div className="text-center">
            <div className="text-2xl font-bold text-gray-600">{result.skipped}</div>
            <div className="text-sm text-muted-foreground">Skipped</div>
          </div>
          {result.missing > 0 && (
            <div className="text-center">
              <div className="text-2xl font-bold text-yellow-600">{result.missing}</div>
              <div className="text-sm text-muted-foreground">Missing</div>
            </div>
          )}
          {result.failed > 0 && (
            <div className="text-center">
              <div className="text-2xl font-bold text-red-600">{result.failed}</div>
              <div className="text-sm text-muted-foreground">Failed</div>
            </div>
          )}
          <div className="text-center">
            <div className="text-2xl font-bold">{result.totalFiles}</div>
            <div className="text-sm text-muted-foreground">Total Files</div>
          </div>
        </div>
      </div>

      {result.files.length > 0 && (
        <div className="space-y-2">
          <h3 className="font-semibold">File Results</h3>
          <div className="max-h-96 overflow-y-auto rounded-lg border border-border">
            <table className="w-full text-sm">
              <thead className="sticky top-0 bg-muted">
                <tr>
                  <th className="px-3 py-2 text-left font-medium">File</th>
                  <th className="px-3 py-2 text-left font-medium">Action</th>
                  <th className="px-3 py-2 text-right font-medium">Rows</th>
                </tr>
              </thead>
              <tbody>
                {result.files.map((file) => (
                  <tr key={file.relativePath} className="border-t border-border hover:bg-muted/50">
                    <td className="px-3 py-2 text-left">
                      <div className="truncate text-xs font-mono">{file.relativePath}</div>
                      {file.ownerName && (
                        <div className="text-xs text-muted-foreground">{file.ownerName}</div>
                      )}
                    </td>
                    <td className="px-3 py-2">
                      <span
                        className={`inline-block px-2 py-1 rounded text-xs font-medium ${
                          ACTION_COLORS[file.action] || 'bg-gray-50 text-gray-600'
                        }`}
                      >
                        {file.action}
                      </span>
                    </td>
                    <td className="px-3 py-2 text-right text-xs">
                      {file.rowsImported ?? '-'}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {hasErrors && (
        <div className="space-y-2">
          <h3 className="font-semibold text-sm text-destructive">Errors</h3>
          <div className="space-y-1">
            {result.files
              .filter((f) => f.error)
              .map((file) => (
                <div
                  key={file.relativePath}
                  className="rounded border border-destructive/20 bg-destructive/5 p-2"
                >
                  <button
                    onClick={() => toggleError(file.relativePath)}
                    className="w-full text-left flex items-center gap-2 text-sm hover:text-destructive"
                  >
                    <ChevronDown
                      className={`h-4 w-4 transition-transform ${
                        expandedErrors.has(file.relativePath) ? 'rotate-180' : ''
                      }`}
                    />
                    <span className="truncate font-mono text-xs flex-1">{file.relativePath}</span>
                  </button>
                  {expandedErrors.has(file.relativePath) && file.error && (
                    <div className="mt-2 ml-6 text-xs text-destructive whitespace-pre-wrap break-words">
                      {file.error}
                    </div>
                  )}
                </div>
              ))}
          </div>
        </div>
      )}

      <Button onClick={onDone} className="w-full">
        Done
      </Button>
    </div>
  )
}

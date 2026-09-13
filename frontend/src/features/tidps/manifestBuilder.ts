export interface ManifestFile {
  field: string
  relativePath: string
  lastModifiedUtc: string
  sizeBytes: number
}

export interface TidpFolderManifest {
  rootName: string
  folders: string[]
  files: ManifestFile[]
}

export async function buildManifestFromHandle(
  dirHandle: FileSystemDirectoryHandle,
  rootName: string
): Promise<{ manifest: TidpFolderManifest; files: Map<string, File> }> {
  const folders = new Set<string>()
  const manifestFiles: ManifestFile[] = []
  const filesMap = new Map<string, File>()
  let fieldIndex = 0

  async function walk(handle: any, path: string) {
    try {
      for await (const entry of (handle as any)) {
        const [name, entryHandle] = entry
        const entryPath = path ? `${path}/${name}` : name
        if (entryHandle.kind === 'directory') {
          folders.add(entryPath)
          await walk(entryHandle, entryPath)
        } else if (entryHandle.kind === 'file') {
          const file = await entryHandle.getFile()
          // Skip lock files and non-xlsx files
          if (file.name.startsWith('~$') || (!file.name.endsWith('.xlsx') && !file.name.endsWith('.xlsm'))) {
            continue
          }
          const field = `f${fieldIndex++}`
          const relativePath = `${rootName}/${entryPath}`
          manifestFiles.push({
            field,
            relativePath,
            lastModifiedUtc: new Date(file.lastModified).toISOString(),
            sizeBytes: file.size,
          })
          filesMap.set(field, file)
        }
      }
    } catch (err) {
      console.warn(`Cannot read directory ${path}:`, err)
    }
  }

  await walk(dirHandle, '')

  return {
    manifest: {
      rootName,
      folders: Array.from(folders).sort(),
      files: manifestFiles,
    },
    files: filesMap,
  }
}

export async function buildManifestFromWebkitDirectory(
  files: FileList,
  rootName: string
): Promise<{ manifest: TidpFolderManifest; files: Map<string, File> }> {
  const folders = new Set<string>()
  const manifestFiles: ManifestFile[] = []
  const filesMap = new Map<string, File>()
  let fieldIndex = 0

  for (let i = 0; i < files.length; i++) {
    const file = files[i]
    const webkitPath = (file as any).webkitRelativePath || ''

    // Skip lock files and non-xlsx files
    if (file.name.startsWith('~$') || (!file.name.endsWith('.xlsx') && !file.name.endsWith('.xlsm'))) {
      continue
    }

    // Extract folder structure from the webkit path
    const parts = webkitPath.split('/').filter(Boolean)
    if (parts.length < 2) continue // Need at least [folderName, ...filePath]

    // Build folder paths
    for (let j = 1; j < parts.length - 1; j++) {
      const folderPath = parts.slice(1, j + 1).join('/')
      folders.add(folderPath)
    }

    const field = `f${fieldIndex++}`
    const relativePath = `${rootName}/${parts.slice(1).join('/')}`
    manifestFiles.push({
      field,
      relativePath,
      lastModifiedUtc: new Date(file.lastModified).toISOString(),
      sizeBytes: file.size,
    })
    filesMap.set(field, file)
  }

  return {
    manifest: {
      rootName,
      folders: Array.from(folders).sort(),
      files: manifestFiles,
    },
    files: filesMap,
  }
}

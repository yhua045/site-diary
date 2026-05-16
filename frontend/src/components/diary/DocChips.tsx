import type { AttachmentDto } from '../../api/types'

interface DocChipsProps {
  docs: AttachmentDto[]
}

function fileTypeIcon(contentType: string): string {
  if (contentType.includes('pdf')) return '📄'
  if (contentType.includes('spreadsheet') || contentType.includes('excel') || contentType.includes('csv')) return '📊'
  if (contentType.includes('word') || contentType.includes('document')) return '📝'
  return '📎'
}

export function DocChips({ docs }: DocChipsProps) {
  if (docs.length === 0) return null

  return (
    <div className="mt-2 flex flex-wrap gap-2">
      {docs.map((doc) => (
        <a
          key={doc.id}
          href={doc.fileUrl}
          download={doc.fileName}
          className="inline-flex items-center gap-1.5 rounded-full bg-gray-100 px-3 py-1 text-sm text-gray-700 hover:bg-gray-200 transition-colors"
        >
          <span>{fileTypeIcon(doc.contentType)}</span>
          <span className="truncate max-w-[180px]">{doc.fileName}</span>
          <span className="text-gray-400">↓</span>
        </a>
      ))}
    </div>
  )
}

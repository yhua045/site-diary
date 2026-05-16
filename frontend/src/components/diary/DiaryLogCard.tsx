import type { DiaryTimelineEntry, FieldDescriptor } from '../../api/types'
import { renderField } from './fieldRenderers'
import { InlineImages } from './InlineImages'
import { DocChips } from './DocChips'

interface DiaryLogCardProps {
  entry: DiaryTimelineEntry
  onImageClick?: (id: number) => void
}

function formatDate(dateStr: string): string {
  return new Date(dateStr + 'T00:00:00').toLocaleDateString(undefined, {
    weekday: 'short',
    day: 'numeric',
    month: 'long',
    year: 'numeric',
  })
}

function authorInitial(name: string): string {
  return name.charAt(0).toUpperCase()
}

export function DiaryLogCard({ entry, onImageClick }: DiaryLogCardProps) {
  const images = entry.attachments.filter((a) => a.contentType.startsWith('image/'))
  const docs = entry.attachments.filter((a) => !a.contentType.startsWith('image/'))

  const sortedSnapshot = [...entry.templateSnapshot].sort((a, b) => a.displayOrder - b.displayOrder)
  const snapshotKeys = new Set(sortedSnapshot.map((d) => d.key))

  // Ad-hoc fields: present in payload but absent from the snapshot
  const adHocKeys = Object.keys(entry.payload).filter((k) => !snapshotKeys.has(k))

  return (
    <article className="rounded-xl border border-gray-200 bg-white shadow-sm overflow-hidden">
      {/* Card Header */}
      <div className="flex items-start justify-between px-4 py-3 bg-gray-50 border-b border-gray-200">
        <div>
          <p className="text-sm font-semibold text-gray-900">{formatDate(entry.date)}</p>
          <div className="mt-1 flex items-center gap-1.5 text-xs text-gray-500">
            <span className="inline-flex h-5 w-5 items-center justify-center rounded-full bg-blue-100 text-blue-700 font-bold text-[10px]">
              {authorInitial(entry.authorName)}
            </span>
            <span className="font-medium text-gray-700">{entry.authorName}</span>
            {entry.authorRole && (
              <>
                <span className="text-gray-300">·</span>
                <span>{entry.authorRole}</span>
              </>
            )}
          </div>
        </div>
      </div>

      {/* Dynamic Fields */}
      {sortedSnapshot.length > 0 && (
        <div className="px-4 py-2">
          <dl className="divide-y divide-gray-100">
            {sortedSnapshot.map((descriptor: FieldDescriptor) => (
              <div key={descriptor.key} className="grid grid-cols-3 gap-2 py-1.5 text-sm">
                <dt className="font-medium text-gray-500">{descriptor.label}</dt>
                <dd className="col-span-2">{renderField(descriptor, entry.payload)}</dd>
              </div>
            ))}
          </dl>
        </div>
      )}

      {/* Ad-hoc fields not in the template snapshot */}
      {adHocKeys.length > 0 && (
        <div className="px-4 pb-2">
          <p className="text-xs font-semibold uppercase tracking-wide text-gray-400 mt-2 mb-1">
            Additional Fields
          </p>
          <dl className="divide-y divide-gray-100">
            {adHocKeys.map((key) => (
              <div key={key} className="grid grid-cols-3 gap-2 py-1.5 text-sm">
                <dt className="font-medium text-gray-500">{key}</dt>
                <dd className="col-span-2 text-gray-800">
                  {entry.payload[key] != null ? String(entry.payload[key]) : '—'}
                </dd>
              </div>
            ))}
          </dl>
        </div>
      )}

      {/* Attachments */}
      {(images.length > 0 || docs.length > 0) && (
        <div className="px-4 pb-4">
          <InlineImages images={images} onImageClick={onImageClick} />
          <DocChips docs={docs} />
        </div>
      )}
    </article>
  )
}

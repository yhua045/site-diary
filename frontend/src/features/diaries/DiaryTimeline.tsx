import type { DiaryTimelineEntry } from '../../api/types'
import { DiaryLogCard } from '../../components/diary/DiaryLogCard'

interface DiaryTimelineProps {
  entries: DiaryTimelineEntry[]
  isLoading: boolean
  onOpenCreate: () => void
  siteName: string
}

function monthKey(dateStr: string): string {
  const d = new Date(dateStr + 'T00:00:00')
  return `${d.getFullYear()}-${d.getMonth()}`
}

function monthLabel(dateStr: string): string {
  return new Date(dateStr + 'T00:00:00').toLocaleDateString(undefined, {
    month: 'long',
    year: 'numeric',
  })
}

export function DiaryTimeline({ entries, isLoading, onOpenCreate, siteName }: DiaryTimelineProps) {
  if (isLoading) {
    return (
      <div data-testid="timeline-loading" className="p-4">
        <div className="animate-pulse space-y-4">
          {[1, 2, 3].map(i => (
            <div key={i} className="h-24 bg-gray-200 rounded-xl" />
          ))}
        </div>
      </div>
    )
  }

  const groups: { month: string; label: string; entries: DiaryTimelineEntry[] }[] = []
  for (const entry of entries) {
    const key = monthKey(entry.date)
    const last = groups[groups.length - 1]
    if (!last || last.month !== key) {
      groups.push({ month: key, label: monthLabel(entry.date), entries: [entry] })
    } else {
      last.entries.push(entry)
    }
  }

  return (
    <div className="flex flex-col min-h-screen bg-gray-50">
      {/* Sticky header */}
      <header className="sticky top-0 z-10 bg-white border-b border-gray-200 px-4 py-3 flex items-center justify-between">
        <div>
          <h1 className="text-lg font-bold text-gray-900">{siteName}</h1>
          <p className="text-xs text-gray-500">Site Diary</p>
        </div>
        <button
          aria-label="Add diary entry"
          onClick={onOpenCreate}
          className="rounded-full bg-blue-600 text-white w-9 h-9 flex items-center justify-center text-xl font-bold shadow hover:bg-blue-700"
        >
          +
        </button>
      </header>

      {/* Content */}
      <main className="flex-1 px-4 py-4 space-y-4 max-w-2xl w-full mx-auto">
        {entries.length === 0 ? (
          <div className="text-center mt-20">
            <p className="text-gray-500 mb-4">No diary entries yet</p>
            <button
              onClick={onOpenCreate}
              className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700"
            >
              Add First Entry
            </button>
          </div>
        ) : (
          groups.map(group => (
            <section key={group.month}>
              <h2 className="text-sm font-semibold uppercase tracking-wider text-gray-400 mb-2">
                {group.label}
              </h2>
              <div className="space-y-3">
                {group.entries.map(entry => (
                  <DiaryLogCard key={entry.id} entry={entry} />
                ))}
              </div>
            </section>
          ))
        )}
      </main>
    </div>
  )
}

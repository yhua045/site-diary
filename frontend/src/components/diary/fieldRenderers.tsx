import type { ReactNode } from 'react'
import type { FieldDescriptor } from '../../api/types'

export type FieldRenderer = (value: unknown, descriptor: FieldDescriptor) => ReactNode

export const FIELD_RENDERERS: Record<string, FieldRenderer> = {
  text: (v) => (
    <span className="text-gray-800">{v != null && v !== '' ? String(v) : '—'}</span>
  ),
  number: (v) => (
    <span className="tabular-nums text-gray-800">{v != null ? String(v) : '—'}</span>
  ),
  boolean: (v) => (
    <span className="text-gray-800">{v == null ? '—' : v ? 'Yes' : 'No'}</span>
  ),
  date: (v) => (
    <span className="text-gray-800">
      {v ? new Date(String(v)).toLocaleDateString(undefined, { day: 'numeric', month: 'long', year: 'numeric' }) : '—'}
    </span>
  ),
  select: (v, d) => {
    const opt = d.options?.find((o) => o.value === v)
    return <span className="text-gray-800">{opt?.label ?? (v != null ? String(v) : '—')}</span>
  },
}

export function renderField(
  descriptor: FieldDescriptor,
  payload: Record<string, unknown>,
): ReactNode {
  const value = Object.prototype.hasOwnProperty.call(payload, descriptor.key)
    ? payload[descriptor.key]
    : null
  const renderer: FieldRenderer =
    FIELD_RENDERERS[descriptor.type] ?? ((v) => <span className="text-gray-800">{v != null ? String(v) : '—'}</span>)
  return renderer(value, descriptor)
}

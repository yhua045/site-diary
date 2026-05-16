import { useState } from 'react'
import type { DiaryTemplate } from '../../api/types'

interface CreateDiaryPayload {
  payload: Record<string, unknown>
  date: string
}

interface DiaryCreateFormProps {
  template: DiaryTemplate
  onSubmit: (data: CreateDiaryPayload) => void
  onClose: () => void
  isSubmitting: boolean
}

function todayIso(): string {
  return new Date().toISOString().slice(0, 10)
}

export function DiaryCreateForm({ template, onSubmit, onClose, isSubmitting }: DiaryCreateFormProps) {
  const [values, setValues] = useState<Record<string, string>>({})
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [date, setDate] = useState(todayIso)

  const allFields = template.sections.flatMap(s => s.fields)

  function validate(): boolean {
    const newErrors: Record<string, string> = {}
    for (const field of allFields) {
      if (field.required && !values[field.id]?.trim()) {
        newErrors[field.id] = `${field.label} is required`
      }
    }
    setErrors(newErrors)
    return Object.keys(newErrors).length === 0
  }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!validate()) return
    const payload: Record<string, unknown> = {}
    for (const field of allFields) {
      if (values[field.id] !== undefined) {
        payload[field.id] =
          field.type === 'number' ? Number(values[field.id]) : values[field.id]
      }
    }
    onSubmit({ payload, date })
  }

  function handleChange(fieldId: string, value: string) {
    setValues(prev => ({ ...prev, [fieldId]: value }))
    if (errors[fieldId]) {
      setErrors(prev => ({ ...prev, [fieldId]: '' }))
    }
  }

  return (
    <div className="bg-white rounded-xl shadow-lg p-6 max-w-lg w-full">
      <h2 className="text-lg font-bold text-gray-900 mb-4">New Diary Entry</h2>
      <form onSubmit={handleSubmit} noValidate>
        {/* Date field */}
        <div className="mb-4">
          <label htmlFor="diary-date" className="block text-sm font-medium text-gray-700 mb-1">
            Date
          </label>
          <input
            id="diary-date"
            type="date"
            value={date}
            onChange={e => setDate(e.target.value)}
            className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm"
          />
        </div>

        {/* Template sections */}
        {template.sections.map(section => (
          <fieldset key={section.id} className="mb-4">
            <legend className="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-2">
              {section.label}
            </legend>
            {section.fields.map(field => (
              <div key={field.id} className="mb-3">
                <label
                  htmlFor={field.id}
                  className="block text-sm font-medium text-gray-700 mb-1"
                >
                  {field.label}
                  {field.required && <span className="text-red-500 ml-0.5">*</span>}
                </label>

                {field.type === 'select' ? (
                  <select
                    id={field.id}
                    value={values[field.id] ?? ''}
                    onChange={e => handleChange(field.id, e.target.value)}
                    className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm"
                  >
                    <option value="">Select…</option>
                    {field.options?.map(opt => (
                      <option key={opt} value={opt}>
                        {opt}
                      </option>
                    ))}
                  </select>
                ) : (
                  <input
                    id={field.id}
                    type={field.type === 'number' ? 'number' : 'text'}
                    placeholder={field.placeholder}
                    value={values[field.id] ?? ''}
                    onChange={e => handleChange(field.id, e.target.value)}
                    className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm"
                  />
                )}

                {errors[field.id] && (
                  <p className="text-red-500 text-xs mt-1">{errors[field.id]}</p>
                )}
              </div>
            ))}
          </fieldset>
        ))}

        {/* File upload area */}
        <div className="mb-4 border-2 border-dashed border-gray-300 rounded-lg p-4 text-center text-sm text-gray-500">
          Add photo / attach file
        </div>

        <div className="flex gap-3 justify-end">
          <button
            type="button"
            onClick={onClose}
            className="px-4 py-2 text-sm text-gray-700 border border-gray-300 rounded-lg hover:bg-gray-50"
          >
            Cancel
          </button>
          <button
            type="submit"
            disabled={isSubmitting}
            className="px-4 py-2 text-sm text-white bg-blue-600 rounded-lg hover:bg-blue-700 disabled:opacity-50"
          >
            Save Entry
          </button>
        </div>
      </form>
    </div>
  )
}

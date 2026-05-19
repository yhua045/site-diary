import { useState } from 'react'
import type { DiaryTemplate, FieldDef } from '../../api/types'

interface CreateDiaryPayload {
  payload: Record<string, unknown>
  date: string
  fieldOverrides?: {
    removed: string[]
    added: FieldDef[]
  }
  files?: File[]
}

interface DiaryCreateFormProps {
  template: DiaryTemplate
  onSubmit: (data: CreateDiaryPayload) => void
  onClose: () => void
  isSubmitting: boolean
}

function todayIso(): string {
  // Return YYYY-MM-DDThh:mm to fit datetime-local input
  const d = new Date()
  return new Date(d.getTime() - d.getTimezoneOffset() * 60000).toISOString().slice(0, 16)
}

export function DiaryCreateForm({ template, onSubmit, onClose, isSubmitting }: DiaryCreateFormProps) {
  const [values, setValues] = useState<Record<string, string>>({})
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [date, setDate] = useState(todayIso)

  const [customFields, setCustomFields] = useState<FieldDef[]>([])
  const [isAddingField, setIsAddingField] = useState(false)
  const [newFieldLabel, setNewFieldLabel] = useState('')
  const [newFieldType, setNewFieldType] = useState('text')
  const [files, setFiles] = useState<File[]>([])

  const baseFields = template.sections.flatMap(s => s.fields)
  const allFields = [...baseFields, ...customFields]

  function validate(): boolean {
    const newErrors: Record<string, string> = {}
    for (const field of allFields) {
      if (field.type === 'dynamic_fields') continue
      if (field.required && !values[field.id]?.trim() && field.type !== 'boolean') {
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
      if (field.type === 'dynamic_fields') continue
      if (values[field.id] !== undefined) {
        payload[field.id] =
          field.type === 'number' ? Number(values[field.id]) : 
          field.type === 'boolean' ? values[field.id] === 'true' :
          values[field.id]
      }
    }
    
    const finalDate = new Date(date).toISOString()
    
    onSubmit({ 
      payload, 
      date: finalDate,
      ...(customFields.length > 0 && {
        fieldOverrides: {
          added: customFields,
          removed: []
        }
      }),
      files: files.length > 0 ? files : undefined
    })
  }

  function handleChange(fieldId: string, value: string) {
    setValues(prev => ({ ...prev, [fieldId]: value }))
    if (errors[fieldId]) {
      setErrors(prev => ({ ...prev, [fieldId]: '' }))
    }
  }

  function handleAddCustomField() {
    if (!newFieldLabel.trim()) return
    const newFieldId = `custom_${Date.now()}`
    const newField: FieldDef = {
      id: newFieldId,
      label: newFieldLabel.trim(),
      type: newFieldType,
      required: false
    }
    setCustomFields(prev => [...prev, newField])
    setNewFieldLabel('')
    setNewFieldType('text')
    setIsAddingField(false)
  }

  function renderCustomFieldsSection() {
    return (
      <div className="bg-gray-50 border border-dashed border-gray-300 rounded-lg p-4 mt-2">
        {customFields.length > 0 && (
          <div className="mb-4 space-y-3">
            <h4 className="text-sm font-medium text-gray-700">Added Fields</h4>
            {customFields.map(cf => (
              <div key={cf.id} className="mb-3 pl-2 border-l-2 border-blue-500">
                <label htmlFor={cf.id} className="block text-sm font-medium text-gray-700 mb-1">
                  {cf.label} <span className="text-xs text-gray-400">({cf.type})</span>
                </label>
                {renderFieldInput(cf)}
                {errors[cf.id] && <p className="text-red-500 text-xs mt-1">{errors[cf.id]}</p>}
              </div>
            ))}
          </div>
        )}

        {!isAddingField ? (
          <button
            type="button"
            onClick={() => setIsAddingField(true)}
            className="text-sm text-blue-600 hover:text-blue-800 font-medium flex items-center gap-1"
          >
            + Add Custom Field
          </button>
        ) : (
          <div className="bg-white p-3 border border-gray-200 rounded-md shadow-sm">
            <div className="mb-3">
              <label className="block text-xs font-medium text-gray-700 mb-1">Field Name / Label</label>
              <input
                type="text"
                value={newFieldLabel}
                onChange={e => setNewFieldLabel(e.target.value)}
                placeholder="e.g., Subcontractor present?"
                className="w-full border border-gray-300 rounded px-2 py-1 text-sm"
                autoFocus
              />
            </div>
            <div className="mb-3">
              <label className="block text-xs font-medium text-gray-700 mb-1">Field Type</label>
              <select
                value={newFieldType}
                onChange={e => setNewFieldType(e.target.value)}
                className="w-full border border-gray-300 rounded px-2 py-1 text-sm"
              >
                <option value="text">Text</option>
                <option value="number">Number</option>
                <option value="boolean">Yes/No</option>
              </select>
            </div>
            <div className="flex gap-2 justify-end">
              <button
                type="button"
                onClick={() => setIsAddingField(false)}
                className="px-2 py-1 text-xs text-gray-600 border border-gray-300 rounded hover:bg-gray-50"
              >
                Cancel
              </button>
              <button
                type="button"
                onClick={handleAddCustomField}
                disabled={!newFieldLabel.trim()}
                className="px-2 py-1 text-xs text-white bg-blue-600 rounded hover:bg-blue-700 disabled:opacity-50"
              >
                Add Field
              </button>
            </div>
          </div>
        )}
      </div>
    )
  }

  function renderFieldInput(field: FieldDef) {
    if (field.type === 'select') {
      return (
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
      )
    }

    if (field.type === 'boolean') {
      return (
        <select
          id={field.id}
          value={values[field.id] ?? ''}
          onChange={e => handleChange(field.id, e.target.value)}
          className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm"
        >
          <option value="">Select…</option>
          <option value="true">Yes</option>
          <option value="false">No</option>
        </select>
      )
    }

    if (field.type === 'dynamic_fields') {
      return null
    }

    if (field.type === 'file_attachment') {
      return (
        <input
          id={field.id}
          type="file"
          multiple
          onChange={e => {
            if (e.target.files) {
              setFiles(Array.from(e.target.files))
            }
          }}
          className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm"
        />
      )
    }

    return (
      <input
        id={field.id}
        type={field.type === 'number' ? 'number' : 'text'}
        placeholder={field.placeholder}
        value={values[field.id] ?? ''}
        onChange={e => handleChange(field.id, e.target.value)}
        className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm"
      />
    )
  }

  return (
    <div className="bg-white rounded-xl shadow-lg p-6 max-w-lg w-full max-h-[90vh] overflow-y-auto">
      <h2 className="text-lg font-bold text-gray-900 mb-4">New Diary Entry</h2>
      <form onSubmit={handleSubmit} noValidate>
        {/* Date field */}
        <div className="mb-4">
          <label htmlFor="diary-date" className="block text-sm font-medium text-gray-700 mb-1">
            Date & Time
          </label>
          <input
            id="diary-date"
            type="datetime-local"
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
                {field.type !== 'dynamic_fields' && (
                  <label
                    htmlFor={field.id}
                    className="block text-sm font-medium text-gray-700 mb-1"
                  >
                    {field.label}
                    {field.required && <span className="text-red-500 ml-0.5">*</span>}
                  </label>
                )}

                {renderFieldInput(field)}

                {errors[field.id] && field.type !== 'dynamic_fields' && (
                  <p className="text-red-500 text-xs mt-1">{errors[field.id]}</p>
                )}
              </div>
            ))}
          </fieldset>
        ))}

        {/* Custom fields are always available now, independent of the template seed data. */}
        <div className="mb-4">
          <h3 className="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-2">
            Custom Fields
          </h3>
          {renderCustomFieldsSection()}
        </div>

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

import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import { renderField, FIELD_RENDERERS } from '../../components/diary/fieldRenderers'
import type { FieldDescriptor } from '../../api/types'

const makeDescriptor = (type: string, key = 'testKey', label = 'Test'): FieldDescriptor => ({
  key,
  label,
  type,
  displayOrder: 1,
})

describe('FIELD_RENDERERS', () => {
  it('renders text value as plain string', () => {
    const { container } = render(FIELD_RENDERERS['text']('Hello', makeDescriptor('text')) as JSX.Element)
    expect(container).toHaveTextContent('Hello')
  })

  it('renders text with null as dash', () => {
    const { container } = render(FIELD_RENDERERS['text'](null, makeDescriptor('text')) as JSX.Element)
    expect(container).toHaveTextContent('—')
  })

  it('renders number value', () => {
    const { container } = render(FIELD_RENDERERS['number'](12, makeDescriptor('number')) as JSX.Element)
    expect(container).toHaveTextContent('12')
  })

  it('renders boolean true as Yes', () => {
    const { container } = render(FIELD_RENDERERS['boolean'](true, makeDescriptor('boolean')) as JSX.Element)
    expect(container).toHaveTextContent('Yes')
  })

  it('renders boolean false as No', () => {
    const { container } = render(FIELD_RENDERERS['boolean'](false, makeDescriptor('boolean')) as JSX.Element)
    expect(container).toHaveTextContent('No')
  })

  it('renders boolean null as dash', () => {
    const { container } = render(FIELD_RENDERERS['boolean'](null, makeDescriptor('boolean')) as JSX.Element)
    expect(container).toHaveTextContent('—')
  })

  it('renders date value as locale string', () => {
    const { container } = render(FIELD_RENDERERS['date']('2026-05-15', makeDescriptor('date')) as JSX.Element)
    // Should contain at least part of the date (locale-dependent)
    expect(container.textContent).not.toBe('—')
    expect(container.textContent?.length).toBeGreaterThan(0)
  })

  it('renders date null as dash', () => {
    const { container } = render(FIELD_RENDERERS['date'](null, makeDescriptor('date')) as JSX.Element)
    expect(container).toHaveTextContent('—')
  })

  it('renders select value using option label', () => {
    const descriptor: FieldDescriptor = {
      ...makeDescriptor('select'),
      options: [{ value: 'sunny', label: 'Sunny Day' }],
    }
    const { container } = render(FIELD_RENDERERS['select']('sunny', descriptor) as JSX.Element)
    expect(container).toHaveTextContent('Sunny Day')
  })

  it('renders select with unknown value as raw value', () => {
    const descriptor: FieldDescriptor = {
      ...makeDescriptor('select'),
      options: [{ value: 'sunny', label: 'Sunny' }],
    }
    const { container } = render(FIELD_RENDERERS['select']('rainy', descriptor) as JSX.Element)
    expect(container).toHaveTextContent('rainy')
  })
})

describe('renderField', () => {
  it('renders field from payload using descriptor', () => {
    const descriptor = makeDescriptor('text', 'notes', 'Notes')
    const payload: Record<string, unknown> = { notes: 'Poured concrete' }
    render(
      <dl>
        <div>{renderField(descriptor, payload)}</div>
      </dl>
    )
    expect(screen.getByText('Poured concrete')).toBeInTheDocument()
  })

  it('renders dash when field key absent from payload', () => {
    const descriptor = makeDescriptor('text', 'safetyCheck', 'Safety')
    const payload: Record<string, unknown> = {}
    render(
      <dl>
        <div>{renderField(descriptor, payload)}</div>
      </dl>
    )
    expect(screen.getByText('—')).toBeInTheDocument()
  })

  it('falls back to string renderer for unknown types', () => {
    const descriptor = makeDescriptor('custom_type', 'field1', 'Custom')
    const payload: Record<string, unknown> = { field1: 'custom value' }
    render(
      <dl>
        <div>{renderField(descriptor, payload)}</div>
      </dl>
    )
    expect(screen.getByText('custom value')).toBeInTheDocument()
  })
})
